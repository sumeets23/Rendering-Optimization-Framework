using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AAAOptimizer.Core;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace AAAOptimizer.Impostors
{
    public static class ImpostorBaker
    {
        private struct RendererMaterials
        {
            public Renderer renderer;
            public Material[] materials;
        }

        private struct LightCullingMask
        {
            public Light light;
            public int cullingMask;
        }

        private struct RendererVisibilityState
        {
            public Renderer renderer;
            public bool enabled;
            public ShadowCastingMode shadowCastingMode;
        }

        private struct LODGroupState
        {
            public LODGroup lodGroup;
        }

        private static readonly Color TransparentBlack = new Color(0f, 0f, 0f, 0f);

        private static bool IsHDRPProject()
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            return rp != null && rp.GetType().FullName.Contains("HighDefinition");
        }

        private static Color[] ReconstructStraightAlphaFromBlackWhite(Color[] blackPixels, Color[] whitePixels)
        {
            int len = blackPixels.Length;
            Color[] output = new Color[len];

            for (int i = 0; i < len; i++)
            {
                Color b = blackPixels[i];
                Color w = whitePixels[i];

                float aR = 1f - Mathf.Clamp01(w.r - b.r);
                float aG = 1f - Mathf.Clamp01(w.g - b.g);
                float aB = 1f - Mathf.Clamp01(w.b - b.b);
                float alpha = Mathf.Clamp01((aR + aG + aB) / 3f);

                if (alpha <= 0.0001f)
                {
                    output[i] = Color.clear;
                    continue;
                }

                float linearR = Mathf.Clamp01(b.r / alpha);
                float linearG = Mathf.Clamp01(b.g / alpha);
                float linearB = Mathf.Clamp01(b.b / alpha);

                // ReadRT reads into a linear Texture2D. The atlas PNG is imported as sRGB.
                // Therefore write gamma encoded RGB bytes into the PNG.
                output[i] = new Color(
                    Mathf.LinearToGammaSpace(linearR),
                    Mathf.LinearToGammaSpace(linearG),
                    Mathf.LinearToGammaSpace(linearB),
                    alpha);
            }

            return output;
        }

        private static Color[] ReconstructStraightAlphaFromBlackWhite(
            Color[] blackPixels,
            Color[] whitePixels,
            Color fallbackTint,
            Texture2D fallbackTexture,
            int frameWidth,
            int frameHeight)
        {
            Color[] output = ReconstructStraightAlphaFromBlackWhite(blackPixels, whitePixels);
            Color average = AverageVisibleAtlasFrameColor(output);
            if (!IsBadFallbackColor(average))
                return output;

            bool canUseFallback = fallbackTexture != null || !IsBadFallbackColor(fallbackTint);
            if (!canUseFallback)
                return output;

            Texture2D readableFallbackTexture = fallbackTexture != null
                ? CreateReadableTextureCopy(fallbackTexture)
                : null;

            Color fallbackGamma = new Color(
                Mathf.Clamp01(fallbackTint.r),
                Mathf.Clamp01(fallbackTint.g),
                Mathf.Clamp01(fallbackTint.b),
                1f);

            const float fallbackTextureExposure = 1.25f;
            for (int i = 0; i < output.Length; i++)
            {
                float alpha = output[i].a;
                if (alpha <= 0.01f)
                {
                    output[i] = Color.clear;
                    continue;
                }

                Color rgb = fallbackGamma;
                if (readableFallbackTexture != null)
                {
                    int x = i % Mathf.Max(1, frameWidth);
                    int y = i / Mathf.Max(1, frameWidth);
                    float u = frameWidth <= 1 ? 0.5f : (float)x / (frameWidth - 1);
                    float v = frameHeight <= 1 ? 0.5f : (float)y / (frameHeight - 1);
                    rgb = readableFallbackTexture.GetPixelBilinear(u, v) * fallbackTextureExposure;
                }

                output[i] = new Color(
                    Mathf.Clamp01(rgb.r),
                    Mathf.Clamp01(rgb.g),
                    Mathf.Clamp01(rgb.b),
                    alpha);
            }

            if (readableFallbackTexture != null)
                Object.DestroyImmediate(readableFallbackTexture);

            Debug.LogWarning($"[ImpostorBaker] Final-colour black/white capture looked invalid for this frame " +
                             $"(avg={average}). Used fallback albedo texture={fallbackTexture?.name}, tint={fallbackTint}.");
            return output;
        }


        private static Color[] CombineColorWithAlphaMask(
            Color[] colourPixels,
            Color[] alphaMaskPixels,
            Color fallbackTint,
            Texture2D fallbackTexture,
            int frameWidth,
            int frameHeight)
        {
            int len = colourPixels.Length;
            Color[] output = new Color[len];

            // Detect whether the colour buffer contains HDR values (> 1.0).
            // When HDRP's StandardRequest delivers a post-processed frame the values
            // are already in LDR [0,1] linear space. When it delivers raw scene-linear
            // values they can be far above 1.0 and need to be tone-mapped.
            float maxLum = 0f;
            for (int i = 0; i < len; i++)
            {
                Color m = alphaMaskPixels[i];
                float alpha = Mathf.Clamp01((m.r + m.g + m.b) / 3f);
                if (alpha <= 0.01f) continue;
                Color c = colourPixels[i];
                float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                if (lum > maxLum) maxLum = lum;
            }

            // Only apply exposure correction when the render is genuinely HDR (> 1.2).
            // Values below this threshold are treated as already-tonemapped LDR output.
            bool isHDR = maxLum > 1.2f;
            float exposure = 1f;
            if (isHDR)
            {
                // Bring the 95th-percentile luminance down to ~0.68 (a commonly
                // calibrated middle grey for filmic tone-mapping).
                var visibleLuminance = new System.Collections.Generic.List<float>(len / 4);
                for (int i = 0; i < len; i++)
                {
                    Color m = alphaMaskPixels[i];
                    float alpha = Mathf.Clamp01((m.r + m.g + m.b) / 3f);
                    if (alpha <= 0.15f) continue;

                    Color c = colourPixels[i];
                    float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    if (lum > 0.0001f) visibleLuminance.Add(lum);
                }
                if (visibleLuminance.Count > 0)
                {
                    visibleLuminance.Sort();
                    int p95 = Mathf.Clamp(Mathf.RoundToInt(visibleLuminance.Count * 0.95f), 0, visibleLuminance.Count - 1);
                    exposure = 0.68f / Mathf.Max(visibleLuminance[p95], 0.0001f);
                }
            }

            Color visibleAverage = AverageVisibleColor(colourPixels, alphaMaskPixels, exposure, isHDR);
            bool useFallback = IsBadFallbackColor(visibleAverage) && (fallbackTexture != null || !IsBadFallbackColor(fallbackTint));
            Texture2D readableFallbackTexture = useFallback && fallbackTexture != null
                ? CreateReadableTextureCopy(fallbackTexture)
                : null;
            Color fallbackLinear = new Color(
                Mathf.GammaToLinearSpace(Mathf.Clamp01(fallbackTint.r)),
                Mathf.GammaToLinearSpace(Mathf.Clamp01(fallbackTint.g)),
                Mathf.GammaToLinearSpace(Mathf.Clamp01(fallbackTint.b)),
                1f);

            for (int i = 0; i < len; i++)
            {
                Color c = colourPixels[i];
                Color m = alphaMaskPixels[i];

                float alpha = Mathf.Clamp01((m.r + m.g + m.b) / 3f);

                if (alpha <= 0.01f)
                {
                    output[i] = Color.clear;
                    continue;
                }

                float r = Mathf.Max(0f, c.r * exposure);
                float g = Mathf.Max(0f, c.g * exposure);
                float b = Mathf.Max(0f, c.b * exposure);

                if (useFallback)
                {
                    if (readableFallbackTexture != null)
                    {
                        const float fallbackTextureExposure = 1.6f;
                        int x = i % Mathf.Max(1, frameWidth);
                        int y = i / Mathf.Max(1, frameWidth);
                        float u = frameWidth <= 1 ? 0.5f : (float)x / (frameWidth - 1);
                        float v = frameHeight <= 1 ? 0.5f : (float)y / (frameHeight - 1);
                        Color sample = readableFallbackTexture.GetPixelBilinear(u, v);
                        r = Mathf.GammaToLinearSpace(Mathf.Clamp01(sample.r)) * fallbackTextureExposure;
                        g = Mathf.GammaToLinearSpace(Mathf.Clamp01(sample.g)) * fallbackTextureExposure;
                        b = Mathf.GammaToLinearSpace(Mathf.Clamp01(sample.b)) * fallbackTextureExposure;
                    }
                    else
                    {
                        r = fallbackLinear.r;
                        g = fallbackLinear.g;
                        b = fallbackLinear.b;
                    }
                }

                if (isHDR)
                {
                    // Soft-shoulder tone-map: preserves colour ratios while
                    // bringing bright HDR values into [0,1] without hard clipping.
                    r = r / (1f + Mathf.Max(0f, r - 1f));
                    g = g / (1f + Mathf.Max(0f, g - 1f));
                    b = b / (1f + Mathf.Max(0f, b - 1f));
                }

                r = Mathf.Clamp01(r);
                g = Mathf.Clamp01(g);
                b = Mathf.Clamp01(b);

                // ReadRT reads into a linear Texture2D. The atlas PNG is imported as
                // sRGB. Write gamma-encoded bytes so the importer produces the correct
                // linear values when sampling at runtime.
                output[i] = new Color(
                    Mathf.LinearToGammaSpace(r),
                    Mathf.LinearToGammaSpace(g),
                    Mathf.LinearToGammaSpace(b),
                    alpha);
            }

            if (readableFallbackTexture != null)
                Object.DestroyImmediate(readableFallbackTexture);

            Debug.Log($"[ImpostorBaker] Colour pass: isHDR={isHDR}, maxLum={maxLum:F3}, exposure={exposure:F3}, " +
                      $"avg={visibleAverage}, fallback={fallbackTint}, fallbackTexture={fallbackTexture?.name}, " +
                      $"usedFallback={useFallback}");
            return output;
        }


        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Layer & visibility helpers
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void SetLayerRecursively(GameObject go, int newLayer, Dictionary<GameObject, int> originalLayers)
        {
            if (go == null) return;
            originalLayers[go] = go.layer;
            go.layer = newLayer;
            foreach (Transform t in go.transform)
                SetLayerRecursively(t.gameObject, newLayer, originalLayers);
        }

        private static void RestoreLayers(Dictionary<GameObject, int> originalLayers)
        {
            foreach (var kvp in originalLayers)
                if (kvp.Key != null) kvp.Key.layer = kvp.Value;
        }

        private static List<LightCullingMask> IncludeLayerInSceneLights(int layer)
        {
            var savedMasks = new List<LightCullingMask>();
            int layerMask = 1 << layer;
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light light in lights)
            {
                if (light == null || (light.cullingMask & layerMask) != 0) continue;
                savedMasks.Add(new LightCullingMask { light = light, cullingMask = light.cullingMask });
                light.cullingMask |= layerMask;
            }
            return savedMasks;
        }

        private static void RestoreLightCullingMasks(List<LightCullingMask> savedMasks)
        {
            foreach (var s in savedMasks)
                if (s.light != null) s.light.cullingMask = s.cullingMask;
        }

        private static List<RendererVisibilityState> HideNonTargetRenderersFromCapture(GameObject targetRoot)
        {
            var savedStates = new List<RendererVisibilityState>();
            var targetRenderers = new HashSet<Renderer>(targetRoot.GetComponentsInChildren<Renderer>(true));
            Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            foreach (Renderer r in allRenderers)
            {
                if (r == null || targetRenderers.Contains(r) || !r.enabled) continue;
                savedStates.Add(new RendererVisibilityState
                {
                    renderer = r,
                    enabled = r.enabled,
                    shadowCastingMode = r.shadowCastingMode
                });
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            return savedStates;
        }

        private static void RestoreRendererVisibilityStates(List<RendererVisibilityState> savedStates)
        {
            foreach (var s in savedStates)
            {
                if (s.renderer != null)
                {
                    s.renderer.enabled = s.enabled;
                    s.renderer.shadowCastingMode = s.shadowCastingMode;
                }
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Material helpers
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static List<RendererMaterials> SaveAndReplaceMaterials(GameObject root, Material replacementMat)
        {
            var savedList = new List<RendererMaterials>();
            if (replacementMat == null) return savedList;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                savedList.Add(new RendererMaterials { renderer = r, materials = r.sharedMaterials });
                Material[] newMats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < newMats.Length; i++) newMats[i] = replacementMat;
                r.sharedMaterials = newMats;
            }
            return savedList;
        }

        private static void RestoreMaterials(List<RendererMaterials> savedList)
        {
            foreach (var item in savedList)
                if (item.renderer != null) item.renderer.sharedMaterials = item.materials;
        }

        private static bool IsBadFallbackColor(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            float sat = max <= 0.0001f ? 0f : (max - min) / max;

            bool nearWhite = c.r > 0.75f && c.g > 0.75f && c.b > 0.75f && sat < 0.2f;
            bool nearBlack = max < 0.04f;
            bool greenMask = c.g > c.r * 1.10f && c.g > c.b * 1.05f;
            bool blueNormal = c.b > c.r * 1.15f && c.b > c.g * 1.10f;

            return nearWhite || nearBlack || greenMask || blueNormal;
        }

        private static Color AverageReadableTexture(Texture2D texture)
        {
            if (texture == null)
                return Color.white;

#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            bool changedReadable = false;

            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                changedReadable = true;
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
#endif

            try
            {
                int stepX = Mathf.Max(1, texture.width / 40);
                int stepY = Mathf.Max(1, texture.height / 40);
                Color total = Color.clear;
                int count = 0;

                for (int y = 0; y < texture.height; y += stepY)
                {
                    for (int x = 0; x < texture.width; x += stepX)
                    {
                        Color c = texture.GetPixel(x, y);
                        if (c.a <= 0.05f || IsBadFallbackColor(c))
                            continue;

                        total += new Color(c.r, c.g, c.b, 1f);
                        count++;
                    }
                }

                return count > 0 ? total / count : Color.white;
            }
            catch
            {
                return Color.white;
            }
#if UNITY_EDITOR
            finally
            {
                if (changedReadable && importer != null)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
#endif
        }

        private static bool IsLikelyNonAlbedoTexture(string propertyName, Texture2D texture)
        {
            string key = $"{propertyName} {texture?.name}".ToLowerInvariant();
            return key.Contains("normal") ||
                   key.Contains("_n") ||
                   key.Contains("_orm") ||
                   key.Contains("mask") ||
                   key.Contains("rough") ||
                   key.Contains("metal") ||
                   key.Contains("occlusion") ||
                   key.Contains("ao") ||
                   key.Contains("height") ||
                   key.Contains("bump") ||
                   key.Contains("specular") ||
                   key.Contains("smooth");
        }

        private static bool IsLikelyAlbedoTexture(string propertyName, Texture2D texture)
        {
            string key = $"{propertyName} {texture?.name}".ToLowerInvariant();
            return key.Contains("base") ||
                   key.Contains("albedo") ||
                   key.Contains("diffuse") ||
                   key.Contains("color") ||
                   key.Contains("colour") ||
                   key.Contains("_bc") ||
                   key.Contains("bc.");
        }

        private static Color TryAverageMaterialTexture(Material material, string propertyName)
        {
            if (material == null || string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
                return Color.white;

            Texture2D tex = material.GetTexture(propertyName) as Texture2D;
            if (tex == null || IsLikelyNonAlbedoTexture(propertyName, tex))
                return Color.white;

            Color avg = AverageReadableTexture(tex);
            return IsBadFallbackColor(avg) ? Color.white : avg;
        }

        private static Texture2D CreateReadableTextureCopy(Texture2D source)
        {
            if (source == null)
                return null;

            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            copy.name = $"{source.name}_ReadableCopy";

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        private static Texture2D GetMaterialAlbedoFallbackTexture(Material material)
        {
            if (material == null)
                return null;

            string[] preferredNames =
            {
                "_BaseColorMap",
                "_BaseMap",
                "_MainTex",
                "_UnlitColorMap",
                "_AlbedoMap",
                "_DiffuseMap",
                "_BaseColorTexture",
                "_ColorMap"
            };

            foreach (string propertyName in preferredNames)
            {
                if (!material.HasProperty(propertyName))
                    continue;

                Texture2D tex = material.GetTexture(propertyName) as Texture2D;
                if (tex != null && !IsLikelyNonAlbedoTexture(propertyName, tex))
                    return tex;
            }

#if UNITY_EDITOR
            if (material.shader != null)
            {
                Texture2D firstUsableTexture = null;
                int propertyCount = material.shader.GetPropertyCount();

                for (int i = 0; i < propertyCount; i++)
                {
                    if (material.shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
                        continue;

                    string propertyName = material.shader.GetPropertyName(i);
                    Texture2D tex = material.GetTexture(propertyName) as Texture2D;
                    if (tex == null || IsLikelyNonAlbedoTexture(propertyName, tex))
                        continue;

                    if (IsLikelyAlbedoTexture(propertyName, tex))
                        return tex;

                    if (firstUsableTexture == null)
                        firstUsableTexture = tex;
                }

                return firstUsableTexture;
            }
#endif

            return null;
        }

        private static Color GetMaterialAlbedoFallback(Material material)
        {
            if (material == null)
                return Color.white;

            string[] textureNames =
            {
                "_BaseColorMap",
                "_BaseMap",
                "_MainTex",
                "_UnlitColorMap",
                "_AlbedoMap",
                "_DiffuseMap",
                "_BaseColorTexture",
                "_ColorMap"
            };

            foreach (string propertyName in textureNames)
            {
                if (!material.HasProperty(propertyName))
                    continue;

                Texture2D tex = material.GetTexture(propertyName) as Texture2D;
                if (tex == null)
                    continue;

                Color avg = AverageReadableTexture(tex);
                if (!IsBadFallbackColor(avg))
                    return avg;
            }

#if UNITY_EDITOR
            if (material.shader != null)
            {
                Color bestFallback = Color.white;
                int propertyCount = material.shader.GetPropertyCount();

                for (int i = 0; i < propertyCount; i++)
                {
                    if (material.shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
                        continue;

                    string propertyName = material.shader.GetPropertyName(i);
                    Texture2D tex = material.GetTexture(propertyName) as Texture2D;
                    if (tex == null || IsLikelyNonAlbedoTexture(propertyName, tex))
                        continue;

                    Color avg = AverageReadableTexture(tex);
                    if (IsBadFallbackColor(avg))
                        continue;

                    if (IsLikelyAlbedoTexture(propertyName, tex))
                        return avg;

                    bestFallback = avg;
                }

                if (!IsBadFallbackColor(bestFallback))
                    return bestFallback;
            }
#endif

            string[] colorNames =
            {
                "_BaseColor",
                "_Color",
                "_UnlitColor",
                "_AlbedoColor",
                "_Tint",
                "_Base_Color",
                "_LeatherColor",
                "_FabricColor",
                "_MainColor"
            };

            foreach (string propertyName in colorNames)
            {
                if (!material.HasProperty(propertyName))
                    continue;

                Color c = material.GetColor(propertyName);
                if (!IsBadFallbackColor(c))
                    return c;
            }

            return Color.white;
        }

        private static Color ComputeDominantMaterialFallback(GameObject root, out Texture2D fallbackTexture)
        {
            fallbackTexture = null;

            if (root == null)
                return new Color(0.64f, 0.36f, 0.20f, 1f);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            Color total = Color.clear;
            int count = 0;

            foreach (Renderer r in renderers)
            {
                if (r == null)
                    continue;

                Material[] mats = r.sharedMaterials;
                foreach (Material m in mats)
                {
                    if (fallbackTexture == null)
                        fallbackTexture = GetMaterialAlbedoFallbackTexture(m);

                    Color c = GetMaterialAlbedoFallback(m);
                    if (IsBadFallbackColor(c))
                        continue;

                    total += new Color(c.r, c.g, c.b, 1f);
                    count++;
                }
            }

            if (count <= 0)
            {
                // Last safe fallback for this leather furniture case.
                return new Color(0.64f, 0.36f, 0.20f, 1f);
            }

            Color result = total / count;
            result.a = 1f;
            return result;
        }

        private static float Saturation(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max <= 0.0001f ? 0f : (max - min) / max;
        }

        private static Color AverageVisibleColor(Color[] colourPixels, Color[] alphaMaskPixels, float exposure, bool toneMapHDR)
        {
            Color total = Color.clear;
            int count = 0;

            for (int i = 0; i < colourPixels.Length; i++)
            {
                Color m = alphaMaskPixels[i];
                float alpha = Mathf.Clamp01((m.r + m.g + m.b) / 3f);
                if (alpha <= 0.01f)
                    continue;

                Color c = colourPixels[i];
                float r = Mathf.Max(0f, c.r * exposure);
                float g = Mathf.Max(0f, c.g * exposure);
                float b = Mathf.Max(0f, c.b * exposure);

                if (toneMapHDR)
                {
                    r = r / (1f + Mathf.Max(0f, r - 1f));
                    g = g / (1f + Mathf.Max(0f, g - 1f));
                    b = b / (1f + Mathf.Max(0f, b - 1f));
                }

                total += new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
                count++;
            }

            if (count == 0)
                return Color.clear;

            Color average = total / count;
            average.a = 1f;
            return average;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // HDRP / URP reflection helpers
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static Color AverageVisibleAtlasFrameColor(Color[] pixels)
        {
            Color total = Color.clear;
            int count = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.a <= 0.01f)
                    continue;

                total += new Color(c.r, c.g, c.b, 1f);
                count++;
            }

            if (count == 0)
                return Color.clear;

            Color average = total / count;
            average.a = 1f;
            return average;
        }

        private static Color AverageAtlasVisibleColor(Texture2D atlas)
        {
            if (atlas == null)
                return Color.clear;

            Color[] pixels = atlas.GetPixels();
            Color total = Color.clear;
            int count = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.a <= 0.01f)
                    continue;

                total += new Color(c.r, c.g, c.b, 1f);
                count++;
            }

            if (count == 0)
                return Color.clear;

            Color average = total / count;
            average.a = 1f;
            return average;
        }

        private static void WarnIfAtlasLooksLikeSilhouette(Texture2D atlas, Color fallbackTint)
        {
            Color average = AverageAtlasVisibleColor(atlas);
            if (!IsBadFallbackColor(average))
                return;

            Debug.LogWarning($"[ImpostorBaker] Albedo atlas still looks like a silhouette or mask. " +
                             $"Average visible colour={average}, fallback={fallbackTint}. " +
                             "Check that the bake target is the original mesh, not an existing impostor prefab.");
        }

        private static void SetHDRPValue(System.Type hdCamDataType, Component hdCamData, string memberName, object value)
        {
            if (hdCamDataType == null || hdCamData == null) return;
            var prop = hdCamDataType.GetProperty(memberName);
            if (prop != null && prop.CanWrite) { try { prop.SetValue(hdCamData, value); } catch { } return; }
            var field = hdCamDataType.GetField(memberName);
            if (field != null) { try { field.SetValue(hdCamData, value); } catch { } }
        }

        private static void SetHDRPBackgroundColor(System.Type hdCamDataType, Component hdCamData, Color color)
            => SetHDRPValue(hdCamDataType, hdCamData, "backgroundColorHDR", color);

        private static void SetHDRPClearColorMode(System.Type hdCamDataType, Component hdCamData, int mode)
        {
            if (hdCamDataType == null || hdCamData == null) return;
            var prop = hdCamDataType.GetProperty("clearColorMode");
            var field = hdCamDataType.GetField("clearColorMode");
            System.Type enumType = prop != null ? prop.PropertyType : field?.FieldType;
            if (enumType == null) return;
            object clearMode = System.Enum.ToObject(enumType, mode);
            if (prop != null && prop.CanWrite) { try { prop.SetValue(hdCamData, clearMode); } catch { } }
            else if (field != null) { try { field.SetValue(hdCamData, clearMode); } catch { } }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // RenderTexture helpers
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void ClearRT(RenderTexture rt, Color color)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, color);
            RenderTexture.active = prev;
        }

        private static void RenderCameraToTarget(Camera cam, RenderTexture destination)
        {
            if (cam == null || destination == null)
                return;

            // HDRP/Unity 6 correct render path.
            // Camera.Render() often gives wrong HDRP results in editor bake tools.
            var request = new RenderPipeline.StandardRequest
            {
                destination = destination
            };

            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
                return;
            }

            RenderTexture previousTarget = cam.targetTexture;
            cam.targetTexture = destination;
            cam.Render();
            cam.targetTexture = previousTarget;
        }

        private static Color[] ReadRT(RenderTexture rt, int w, int h)
        {
            RenderTexture.active = rt;
            // Read into a linear Texture2D â€” values remain in linear space.
            // We will convert to gamma ourselves before encoding to PNG so that
            // the sRGB texture importer receives properly gamma-encoded bytes.
            Texture2D tmp = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tmp.Apply();
            Color[] pixels = tmp.GetPixels();
            Object.DestroyImmediate(tmp);
            return pixels;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Alpha extraction â€” white / black dual-render method
        //
        //   white_render: C = obj_rgb * Î±  +  1 * (1-Î±)   â†’  C_w
        //   black_render: C = obj_rgb * Î±  +  0 * (1-Î±)   â†’  C_b  (= obj_rgb * Î±)
        //
        //   Î±   = 1 - (C_w.r - C_b.r)      (average over RGB for robustness)
        //   rgb = C_b / Î±                   (un-premultiply)
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static Color[] ExtractAlphaWhiteBlack(Color[] whitePixels, Color[] blackPixels)
        {
            Color[] result = new Color[whitePixels.Length];
            for (int p = 0; p < result.Length; p++)
            {
                Color w = whitePixels[p];
                Color b = blackPixels[p];

                // Both renders are in linear space from the RT.
                // Derive alpha from the diff of each channel and pick the max for reliability
                float dR = Mathf.Clamp01(w.r - b.r);
                float dG = Mathf.Clamp01(w.g - b.g);
                float dB = Mathf.Clamp01(w.b - b.b);
                float alpha = Mathf.Clamp01(1.0f - (dR + dG + dB) / 3.0f);

                if (alpha < 0.01f)
                {
                    result[p] = Color.clear;
                }
                else
                {
                    // Un-premultiply: the black render already gives us obj * alpha
                    float invA = 1.0f / alpha;
                    // Convert linear RGB to gamma (sRGB) before writing to the CPU texture
                    // so that when Unity imports with sRGBTexture=true the values are correct.
                    float rLinear = Mathf.Clamp01(b.r * invA);
                    float gLinear = Mathf.Clamp01(b.g * invA);
                    float bLinear = Mathf.Clamp01(b.b * invA);
                    result[p] = new Color(
                        Mathf.LinearToGammaSpace(rLinear),
                        Mathf.LinearToGammaSpace(gLinear),
                        Mathf.LinearToGammaSpace(bLinear),
                        alpha);
                }
            }
            return result;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Shader / renderer helpers
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static Shader FindCaptureShader(string shaderName, string assetPath)
        {
            Shader s = Shader.Find(shaderName);
#if UNITY_EDITOR
            if (s == null) s = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
#endif
            return s;
        }

        private static Renderer[] GetBakeRenderers(GameObject targetRoot)
        {
            LODGroup lodGroup = targetRoot.GetComponentInChildren<LODGroup>();
            if (lodGroup != null)
            {
                LOD[] lods = lodGroup.GetLODs();
                if (lods.Length > 0 && lods[0].renderers != null && lods[0].renderers.Length > 0)
                {
                    var lod0 = new List<Renderer>();
                    foreach (Renderer r in lods[0].renderers)
                        if (r != null) lod0.Add(r);
                    if (lod0.Count > 0) return lod0.ToArray();
                }
            }
            return targetRoot.GetComponentsInChildren<Renderer>();
        }

        private static List<LODGroupState> ForceLOD0(GameObject targetRoot)
        {
            var states = new List<LODGroupState>();
            foreach (LODGroup lg in targetRoot.GetComponentsInChildren<LODGroup>(true))
            {
                if (lg == null) continue;
                states.Add(new LODGroupState { lodGroup = lg });
                lg.ForceLOD(0);
            }
            return states;
        }

        private static void RestoreLODGroups(List<LODGroupState> states)
        {
            foreach (var s in states)
                if (s.lodGroup != null) s.lodGroup.ForceLOD(-1);
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Tight-crop orthographic size calculation
        //
        // We compute the largest half-extent the camera will ever need regardless
        // of which yaw angle we are capturing from so every frame is consistently
        // framed.  For a pure yaw-only orbit (horizontal circle around the object)
        // the worst-case horizontal half-extent at any angle is the circumscribed
        // radius of the XZ footprint, i.e. half of sqrt(sizeXÂ² + sizeZÂ²).
        // The vertical half-extent is simply half the object height.
        // We take the max of the two to keep a square ortho view that tightly fits
        // the tallest / widest possible silhouette and nothing more.
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static float ComputeTightOrthoHalfSize(Bounds b)
        {
            float halfHorizontal = Mathf.Sqrt(b.size.x * b.size.x + b.size.z * b.size.z) * 0.5f;
            float halfVertical = b.size.y * 0.5f;
            // Add a tiny 1 % margin so the silhouette edge is never clipped by a
            // sub-pixel rounding artefact.
            return Mathf.Max(halfHorizontal, halfVertical) * 1.01f;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Main bake entry point
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public static void Bake(GameObject target, int directions, int atlasSize, int frameRes, float swapDistance)
        {
            if (target == null) return;
            directions = Mathf.Max(1, directions);
            atlasSize = Mathf.Max(64, atlasSize);

#if UNITY_EDITOR
            string folderPath = "Assets/AAAOptimizer/Data/Impostors";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            string targetNameClean = target.name;
            foreach (char c in Path.GetInvalidFileNameChars())
                targetNameClean = targetNameClean.Replace(c, '_');
            targetNameClean = targetNameClean.Replace(" ", "_");

            bool isPrefab = string.IsNullOrEmpty(target.scene.name);
            GameObject tempInstance = null;
            GameObject bakeTarget = target;
            if (isPrefab)
            {
                tempInstance = Application.isPlaying
                    ? Object.Instantiate(target)
                    : (GameObject)PrefabUtility.InstantiatePrefab(target);
            }
            else
            {
                tempInstance = Object.Instantiate(target);
            }

            if (tempInstance != null)
            {
                tempInstance.transform.position = target.transform.position;
                tempInstance.transform.rotation = target.transform.rotation;
                tempInstance.transform.localScale = target.transform.lossyScale;
                bakeTarget = tempInstance;
            }

            List<LODGroupState> forcedLODGroups = ForceLOD0(bakeTarget);

            Renderer[] renderers = GetBakeRenderers(bakeTarget);
            if (renderers.Length == 0)
            {
                RestoreLODGroups(forcedLODGroups);
                if (tempInstance != null) Object.DestroyImmediate(tempInstance);
                return;
            }

            // â”€â”€ Compute world-space bounds from LOD0 renderers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            // â”€â”€ Tight-crop orthographic size (see comment above helper) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            float orthoHalfSize = ComputeTightOrthoHalfSize(bounds);
            float captureViewSize = orthoHalfSize * 2.0f;  // full side length of the square view

            // Camera orbit distance: place camera far enough that the near-clip
            // never clips the object regardless of its depth along any yaw angle.
            // The object's depth from centre = half of the XZ diagonal.
            float halfDepth = Mathf.Sqrt(bounds.size.x * bounds.size.x + bounds.size.z * bounds.size.z) * 0.5f;
            float orbitDistance = halfDepth + bounds.size.magnitude; // generous but finite
            float nearClip = Mathf.Max(0.01f, orbitDistance - halfDepth - 0.1f);
            float farClip = orbitDistance + halfDepth + bounds.size.magnitude;

            // â”€â”€ Atlas layout: use ceil(sqrt) Ã— ceil(sqrt) to fit all directions â”€â”€
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(directions)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)directions / cols));
            int totalFrames = cols * rows;                              // may be >= directions
            frameRes = Mathf.Clamp(frameRes, 16, Mathf.Min(atlasSize / cols, atlasSize / rows));
            int atlasWidth = cols * frameRes;
            int atlasHeight = rows * frameRes;

            // â”€â”€ Spawn orthographic capture camera â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            GameObject camGo = new GameObject("_ImpostorCaptureCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoHalfSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = TransparentBlack;
            cam.nearClipPlane = nearClip;
            cam.farClipPlane = farClip;
            cam.allowHDR = true;
            cam.allowMSAA = false;

            // â”€â”€ HDRP â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            System.Type hdCamDataType = RenderPipelineBridgeUtility.FindType(
                "UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData");
            Component hdCamData = null;
            if (hdCamDataType != null)
            {
                hdCamData = camGo.AddComponent(hdCamDataType);

                // Include all layers in HDRP volume evaluation
                var volumeLayerMaskField = hdCamDataType.GetField("volumeLayerMask");
                if (volumeLayerMaskField != null)
                    // volumeLayerMaskField.SetValue(hdCamData, (LayerMask)0);

                // Transparent/solid-color background so sky is never captured
                SetHDRPClearColorMode(hdCamDataType, hdCamData, 1);
                SetHDRPBackgroundColor(hdCamDataType, hdCamData, TransparentBlack);

                // NOTE: leave frameSettings at pipeline defaults so HDRP post-processing
                // (tonemapping, exposure, colour grading) runs normally on this camera.
                // This makes the baked atlas colours match the in-game appearance exactly.
                // Our CombineColorWithAlphaMask detects whether the RT is HDR or LDR and
                // skips unnecessary tone-mapping when post-processing already ran.
            }


            // â”€â”€ URP â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            System.Type urpCamDataType = RenderPipelineBridgeUtility.FindType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            if (urpCamDataType != null)
            {
                Component urpCamData = camGo.GetComponent(urpCamDataType)
                                    ?? camGo.AddComponent(urpCamDataType);
                if (urpCamData != null)
                {
                    var ppProp = urpCamDataType.GetProperty("renderPostProcessing");
                    if (ppProp != null) ppProp.SetValue(urpCamData, false);

                    var aaProp = urpCamDataType.GetProperty("antialiasing");
                    if (aaProp != null)
                    {
                        System.Type aaType = RenderPipelineBridgeUtility.FindType(
                            "UnityEngine.Rendering.Universal.AntialiasingMode");
                        if (aaType != null) aaProp.SetValue(urpCamData, System.Enum.ToObject(aaType, 0));
                    }

                    // Force solid-color background so the skybox is never captured
                    var bgProp = urpCamDataType.GetProperty("backgroundType");
                    if (bgProp != null)
                    {
                        try
                        {
                            object bgVal = null;
                            foreach (var name in System.Enum.GetNames(bgProp.PropertyType))
                            {
                                if (name == "Color" || name == "SolidColor")
                                { bgVal = System.Enum.Parse(bgProp.PropertyType, name); break; }
                            }
                            if (bgVal == null)
                            {
                                System.Type bgEnumType = RenderPipelineBridgeUtility.FindType(
                                    "UnityEngine.Rendering.Universal.CameraBackgroundType");
                                if (bgEnumType != null) bgVal = System.Enum.ToObject(bgEnumType, 1);
                            }
                            if (bgVal != null) bgProp.SetValue(urpCamData, bgVal);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[ImpostorBaker] Failed to set URP backgroundType: {ex.Message}");
                        }
                    }
                }
            }

            // â”€â”€ Render textures â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // Two RTs for the dual-render (white bg + black bg) alpha extraction
            RenderTexture rtCapture = new RenderTexture(
                frameRes, frameRes, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Default);
            cam.targetTexture = rtCapture;

            // CPU atlas - linear=false so EncodeToPNG outputs gamma-corrected bytes
            // that match Unity's sRGB texture import pipeline exactly.
            Texture2D colorAtlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, true, false);
            Color[] clearColors = new Color[atlasWidth * atlasHeight];
            colorAtlas.SetPixels(clearColors);

            Texture2D normalAtlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBAFloat, true, true);
            Color[] clearNormals = new Color[atlasWidth * atlasHeight];
            for (int i = 0; i < clearNormals.Length; i++) clearNormals[i] = new Color(0.5f, 0.5f, 1.0f, 1f);
            normalAtlas.SetPixels(clearNormals);

            Shader normalShader = FindCaptureShader("Debug/ImpostorNormalCapture",
                "Assets/AAAOptimizer/Shaders/Debug/ImpostorNormalCapture.shader");
            Shader depthShader = FindCaptureShader("Debug/ImpostorDepthCapture",
                "Assets/AAAOptimizer/Shaders/Debug/ImpostorDepthCapture.shader");
            Shader alphaShader = FindCaptureShader("Debug/ImpostorAlphaCapture",
                "Assets/AAAOptimizer/Shaders/Debug/ImpostorAlphaCapture.shader");

            Material normalTempMat = normalShader != null ? new Material(normalShader) : null;
            Material depthTempMat = depthShader != null ? new Material(depthShader) : null;
            Material alphaTempMat = alphaShader != null ? new Material(alphaShader) : null;

            if (normalTempMat == null)
                Debug.LogWarning("[ImpostorBaker] Debug/ImpostorNormalCapture shader not found â€” normal atlas will be skipped.");
            if (depthTempMat == null)
                Debug.LogWarning("[ImpostorBaker] Debug/ImpostorDepthCapture shader not found â€” depth pass will be skipped.");
            if (alphaTempMat == null)
                Debug.LogWarning("[ImpostorBaker] Debug/ImpostorAlphaCapture shader not found - alpha silhouettes will be reconstructed from black/white fallback.");

            // â”€â”€ Move target to capture layer (31) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var originalLayers = new Dictionary<GameObject, int>();
            SetLayerRecursively(bakeTarget, 31, originalLayers);

            List<RendererVisibilityState> hiddenSceneRenderers = HideNonTargetRenderersFromCapture(bakeTarget);
            cam.cullingMask = 1 << 31;

            bool originalFog = RenderSettings.fog;
            RenderSettings.fog = false;

            Texture2D fallbackTexture;
            Color fallbackTint = ComputeDominantMaterialFallback(bakeTarget, out fallbackTexture);
            Debug.Log($"[ImpostorBaker] Source material fallback tint: {fallbackTint}, texture: {fallbackTexture?.name}");

            // â”€â”€ Capture loop â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Vector3 center = bounds.center;

            for (int i = 0; i < totalFrames; i++)
            {
                int col = i % cols;
                int row = i / cols;
                int atlasRow = (rows - 1) - row;   // row 0 at top of atlas PNG

                // For frames beyond the requested direction count, copy frame 0
                // (keeps a valid atlas without transparent holes)
                int captureIndex = i < directions ? i : 0;

                // â”€â”€ Local-space orbit so frame indices match runtime Atan2 â”€â”€â”€â”€â”€â”€â”€â”€
                // Atan2(toCamera.x, toCamera.z) = 0 when camera is at local +Z.
                // Frame 0 = camera at local +Z, frame N/4 = camera at local +X, etc.
                float angle = ((float)captureIndex / directions) * Mathf.PI * 2.0f;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 worldDir = target.transform.rotation * dir;

                // Place camera at the orbit distance from the object's bounding centre
                camGo.transform.position = center + worldDir * orbitDistance;
                camGo.transform.LookAt(center, target.transform.up);

                // Render source colour over neutral grey. A black/transparent background drives HDRP exposure too high and produces a white silhouette; alpha is still taken from the mask pass below.
                Color colorCaptureBackground = new Color(0.5f, 0.5f, 0.5f, 1.0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = colorCaptureBackground;
                if (hdCamData != null) SetHDRPClearColorMode(hdCamDataType, hdCamData, 1); // 1 = Color
                if (hdCamData != null) SetHDRPBackgroundColor(hdCamDataType, hdCamData, colorCaptureBackground);
                ClearRT(rtCapture, colorCaptureBackground);
                RenderCameraToTarget(cam, rtCapture);
                Color[] colourPixels = ReadRT(rtCapture, frameRes, frameRes);

                Color[] colorPixels;
                if (alphaTempMat != null)
                {
                    var savedAlphaMats = SaveAndReplaceMaterials(bakeTarget, alphaTempMat);

                    cam.clearFlags = CameraClearFlags.SolidColor;
                    if (hdCamData != null) SetHDRPClearColorMode(hdCamDataType, hdCamData, 1); // 1 = Color
                    cam.backgroundColor = Color.black;
                    if (hdCamData != null) SetHDRPBackgroundColor(hdCamDataType, hdCamData, Color.black);
                    ClearRT(rtCapture, Color.black);
                    RenderCameraToTarget(cam, rtCapture);
                    Color[] alphaMaskPixels = ReadRT(rtCapture, frameRes, frameRes);

                    RestoreMaterials(savedAlphaMats);
                    colorPixels = CombineColorWithAlphaMask(
                        colourPixels,
                        alphaMaskPixels,
                        fallbackTint,
                        fallbackTexture,
                        frameRes,
                        frameRes);
                }
                else
                {
                    cam.backgroundColor = Color.white;
                    if (hdCamData != null) SetHDRPBackgroundColor(hdCamDataType, hdCamData, Color.white);
                    ClearRT(rtCapture, Color.white);
                    RenderCameraToTarget(cam, rtCapture);
                    Color[] whitePixels = ReadRT(rtCapture, frameRes, frameRes);

                    colorPixels = ReconstructStraightAlphaFromBlackWhite(
                        colourPixels,
                        whitePixels,
                        fallbackTint,
                        fallbackTexture,
                        frameRes,
                        frameRes);
                }

                colorAtlas.SetPixels(col * frameRes, atlasRow * frameRes, frameRes, frameRes, colorPixels);
                
                // Pass C: world-space normals
                Color[] normalRaw = null;
                if (normalTempMat != null)
                {
                    var savedMats = SaveAndReplaceMaterials(bakeTarget, normalTempMat);
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    if (hdCamData != null) SetHDRPClearColorMode(hdCamDataType, hdCamData, 1); // 1 = Color
                    cam.backgroundColor = new Color(0.5f, 0.5f, 1.0f, 0f);
                    if (hdCamData != null)
                        SetHDRPBackgroundColor(hdCamDataType, hdCamData, new Color(0.5f, 0.5f, 1.0f, 0f));
                    ClearRT(rtCapture, new Color(0.5f, 0.5f, 1.0f, 0f));
                    RenderCameraToTarget(cam, rtCapture);
                    normalRaw = ReadRT(rtCapture, frameRes, frameRes);
                    RestoreMaterials(savedMats);
                }

                // Pass D: linear depth -> packed into normal atlas alpha
                if (depthTempMat != null && normalRaw != null)
                {
                    var savedMats2 = SaveAndReplaceMaterials(bakeTarget, depthTempMat);
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    if (hdCamData != null) SetHDRPClearColorMode(hdCamDataType, hdCamData, 1); // 1 = Color
                    cam.backgroundColor = Color.white;
                    if (hdCamData != null)
                        SetHDRPBackgroundColor(hdCamDataType, hdCamData, Color.white);
                    ClearRT(rtCapture, Color.white);
                    RenderCameraToTarget(cam, rtCapture);
                    Color[] depthRaw = ReadRT(rtCapture, frameRes, frameRes);

                    for (int p = 0; p < normalRaw.Length; p++)
                        normalRaw[p].a = depthRaw[p].r; // opaque card: depth written for all pixels

                    normalAtlas.SetPixels(col * frameRes, atlasRow * frameRes, frameRes, frameRes, normalRaw);
                    RestoreMaterials(savedMats2);
                }

            // â”€â”€ Restore scene state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            }
            RenderSettings.fog = originalFog;
            RestoreRendererVisibilityStates(hiddenSceneRenderers);
            RestoreLayers(originalLayers);
            RestoreLODGroups(forcedLODGroups);

            colorAtlas.Apply();
            normalAtlas.Apply();
            WarnIfAtlasLooksLikeSilhouette(colorAtlas, fallbackTint);

            // â”€â”€ Save textures â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            string pngPath = $"{folderPath}/{targetNameClean}_ImpostorAtlas.png";
            string normalPngPath = $"{folderPath}/{targetNameClean}_ImpostorNormal.png";

            File.WriteAllBytes(pngPath, colorAtlas.EncodeToPNG());
            File.WriteAllBytes(normalPngPath, normalAtlas.EncodeToPNG());
            AssetDatabase.ImportAsset(pngPath);
            AssetDatabase.ImportAsset(normalPngPath);

            TextureImporter albedoImporter = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (albedoImporter != null)
            {
                albedoImporter.textureType = TextureImporterType.Default;
                albedoImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                albedoImporter.alphaIsTransparency = true;
                albedoImporter.sRGBTexture = true;
                albedoImporter.mipmapEnabled = true;
                albedoImporter.mipmapFilter = TextureImporterMipFilter.BoxFilter;
                albedoImporter.filterMode = FilterMode.Bilinear;
                albedoImporter.wrapMode = TextureWrapMode.Clamp;
                albedoImporter.SaveAndReimport();
            }

            TextureImporter normalImporter = AssetImporter.GetAtPath(normalPngPath) as TextureImporter;
            if (normalImporter != null)
            {
                // This texture is packed data: RGB = normal/debug data, A = depth.
                // Do not import as NormalMap, because Unity can repack/modify channels.
                normalImporter.textureType = TextureImporterType.Default;
                normalImporter.sRGBTexture = false;
                normalImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                normalImporter.alphaIsTransparency = false;
                normalImporter.wrapMode = TextureWrapMode.Clamp;
                normalImporter.mipmapEnabled = true;
                normalImporter.filterMode = FilterMode.Bilinear;
                normalImporter.SaveAndReimport();
            }

            Texture2D savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            Texture2D savedNormalAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPngPath);

            // â”€â”€ Cleanup GPU resources â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rtCapture);
            Object.DestroyImmediate(camGo);
            if (normalTempMat != null) Object.DestroyImmediate(normalTempMat);
            if (depthTempMat != null) Object.DestroyImmediate(depthTempMat);
            if (alphaTempMat != null) Object.DestroyImmediate(alphaTempMat);
            Object.DestroyImmediate(colorAtlas);
            Object.DestroyImmediate(normalAtlas);

            // â”€â”€ Material â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            const string impostorShaderName = "AAAOptimizer/Impostor/HDRPUnlitAtlasCutout";
            const string impostorShaderPath = "Assets/AAAOptimizer/Shaders/AAAImpostorHDRPUnlitAtlasCutout_Fixed.shader";

            AssetDatabase.ImportAsset(impostorShaderPath, ImportAssetOptions.ForceUpdate);
            Shader activeShader = AssetDatabase.LoadAssetAtPath<Shader>(impostorShaderPath);
            if (activeShader == null) activeShader = Shader.Find(impostorShaderName);
            if (activeShader == null) activeShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (activeShader == null) activeShader = Shader.Find("Unlit/Texture");
            if (activeShader == null) activeShader = Shader.Find("Standard");

            if (activeShader == null)
            {
                throw new System.InvalidOperationException($"[ImpostorBaker] Cannot create impostor material. Shader asset failed to import: {impostorShaderPath}");
            }
            Material impostorMat = new Material(activeShader);
            impostorMat.name = $"{targetNameClean}_ImpostorMat";

            if (impostorMat.HasProperty("_UnlitColorMap")) impostorMat.SetTexture("_UnlitColorMap", savedAtlas);
            if (impostorMat.HasProperty("_BaseColorMap")) impostorMat.SetTexture("_BaseColorMap", savedAtlas);
            if (impostorMat.HasProperty("_BaseMap")) impostorMat.SetTexture("_BaseMap", savedAtlas);
            if (impostorMat.HasProperty("_MainTex")) impostorMat.SetTexture("_MainTex", savedAtlas);
            impostorMat.mainTexture = savedAtlas;

            if (impostorMat.HasProperty("_UnlitColor")) impostorMat.SetColor("_UnlitColor", Color.white);
            if (impostorMat.HasProperty("_BaseColor")) impostorMat.SetColor("_BaseColor", Color.white);
            if (impostorMat.HasProperty("_Color")) impostorMat.SetColor("_Color", Color.white);

            if (savedNormalAtlas != null)
            {
                if (impostorMat.HasProperty("_BumpMap")) impostorMat.SetTexture("_BumpMap", savedNormalAtlas);
                if (impostorMat.HasProperty("_NormalMap")) impostorMat.SetTexture("_NormalMap", savedNormalAtlas);
                if (impostorMat.HasProperty("_NormalDepthMap")) impostorMat.SetTexture("_NormalDepthMap", savedNormalAtlas);
            }

            Vector4 atlasScale = new Vector4(1.0f / cols, 1.0f / rows, 0, 0);
            Vector4 atlasOffset = new Vector4(0, (float)(rows - 1) / rows, 0, 0);
            Vector4 firstFrameST = new Vector4(1.0f / cols, 1.0f / rows, 0, (float)(rows - 1) / rows);

            if (impostorMat.HasProperty("_AtlasScale")) impostorMat.SetVector("_AtlasScale", atlasScale);
            if (impostorMat.HasProperty("_AtlasOffset")) impostorMat.SetVector("_AtlasOffset", atlasOffset);
            if (impostorMat.HasProperty("_UnlitColorMap_ST")) impostorMat.SetVector("_UnlitColorMap_ST", firstFrameST);
            if (impostorMat.HasProperty("_BaseColorMap_ST")) impostorMat.SetVector("_BaseColorMap_ST", firstFrameST);
            if (impostorMat.HasProperty("_BaseMap_ST")) impostorMat.SetVector("_BaseMap_ST", firstFrameST);
            if (impostorMat.HasProperty("_MainTex_ST")) impostorMat.SetVector("_MainTex_ST", firstFrameST);

            if (impostorMat.HasProperty("_DepthParams")) impostorMat.SetVector("_DepthParams", new Vector4(nearClip, farClip, orbitDistance, orthoHalfSize));
            if (impostorMat.HasProperty("_ImpostorSunDir")) impostorMat.SetVector("_ImpostorSunDir", RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.up);
            if (impostorMat.HasProperty("_ImpostorLightColor")) impostorMat.SetColor("_ImpostorLightColor", RenderSettings.sun != null ? RenderSettings.sun.color * RenderSettings.sun.intensity : Color.white);
            if (impostorMat.HasProperty("_ImpostorAmbientColor")) impostorMat.SetColor("_ImpostorAmbientColor", RenderSettings.ambientLight.maxColorComponent > 0.001f ? RenderSettings.ambientLight : new Color(0.45f, 0.45f, 0.45f, 1.0f));
            if (impostorMat.HasProperty("_ImpostorLightStrength")) impostorMat.SetFloat("_ImpostorLightStrength", RenderSettings.sun != null ? 0.75f : 0.0f);
            if (impostorMat.HasProperty("_ImpostorAmbientStrength")) impostorMat.SetFloat("_ImpostorAmbientStrength", 0.55f);
            if (impostorMat.HasProperty("_Cutoff")) impostorMat.SetFloat("_Cutoff", 0.05f);
            if (impostorMat.HasProperty("_AlphaCutoff")) impostorMat.SetFloat("_AlphaCutoff", 0.05f);
            if (impostorMat.HasProperty("_AlphaCutoffEnable")) impostorMat.SetFloat("_AlphaCutoffEnable", 1.0f);

            if (impostorMat.HasProperty("_AlphaClip")) impostorMat.SetFloat("_AlphaClip", 1.0f);
            if (impostorMat.HasProperty("_Surface")) impostorMat.SetFloat("_Surface", 0.0f);
            if (impostorMat.HasProperty("_SurfaceType")) impostorMat.SetFloat("_SurfaceType", 0.0f);
            if (impostorMat.HasProperty("_Blend")) impostorMat.SetFloat("_Blend", 0.0f);
            if (impostorMat.HasProperty("_BlendMode")) impostorMat.SetFloat("_BlendMode", 0.0f);
            if (impostorMat.HasProperty("_SrcBlend")) impostorMat.SetFloat("_SrcBlend", 1.0f);
            if (impostorMat.HasProperty("_DstBlend")) impostorMat.SetFloat("_DstBlend", 0.0f);
            if (impostorMat.HasProperty("_ZWrite")) impostorMat.SetFloat("_ZWrite", 1.0f);
            if (impostorMat.HasProperty("_DoubleSidedEnable")) impostorMat.SetFloat("_DoubleSidedEnable", 1.0f);

            impostorMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            impostorMat.SetOverrideTag("RenderType", "TransparentCutout");
            impostorMat.EnableKeyword("_ALPHATEST_ON");
            impostorMat.EnableKeyword("ALPHATEST_ON");
            impostorMat.DisableKeyword("_ALPHABLEND_ON");
            impostorMat.EnableKeyword("_DOUBLE_SIDED_ON");

            string matPath = $"{folderPath}/{targetNameClean}_ImpostorMat.mat";
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
            {
                existingMat.shader = activeShader;
                existingMat.CopyPropertiesFromMaterial(impostorMat);
                existingMat.shader = activeShader;

                if (existingMat.HasProperty("_UnlitColorMap")) existingMat.SetTexture("_UnlitColorMap", savedAtlas);
                if (existingMat.HasProperty("_BaseColorMap")) existingMat.SetTexture("_BaseColorMap", savedAtlas);
                if (existingMat.HasProperty("_BaseMap")) existingMat.SetTexture("_BaseMap", savedAtlas);
                if (existingMat.HasProperty("_MainTex")) existingMat.SetTexture("_MainTex", savedAtlas);
                existingMat.mainTexture = savedAtlas;

                existingMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                existingMat.SetOverrideTag("RenderType", "TransparentCutout");

                EditorUtility.SetDirty(existingMat);
                impostorMat = existingMat;
            }
            else
            {
                AssetDatabase.CreateAsset(impostorMat, matPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ImpostorBaker] Assigned impostor material shader: " + impostorMat.shader.name);

            // â”€â”€ Billboard quad â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // The quad matches the tight-crop capture view exactly, so the rendered
            // silhouette fills the quad edge-to-edge with no empty border.
            Mesh billboardMesh = CreateBillboardQuad(captureViewSize, captureViewSize);
            string meshPath = $"{folderPath}/{targetNameClean}_ImpostorMesh.asset";

            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                existingMesh.vertices = billboardMesh.vertices;
                existingMesh.uv = billboardMesh.uv;
                existingMesh.triangles = billboardMesh.triangles;
                existingMesh.normals = billboardMesh.normals;
                existingMesh.tangents = billboardMesh.tangents;
                existingMesh.RecalculateBounds();
                EditorUtility.SetDirty(existingMesh);
                billboardMesh = existingMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(billboardMesh, meshPath);
            }
            AssetDatabase.SaveAssets();

            // â”€â”€ Build impostor prefab â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            GameObject billboardGo = new GameObject($"{target.name}_Impostor");

            // Child quad carries the mesh so rotation pivot stays at the base
            GameObject quadGo = new GameObject("Quad");
            quadGo.transform.SetParent(billboardGo.transform);
            // Centre the quad on the bounding-box centre so the silhouette aligns
            // with the original object when both are at the same world position
            Vector3 localCenter = bounds.center - bakeTarget.transform.position;
            quadGo.transform.localPosition = localCenter;

            MeshFilter mf = quadGo.AddComponent<MeshFilter>();
            mf.sharedMesh = billboardMesh;

            MeshRenderer mr = quadGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = impostorMat;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;

            ImpostorBillboard billboardComp = billboardGo.AddComponent<ImpostorBillboard>();
            billboardComp.directions = directions;
            billboardComp.columns = cols;
            billboardComp.rows = rows;
            billboardComp.impostorRenderer = mr;
            billboardComp.transitionDistance = swapDistance;
            billboardComp.originalTarget = target;
            billboardComp.originalTargetName = target.name;
            billboardComp.quadTransform = quadGo.transform;
            // Use FaceCameraYaw so the correct atlas frame is chosen at runtime
            billboardComp.rotationMode = ImpostorBillboard.BillboardRotationMode.FaceCameraYaw;
            billboardComp.disableOriginalRenderersOnly = true;

            string prefabPath = $"{folderPath}/{targetNameClean}_Impostor.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(billboardGo, prefabPath);
            Object.DestroyImmediate(billboardGo);

            if (savedPrefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(savedPrefab);
                if (!isPrefab)
                {
                    instance.transform.SetParent(target.transform.parent);
                    instance.transform.position = target.transform.position;
                    instance.transform.rotation = target.transform.rotation;
                }
                else
                {
                    instance.transform.position = Vector3.zero;
                    instance.transform.rotation = Quaternion.identity;
                }

                ImpostorBillboard instComp = instance.GetComponent<ImpostorBillboard>();
                if (instComp != null)
                {
                    instComp.originalTarget = target;
                    instComp.originalTargetName = target.name;
                }

                EditorGUIUtility.PingObject(instance);
                Selection.activeGameObject = instance;
            }

            if (tempInstance != null) Object.DestroyImmediate(tempInstance);

            Debug.Log($"[ImpostorBaker] âœ“ Baked impostor for '{target.name}' â†’ {prefabPath}  " +
                      $"({cols}Ã—{rows} atlas, {frameRes}px frames, orthoSize={orthoHalfSize:F3})");
#endif
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Billboard quad mesh
        // Vertices are centred on the pivot so the quad auto-aligns to the object
        // bounding-box centre when quadGo.localPosition is set to bounds.center.
        // Normals are spherically bent outwards for soft environmental lighting.
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static Mesh CreateBillboardQuad(float width, float height)
        {
            Mesh mesh = new Mesh { name = "ImpostorQuad" };

            float w = width * 0.5f;
            float h = height * 0.5f;

            mesh.vertices = new Vector3[]
            {
                new Vector3(-w, -h, 0),
                new Vector3( w, -h, 0),
                new Vector3(-w,  h, 0),
                new Vector3( w,  h, 0)
            };

            float zBend = Mathf.Max(w, h) * 0.5f;
            mesh.normals = new Vector3[]
            {
                new Vector3(-w, -h, -zBend).normalized,
                new Vector3( w, -h, -zBend).normalized,
                new Vector3(-w,  h, -zBend).normalized,
                new Vector3( w,  h, -zBend).normalized
            };

            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}

