using System.Collections.Generic;
using UnityEngine;

namespace AAAOptimizer.HLOD
{
    public class HLODCluster
    {
        public Vector3Int gridCoords;
        public Bounds bounds;
        public List<MeshRenderer> renderers = new List<MeshRenderer>();
    }

    public static class HLODClusterer
    {
        public static List<HLODCluster> GenerateClusters(float gridSize, int minRenderersCount)
        {
            List<HLODCluster> clusters = new List<HLODCluster>();
            MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Dictionary<Vector3Int, HLODCluster> clusterMap = new Dictionary<Vector3Int, HLODCluster>();

            foreach (var renderer in renderers)
            {
                // Only cluster static renderers that aren't already part of an HLOD or simple LOD sub-hierarchy
                if (!renderer.gameObject.isStatic) continue;
                if (renderer.GetComponentInParent<HLODController>() != null) continue;
                if (renderer.gameObject.name.StartsWith("HLOD_") ||
                    renderer.gameObject.name.Contains("_Proxy")) continue;
                if (renderer.gameObject.name.Contains("_LOD1") || 
                    renderer.gameObject.name.Contains("_LOD2") || 
                    renderer.gameObject.name.Contains("_LOD3")) continue;

                Vector3 position = renderer.bounds.center;
                Vector3Int gridCoords = new Vector3Int(
                    Mathf.FloorToInt(position.x / gridSize),
                    Mathf.FloorToInt(position.y / gridSize),
                    Mathf.FloorToInt(position.z / gridSize)
                );

                if (!clusterMap.TryGetValue(gridCoords, out HLODCluster cluster))
                {
                    cluster = new HLODCluster
                    {
                        gridCoords = gridCoords,
                        bounds = new Bounds(renderer.bounds.center, renderer.bounds.size)
                    };
                    clusterMap.Add(gridCoords, cluster);
                }
                else
                {
                    Bounds b = cluster.bounds;
                    b.Encapsulate(renderer.bounds);
                    cluster.bounds = b;
                }

                cluster.renderers.Add(renderer);
            }

            // Filter clusters by minimum size requirements
            foreach (var cluster in clusterMap.Values)
            {
                if (cluster.renderers.Count >= minRenderersCount)
                {
                    clusters.Add(cluster);
                }
            }

            Debug.Log($"[HLODClusterer] Generated {clusters.Count} clusters from {renderers.Length} static renderers with cell size {gridSize}.");
            return clusters;
        }
    }
}
