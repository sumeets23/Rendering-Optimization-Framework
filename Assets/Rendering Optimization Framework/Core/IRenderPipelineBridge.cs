using UnityEngine;

namespace AAAOptimizer.Core
{
    public interface IRenderPipelineBridge
    {
        string GetPipelineName();
        bool IsHDRP();
        bool IsURP();
        
        void ConfigureMaterialForInstancing(Material mat, bool enable);
        bool IsMaterialSRPBatcherCompatible(Material mat);
        
        void SetVolumetricLightingQuality(int level); // 0=Low, 1=Medium, 2=High
        
        Shader GetImpostorShader();
    }
}
