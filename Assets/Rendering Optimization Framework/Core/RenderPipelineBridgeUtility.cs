using UnityEngine;
using UnityEngine.Rendering;
using AAAOptimizer.Rendering;

namespace AAAOptimizer.Core
{
    public static class RenderPipelineBridgeUtility
    {
        private static IRenderPipelineBridge activeBridge;

        public static IRenderPipelineBridge GetActiveBridge()
        {
            if (activeBridge != null) return activeBridge;

            RenderPipelineAsset currentAsset = GraphicsSettings.currentRenderPipeline;
            if (currentAsset == null)
            {
                // In Unity 6, check default render pipeline
                currentAsset = GraphicsSettings.defaultRenderPipeline;
            }

            if (currentAsset != null)
            {
                string assetType = currentAsset.GetType().Name.ToLower();
#if AAA_URP_AVAILABLE
                if (assetType.Contains("universal"))
                {
                    activeBridge = new URPPipelineBridge();
                }
                else
#endif
#if AAA_HDRP_AVAILABLE
                if (assetType.Contains("hd") || assetType.Contains("highdefinition"))
                {
                    activeBridge = new HDRPPipelineBridge();
                }
#endif
            }

            // Fallback default: URP or HDRP depending on what assembly is loaded, or Default
            if (activeBridge == null)
            {
#if AAA_HDRP_AVAILABLE
                activeBridge = new HDRPPipelineBridge();
#elif AAA_URP_AVAILABLE
                activeBridge = new URPPipelineBridge();
#else
                activeBridge = new DefaultPipelineBridge();
#endif
            }

            Debug.Log($"[Rendering Optimization System] Detected render pipeline: {activeBridge.GetPipelineName()}");
            return activeBridge;
        }

        public class DefaultPipelineBridge : IRenderPipelineBridge
        {
            public string GetPipelineName() => "Built-in or Unknown";
            public bool IsHDRP() => false;
            public bool IsURP() => false;
            public void ConfigureMaterialForInstancing(Material mat, bool enable) {}
            public bool IsMaterialSRPBatcherCompatible(Material mat) => false;
            public void SetVolumetricLightingQuality(int level) {}
            public Shader GetImpostorShader() => Shader.Find("Standard");
        }
        public static System.Type FindType(string fullName)
        {
            System.Type type = System.Type.GetType(fullName);
            if (type != null) return type;

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }
    }
}
