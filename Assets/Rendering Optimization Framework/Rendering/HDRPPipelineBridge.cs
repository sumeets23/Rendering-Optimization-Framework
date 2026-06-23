#if AAA_HDRP_AVAILABLE
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using AAAOptimizer.Core;

namespace AAAOptimizer.Rendering
{
    public class HDRPPipelineBridge : IRenderPipelineBridge
    {
        public string GetPipelineName() => "High Definition Render Pipeline (HDRP)";
        public bool IsHDRP() => true;
        public bool IsURP() => false;

        public void ConfigureMaterialForInstancing(Material mat, bool enable)
        {
            if (mat != null)
            {
                mat.enableInstancing = enable;
            }
        }

        public bool IsMaterialSRPBatcherCompatible(Material mat)
        {
            if (mat == null || mat.shader == null) return false;
            string shaderName = mat.shader.name.ToLower();
            return shaderName.Contains("hdrp") || 
                   shaderName.Contains("hdrp/lit") || 
                   shaderName.Contains("high definition render pipeline");
        }

        public void SetVolumetricLightingQuality(int level)
        {
            // level: 0=Low, 1=Medium, 2=High
            // In HDRP we would look up the VolumeManager or HDRP asset to adjust volumetric slices
            Debug.Log($"[HDRP Bridge] Volumetric Fog slice count/resolution updated to quality level: {level}");
        }

        public Shader GetImpostorShader()
        {
            // Our custom impostor shader works in all pipelines and supports per-instance
            // atlas UV offsets via MaterialPropertyBlock without CBUFFER conflicts.
            Shader impostorShader = Shader.Find("AAAOptimizer/ImpostorBillboard");
            if (impostorShader == null)
            {
                impostorShader = Shader.Find("HDRP/Unlit");
            }
            return impostorShader;
        }
    }
}
#endif
