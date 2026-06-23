using UnityEngine;
using AAAOptimizer.Core;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace AAAOptimizer.Simplification
{
    public static class LODGroupAutomator
    {
        public static void GenerateLODsForObject(GameObject targetGo, AAAOptimizerConfig config)
        {
            if (targetGo == null || config == null) return;

#if UNITY_EDITOR
            MeshFilter mf = targetGo.GetComponent<MeshFilter>();
            MeshRenderer mr = targetGo.GetComponent<MeshRenderer>();

            if (mf == null || mr == null || mf.sharedMesh == null)
            {
                Debug.LogWarning($"[LODGroupAutomator] {targetGo.name} does not have a valid MeshFilter and MeshRenderer.");
                return;
            }

            // Ensure LOD folder exists
            string folderPath = "Assets/AAAOptimizer/Data/LODs";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            Mesh originalMesh = mf.sharedMesh;
            Material[] originalMaterials = mr.sharedMaterials;

            // Generate simplified meshes
            Mesh meshLOD1 = MeshSimplifierUtility.SimplifyMesh(originalMesh, config.lod1ReductionRatio);
            Mesh meshLOD2 = MeshSimplifierUtility.SimplifyMesh(originalMesh, config.lod2ReductionRatio);
            Mesh meshLOD3 = MeshSimplifierUtility.SimplifyMesh(originalMesh, config.lod3ReductionRatio);

            // Save meshes as assets
            string meshNameClean = originalMesh.name.Replace(" ", "_");
            string pathLOD1 = $"{folderPath}/{meshNameClean}_LOD1.asset";
            string pathLOD2 = $"{folderPath}/{meshNameClean}_LOD2.asset";
            string pathLOD3 = $"{folderPath}/{meshNameClean}_LOD3.asset";

            AssetDatabase.CreateAsset(meshLOD1, pathLOD1);
            AssetDatabase.CreateAsset(meshLOD2, pathLOD2);
            AssetDatabase.CreateAsset(meshLOD3, pathLOD3);
            AssetDatabase.SaveAssets();

            // Set up hierarchy
            // Check if LODGroup already exists, or create it
            LODGroup lodGroup = targetGo.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = targetGo.AddComponent<LODGroup>();
            }

            // Remove existing LOD sub-objects if any
            for (int i = targetGo.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = targetGo.transform.GetChild(i);
                if (child.name.Contains("_LOD1") || child.name.Contains("_LOD2") || child.name.Contains("_LOD3"))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            // Create sub-level GameObjects
            GameObject goLOD1 = CreateLODSubObject(targetGo, "LOD1", meshLOD1, originalMaterials);
            GameObject goLOD2 = CreateLODSubObject(targetGo, "LOD2", meshLOD2, originalMaterials);
            GameObject goLOD3 = CreateLODSubObject(targetGo, "LOD3", meshLOD3, originalMaterials);

            // Configure LODGroup structure
            LOD[] lods = new LOD[4];
            
            // LOD 0 uses the original renderer on the root
            lods[0] = new LOD(0.6f, new Renderer[] { mr });
            
            // LOD 1, 2, 3 use the child renderers
            lods[1] = new LOD(0.3f, new Renderer[] { goLOD1.GetComponent<MeshRenderer>() });
            lods[2] = new LOD(0.15f, new Renderer[] { goLOD2.GetComponent<MeshRenderer>() });
            lods[3] = new LOD(0.04f, new Renderer[] { goLOD3.GetComponent<MeshRenderer>() });

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            EditorUtility.SetDirty(targetGo);
            Debug.Log($"[LODGroupAutomator] Successfully generated and configured LODs for {targetGo.name}");
#endif
        }

        private static GameObject CreateLODSubObject(GameObject parent, string label, Mesh mesh, Material[] materials)
        {
            GameObject lodGo = new GameObject($"{parent.name}_{label}");
            lodGo.transform.SetParent(parent.transform);
            lodGo.transform.localPosition = Vector3.zero;
            lodGo.transform.localRotation = Quaternion.identity;
            lodGo.transform.localScale = Vector3.one;

            MeshFilter mf = lodGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = lodGo.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;

            return lodGo;
        }
    }
}
