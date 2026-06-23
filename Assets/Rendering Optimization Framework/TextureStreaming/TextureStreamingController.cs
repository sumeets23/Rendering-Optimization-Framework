using UnityEngine;
using AAAOptimizer.Core;

namespace AAAOptimizer.TextureStreaming
{
    public class TextureStreamingController : MonoBehaviour
    {
        public AAAOptimizerConfig config;
        
        [Header("Runtime Status")]
        public float currentUsageMB;
        public float budgetMB;
        public int activeMipLimit = 0;

        private float nextCheckTime;
        private const float checkInterval = 1.0f; // Check VRAM footprint every second

        private void Start()
        {
            if (config == null)
            {
                // Find default config
                AAAOptimizerConfig[] configs = Resources.FindObjectsOfTypeAll<AAAOptimizerConfig>();
                if (configs.Length > 0) config = configs[0];
            }

            ApplyConfigSettings();
        }

        private void Update()
        {
            if (config == null || !config.enableTextureStreaming) return;

            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInterval;
                MonitorVRAMFootprint();
            }
        }

        public void ApplyConfigSettings()
        {
            if (config == null) return;

            QualitySettings.streamingMipmapsActive = config.enableTextureStreaming;
            QualitySettings.streamingMipmapsMemoryBudget = (int)config.textureMemoryBudgetMB;
            Texture.streamingTextureDiscardUnusedMips = true;

            budgetMB = config.textureMemoryBudgetMB;
            Debug.Log($"[TextureStreamingController] Applied Mipmap Streaming Config: Active={config.enableTextureStreaming}, Budget={budgetMB}MB");
        }

        private void MonitorVRAMFootprint()
        {
            // Unity provides APIs to query texture memory allocations
            long desiredBytes = (long)Texture.desiredTextureMemory;
            long currentBytes = (long)Texture.currentTextureMemory;

            currentUsageMB = (float)currentBytes / (1024f * 1024f);
            float desiredUsageMB = (float)desiredBytes / (1024f * 1024f);

            // Dynamic quality capping
            if (currentUsageMB > budgetMB && activeMipLimit < config.maxMipMapReduction)
            {
                // VRAM exceeded budget. Degrade quality by shifting mip limit.
                activeMipLimit++;
                QualitySettings.globalTextureMipmapLimit = activeMipLimit;
                Debug.LogWarning($"[TextureStreamingController] VRAM budget exceeded ({currentUsageMB:F1}MB > {budgetMB}MB). Capping global mip quality to limit level {activeMipLimit}.");
            }
            else if (currentUsageMB < budgetMB * 0.8f && activeMipLimit > 0)
            {
                // VRAM is safe. Recover mip quality.
                activeMipLimit--;
                QualitySettings.globalTextureMipmapLimit = activeMipLimit;
                Debug.Log($"[TextureStreamingController] VRAM usage stabilized ({currentUsageMB:F1}MB). Restoring global mip quality limit to level {activeMipLimit}.");
            }
        }
    }
}
