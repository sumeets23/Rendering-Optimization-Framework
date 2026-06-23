using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AAAOptimizer.Core;
using AAAOptimizer.HLOD;
using AAAOptimizer.Simplification;

namespace AAAOptimizer.Editor
{
    public class HLODGeneratorWindow : EditorWindow
    {
        private AAAOptimizerConfig config;
        private List<HLODCluster> currentClusters = new List<HLODCluster>();
        private Vector2 scrollPosition;
        private bool showLODSection = true;
        private bool showHLODSection = true;

        // Selection variables for LOD
        private GameObject selectedLODObject;
        private Transform customHLODRoot;

        public static void ShowWindow()
        {
            HLODGeneratorWindow window = GetWindow<HLODGeneratorWindow>("HLOD & LOD Generator");
            window.minSize = new Vector2(500, 450);
            window.Show();
        }

        private void OnEnable()
        {
            // Find default config
            string[] guids = AssetDatabase.FindAssets("t:AAAOptimizerConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<AAAOptimizerConfig>(path);
            }
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            DrawHeader();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Config Object Selection
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            config = (AAAOptimizerConfig)EditorGUILayout.ObjectField("Optimizer Config", config, typeof(AAAOptimizerConfig), false);
            EditorGUILayout.EndVertical();

            if (config == null)
            {
                EditorGUILayout.HelpBox("Please assign or create a AAAOptimizerConfig ScriptableObject.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            GUILayout.Space(10);

            // STANDALONE LOD SECTION
            showLODSection = EditorGUILayout.BeginFoldoutHeaderGroup(showLODSection, "Automatic LOD Generator (LOD0-LOD3)");
            if (showLODSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox("Select a GameObject with a MeshFilter and MeshRenderer in the hierarchy. This tool generates decimated meshes for LOD1, LOD2, LOD3 and wires them to an active LODGroup.", MessageType.Info);
                
                selectedLODObject = (GameObject)EditorGUILayout.ObjectField("Target GameObject", selectedLODObject, typeof(GameObject), true);
                
                GUILayout.Space(5);
                if (GUILayout.Button("GENERATE LOD GROUP", GUILayout.Height(30)))
                {
                    if (selectedLODObject == null)
                    {
                        EditorUtility.DisplayDialog("Error", "Please assign a Target GameObject.", "OK");
                    }
                    else
                    {
                        GenerateLODGroup();
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            GUILayout.Space(15);

            // HLOD SECTION
            showHLODSection = EditorGUILayout.BeginFoldoutHeaderGroup(showHLODSection, "Hierarchical LOD (HLOD) Generator");
            if (showHLODSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox("Splits all static meshes in the scene into spatial cell volumes, combines them, packs texture atlases, and simplifies far-distance clusters.", MessageType.Info);

                customHLODRoot = (Transform)EditorGUILayout.ObjectField(new GUIContent("Custom HLOD Root", "Optional. If assigned, baked clusters will be parented here instead of creating a new HLOD_Root."), customHLODRoot, typeof(Transform), true);
                GUILayout.Space(5);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("1. CALCULATE CLUSTERS", GUILayout.Height(30)))
                {
                    CalculateClusters();
                }

                if (GUILayout.Button("2. BAKE HLOD PROXIES", GUILayout.Height(30)))
                {
                    BakeHLODs();
                }

                if (GUILayout.Button("CLEAR HLODS", GUILayout.Height(30)))
                {
                    ClearHLODs();
                }
                GUILayout.EndHorizontal();

                if (currentClusters.Count > 0)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField($"Active Clusters Visualized: {currentClusters.Count}", EditorStyles.boldLabel);
                    
                    for (int i = 0; i < currentClusters.Count; i++)
                    {
                        var cluster = currentClusters[i];
                        EditorGUILayout.LabelField($"Cluster #{i}: Cell {cluster.gridCoords} | Renderers: {cluster.renderers.Count} | Bounding Size: {cluster.bounds.size:F1}");
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            Color originalColor = GUI.contentColor;
            GUI.contentColor = new Color(0.3f, 0.7f, 1f);
            GUILayout.Label("HLOD & LOD GEOMETRY GENERATOR", headerStyle, GUILayout.Height(25));
            GUI.contentColor = originalColor;
            GUILayout.Space(10);
        }

        private void GenerateLODGroup()
        {
            EditorUtility.DisplayProgressBar("Generating LODs", $"Baking simplified meshes for {selectedLODObject.name}...", 0.4f);
            try
            {
                LODGroupAutomator.GenerateLODsForObject(selectedLODObject, config);
                EditorUtility.DisplayDialog("Success", $"LOD Group generated for {selectedLODObject.name}.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void CalculateClusters()
        {
            currentClusters = HLODClusterer.GenerateClusters(config.clusterGridSize, config.minMeshesPerCluster);
            SceneView.RepaintAll();
        }

        private void ClearHLODs()
        {
            // Find all HLODController instances in the scene (including inactive/active)
            HLODController[] controllers = Object.FindObjectsByType<HLODController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            foreach (var controller in controllers)
            {
                if (controller != null)
                {
                    Transform controllerParent = controller.transform.parent;
                    
                    // Reparent any child that was reparented under us
                    foreach (var child in controller.highDetailChildren)
                    {
                        if (child != null)
                        {
                            child.SetActive(true);
                            if (child.transform.parent == controller.transform)
                            {
                                // Restore original hierarchy approximation by parenting to the controller's parent
                                Undo.SetTransformParent(child.transform, controllerParent, "HLOD Clean Reparenting");
                            }
                        }
                    }
                    
                    Undo.DestroyObjectImmediate(controller.gameObject);
                    count++;
                }
            }

            // Also check for the root gameobject "HLOD_Root"
            GameObject root = GameObject.Find("HLOD_Root");
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
            }

            Debug.Log($"[HLODGeneratorWindow] Cleared {count} HLOD clusters from the scene.");
        }

        private void BakeHLODs()
        {
            // 0. Auto-clean existing HLODs first to avoid duplicates
            ClearHLODs();

            if (currentClusters.Count == 0)
            {
                CalculateClusters();
            }

            if (currentClusters.Count == 0)
            {
                EditorUtility.DisplayDialog("Bake Aborted", "No spatial clusters found in scene static assets. Make sure meshes are marked static.", "OK");
                return;
            }

            // Create or find a central HLOD_Root
            GameObject hlodRoot = null;
            if (customHLODRoot != null)
            {
                hlodRoot = customHLODRoot.gameObject;
            }
            else
            {
                hlodRoot = GameObject.Find("HLOD_Root");
                if (hlodRoot == null)
                {
                    hlodRoot = new GameObject("HLOD_Root");
                    hlodRoot.isStatic = true;
                    Undo.RegisterCreatedObjectUndo(hlodRoot, "Create HLOD Root");
                }
            }

            int bakedCount = 0;
            for (int i = 0; i < currentClusters.Count; i++)
            {
                var cluster = currentClusters[i];
                float progress = (float)i / currentClusters.Count;
                EditorUtility.DisplayProgressBar("Baking HLOD Proxies", $"Baking cluster {i + 1} of {currentClusters.Count}...", progress);

                string clusterName = $"HLOD_Cluster_{cluster.gridCoords.x}_{cluster.gridCoords.y}_{cluster.gridCoords.z}";

                // 1. Pack Material Atlas
                Dictionary<Material, Rect> materialRectMap;
                Material atlasMaterial = MaterialAtlasBuilder.BuildAtlas(cluster.renderers, config.maxAtlasResolution, config.atlasPadding, clusterName, out materialRectMap);

                // 2. Collect MeshFilters
                List<MeshFilter> meshFilters = new List<MeshFilter>();
                foreach (var mr in cluster.renderers)
                {
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    if (mf != null) meshFilters.Add(mf);
                }

                // 3. Combine and remap UVs
                Matrix4x4 clusterWorldToLocal = Matrix4x4.TRS(cluster.bounds.center, Quaternion.identity, Vector3.one).inverse;
                Mesh combinedMesh = MeshCombiner.CombineAndRemapUVs(meshFilters, materialRectMap, $"{clusterName}_Mesh", clusterWorldToLocal);

                if (combinedMesh != null)
                {
                    // 4. Simplify the combined mesh
                    Mesh simplifiedCombined = MeshSimplifierUtility.SimplifyMesh(combinedMesh, config.lod2ReductionRatio);
                    
                    // Clean up intermediate unsimplified combined mesh memory if a new mesh was instantiated
                    if (simplifiedCombined != combinedMesh)
                    {
                        Object.DestroyImmediate(combinedMesh);
                    }

                    // Save simplified mesh asset
                    string meshPath = $"Assets/AAAOptimizer/Data/Atlases/{clusterName}_ProxyMesh.asset";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(simplifiedCombined, meshPath);
                    AssetDatabase.SaveAssets();

                    // 5. Build Proxy GameObject
                    GameObject clusterRoot = new GameObject(clusterName);
                    clusterRoot.transform.position = cluster.bounds.center;
                    clusterRoot.transform.SetParent(hlodRoot.transform, true);
                    clusterRoot.isStatic = true; // Mark as static for batching optimizations

                    GameObject proxyGo = new GameObject($"{clusterName}_Proxy");
                    proxyGo.transform.SetParent(clusterRoot.transform, false);
                    proxyGo.transform.localPosition = Vector3.zero;
                    proxyGo.transform.localRotation = Quaternion.identity;
                    proxyGo.transform.localScale = Vector3.one;
                    proxyGo.isStatic = true; // Mark proxy as static too

                    MeshFilter proxyMf = proxyGo.AddComponent<MeshFilter>();
                    proxyMf.sharedMesh = simplifiedCombined;

                    MeshRenderer proxyMr = proxyGo.AddComponent<MeshRenderer>();
                    proxyMr.sharedMaterial = atlasMaterial;

                    HLODController hlodController = clusterRoot.AddComponent<HLODController>();
                    hlodController.proxyRenderer = proxyMr;
                    hlodController.transitionDistance = config.hlodTransitionDistance;

                    // Wire references WITHOUT reparenting to preserve original scene hierarchy
                    foreach (var mr in cluster.renderers)
                    {
                        hlodController.highDetailChildren.Add(mr.gameObject);
                    }

                    Undo.RegisterCreatedObjectUndo(clusterRoot, "Bake HLOD Cluster");
                    bakedCount++;
                }
            }

            EditorUtility.ClearProgressBar();
            currentClusters.Clear();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Success", $"Bake complete! Created {bakedCount} HLOD clusters in scene.", "OK");
        }

        // Draw Cluster bounding volumes in the Scene View during design time
        private void OnSceneGUI(SceneView sceneView)
        {
            if (currentClusters == null) return;

            foreach (var cluster in currentClusters)
            {
                Handles.color = Color.cyan;
                Handles.DrawWireCube(cluster.bounds.center, cluster.bounds.size);

                Handles.Label(cluster.bounds.center, $"Cluster: {cluster.renderers.Count} Meshes");
            }
        }
    }
}
