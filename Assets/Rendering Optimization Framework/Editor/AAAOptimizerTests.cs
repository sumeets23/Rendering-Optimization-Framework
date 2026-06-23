using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using AAAOptimizer.Core;
using AAAOptimizer.Data;
using AAAOptimizer.HLOD;

namespace AAAOptimizer.Tests
{
    public class AAAOptimizerTests
    {
        private AAAOptimizerConfig testConfig;

        [SetUp]
        public void Setup()
        {
            testConfig = ScriptableObject.CreateInstance<AAAOptimizerConfig>();
            testConfig.polyCountWarningThreshold = 1000;
            testConfig.clusterGridSize = 10f;
            testConfig.minMeshesPerCluster = 1;
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(testConfig);
        }

        [Test]
        public void TestSceneAnalyzer_FindsMockRenderers()
        {
            // Create a temporary cube primitive
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "TestCubePrimitive";
            cube.isStatic = true;

            try
            {
                // Run analysis
                SceneAnalysisData result = SceneAnalyzer.AnalyzeActiveScene(testConfig);

                // Assert
                Assert.NotNull(result);
                Assert.GreaterOrEqual(result.totalRenderers, 1, "SceneAnalyzer should find at least one renderer.");
                Assert.GreaterOrEqual(result.totalVertices, 24, "Cube primitive should have at least 24 vertices.");
            }
            finally
            {
                // Clean up scene
                Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void TestHLODClusterer_GroupsStaticMeshes()
        {
            // Create two cubes positioned close to each other
            GameObject cubeA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeA.transform.position = new Vector3(1, 0, 1);
            cubeA.isStatic = true;

            GameObject cubeB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeB.transform.position = new Vector3(2, 0, 2);
            cubeB.isStatic = true;

            try
            {
                // Grid size is 10, both cubes should be inside cell 0,0,0
                List<HLODCluster> clusters = HLODClusterer.GenerateClusters(10f, 1);

                Assert.NotNull(clusters);
                Assert.GreaterOrEqual(clusters.Count, 1, "Should generate at least one cluster.");
                
                bool foundCombinedCell = false;
                foreach (var c in clusters)
                {
                    if (c.gridCoords == Vector3Int.zero && c.renderers.Count >= 2)
                    {
                        foundCombinedCell = true;
                        break;
                    }
                }
                Assert.True(foundCombinedCell, "Both static cubes should be grouped in cell (0,0,0).");
            }
            finally
            {
                Object.DestroyImmediate(cubeA);
                Object.DestroyImmediate(cubeB);
            }
        }

        [Test]
        public void TestMeshCombiner_CombinesVerticesCorrectly()
        {
            GameObject cube1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject cube2 = GameObject.CreatePrimitive(PrimitiveType.Cube);

            MeshFilter mf1 = cube1.GetComponent<MeshFilter>();
            MeshFilter mf2 = cube2.GetComponent<MeshFilter>();

            List<MeshFilter> filters = new List<MeshFilter> { mf1, mf2 };
            Dictionary<Material, Rect> materialRectMap = new Dictionary<Material, Rect>();

            try
            {
                Mesh combined = MeshCombiner.CombineAndRemapUVs(filters, materialRectMap, "CombinedTestMesh");

                Assert.NotNull(combined);
                // Each cube has 24 vertices, so combined mesh should have 48 vertices
                Assert.AreEqual(48, combined.vertexCount, "Combined mesh vertex count should be 48.");
            }
            finally
            {
                Object.DestroyImmediate(cube1);
                Object.DestroyImmediate(cube2);
            }
        }

        [Test]
        public void TestMeshCombiner_UsesClusterRelativeTransform()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(25f, 0f, 0f);

            MeshFilter mf = cube.GetComponent<MeshFilter>();
            List<MeshFilter> filters = new List<MeshFilter> { mf };
            Dictionary<Material, Rect> materialRectMap = new Dictionary<Material, Rect>();
            Matrix4x4 clusterWorldToLocal = Matrix4x4.TRS(new Vector3(20f, 0f, 0f), Quaternion.identity, Vector3.one).inverse;

            try
            {
                Mesh combined = MeshCombiner.CombineAndRemapUVs(filters, materialRectMap, "RelativeCombinedTestMesh", clusterWorldToLocal);

                Assert.NotNull(combined);
                Assert.That(combined.bounds.center.x, Is.EqualTo(5f).Within(0.01f), "Combined mesh should be local to the HLOD cluster root, not double-offset in world space.");
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void TestMeshSimplifier_ReducesTriangleCount()
        {
            // Create a sphere primitive (has enough triangles for clustering)
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            MeshFilter mf = sphere.GetComponent<MeshFilter>();
            Mesh originalMesh = mf.sharedMesh;

            try
            {
                // Simplify with a low ratio (e.g. 0.10f for heavy clustering collapse)
                Mesh simplified = AAAOptimizer.Simplification.MeshSimplifierUtility.SimplifyMesh(originalMesh, 0.10f);

                Assert.NotNull(simplified);
                Assert.Less(simplified.triangles.Length, originalMesh.triangles.Length, "Simplified sphere should have fewer triangles.");
                Assert.Less(simplified.vertexCount, originalMesh.vertexCount, "Simplified sphere should have fewer vertices.");
            }
            finally
            {
                Object.DestroyImmediate(sphere);
            }
        }
    }
}
