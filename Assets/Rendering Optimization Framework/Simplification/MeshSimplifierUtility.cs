using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AAAOptimizer.Simplification
{
    public static class MeshSimplifierUtility
    {
        /// <summary>
        /// Simplifies a mesh by a target decimation ratio using Vertex Clustering.
        /// </summary>
        /// <param name="sourceMesh">The original mesh asset.</param>
        /// <param name="reductionRatio">Value between 0.01 (extremely simplified) and 1.0 (no reduction).</param>
        /// <returns>A new simplified Mesh object.</returns>
        public static Mesh SimplifyMesh(Mesh sourceMesh, float reductionRatio)
        {
            if (sourceMesh == null) return null;
            if (reductionRatio >= 0.99f) return sourceMesh;

            // Clamping reduction ratio to a safe range
            reductionRatio = Mathf.Clamp(reductionRatio, 0.01f, 0.95f);

            // Compute grid resolution based on the reduction ratio.
            // A higher ratio keeps more vertices (e.g. 0.90 -> 57 grid cells per dimension).
            // A lower ratio collapses more vertices (e.g. 0.10 -> 8 grid cells per dimension).
            int gridResolution = Mathf.Clamp(Mathf.RoundToInt(reductionRatio * 60f), 6, 60);

            Bounds bounds = sourceMesh.bounds;
            Vector3 min = bounds.min;
            Vector3 size = bounds.size;

            float cellSizeX = size.x > 0 ? size.x / gridResolution : 0.01f;
            float cellSizeY = size.y > 0 ? size.y / gridResolution : 0.01f;
            float cellSizeZ = size.z > 0 ? size.z / gridResolution : 0.01f;

            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] sourceNormals = sourceMesh.normals;
            Vector2[] sourceUVs = sourceMesh.uv;

            bool hasNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length;
            bool hasUVs = sourceUVs != null && sourceUVs.Length == sourceVertices.Length;

            // Map grid coordinates to cell info
            Dictionary<Vector3Int, int> cellToIndex = new Dictionary<Vector3Int, int>();
            List<Vector3> collapsedVertices = new List<Vector3>();
            List<Vector3> collapsedNormals = new List<Vector3>();
            List<Vector2> collapsedUVs = new List<Vector2>();

            // Lists to accumulate averages for each cluster
            List<List<Vector3>> clusterVerts = new List<List<Vector3>>();
            List<List<Vector3>> clusterNorms = new List<List<Vector3>>();
            List<List<Vector2>> clusterUVs = new List<List<Vector2>>();

            int[] oldToNew = new int[sourceVertices.Length];

            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 v = sourceVertices[i];
                int cx = Mathf.Clamp(Mathf.FloorToInt((v.x - min.x) / cellSizeX), 0, gridResolution - 1);
                int cy = Mathf.Clamp(Mathf.FloorToInt((v.y - min.y) / cellSizeY), 0, gridResolution - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt((v.z - min.z) / cellSizeZ), 0, gridResolution - 1);
                Vector3Int cellCoord = new Vector3Int(cx, cy, cz);

                if (!cellToIndex.TryGetValue(cellCoord, out int newIndex))
                {
                    newIndex = collapsedVertices.Count;
                    cellToIndex.Add(cellCoord, newIndex);

                    collapsedVertices.Add(Vector3.zero);
                    if (hasNormals) collapsedNormals.Add(Vector3.zero);
                    if (hasUVs) collapsedUVs.Add(Vector2.zero);

                    clusterVerts.Add(new List<Vector3>());
                    if (hasNormals) clusterNorms.Add(new List<Vector3>());
                    if (hasUVs) clusterUVs.Add(new List<Vector2>());
                }

                oldToNew[i] = newIndex;
                clusterVerts[newIndex].Add(v);
                if (hasNormals) clusterNorms[newIndex].Add(sourceNormals[i]);
                if (hasUVs) clusterUVs[newIndex].Add(sourceUVs[i]);
            }

            // Compute averages for each cluster/cell
            for (int i = 0; i < collapsedVertices.Count; i++)
            {
                // Position average
                Vector3 avgPos = Vector3.zero;
                foreach (var p in clusterVerts[i]) avgPos += p;
                collapsedVertices[i] = avgPos / clusterVerts[i].Count;

                // Normal average
                if (hasNormals)
                {
                    Vector3 avgNorm = Vector3.zero;
                    foreach (var n in clusterNorms[i]) avgNorm += n;
                    collapsedNormals[i] = avgNorm.normalized;
                }

                // UV average
                if (hasUVs)
                {
                    Vector2 avgUV = Vector2.zero;
                    foreach (var u in clusterUVs[i]) avgUV += u;
                    collapsedUVs[i] = avgUV / clusterUVs[i].Count;
                }
            }

            // Rebuild submeshes & discard degenerate triangles
            int subMeshCount = sourceMesh.subMeshCount;
            List<int[]> newSubMeshTriangles = new List<int[]>();
            for (int s = 0; s < subMeshCount; s++)
            {
                int[] sourceTriangles = sourceMesh.GetTriangles(s);
                List<int> newTris = new List<int>();

                for (int i = 0; i < sourceTriangles.Length; i += 3)
                {
                    int a = oldToNew[sourceTriangles[i]];
                    int b = oldToNew[sourceTriangles[i + 1]];
                    int c = oldToNew[sourceTriangles[i + 2]];

                    // Discard degenerate triangle (must have 3 distinct points)
                    if (a != b && b != c && a != c)
                    {
                        newTris.Add(a);
                        newTris.Add(b);
                        newTris.Add(c);
                    }
                }
                newSubMeshTriangles.Add(newTris.ToArray());
            }

            // Create simplified mesh
            Mesh simplifiedMesh = new Mesh();
            simplifiedMesh.name = $"{sourceMesh.name}_LOD_Clustered";
            simplifiedMesh.indexFormat = sourceMesh.indexFormat;
            
            simplifiedMesh.vertices = collapsedVertices.ToArray();
            if (hasNormals) simplifiedMesh.normals = collapsedNormals.ToArray();
            if (hasUVs) simplifiedMesh.uv = collapsedUVs.ToArray();

            simplifiedMesh.subMeshCount = subMeshCount;
            for (int s = 0; s < subMeshCount; s++)
            {
                simplifiedMesh.SetTriangles(newSubMeshTriangles[s], s);
            }

            simplifiedMesh.RecalculateBounds();
            simplifiedMesh.RecalculateTangents();

            Debug.Log($"[MeshSimplifierUtility] Simplified '{sourceMesh.name}' using Vertex Clustering. Vertices: {sourceVertices.Length} -> {collapsedVertices.Count}, Triangles: {sourceMesh.triangles.Length/3} -> {simplifiedMesh.triangles.Length/3}");

            return simplifiedMesh;
        }
    }
}
