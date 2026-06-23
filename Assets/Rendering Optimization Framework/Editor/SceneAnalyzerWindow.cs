using System;
using UnityEngine;
using UnityEditor;
using AAAOptimizer.Core;
using AAAOptimizer.Data;

namespace AAAOptimizer.Editor
{
    public class SceneAnalyzerWindow : EditorWindow
    {
        private AAAOptimizerConfig config;
        private SceneAnalysisData analysisData;
        private Vector2 scrollPosition;
        private int activeTab = 0;
        private string[] tabs = { "Overview & Warnings", "High-Poly Meshes", "Materials", "Textures & VRAM", "Duplicate Materials" };

        public static void ShowWindow()
        {
            SceneAnalyzerWindow window = GetWindow<SceneAnalyzerWindow>("Scene Analyzer");
            window.minSize = new Vector2(600, 500);
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
        }

        private void OnGUI()
        {
            // Title Header
            DrawHeader();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Configuration Settings", EditorStyles.boldLabel);
            config = (AAAOptimizerConfig)EditorGUILayout.ObjectField("Optimizer Config", config, typeof(AAAOptimizerConfig), false);
            EditorGUILayout.EndVertical();

            if (config == null)
            {
                EditorGUILayout.HelpBox("Please assign or create a AAAOptimizerConfig ScriptableObject to begin scanning.", MessageType.Warning);
                if (GUILayout.Button("Create New Configuration"))
                {
                    CreateNewConfig();
                }
                return;
            }

            GUILayout.Space(10);

            if (GUILayout.Button("SCAN ACTIVE SCENE", GUILayout.Height(40)))
            {
                AnalyzeScene();
            }

            if (analysisData == null)
            {
                EditorGUILayout.HelpBox("No scan data available. Click 'SCAN ACTIVE SCENE' to profile the scene.", MessageType.Info);
                return;
            }

            GUILayout.Space(10);
            activeTab = GUILayout.Toolbar(activeTab, tabs);
            GUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            switch (activeTab)
            {
                case 0:
                    DrawOverviewTab();
                    break;
                case 1:
                    DrawHighPolyTab();
                    break;
                case 2:
                    DrawMaterialsTab();
                    break;
                case 3:
                    DrawTexturesTab();
                    break;
                case 4:
                    DrawDuplicatesTab();
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
            GUI.contentColor = new Color(0.3f, 0.7f, 1f); // Vibrant light blue text
            GUILayout.Label("RENDERING OPTIMIZATION SYSTEM: SCENE ANALYZER", headerStyle, GUILayout.Height(30));
            GUI.contentColor = originalColor;
            GUILayout.Space(5);
        }

        private void DrawOverviewTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Global Stats Overview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Scene Name: {analysisData.sceneName}");
            EditorGUILayout.LabelField($"Scan Time: {analysisData.scanTimestamp:yyyy-MM-dd HH:mm:ss}");
            GUILayout.Space(5);
            EditorGUILayout.LabelField($"Total Renderers: {analysisData.totalRenderers:N0}");
            EditorGUILayout.LabelField($"Total Vertices: {analysisData.totalVertices:N0}");
            EditorGUILayout.LabelField($"Total Triangles: {analysisData.totalTriangles:N0}");
            EditorGUILayout.LabelField($"Total Unique Materials: {analysisData.totalMaterialsCount:N0}");
            EditorGUILayout.LabelField($"Total Unique Textures: {analysisData.totalTexturesCount:N0}");
            EditorGUILayout.LabelField($"Estimated Texture VRAM: {FormatBytes(analysisData.estimatedVRAMBytes)}");
            EditorGUILayout.LabelField($"LODGroups Count: {analysisData.lodGroupsCount:N0}");
            EditorGUILayout.LabelField($"Shadow Casters: {analysisData.shadowCasterCount:N0}");
            EditorGUILayout.LabelField($"Static Batchable Objects: {analysisData.staticBatchableCount:N0}");
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Warnings & Recommendations", EditorStyles.boldLabel);
            if (analysisData.warningLogs.Count == 0)
            {
                EditorGUILayout.HelpBox("Scene is well within target budgets! No warnings generated.", MessageType.Info);
            }
            else
            {
                foreach (string warning in analysisData.warningLogs)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }
        }

