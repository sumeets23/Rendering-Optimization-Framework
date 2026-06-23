using UnityEngine;
using UnityEditor;
using AAAOptimizer.Core;

namespace AAAOptimizer.Editor
{
    public class TextureStreamingWindow : EditorWindow
    {
        private AAAOptimizerConfig config;

        public static void ShowWindow()
        {
            TextureStreamingWindow window = GetWindow<TextureStreamingWindow>("Texture Streaming Manager");
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
        }

        private void OnGUI()
        {
            DrawHeader();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Global Configuration", EditorStyles.boldLabel);
            config = (AAAOptimizerConfig)EditorGUILayout.ObjectField("Optimizer Config", config, typeof(AAAOptimizerConfig), false);
            EditorGUILayout.EndVertical();

            if (config == null)
            {
                EditorGUILayout.HelpBox("Please assign or create a AAAOptimizerConfig ScriptableObject.", MessageType.Warning);
                return;
            }

            GUILayout.Space(10);

            // Mipmap settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Built-in Mipmap Streaming Stats", EditorStyles.boldLabel);
            bool systemActive = QualitySettings.streamingMipmapsActive;
            float budget = QualitySettings.streamingMipmapsMemoryBudget;

            EditorGUILayout.LabelField($"System Active (Quality Settings): {systemActive}");
            EditorGUILayout.LabelField($"VRAM Budget Allocation: {budget} MB");
            
            GUILayout.Space(5);
            if (GUILayout.Button("Synchronize with Config Budgets"))
            {
                QualitySettings.streamingMipmapsActive = config.enableTextureStreaming;
                QualitySettings.streamingMipmapsMemoryBudget = (int)config.textureMemoryBudgetMB;
                EditorUtility.DisplayDialog("Sync Complete", "Synchronized project quality settings with optimizer config.", "OK");
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Batch setup
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Batch Asset Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Unity's texture streaming only affects texture assets that have 'Streaming Mipmaps' enabled in their import settings. Press the button below to scan all project textures and enable it in batch.", MessageType.Info);
            
            if (GUILayout.Button("Enable Streaming on All Textures", GUILayout.Height(35)))
            {
                EnableStreamingOnAllTextures();
            }
            EditorGUILayout.EndVertical();
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
            GUILayout.Label("TEXTURE STREAMING CONTROLLER", headerStyle, GUILayout.Height(25));
            GUI.contentColor = originalColor;
            GUILayout.Space(10);
        }

        private void EnableStreamingOnAllTextures()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D");
            int updatedCount = 0;

            EditorUtility.DisplayProgressBar("Configuring Texture Assets", "Scanning project directory...", 0f);
            try
            {
                for (int i = 0; i < textureGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                    float progress = (float)i / textureGuids.Length;
                    EditorUtility.DisplayProgressBar("Configuring Texture Assets", $"Setting up texture: {path}...", progress);

                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && !importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = true;
                        importer.streamingMipmaps = true;
                        importer.SaveAndReimport();
                        updatedCount++;
                    }
                    else if (importer != null && !importer.streamingMipmaps)
                    {
                        importer.streamingMipmaps = true;
                        importer.SaveAndReimport();
                        updatedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("Setup Complete", $"Enabled Streaming Mipmaps on {updatedCount} texture assets.", "OK");
        }
    }
}
