using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using AAAOptimizer.Data;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AAAOptimizer.Core
{
#if UNITY_EDITOR
    public static class SceneAnalyzer
    {
        public static SceneAnalysisData AnalyzeActiveScene(AAAOptimizerConfig config)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            
            // Create data instance
            SceneAnalysisData data = ScriptableObject.CreateInstance<SceneAnalysisData>();
            data.sceneName = activeScene.name;
            data.scanTimestamp = DateTime.Now;

            // Collect all renderers in the active scene
            MeshRenderer[] meshRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            SkinnedMeshRenderer[] skinnedRenderers = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            data.totalRenderers = meshRenderers.Length + skinnedRenderers.Length;

            IRenderPipelineBridge bridge = RenderPipelineBridgeUtility.GetActiveBridge();

            Dictionary<Mesh, MeshMetric> uniqueMeshes = new Dictionary<Mesh, MeshMetric>();
            Dictionary<Material, MaterialMetric> uniqueMaterials = new Dictionary<Material, MaterialMetric>();
            Dictionary<Texture2D, TextureMetric> uniqueTextures = new Dictionary<Texture2D, TextureMetric>();
            
            // Temporary mapping to detect duplicate materials (materials that share names and properties but are separate instances)
            Dictionary<string, List<Material>> materialNameGroups = new Dictionary<string, List<Material>>();

            // Process MeshRenderers
            foreach (var renderer in meshRenderers)
            {
                ProcessRenderer(renderer, renderer.gameObject, config, bridge, ref data, uniqueMeshes, uniqueMaterials, uniqueTextures, materialNameGroups);
            }

            // Process SkinnedMeshRenderers
            foreach (var renderer in skinnedRenderers)
            {
                ProcessRenderer(renderer, renderer.gameObject, config, bridge, ref data, uniqueMeshes, uniqueMaterials, uniqueTextures, materialNameGroups);
            }

            // Populate lists on analysis object
            data.highPolyMeshes.AddRange(uniqueMeshes.Values);
            // Sort meshes by triangle count descending
            data.highPolyMeshes.Sort((a, b) => b.triangleCount.CompareTo(a.triangleCount));

            data.materials.AddRange(uniqueMaterials.Values);
            data.textures.AddRange(uniqueTextures.Values);
            // Sort textures by estimated bytes descending
            data.textures.Sort((a, b) => b.estimatedBytes.CompareTo(a.estimatedBytes));

            // Find duplicate materials
            DetectDuplicateMaterials(materialNameGroups, ref data);

            // Compute global statistics
            data.totalMaterialsCount = uniqueMaterials.Count;
            data.totalTexturesCount = uniqueTextures.Count;

            long totalTexVRAM = 0;
            foreach (var tex in uniqueTextures.Values)
            {
                totalTexVRAM += tex.estimatedBytes;
            }
            data.estimatedVRAMBytes = totalTexVRAM;

            // Final audits / warnings
            RunAudits(data, config);

            return data;
        }

        private static void ProcessRenderer(Renderer renderer, GameObject go, AAAOptimizerConfig config, IRenderPipelineBridge bridge, 
            ref SceneAnalysisData data, 
            Dictionary<Mesh, MeshMetric> uniqueMeshes, 
            Dictionary<Material, MaterialMetric> uniqueMaterials, 
            Dictionary<Texture2D, TextureMetric> uniqueTextures,
            Dictionary<string, List<Material>> materialNameGroups)
        {
            // Bounding limits
            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
            {
                data.shadowCasterCount++;
            }

            if (go.isStatic)
            {
                data.staticBatchableCount++;
            }

            LODGroup lodGroup = go.GetComponentInParent<LODGroup>();
            bool hasLOD = lodGroup != null;
            if (hasLOD && lodGroup.gameObject == go)
            {
                data.lodGroupsCount++;
            }

            // Extract Mesh
            Mesh mesh = null;
            if (renderer is MeshRenderer mr)
            {
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer smr)
            {
                mesh = smr.sharedMesh;
            }

            if (mesh != null)
            {
                data.totalVertices += mesh.vertexCount;
                data.totalTriangles += mesh.triangles.Length / 3;

                if (!uniqueMeshes.ContainsKey(mesh))
                {
                    MeshMetric metric = new MeshMetric
                    {
                        meshName = mesh.name,
                        objectPath = GetGameObjectPath(go),
                        vertexCount = mesh.vertexCount,
                        triangleCount = mesh.triangles.Length / 3,
                        subMeshCount = mesh.subMeshCount,
                        hasLODGroup = hasLOD
                    };
                    uniqueMeshes.Add(mesh, metric);
                }
            }

            // Extract Materials
            Material[] sharedMaterials = renderer.sharedMaterials;
            foreach (Material mat in sharedMaterials)
            {
                if (mat == null) continue;

                // Track by name for duplicates
                string matName = mat.name;
                if (!materialNameGroups.ContainsKey(matName))
                {
                    materialNameGroups[matName] = new List<Material>();
                }
                if (!materialNameGroups[matName].Contains(mat))
                {
                    materialNameGroups[matName].Add(mat);
                }

                if (!uniqueMaterials.ContainsKey(mat))
                {
                    MaterialMetric matMetric = new MaterialMetric
                    {
                        materialName = mat.name,
                        shaderName = mat.shader != null ? mat.shader.name : "Missing Shader",
                        isSRPBatcherCompatible = bridge.IsMaterialSRPBatcherCompatible(mat),
                        enableInstancing = mat.enableInstancing
                    };
                    matMetric.associatedObjects.Add(GetGameObjectPath(go));
                    uniqueMaterials.Add(mat, matMetric);
                }
                else
                {
                    uniqueMaterials[mat].associatedObjects.Add(GetGameObjectPath(go));
                }

                // Process Textures of this Material
                if (mat.shader != null)
                {
                    int propertyCount = mat.shader.GetPropertyCount();
                    for (int i = 0; i < propertyCount; i++)
                    {
                        if (mat.shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                        {
                            string propertyName = mat.shader.GetPropertyName(i);
                            Texture tex = mat.GetTexture(propertyName);
                            if (tex is Texture2D tex2D)
                            {
                                if (!uniqueTextures.ContainsKey(tex2D))
                                {
                                    TextureMetric texMetric = new TextureMetric
                                    {
                                        textureName = tex2D.name,
                                        width = tex2D.width,
                                        height = tex2D.height,
                                        format = tex2D.format.ToString(),
                                        streamingEnabled = tex2D.streamingMipmaps,
                                        estimatedBytes = EstimateTextureSizeInBytes(tex2D)
                                    };
                                    uniqueTextures.Add(tex2D, texMetric);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void DetectDuplicateMaterials(Dictionary<string, List<Material>> materialNameGroups, ref SceneAnalysisData data)
        {
            foreach (var kvp in materialNameGroups)
            {
                if (kvp.Value.Count > 1)
                {
                    DuplicateMaterialGroup group = new DuplicateMaterialGroup
                    {
                        baseMaterialName = kvp.Key
                    };

                    foreach (var mat in kvp.Value)
                    {
                        // Add path/identifier of the material instance
#if UNITY_EDITOR
                        string path = AssetDatabase.GetAssetPath(mat);
                        if (string.IsNullOrEmpty(path)) path = "Instance/Memory Object (" + mat.GetInstanceID() + ")";
                        group.duplicateMaterialPaths.Add(path);
#else
                        group.duplicateMaterialPaths.Add("Memory Instance ID: " + mat.GetInstanceID());
#endif
                    }

                    data.duplicateMaterials.Add(group);
                }
            }
        }

        private static void RunAudits(SceneAnalysisData data, AAAOptimizerConfig config)
        {
            // Audit vertices
            if (data.totalTriangles > 5000000)
            {
                data.warningLogs.Add($"Extreme poly count detected ({data.totalTriangles:N0} triangles). Consider baking HLOD clusters or generating billboard impostors.");
            }

            // Audit duplicate materials
            if (data.duplicateMaterials.Count > 0)
            {
                data.warningLogs.Add($"Found {data.duplicateMaterials.Count} groups of duplicate materials. Consolidating materials will optimize SRP Batching.");
            }

            // Audit textures size
            int largeTextures = 0;
            foreach (var tex in data.textures)
            {
                if (tex.width > config.textureSizeWarningThreshold || tex.height > config.textureSizeWarningThreshold)
                {
                    largeTextures++;
                }
            }
            if (largeTextures > 0)
            {
                data.warningLogs.Add($"{largeTextures} textures are larger than {config.textureSizeWarningThreshold}px. Check texture import settings and downscale or enable Texture Streaming.");
            }

            // Audit LOD groups
            int totalStaticRenderers = data.staticBatchableCount;
            if (totalStaticRenderers > 50 && data.lodGroupsCount == 0)
            {
                data.warningLogs.Add($"The scene has {totalStaticRenderers} static objects but 0 LODGroups. Generate LOD meshes to reduce vertex shader load.");
            }
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }

        private static long EstimateTextureSizeInBytes(Texture2D tex)
        {
            // Basic estimation based on width, height, and compressed bytes ratio
            int bytesPerPixel = 4; // Default uncompressed 32-bit RGBA
            string format = tex.format.ToString().ToLower();

            if (format.Contains("dxt") || format.Contains("bc1") || format.Contains("dxt1"))
            {
                bytesPerPixel = 1; // 0.5 bytes actually, conservative estimate
            }
            else if (format.Contains("bc7") || format.Contains("astc") || format.Contains("etc2"))
            {
                bytesPerPixel = 1;
            }

            long baseSize = (long)tex.width * tex.height * bytesPerPixel;
            if (tex.mipmapCount > 1)
            {
                baseSize = (long)(baseSize * 1.333333f); // Include mipmap chain overhead
            }
            return baseSize;
        }
    }
#endif
}
