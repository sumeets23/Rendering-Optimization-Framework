using System.Collections.Generic;
using UnityEngine;

namespace AAAOptimizer.HLOD
{
    public static class MeshCombiner
    {
        public static Mesh CombineAndRemapUVs(List<MeshFilter> filters, Dictionary<Material, Rect> materialRectMap, string combinedName)
        {
            return CombineAndRemapUVs(filters, materialRectMap, combinedName, Matrix4x4.identity);
        }

        public static Mesh CombineAndRemapUVs(List<MeshFilter> filters, Dictionary<Material, Rect> materialRectMap, string combinedName, Matrix4x4 rootWorldToLocal, int atlasResolution = 2048)
        {
            if (filters == null || filters.Count == 0) return null;

            List<CombineInstance> combineInstances = new List<CombineInstance>();
            List<Mesh> tempClones = new List<Mesh>();

            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;

                int subMeshCount = Mathf.Max(1, mf.sharedMesh.subMeshCount);
                Material[] materials = mr.sharedMaterials;
                for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                {
                    Material material = materials.Length > 0 ? materials[Mathf.Min(subMeshIndex, materials.Length - 1)] : mr.sharedMaterial;
                    Rect rect = new Rect(0, 0, 1, 1);

                    if (material != null && materialRectMap != null && materialRectMap.TryGetValue(material, out Rect matchedRect))
                    {
                        rect = matchedRect;
                    }

                    // Clone the mesh so we don't modify original assets while remapping UVs per submesh material.
                    Mesh clone = Object.Instantiate(mf.sharedMesh);
                    tempClones.Add(clone);

                    Vector2[] uvs = clone.uv;
                    if (uvs == null || uvs.Length != clone.vertexCount)
                    {
                        uvs = new Vector2[clone.vertexCount];
                    }

                    // Retrieve material tiling (scale) and offset
                    Vector2 scale = Vector2.one;
                    Vector2 offset = Vector2.zero;
                    if (material != null)
                    {
                        scale = material.mainTextureScale;
                        offset = material.mainTextureOffset;
                    }

                    for (int i = 0; i < uvs.Length; i++)
                    {
                        // Apply tiling and offset
                        float u = uvs[i].x * scale.x + offset.x;
                        float v = uvs[i].y * scale.y + offset.y;

                        // Clamp to inner texel boundary to prevent bilinear bleeding relative to slot size
                        float slotPixelSize = rect.width * atlasResolution;
                        float halfTexel = slotPixelSize > 0 ? 0.5f / slotPixelSize : 0.002f;
                        
                        u = Mathf.Clamp(u, halfTexel, 1f - halfTexel);
                        v = Mathf.Clamp(v, halfTexel, 1f - halfTexel);

                        // Scale and shift into the atlas rect
                        uvs[i] = new Vector2(
                            rect.x + u * rect.width,
                            rect.y + v * rect.height
                        );
                    }
                    clone.uv = uvs;

                    CombineInstance ci = new CombineInstance
                    {
                        mesh = clone,
                        subMeshIndex = subMeshIndex,
                        transform = rootWorldToLocal * mf.transform.localToWorldMatrix
                    };
                    combineInstances.Add(ci);
                }
            }

            if (combineInstances.Count == 0) return null;

            Mesh combinedMesh = new Mesh();
            combinedMesh.name = combinedName;
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Allow large vert counts
            combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

            combinedMesh.RecalculateBounds();
            // We DO NOT call RecalculateNormals() or RecalculateTangents() here.
            // CombineMeshes preserves the original normals and tangents perfectly.
            // Recalculating them would destroy original smoothing groups and custom vertex normals!

            // Destroy temp mesh clones to prevent memory leak
            foreach (var temp in tempClones)
            {
                Object.DestroyImmediate(temp);
            }

            return combinedMesh;
        }
    }
}