        private void DrawHighPolyTab()
        {
            EditorGUILayout.LabelField("Meshes Sorted by Triangle Count (Descending)", EditorStyles.boldLabel);
            if (analysisData.highPolyMeshes.Count == 0)
            {
                EditorGUILayout.LabelField("No mesh details found.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mesh Name", EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label("Triangle Count", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("Vertex Count", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("Submeshes", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("Has LOD", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            foreach (var mesh in analysisData.highPolyMeshes)
            {
                if (mesh.triangleCount > config.polyCountWarningThreshold / 2)
                {
                    GUI.contentColor = Color.yellow;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(mesh.meshName, GUILayout.Width(150));
                GUILayout.Label(mesh.triangleCount.ToString("N0"), GUILayout.Width(100));
                GUILayout.Label(mesh.vertexCount.ToString("N0"), GUILayout.Width(100));
                GUILayout.Label(mesh.subMeshCount.ToString(), GUILayout.Width(80));
                GUILayout.Label(mesh.hasLODGroup ? "Yes" : "No", GUILayout.Width(60));
                GUILayout.EndHorizontal();
                GUI.contentColor = Color.white;
            }
        }

        private void DrawMaterialsTab()
        {
            EditorGUILayout.LabelField("Unique Materials Overview", EditorStyles.boldLabel);
            if (analysisData.materials.Count == 0)
            {
                EditorGUILayout.LabelField("No material details found.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Material Name", EditorStyles.boldLabel, GUILayout.Width(180));
            GUILayout.Label("Shader Name", EditorStyles.boldLabel, GUILayout.Width(180));
            GUILayout.Label("SRP Compatible", EditorStyles.boldLabel, GUILayout.Width(110));
            GUILayout.Label("Instancing Enabled", EditorStyles.boldLabel, GUILayout.Width(110));
            GUILayout.EndHorizontal();

            foreach (var mat in analysisData.materials)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(mat.materialName, GUILayout.Width(180));
                GUILayout.Label(mat.shaderName, GUILayout.Width(180));
                GUILayout.Label(mat.isSRPBatcherCompatible ? "Yes" : "No", GUILayout.Width(110));
                GUILayout.Label(mat.enableInstancing ? "Yes" : "No", GUILayout.Width(110));
                GUILayout.EndHorizontal();
            }
        }

        private void DrawTexturesTab()
        {
            EditorGUILayout.LabelField($"Textures Breakdown (Total VRAM: {FormatBytes(analysisData.estimatedVRAMBytes)})", EditorStyles.boldLabel);
            if (analysisData.textures.Count == 0)
            {
                EditorGUILayout.LabelField("No texture details found.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Texture Name", EditorStyles.boldLabel, GUILayout.Width(180));
            GUILayout.Label("Dimensions", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("VRAM Size", EditorStyles.boldLabel, GUILayout.Width(90));
            GUILayout.Label("Format", EditorStyles.boldLabel, GUILayout.Width(120));
            GUILayout.Label("Streaming Mips", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            foreach (var tex in analysisData.textures)
            {
                if (tex.width > config.textureSizeWarningThreshold)
                {
                    GUI.contentColor = Color.yellow;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(tex.textureName, GUILayout.Width(180));
                GUILayout.Label($"{tex.width}x{tex.height}", GUILayout.Width(100));
                GUILayout.Label(FormatBytes(tex.estimatedBytes), GUILayout.Width(90));
                GUILayout.Label(tex.format, GUILayout.Width(120));
                GUILayout.Label(tex.streamingEnabled ? "Yes" : "No", GUILayout.Width(100));
                GUILayout.EndHorizontal();
                GUI.contentColor = Color.white;
            }
        }

        private void DrawDuplicatesTab()
        {
            EditorGUILayout.LabelField("Duplicate Materials (Separate instances with identical names)", EditorStyles.boldLabel);
            if (analysisData.duplicateMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox("No duplicate materials found. Great job!", MessageType.Info);
                return;
            }

            foreach (var group in analysisData.duplicateMaterials)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Material Group: {group.baseMaterialName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Duplicate Instance Counts: {group.duplicateMaterialPaths.Count}");
                GUILayout.Label("Resource Paths:");
                foreach (string path in group.duplicateMaterialPaths)
                {
                    GUILayout.Label("  - " + path);
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }
        }

        private void AnalyzeScene()
        {
            EditorUtility.DisplayProgressBar("Scanning Scene", "Traversing renderers and collecting metrics...", 0.3f);
            try
            {
                analysisData = SceneAnalyzer.AnalyzeActiveScene(config);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
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

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1073741824) return $"{(double)bytes / 1073741824:F2} GB";
            if (bytes >= 1048576) return $"{(double)bytes / 1048576:F2} MB";
            if (bytes >= 1024) return $"{(double)bytes / 1024:F2} KB";
            return $"{bytes} Bytes";
        }
    }
}
