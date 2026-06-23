using UnityEngine;
using UnityEditor;
using AAAOptimizer.Core;
using AAAOptimizer.Streaming;

namespace AAAOptimizer.Editor
{
    public class ChunkStreamingWindow : EditorWindow
    {
        private AAAOptimizerConfig config;
        private bool drawGridInScene = true;
        private int previewGridRange = 5;

        public static void ShowWindow()
        {
            ChunkStreamingWindow window = GetWindow<ChunkStreamingWindow>("World Chunk Streaming");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
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

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("World Partition Settings", EditorStyles.boldLabel);
            config = (AAAOptimizerConfig)EditorGUILayout.ObjectField("Optimizer Config", config, typeof(AAAOptimizerConfig), false);
            EditorGUILayout.EndVertical();

            if (config == null)
            {
                EditorGUILayout.HelpBox("Please assign or create a AAAOptimizerConfig ScriptableObject.", MessageType.Warning);
                return;
            }

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Grid Designer & Visualizer", EditorStyles.boldLabel);
            config.cellSize = EditorGUILayout.FloatField("Cell Coordinate Size", config.cellSize);
            config.loadRadius = EditorGUILayout.FloatField("Loading Radius", config.loadRadius);
            config.unloadRadiusMargin = EditorGUILayout.FloatField("Unload Margin (Hysteresis)", config.unloadRadiusMargin);
            config.predictiveLoading = EditorGUILayout.Toggle("Predictive Preloading", config.predictiveLoading);
            
            GUILayout.Space(5);
            drawGridInScene = EditorGUILayout.Toggle("Draw Grid in Scene View", drawGridInScene);
            previewGridRange = EditorGUILayout.IntSlider("Preview Grid Range", previewGridRange, 2, 20);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            EditorGUILayout.HelpBox("World Streaming expects scene files to be named using the 'Chunk_X_Z' format (e.g. Chunk_0_0, Chunk_0_1, Chunk_1_0) and added to your Editor Build Settings.", MessageType.Info);
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
            GUILayout.Label("WORLD CHUNK STREAMING DESIGNER", headerStyle, GUILayout.Height(25));
            GUI.contentColor = originalColor;
            GUILayout.Space(10);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!drawGridInScene || config == null || config.cellSize <= 5f) return;

            Handles.color = new Color(0f, 0.8f, 0f, 0.1f);
            
            Vector3 cameraPos = SceneView.lastActiveSceneView.camera.transform.position;
            int cellX = Mathf.FloorToInt(cameraPos.x / config.cellSize);
            int cellZ = Mathf.FloorToInt(cameraPos.z / config.cellSize);

            float size = config.cellSize;

            // Draw grid lines
            for (int x = cellX - previewGridRange; x <= cellX + previewGridRange; x++)
            {
                for (int z = cellZ - previewGridRange; z <= cellZ + previewGridRange; z++)
                {
                    Vector3 center = new Vector3((x + 0.5f) * size, 0, (z + 0.5f) * size);
                    Vector3 sizeVec = new Vector3(size, 0, size);

                    // Draw grid wire frame on the flat plane
                    Handles.color = new Color(0f, 0.8f, 0f, 0.2f);
                    Handles.DrawWireCube(center, sizeVec);

                    // Label cell coords
                    Handles.Label(center, $"Cell {x}, {z}");
                }
            }

            // Draw active camera cell
            Vector3 activeCenter = new Vector3((cellX + 0.5f) * size, 0, (cellZ + 0.5f) * size);
            Handles.color = Color.green;
            Handles.DrawWireCube(activeCenter, new Vector3(size, 1f, size));
        }
    }
}
