using UnityEngine;
using UnityEditor;
using AAAOptimizer.Core;
using AAAOptimizer.Impostors;

namespace AAAOptimizer.Editor
{
    public class ImpostorBakerWindow : EditorWindow
    {
        private AAAOptimizerConfig config;
        private GameObject targetGo;
        private int directions = 8;
        private int atlasResolution = 2048;
        private int frameResolution = 256;
        private float swapDistance = 150.0f;

        // ─────────────────────────────────────────────────────────────────────
        // Window entry point
        // ─────────────────────────────────────────────────────────────────────

        public static void ShowWindow()
        {
            ImpostorBakerWindow window = GetWindow<ImpostorBakerWindow>("Impostor Baker");
            window.minSize = new Vector2(420, 380);
            window.Show();
        }

        private void OnEnable()
        {
            string[] guids = AssetDatabase.FindAssets("t:AAAOptimizerConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<AAAOptimizerConfig>(path);
                if (config != null) LoadFromConfig();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Config helper
        // ─────────────────────────────────────────────────────────────────────

        private void LoadFromConfig()
        {
            if (config == null) return;
            directions = config.captureDirections;
            atlasResolution = config.impostorTextureSize;
            frameResolution = config.singleFrameResolution;
            swapDistance = config.impostorSwapDistance;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GUI
        // ─────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawHeader();

            // ── Config block ──────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Baking Configuration", EditorStyles.boldLabel);
            config = (AAAOptimizerConfig)EditorGUILayout.ObjectField(
                "Optimizer Config", config, typeof(AAAOptimizerConfig), false);

            if (config != null && GUILayout.Button("Load Config Presets"))
                LoadFromConfig();

            EditorGUILayout.EndVertical();
            GUILayout.Space(8);

            // ── Target block ──────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Capture Target", EditorStyles.boldLabel);
            targetGo = (GameObject)EditorGUILayout.ObjectField(
                "Target GameObject", targetGo, typeof(GameObject), true);

            // Validation feedback below the object field
            if (targetGo != null)
            {
                Renderer[] renderers = targetGo.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    EditorGUILayout.HelpBox(
                        "The selected GameObject has no Renderers. Nothing to bake.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Found {renderers.Length} renderer(s). Ready to bake.",
                        MessageType.Info);
                }
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(8);

            // ── Settings block ────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Capture Settings", EditorStyles.boldLabel);

            directions = EditorGUILayout.IntSlider("Angle Directions", directions, 1, 32);

            frameResolution = EditorGUILayout.IntPopup(
                "Frame Resolution", frameResolution,
                new string[] { "64 × 64", "128 × 128", "256 × 256", "512 × 512" },
                new int[] { 64, 128, 256, 512 });

            atlasResolution = EditorGUILayout.IntPopup(
                "Atlas Size", atlasResolution,
                new string[] { "1024 × 1024", "2048 × 2048", "4096 × 4096" },
                new int[] { 1024, 2048, 4096 });

            swapDistance = EditorGUILayout.FloatField("Swap Distance (m)", swapDistance);

            // Warn if the requested frame resolution won't fit in the atlas
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(directions)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)directions / cols));
            int maxFrameForAtlas = Mathf.Min(atlasResolution / cols, atlasResolution / rows);
            if (frameResolution > maxFrameForAtlas)
            {
                EditorGUILayout.HelpBox(
                    $"Frame resolution ({frameResolution}px) will be clamped to {maxFrameForAtlas}px " +
                    $"to fit {cols}×{rows} frames into the {atlasResolution}px atlas.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Atlas layout: {cols} columns × {rows} rows = {cols * rows} frames " +
                    $"({frameResolution}px each).",
                    MessageType.None);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(12);

            // ── Bake button ───────────────────────────────────────────────────
            GUI.enabled = targetGo != null;
            if (GUILayout.Button("BAKE IMPOSTOR PREFAB", GUILayout.Height(40)))
                ExecuteBake();
            GUI.enabled = true;

            if (targetGo == null)
            {
                EditorGUILayout.HelpBox("Assign a Target GameObject above to enable baking.",
                    MessageType.Info);
            }
        }

        private void DrawHeader()
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter
            };
            Color prev = GUI.contentColor;
            GUI.contentColor = new Color(0.3f, 0.75f, 1f);
            GUILayout.Label("IMPOSTOR BILLBOARD BAKER", style, GUILayout.Height(28));
            GUI.contentColor = prev;
            GUILayout.Space(8);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Bake execution
        // ─────────────────────────────────────────────────────────────────────

        private void ExecuteBake()
        {
            if (targetGo == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Target GameObject.", "OK");
                return;
            }

            Renderer[] renderers = targetGo.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                EditorUtility.DisplayDialog("Error",
                    "The selected GameObject has no Renderers — nothing to bake.", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Baking Impostor",
                $"Capturing camera views for '{targetGo.name}'…", 0.1f);
            try
            {
                ImpostorBaker.Bake(targetGo, directions, atlasResolution, frameResolution, swapDistance);
                EditorUtility.DisplayDialog("Success",
                    $"Impostor baked successfully for '{targetGo.name}'.\n\n" +
                    "The prefab has been saved to Assets/AAAOptimizer/Data/Impostors " +
                    "and an instance has been placed in the scene.", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ImpostorBaker] Bake failed: {ex}");
                EditorUtility.DisplayDialog("Bake Failed",
                    $"An error occurred during baking:\n\n{ex.Message}\n\n" +
                    "Check the Console for the full stack trace.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
