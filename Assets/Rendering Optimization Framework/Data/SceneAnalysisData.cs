using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAAOptimizer.Data
{
    [Serializable]
    public class MeshMetric
    {
        public string meshName;
        public string objectPath;
        public int vertexCount;
        public int triangleCount;
        public int subMeshCount;
        public bool hasLODGroup;
    }

    [Serializable]
    public class MaterialMetric
    {
        public string materialName;
        public string shaderName;
        public bool isSRPBatcherCompatible;
        public bool enableInstancing;
        public List<string> associatedObjects = new List<string>();
    }

    [Serializable]
    public class TextureMetric
    {
        public string textureName;
        public int width;
        public int height;
        public long estimatedBytes;
        public string format;
        public bool streamingEnabled;
    }

    [Serializable]
    public class DuplicateMaterialGroup
    {
        public string baseMaterialName;
        public List<string> duplicateMaterialPaths = new List<string>();
        public List<string> affectedObjects = new List<string>();
    }

    public class SceneAnalysisData : ScriptableObject
    {
        [Header("Global Statistics")]
        public string sceneName;
        public DateTime scanTimestamp;
        public int totalRenderers;
        public int totalVertices;
        public int totalTriangles;
        public int totalMaterialsCount;
        public int totalTexturesCount;
        public long estimatedVRAMBytes;
        public int shadowCasterCount;
        public int lodGroupsCount;
        public int staticBatchableCount;

        [Header("Detailed Breakdowns")]
        public List<MeshMetric> highPolyMeshes = new List<MeshMetric>();
        public List<MaterialMetric> materials = new List<MaterialMetric>();
        public List<TextureMetric> textures = new List<TextureMetric>();
        public List<DuplicateMaterialGroup> duplicateMaterials = new List<DuplicateMaterialGroup>();
        public List<string> warningLogs = new List<string>();
    }
}
