using UnityEngine;

namespace AAAOptimizer.Core
{
    [CreateAssetMenu(fileName = "RenderingOptimizationConfig", menuName = "Rendering Optimization System/Configuration", order = 1)]
    public class AAAOptimizerConfig : ScriptableObject
    {
        [Header("Scene Analyzer Settings")]
        public int polyCountWarningThreshold = 50000;
        public int materialCountWarningThreshold = 10;
        public int textureSizeWarningThreshold = 2048;

        [Header("Mesh Simplification & LOD")]
        [Range(0.05f, 0.95f)]
        public float lod1ReductionRatio = 0.60f;
        [Range(0.05f, 0.95f)]
        public float lod2ReductionRatio = 0.30f;
        [Range(0.05f, 0.95f)]
        public float lod3ReductionRatio = 0.10f;
        public bool autoConfigureLODGroups = true;

        [Header("HLOD Settings")]
        public float clusterGridSize = 50.0f;
        public int minMeshesPerCluster = 2;
        public int maxAtlasResolution = 4096;
        public int atlasPadding = 16;
        public float hlodTransitionDistance = 100.0f;

        [Header("Impostor Settings")]
        public int captureDirections = 8; // 8 or 16
        public int impostorTextureSize = 2048;
        public int singleFrameResolution = 256;
        public float impostorSwapDistance = 150.0f;
        public bool enableDepthImpostors = true;

        [Header("Texture Streaming")]
        public bool enableTextureStreaming = true;
        public float textureMemoryBudgetMB = 1024.0f; // 1GB default
        public int maxMipMapReduction = 3;

        [Header("World Streaming (Grid Partitioning)")]
        public bool enableWorldStreaming = true;
        public float cellSize = 100.0f;
        public float loadRadius = 150.0f;
        public float unloadRadiusMargin = 50.0f; // Hysteresis buffer
        public bool predictiveLoading = true;
        public int maxSceneLoadsPerFrame = 1;
    }
}
