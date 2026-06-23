using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AAAOptimizer.Core;
using AAAOptimizer.Data;
using AAAOptimizer.HLOD;
using AAAOptimizer.Impostors;
using AAAOptimizer.Simplification;
using AAAOptimizer.Streaming;
using AAAOptimizer.TextureStreaming;

namespace AAAOptimizer.Editor
{
    public class AAAOptimizerWindow : EditorWindow
    {
        private AAAOptimizerConfig config;
        private int activeTab = 0;
        private string[] tabs = { "Scene Audit", "HLOD & LOD", "Impostors", "Draw Calls", "Streaming" };
        private Vector2 scrollPosition;

        // Scene Analyzer fields
        private SceneAnalysisData analysisData;
        private int analyzerTab = 0;
        private string[] analyzerSubTabs = { "Overview", "High-Poly Meshes", "Materials", "Textures & VRAM", "Duplicates" };

        // LOD / HLOD fields
        private GameObject selectedLODObject;
        private List<HLODCluster> currentClusters = new List<HLODCluster>();
        private bool drawGridInScene = true;

        // Impostors fields
        private GameObject impostorTargetGo;
        private int impostorDirections = 8;
        private int impostorAtlasRes = 2048;
        private int impostorFrameRes = 256;
        private float impostorSwapDistance = 150.0f;

        // Draw Calls fields
        private GameObject rootToCombine;

        [MenuItem("Tools/Rendering Optimization System/Dashboard", false, 0)]
        public static void ShowWindow()
        {
            AAAOptimizerWindow window = GetWindow<AAAOptimizerWindow>("Rendering Optimization System");
            window.minSize = new Vector2(650, 550);
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
                if (config != null)
                {
                    impostorDirections = config.captureDirections;
                    impostorAtlasRes = config.impostorTextureSize;
                    impostorFrameRes = config.singleFrameResolution;
                    impostorSwapDistance = config.impostorSwapDistance;
                }
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

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            config = (AAAOptimizerConfig)EditorGUILayout.ObjectField("Optimizer Configuration", config, typeof(AAAOptimizerConfig), false);
            EditorGUILayout.EndVertical();

            if (config == null)
            {
                EditorGUILayout.HelpBox("Please assign or create a AAAOptimizerConfig ScriptableObject to begin optimization operations.", MessageType.Warning);
                if (GUILayout.Button("Create New Configuration"))
                {
                    CreateNewConfig();
                }
                return;
            }

            GUILayout.Space(10);
            activeTab = GUILayout.Toolbar(activeTab, tabs);
            GUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (activeTab)
            {
                case 0:
                    DrawSceneAuditTab();
                    break;
                case 1:
                    DrawHLODTab();
                    break;
                case 2:
                    DrawImpostorsTab();
                    break;
                case 3:
                    DrawDrawCallsTab();
                    break;
                case 4:
                    DrawStreamingTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
            Color originalColor = GUI.contentColor;
            GUI.contentColor = new Color(0.3f, 0.7f, 1f); // Vibrant light blue
            GUILayout.Label("RENDERING OPTIMIZATION SYSTEM", headerStyle, GUILayout.Height(30));
            GUI.contentColor = originalColor;
            GUILayout.Space(5);
        }

        private void CreateNewConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Rendering Optimization Config", "RenderingOptimizationConfig", "asset", "Save Config File");
            if (!string.IsNullOrEmpty(path))
            {
                AAAOptimizerConfig newConfig = ScriptableObject.CreateInstance<AAAOptimizerConfig>();
                AssetDatabase.CreateAsset(newConfig, path);
                AssetDatabase.SaveAssets();
                config = newConfig;
            }
        }

