using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace AAAOptimizer.HLOD
{
    public static class MaterialAtlasBuilder
    {
        public static Material BuildAtlas(List<MeshRenderer> renderers, int maxAtlasResolution, int padding, string assetName, out Dictionary<Material, Rect> materialRectMap)
        {
            materialRectMap = new Dictionary<Material, Rect>();
            List<Texture2D> texturesToPack = new List<Texture2D>();
            List<Material> materialsToPack = new List<Material>();

            // Collect unique materials. Texture-only keys lose per-material colors and submesh assignments.
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || materialsToPack.Contains(material)) continue;

                    Texture2D sourceTexture = material.mainTexture as Texture2D;
                    Texture2D packTexture = CreateReadablePackTexture(sourceTexture, GetMaterialColor(material), material.name);
                    texturesToPack.Add(packTexture);
                    materialsToPack.Add(material);
                }
            }

            if (texturesToPack.Count == 0)
            {
                // Fallback material if no textures found
                Material fallbackMat = new Material(Shader.Find("HDRP/Lit"));
                if (fallbackMat.shader == null) fallbackMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (fallbackMat.shader == null) fallbackMat = new Material(Shader.Find("Standard"));
                return fallbackMat;
            }

            // Calculate grid layout
            int count = texturesToPack.Count;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / columns);
            
            int cellSize = maxAtlasResolution / columns;
            
            RenderTexture atlasRT = new RenderTexture(maxAtlasResolution, maxAtlasResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            
            // Clear the render texture
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = atlasRT;
            GL.Clear(false, true, new Color(0, 0, 0, 0));
            
            // Setup AtlasBlit material
            Material blitMat = new Material(Shader.Find("Hidden/AAAOptimizer/AtlasBlit"));
            
            // Render textures into grid slots using AtlasBlit material
            for (int i = 0; i < count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                
                // Calculate absolute pixel rect with padding
                int x = col * cellSize + padding;
                int y = row * cellSize + padding;
                int size = cellSize - 2 * padding;
                
                // Normalized destination rect for the shader
                Rect normRect = new Rect(
                    (float)x / maxAtlasResolution,
                    (float)y / maxAtlasResolution,
                    (float)size / maxAtlasResolution,
                    (float)size / maxAtlasResolution
                );
                
                if (texturesToPack[i] != null)
                {
                    RenderTexture tempRT = RenderTexture.GetTemporary(texturesToPack[i].width, texturesToPack[i].height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(texturesToPack[i], tempRT);
                    
                    // Restrict rasterizer to this slot's pixel bounds to prevent any shader out-of-bounds leakage
                    RenderTexture.active = atlasRT;
                    GL.Viewport(new Rect(x, y, size, size));
                    
                    blitMat.SetVector("_DstRect", new Vector4(normRect.x, normRect.y, normRect.width, normRect.height));
                    Graphics.Blit(tempRT, atlasRT, blitMat);
                    
                    RenderTexture.ReleaseTemporary(tempRT);
                }
                
                // Store normalized UV rect for MeshCombiner
                materialRectMap[materialsToPack[i]] = normRect;
            }
            
            // Restore full viewport
            RenderTexture.active = atlasRT;
            GL.Viewport(new Rect(0, 0, atlasRT.width, atlasRT.height));
            
            Object.DestroyImmediate(blitMat);
            
            // Read back to Texture2D
            Texture2D atlasTex = new Texture2D(maxAtlasResolution, maxAtlasResolution, TextureFormat.RGBA32, true);
            atlasTex.ReadPixels(new Rect(0, 0, maxAtlasResolution, maxAtlasResolution), 0, 0);
            atlasTex.Apply();
            
            RenderTexture.active = prevRT;
            Object.DestroyImmediate(atlasRT);

            // Destroy temporary pack textures to prevent memory leak
            foreach (var tex in texturesToPack)
            {
                if (tex != null)
                {
                    Object.DestroyImmediate(tex);
                }
            }
            texturesToPack.Clear();

#if UNITY_EDITOR
            // In the Editor, serialize the texture atlas asset
            string folderPath = "Assets/AAAOptimizer/Data/Atlases";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            byte[] pngBytes = atlasTex.EncodeToPNG();
            string texPath = $"{folderPath}/{assetName}_Atlas.png";
            File.WriteAllBytes(texPath, pngBytes);
            
            // Clean up the temporary in-memory atlas texture immediately
            Object.DestroyImmediate(atlasTex);
            
            AssetDatabase.ImportAsset(texPath);

            // Set import settings to ensure bilinear and correct compression
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = maxAtlasResolution;
                importer.compressionQuality = 100;
                importer.SaveAndReimport();
            }

            // Load serialized texture
            Texture2D savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            
            // Build and serialize material
            Shader activeShader = Shader.Find("HDRP/Lit");
            if (activeShader == null) activeShader = Shader.Find("Universal Render Pipeline/Lit");
            if (activeShader == null) activeShader = Shader.Find("Standard");

            Material atlasMaterial = new Material(activeShader);
            atlasMaterial.name = $"{assetName}_Material";
            
            // Set texture references on common properties to be completely safe
            atlasMaterial.mainTexture = savedAtlas;
            if (atlasMaterial.HasProperty("_BaseMap")) atlasMaterial.SetTexture("_BaseMap", savedAtlas);
            if (atlasMaterial.HasProperty("_BaseColorMap")) atlasMaterial.SetTexture("_BaseColorMap", savedAtlas);
            
            // Explicitly set base color to white to prevent double tinting
            if (atlasMaterial.HasProperty("_BaseColor")) atlasMaterial.SetColor("_BaseColor", Color.white);
            else if (atlasMaterial.HasProperty("_Color")) atlasMaterial.SetColor("_Color", Color.white);

            // Validate HDRP material via reflection if in HDRP project
            System.Type hdMaterialType = AAAOptimizer.Core.RenderPipelineBridgeUtility.FindType("UnityEngine.Rendering.HighDefinition.HDMaterial");
            if (hdMaterialType != null)
            {
                var validateMethod = hdMaterialType.GetMethod("ValidateMaterial", new System.Type[] { typeof(Material) });
                if (validateMethod != null)
                {
                    validateMethod.Invoke(null, new object[] { atlasMaterial });
                }
            }

            string matPath = $"{folderPath}/{assetName}_Material.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(atlasMaterial, matPath);
            AssetDatabase.SaveAssets();

            return atlasMaterial;
#else
            // Runtime fallback
            Material runtimeMat = new Material(Shader.Find("Standard"));
            runtimeMat.mainTexture = atlasTex;
            if (runtimeMat.HasProperty("_Color")) runtimeMat.SetColor("_Color", Color.white);
            return runtimeMat;
#endif
        }

        private static Color GetMaterialColor(Material material)
        {
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return Color.white;
        }

        private static Texture2D CreateReadablePackTexture(Texture2D source, Color tint, string materialName)
        {
            Texture2D readable;
            if (source != null)
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
            else
            {
                readable = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                readable.SetPixels(pixels);
                readable.Apply();
            }

            if (tint != Color.white)
            {
                Color[] pixels = readable.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] *= tint;
                readable.SetPixels(pixels);
                readable.Apply();
            }

            readable.name = $"{materialName}_HLODAtlasSource";
            return readable;
        }
    }
}
