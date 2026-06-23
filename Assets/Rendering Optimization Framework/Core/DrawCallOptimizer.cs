using System.Collections.Generic;
using UnityEngine;
using AAAOptimizer.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AAAOptimizer.Core
{
    public static class DrawCallOptimizer
    {
        public static void EnableGPUInstancingOnAllMaterials()
        {
            IRenderPipelineBridge bridge = RenderPipelineBridgeUtility.GetActiveBridge();
            
            // Find all materials in active scene renderers
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<Material> sceneMaterials = new HashSet<Material>();

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        sceneMaterials.Add(mat);
                    }
                }
            }

            int enabledCount = 0;
            foreach (var mat in sceneMaterials)
            {
                if (!mat.enableInstancing)
                {
                    bridge.ConfigureMaterialForInstancing(mat, true);
                    enabledCount++;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(mat);
#endif
                }
            }

            Debug.Log($"[DrawCallOptimizer] Enabled GPU Instancing on {enabledCount} materials in the scene.");
        }

        public static List<GameObject> CombineStaticMeshesByMaterial(GameObject rootObject)
        {
            if (rootObject == null) return new List<GameObject>();

            MeshFilter[] meshFilters = rootObject.GetComponentsInChildren<MeshFilter>();
            Dictionary<Material, List<MeshFilter>> materialFilterMap = new Dictionary<Material, List<MeshFilter>>();

            // Map filters by shared material
            foreach (var mf in meshFilters)
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.gameObject.isStatic || mf.sharedMesh == null) continue;

                Material mat = mr.sharedMaterial;
                if (mat == null) continue;

                if (!materialFilterMap.ContainsKey(mat))
                {
                    materialFilterMap[mat] = new List<MeshFilter>();
                }
                materialFilterMap[mat].Add(mf);
            }

            List<GameObject> combinedObjects = new List<GameObject>();

            // Combine meshes for each material group
            foreach (var kvp in materialFilterMap)
            {
                Material mat = kvp.Key;
                List<MeshFilter> filters = kvp.Value;

                if (filters.Count <= 1) continue; // No need to combine a single mesh

                List<CombineInstance> combineInstances = new List<CombineInstance>();
                
                foreach (var filter in filters)
                {
                    CombineInstance ci = new CombineInstance
                    {
                        mesh = filter.sharedMesh,
                        transform = rootObject.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix
                    };
                    combineInstances.Add(ci);

                    // Deactivate original mesh renderer
                    MeshRenderer mr = filter.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                }

                // Create combined game object
                GameObject combinedGo = new GameObject($"Combined_{mat.name}");
                combinedGo.transform.SetParent(rootObject.transform);
                combinedGo.transform.localPosition = Vector3.zero;
                combinedGo.transform.localRotation = Quaternion.identity;
                combinedGo.transform.localScale = Vector3.one;
                combinedGo.isStatic = true;

                MeshFilter combinedMf = combinedGo.AddComponent<MeshFilter>();
                MeshRenderer combinedMr = combinedGo.AddComponent<MeshRenderer>();

                Mesh combinedMesh = new Mesh();
                combinedMesh.name = $"Combined_{mat.name}_Mesh";
                // Max vertices is 65535 unless 32-bit indices are used.
                // We'll set indexFormat to 32-bit for large merges.
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

                combinedMf.sharedMesh = combinedMesh;
                combinedMr.sharedMaterial = mat;

                combinedObjects.Add(combinedGo);

#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(combinedGo, "Combine Meshes");
#endif
            }

            Debug.Log($"[DrawCallOptimizer] Combined static meshes of {rootObject.name} into {combinedObjects.Count} material-batch groups.");
            return combinedObjects;
        }
    }
}
