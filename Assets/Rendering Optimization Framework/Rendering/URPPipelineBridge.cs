#if AAA_URP_AVAILABLE
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AAAOptimizer.Core;

namespace AAAOptimizer.Rendering
{
    public class URPPipelineBridge : IRenderPipelineBridge
    {
        public string GetPipelineName() => "Universal Render Pipeline (URP)";
        public bool IsHDRP() => false;
        public bool IsURP() => true;

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
            // A simple check: most standard URP shaders are compatible.
            // In editor we could check ShaderUtil.GetShaderPropertyCount etc.
            string shaderName = mat.shader.name.ToLower();
            return shaderName.Contains("universal render pipeline") || 
                   string.Equals(shaderName, "urp") || 
                   shaderName.Contains("lit") || 
                   shaderName.Contains("simple lit");
        }

        public void SetVolumetricLightingQuality(int level)
        {
            // URP does not have built-in volumetric fog like HDRP, but we could adjust custom volumetric assets
            Debug.Log($"[URP Bridge] Setting Volumetric quality (URP stub) to level: {level}");
        }

        public Shader GetImpostorShader()
        {
            Shader impostorShader = Shader.Find("AAAOptimizer/ImpostorBillboard");
            if (impostorShader == null)
            {
                impostorShader = Shader.Find("Shader Graphs/ImpostorShaderURP");
            }
            if (impostorShader == null)
            {
                impostorShader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return impostorShader;
        }
    }
}
#endif