        #region Tab 0: Scene Audit
        private void DrawSceneAuditTab()
        {
            if (GUILayout.Button("SCAN ACTIVE SCENE", GUILayout.Height(40)))
            {
                AnalyzeScene();
            }

            if (analysisData == null)
            {
                EditorGUILayout.HelpBox("No scene scan data. Click 'SCAN ACTIVE SCENE' to profile rendering metrics and warn bounds.", MessageType.Info);
                return;
            }

            GUILayout.Space(15);
            analyzerTab = GUILayout.Toolbar(analyzerTab, analyzerSubTabs);
            GUILayout.Space(10);

            switch (analyzerTab)
            {
                case 0:
                    DrawAuditOverview();
                    break;
                case 1:
                    DrawAuditHighPoly();
                    break;
                case 2:
                    DrawAuditMaterials();
                    break;
                case 3:
                    DrawAuditTextures();
                    break;
                case 4:
                    DrawAuditDuplicates();
                    break;
            }
        }

        private void AnalyzeScene()
        {
            EditorUtility.DisplayProgressBar("Scanning Scene", "Collecting statistics and rendering metrics...", 0.3f);
            try
            {
                analysisData = SceneAnalyzer.AnalyzeActiveScene(config);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void DrawAuditOverview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene Analysis Report Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Scene: {analysisData.sceneName}");
            EditorGUILayout.LabelField($"Scanned At: {analysisData.scanTimestamp:yyyy-MM-dd HH:mm:ss}");
            GUILayout.Space(5);
            EditorGUILayout.LabelField($"Total Renderers: {analysisData.totalRenderers:N0}");
            EditorGUILayout.LabelField($"Total Vertices: {analysisData.totalVertices:N0}");
            EditorGUILayout.LabelField($"Total Triangles: {analysisData.totalTriangles:N0}");
            EditorGUILayout.LabelField($"Total Unique Materials: {analysisData.totalMaterialsCount:N0}");
            EditorGUILayout.LabelField($"Total Unique Textures: {analysisData.totalTexturesCount:N0}");
            EditorGUILayout.LabelField($"Estimated VRAM Footprint: {FormatBytes(analysisData.estimatedVRAMBytes)}");
            EditorGUILayout.LabelField($"Active LODGroups: {analysisData.lodGroupsCount:N0}");
            EditorGUILayout.LabelField($"Shadow Caster Renderers: {analysisData.shadowCasterCount:N0}");
            EditorGUILayout.LabelField($"Static GameObjects: {analysisData.staticBatchableCount:N0}");
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Diagnostic Logs & Warnings", EditorStyles.boldLabel);
            if (analysisData.warningLogs.Count == 0)
            {
                EditorGUILayout.HelpBox("Excellent! The scene matches the target optimization profile requirements.", MessageType.Info);
            }
            else
            {
                foreach (string log in analysisData.warningLogs)
                {
                    EditorGUILayout.HelpBox(log, MessageType.Warning);
                }
            }
        }

        private void DrawAuditHighPoly()
        {
            EditorGUILayout.LabelField("Meshes Sorted by Complexity (Descending)", EditorStyles.boldLabel);
            if (analysisData.highPolyMeshes.Count == 0)
            {
                EditorGUILayout.LabelField("No mesh render data collected.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mesh Asset Name", EditorStyles.boldLabel, GUILayout.Width(200));
            GUILayout.Label("Triangles", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("Vertices", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("Submeshes", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("LOD Grouped", EditorStyles.boldLabel, GUILayout.Width(90));
            GUILayout.EndHorizontal();

            foreach (var mesh in analysisData.highPolyMeshes)
            {
                if (mesh.triangleCount > config.polyCountWarningThreshold / 2)
                {
                    GUI.contentColor = Color.yellow;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(mesh.meshName, GUILayout.Width(200));
                GUILayout.Label(mesh.triangleCount.ToString("N0"), GUILayout.Width(100));
                GUILayout.Label(mesh.vertexCount.ToString("N0"), GUILayout.Width(100));
                GUILayout.Label(mesh.subMeshCount.ToString(), GUILayout.Width(80));
                GUILayout.Label(mesh.hasLODGroup ? "Yes" : "No", GUILayout.Width(90));
                GUILayout.EndHorizontal();
                GUI.contentColor = Color.white;
            }
        }

        private void DrawAuditMaterials()
        {
            EditorGUILayout.LabelField("Unique Material Resource Auditing", EditorStyles.boldLabel);
            if (analysisData.materials.Count == 0)
            {
                EditorGUILayout.LabelField("No material resources discovered.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Material", EditorStyles.boldLabel, GUILayout.Width(200));
            GUILayout.Label("Shader Name", EditorStyles.boldLabel, GUILayout.Width(200));
            GUILayout.Label("SRP Batcher", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("GPU Instanced", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            foreach (var mat in analysisData.materials)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(mat.materialName, GUILayout.Width(200));
                GUILayout.Label(mat.shaderName, GUILayout.Width(200));
                GUILayout.Label(mat.isSRPBatcherCompatible ? "Compatible" : "Incompatible", GUILayout.Width(100));
                GUILayout.Label(mat.enableInstancing ? "Enabled" : "Disabled", GUILayout.Width(100));
                GUILayout.EndHorizontal();
            }
        }

        private void DrawAuditTextures()
        {
            EditorGUILayout.LabelField("Textures VRAM Allocation List", EditorStyles.boldLabel);
            if (analysisData.textures.Count == 0)
            {
                EditorGUILayout.LabelField("No textures found.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Texture Name", EditorStyles.boldLabel, GUILayout.Width(200));
            GUILayout.Label("Resolution", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("Est. Size", EditorStyles.boldLabel, GUILayout.Width(90));
            GUILayout.Label("Format", EditorStyles.boldLabel, GUILayout.Width(120));
            GUILayout.Label("Mip Streaming", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            foreach (var tex in analysisData.textures)
            {
                if (tex.width > config.textureSizeWarningThreshold)
                {
                    GUI.contentColor = Color.yellow;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(tex.textureName, GUILayout.Width(200));
                GUILayout.Label($"{tex.width}x{tex.height}", GUILayout.Width(100));
                GUILayout.Label(FormatBytes(tex.estimatedBytes), GUILayout.Width(90));
                GUILayout.Label(tex.format, GUILayout.Width(120));
                GUILayout.Label(tex.streamingEnabled ? "Active" : "Off", GUILayout.Width(100));
                GUILayout.EndHorizontal();
                GUI.contentColor = Color.white;
            }
        }

        private void DrawAuditDuplicates()
        {
            EditorGUILayout.LabelField("Duplicate Material Asset Instances", EditorStyles.boldLabel);
            if (analysisData.duplicateMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox("Great! No duplicate material references found in rendering resources.", MessageType.Info);
                return;
            }

            foreach (var group in analysisData.duplicateMaterials)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Material Reference: {group.baseMaterialName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Shared Duplicate Count: {group.duplicateMaterialPaths.Count}");
                GUILayout.Label("Active Reference Paths:");
                foreach (string path in group.duplicateMaterialPaths)
                {
                    GUILayout.Label("  - " + path);
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }
        }
        #endregion

        #region Tab 1: HLOD & LOD
        private void DrawHLODTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Automatic Individual GameObject LOD Generator", EditorStyles.boldLabel);
            selectedLODObject = (GameObject)EditorGUILayout.ObjectField("Target GameObject", selectedLODObject, typeof(GameObject), true);
            
            GUILayout.Space(5);
            if (GUILayout.Button("GENERATE LODS (LOD0 -> LOD3)", GUILayout.Height(30)))
            {
                if (selectedLODObject == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign a Target GameObject.", "OK");
                }
                else
                {
                    EditorUtility.DisplayProgressBar("Generating LODs", $"Simplifying meshes for {selectedLODObject.name}...", 0.4f);
                    try
                    {
                        LODGroupAutomator.GenerateLODsForObject(selectedLODObject, config);
                        EditorUtility.DisplayDialog("Success", $"LOD Group generated successfully for {selectedLODObject.name}.", "OK");
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Hierarchical LOD (HLOD) Spatial Merging", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CALCULATE CLUSTERS", GUILayout.Height(30)))
            {
                currentClusters = HLODClusterer.GenerateClusters(config.clusterGridSize, config.minMeshesPerCluster);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("BAKE HLOD PROXIES", GUILayout.Height(30)))
            {
                BakeHLODs();
            }

            if (GUILayout.Button("CLEAR ALL SCENE HLODS", GUILayout.Height(30)))
            {
                ClearHLODs();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            drawGridInScene = EditorGUILayout.Toggle("Visualize Cell Grid in SceneView", drawGridInScene);

            if (currentClusters.Count > 0)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField($"Identified Spatial Grid Clusters: {currentClusters.Count}", EditorStyles.boldLabel);
                for (int i = 0; i < currentClusters.Count; i++)
                {
                    var cluster = currentClusters[i];
                    EditorGUILayout.LabelField($"Cluster #{i}: Cell {cluster.gridCoords} | Renderers: {cluster.renderers.Count} | Size: {cluster.bounds.size:F1}m");
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void BakeHLODs()
        {
            ClearHLODs();

            if (currentClusters.Count == 0)
            {
                currentClusters = HLODClusterer.GenerateClusters(config.clusterGridSize, config.minMeshesPerCluster);
            }

            if (currentClusters.Count == 0)
            {
                EditorUtility.DisplayDialog("Bake Aborted", "No spatial clustering cells identified. Check static mesh flags.", "OK");
                return;
            }

            GameObject hlodRoot = GameObject.Find("HLOD_Root");
            if (hlodRoot == null)
            {
                hlodRoot = new GameObject("HLOD_Root");
                hlodRoot.isStatic = true;
                Undo.RegisterCreatedObjectUndo(hlodRoot, "Create HLOD Root");
            }

            int bakedCount = 0;
            for (int i = 0; i < currentClusters.Count; i++)
            {
                var cluster = currentClusters[i];
                float progress = (float)i / currentClusters.Count;
                EditorUtility.DisplayProgressBar("Baking HLOD Proxies", $"Baking cluster {i + 1} of {currentClusters.Count}...", progress);

                string clusterName = $"HLOD_Cluster_{cluster.gridCoords.x}_{cluster.gridCoords.y}_{cluster.gridCoords.z}";

                Dictionary<Material, Rect> materialRectMap;
                Material atlasMaterial = MaterialAtlasBuilder.BuildAtlas(cluster.renderers, config.maxAtlasResolution, config.atlasPadding, clusterName, out materialRectMap);

                List<MeshFilter> meshFilters = new List<MeshFilter>();
                foreach (var mr in cluster.renderers)
                {
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    if (mf != null) meshFilters.Add(mf);
                }

                Matrix4x4 clusterWorldToLocal = Matrix4x4.TRS(cluster.bounds.center, Quaternion.identity, Vector3.one).inverse;
                Mesh combinedMesh = MeshCombiner.CombineAndRemapUVs(meshFilters, materialRectMap, $"{clusterName}_Mesh", clusterWorldToLocal);

                if (combinedMesh != null)
                {
                    Mesh simplifiedCombined = MeshSimplifierUtility.SimplifyMesh(combinedMesh, config.lod2ReductionRatio);
                    if (simplifiedCombined != combinedMesh)
                    {
                        DestroyImmediate(combinedMesh);
                    }

                    string meshPath = $"Assets/AAAOptimizer/Data/Atlases/{clusterName}_ProxyMesh.asset";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(simplifiedCombined, meshPath);
                    AssetDatabase.SaveAssets();

                    GameObject clusterRoot = new GameObject(clusterName);
                    clusterRoot.transform.position = cluster.bounds.center;
                    clusterRoot.transform.SetParent(hlodRoot.transform, true);
                    clusterRoot.isStatic = true;

                    GameObject proxyGo = new GameObject($"{clusterName}_Proxy");
                    proxyGo.transform.SetParent(clusterRoot.transform, false);
                    proxyGo.transform.localPosition = Vector3.zero;
                    proxyGo.transform.localRotation = Quaternion.identity;
                    proxyGo.transform.localScale = Vector3.one;
                    proxyGo.isStatic = true;

                    MeshFilter proxyMf = proxyGo.AddComponent<MeshFilter>();
                    proxyMf.sharedMesh = simplifiedCombined;

                    MeshRenderer proxyMr = proxyGo.AddComponent<MeshRenderer>();
                    proxyMr.sharedMaterial = atlasMaterial;

                    HLODController hlodController = clusterRoot.AddComponent<HLODController>();
                    hlodController.proxyRenderer = proxyMr;
                    hlodController.transitionDistance = config.hlodTransitionDistance;

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

        private void ClearHLODs()
        {
            HLODController[] controllers = FindObjectsByType<HLODController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            foreach (var controller in controllers)
            {
                if (controller != null)
                {
                    Transform controllerParent = controller.transform.parent;
                    foreach (var child in controller.highDetailChildren)
                    {
                        if (child != null)
                        {
                            child.SetActive(true);
                            if (child.transform.parent == controller.transform)
                            {
                                Undo.SetTransformParent(child.transform, controllerParent, "HLOD Clean Reparenting");
                            }
                        }
                    }
                    Undo.DestroyObjectImmediate(controller.gameObject);
                    count++;
                }
            }

            GameObject root = GameObject.Find("HLOD_Root");
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
            }

            Debug.Log($"[Rendering Optimization System] Cleared {count} HLOD clusters from scene.");
        }
        #endregion

        #region Tab 2: Impostors
        private void DrawImpostorsTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Capture Target setup", EditorStyles.boldLabel);
            impostorTargetGo = (GameObject)EditorGUILayout.ObjectField("Target GameObject", impostorTargetGo, typeof(GameObject), true);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Capture Parameters", EditorStyles.boldLabel);
            impostorDirections = EditorGUILayout.IntSlider("Angle Divisions", impostorDirections, 4, 32);
            impostorFrameRes = EditorGUILayout.IntPopup("Individual Frame Res", impostorFrameRes,
                new string[] { "64 x 64", "128 x 128", "256 x 256", "512 x 512" },
                new int[] { 64, 128, 256, 512 });
            impostorAtlasRes = EditorGUILayout.IntPopup("Atlas Resolution", impostorAtlasRes,
                new string[] { "1024 x 1024", "2048 x 2048", "4096 x 4096" },
                new int[] { 1024, 2048, 4096 });
            impostorSwapDistance = EditorGUILayout.FloatField("Swap Distance Threshold", impostorSwapDistance);
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            if (GUILayout.Button("BAKE MULTI-PASS IMPOSTOR PREFAB", GUILayout.Height(40)))
            {
                if (impostorTargetGo == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign a Target GameObject.", "OK");
                }
                else
                {
                    ExecuteImpostorBake();
                }
            }
        }

        private void ExecuteImpostorBake()
        {
            EditorUtility.DisplayProgressBar("Baking Impostor Maps", $"Rendering camera passes for {impostorTargetGo.name}...", 0.5f);
            try
            {
                ImpostorBaker.Bake(impostorTargetGo, impostorDirections, impostorAtlasRes, impostorFrameRes, impostorSwapDistance);
                EditorUtility.DisplayDialog("Success", $"Successfully baked color, normal, and depth impostor prefab card.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        #endregion

        #region Tab 3: Draw Calls
        private void DrawDrawCallsTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("GPU Instancing Optimizer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Scan all materials assigned to renderers in the active scene and force enable GPU Instancing to batch them on GPU.", MessageType.Info);
            if (GUILayout.Button("Enable GPU Instancing on All Materials", GUILayout.Height(30)))
            {
                DrawCallOptimizer.EnableGPUInstancingOnAllMaterials();
                EditorUtility.DisplayDialog("Success", "Forced GPU Instancing on active scene materials.", "OK");
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Static Mesh Batch Combiner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Combines nested static submeshes under a hierarchy into single vertex buffers grouped by material to save draw calls.", MessageType.Info);
            rootToCombine = (GameObject)EditorGUILayout.ObjectField("Root Object", rootToCombine, typeof(GameObject), true);

            GUILayout.Space(5);
            if (GUILayout.Button("Combine Meshes by Material", GUILayout.Height(30)))
            {
                if (rootToCombine == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign a Root GameObject.", "OK");
                }
                else
                {
                    EditorUtility.DisplayProgressBar("Combining Meshes", "Analyzing materials and combining vertex buffers...", 0.5f);
                    try
                    {
                        var list = DrawCallOptimizer.CombineStaticMeshesByMaterial(rootToCombine);
                        EditorUtility.DisplayDialog("Success", $"Batched submeshes of {rootToCombine.name} into {list.Count} materials group meshes.", "OK");
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region Tab 4: Streaming
        private void DrawStreamingTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("World Grid Coordinate Partitioning", EditorStyles.boldLabel);
            config.cellSize = EditorGUILayout.FloatField("Cell Dimension Size (m)", config.cellSize);
            config.loadRadius = EditorGUILayout.FloatField("Loading Radius (m)", config.loadRadius);
            config.unloadRadiusMargin = EditorGUILayout.FloatField("Unload Margin Hysteresis (m)", config.unloadRadiusMargin);
            config.predictiveLoading = EditorGUILayout.Toggle("Velocity-based Predictive Loading", config.predictiveLoading);
            config.maxSceneLoadsPerFrame = EditorGUILayout.IntSlider("Max Scene Loads Per Frame", config.maxSceneLoadsPerFrame, 1, 5);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Coordinate streaming operates on additively loaded scene chunks named 'Chunk_X_Z' (e.g. Chunk_0_0) configured in the Editor Build Settings.", MessageType.Info);
        }
        #endregion

        // Handles rendering cell grids in SceneView
        private void OnSceneGUI(SceneView sceneView)
        {
            if (!drawGridInScene || config == null || config.cellSize <= 5f) return;

            // Render grid lines around camera position in SceneView
            Camera sceneCam = sceneView.camera;
            if (sceneCam == null) return;

            Vector3 camPos = sceneCam.transform.position;
            int cellX = Mathf.FloorToInt(camPos.x / config.cellSize);
            int cellZ = Mathf.FloorToInt(camPos.z / config.cellSize);

            float size = config.cellSize;
            int range = 6;

            for (int x = cellX - range; x <= cellX + range; x++)
            {
                for (int z = cellZ - range; z <= cellZ + range; z++)
                {
                    Vector3 center = new Vector3((x + 0.5f) * size, 0, (z + 0.5f) * size);
                    Vector3 sizeVec = new Vector3(size, 0f, size);

                    Handles.color = new Color(0f, 0.8f, 0f, 0.15f);
                    Handles.DrawWireCube(center, sizeVec);
                }
            }

            // Draw current active cell in solid green outline
            Vector3 activeCenter = new Vector3((cellX + 0.5f) * size, 0, (cellZ + 0.5f) * size);
            Handles.color = Color.green;
            Handles.DrawWireCube(activeCenter, new Vector3(size, 2f, size));
            Handles.Label(activeCenter + Vector3.up, $"Active Cell: {cellX}, {cellZ}");
        }

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1073741824) return $"{(double)bytes / 1073741824:F2} GB";
            if (bytes >= 1048576) return $"{(double)bytes / 1048576:F2} MB";
            if (bytes >= 1024) return $"{(double)bytes / 1024:F2} KB";
            return $"{bytes} Bytes";
        }
    }
}

namespace AAAOptimizer.Editor
{
    [UnityEditor.InitializeOnLoad]
    public static class ImpostorMaterialRepairUtility
    {
        private const string ImpostorShaderName = "AAAOptimizer/Impostor/HDRPUnlitAtlasCutout";
        private const string ImpostorShaderPath = "Assets/AAAOptimizer/Shaders/AAAImpostorHDRPUnlitAtlasCutout_Fixed.shader";
        private const string ImpostorMaterialFolder = "Assets/AAAOptimizer/Data/Impostors";

        static ImpostorMaterialRepairUtility()
        {
            UnityEditor.EditorApplication.delayCall += RepairGeneratedMaterialsIfNeeded;
        }

        [UnityEditor.MenuItem("Tools/AAA Optimizer/Repair Impostor Materials")]
        public static void RepairGeneratedMaterialsIfNeeded()
        {
            UnityEngine.Shader shader = ResolveImpostorShader();
            if (shader == null)
            {
                UnityEngine.Debug.LogError($"[ImpostorMaterialRepairUtility] Could not resolve impostor shader at {ImpostorShaderPath}.");
                return;
            }

            string[] materialGuids = UnityEditor.AssetDatabase.FindAssets("t:Material", new[] { ImpostorMaterialFolder });
            int repaired = 0;

            foreach (string guid in materialGuids)
            {
                string materialPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (!materialPath.EndsWith("_ImpostorMat.mat")) continue;

                UnityEngine.Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(materialPath);
                if (material == null) continue;

                bool changed = false;
                if (material.shader != shader)
                {
                    material.shader = shader;
                    changed = true;
                }

                if (material.HasProperty("_ImpostorSunDir")) material.SetVector("_ImpostorSunDir", UnityEngine.RenderSettings.sun != null ? -UnityEngine.RenderSettings.sun.transform.forward : UnityEngine.Vector3.up);
                if (material.HasProperty("_ImpostorLightColor")) material.SetColor("_ImpostorLightColor", UnityEngine.RenderSettings.sun != null ? UnityEngine.RenderSettings.sun.color * UnityEngine.RenderSettings.sun.intensity : UnityEngine.Color.white);
                if (material.HasProperty("_ImpostorAmbientColor")) material.SetColor("_ImpostorAmbientColor", UnityEngine.RenderSettings.ambientLight.maxColorComponent > 0.001f ? UnityEngine.RenderSettings.ambientLight : new UnityEngine.Color(0.45f, 0.45f, 0.45f, 1.0f));
                if (material.HasProperty("_ImpostorLightStrength")) material.SetFloat("_ImpostorLightStrength", UnityEngine.RenderSettings.sun != null ? 0.75f : 0.0f);
                if (material.HasProperty("_ImpostorAmbientStrength")) material.SetFloat("_ImpostorAmbientStrength", 0.55f);
                changed = true;
                UnityEditor.EditorUtility.SetDirty(material);
                if (changed) repaired++;
            }

            if (repaired > 0)
            {
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
                UnityEngine.Debug.Log($"[ImpostorMaterialRepairUtility] Repaired shader assignment and relight defaults on {repaired} generated impostor material(s).");
            }
        }

        private static UnityEngine.Shader ResolveImpostorShader()
        {
            UnityEditor.AssetDatabase.ImportAsset(ImpostorShaderPath, UnityEditor.ImportAssetOptions.ForceUpdate);

            UnityEngine.Shader shader = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(ImpostorShaderPath);
            if (shader != null) return shader;

            shader = UnityEngine.Shader.Find(ImpostorShaderName);
            if (shader != null) return shader;

            shader = UnityEngine.Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) return shader;

            shader = UnityEngine.Shader.Find("Unlit/Texture");
            if (shader != null) return shader;

            return UnityEngine.Shader.Find("Standard");
        }
    }
}