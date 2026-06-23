using UnityEngine;
using UnityEditor;
using AAAOptimizer.Core;

namespace AAAOptimizer.Editor
{
    public class DrawCallOptimizerWindow : EditorWindow
    {
        private GameObject rootToCombine;

        public static void ShowWindow()
        {
            DrawCallOptimizerWindow window = GetWindow<DrawCallOptimizerWindow>("Draw Call Optimizer");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("GPU Instancing Automation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This operation scans the active scene renderers, identifies unique materials, and automatically enables GPU Instancing on them.", MessageType.Info);
            if (GUILayout.Button("Enable Instancing on All Materials", GUILayout.Height(30)))
            {
                DrawCallOptimizer.EnableGPUInstancingOnAllMaterials();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Static Mesh Combiner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select a root GameObject containing static children. This tool will group child meshes by material, combine them into single meshes to save draw calls, and disable the original renderers.", MessageType.Info);
            
            rootToCombine = (GameObject)EditorGUILayout.ObjectField("Root GameObject", rootToCombine, typeof(GameObject), true);

            GUILayout.Space(5);
            if (GUILayout.Button("Combine Child Meshes", GUILayout.Height(30)))
            {
                if (rootToCombine == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign a Root GameObject first.", "OK");
                }
                else
                {
                    CombineMeshes();
                }
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
            GUILayout.Label("DRAW CALL OPTIMIZER", headerStyle, GUILayout.Height(25));
            GUI.contentColor = originalColor;
            GUILayout.Space(10);
        }

        private void CombineMeshes()
        {
            EditorUtility.DisplayProgressBar("Combining Meshes", "Analyzing materials and grouping vertex buffers...", 0.5f);
            try
            {
                var combined = DrawCallOptimizer.CombineStaticMeshesByMaterial(rootToCombine);
                EditorUtility.DisplayDialog("Success", $"Combined meshes of {rootToCombine.name} into {combined.Count} material groups.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
