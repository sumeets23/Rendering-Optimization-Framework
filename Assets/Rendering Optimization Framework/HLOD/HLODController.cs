using System.Collections.Generic;
using UnityEngine;

namespace AAAOptimizer.HLOD
{
    [ExecuteAlways]
    public class HLODController : MonoBehaviour
    {
        [Header("Proxy Geometry")]
        public Renderer proxyRenderer;
        
        [Header("Transition Settings")]
        public float transitionDistance = 100.0f;
        public float checkInterval = 0.5f;

        [Header("Clustered Child Objects")]
        public List<GameObject> highDetailChildren = new List<GameObject>();

        private Transform cameraTransform;
        private float nextCheckTime;
        private bool isShowingProxy = false;

        private void Start()
        {
            UpdateCameraTransform();
            EvaluateTransition(true);
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            UpdateCameraTransform();
            EvaluateTransition(true);
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            UpdateCameraTransform();
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Restore original state in editor when script is disabled to avoid saving dirty active states
                if (proxyRenderer != null)
                {
                    proxyRenderer.enabled = false;
                }
                foreach (var child in highDetailChildren)
                {
                    if (child != null)
                    {
                        child.SetActive(true);
                    }
                }
            }
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UpdateCameraTransform();
                EvaluateTransition(false);
                return;
            }
#endif

            if (cameraTransform == null) return;

            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInterval;
                EvaluateTransition(false);
            }
        }

        private void UpdateCameraTransform()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (UnityEditor.SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.camera != null)
                {
                    cameraTransform = UnityEditor.SceneView.lastActiveSceneView.camera.transform;
                }
                return;
            }
#endif

            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void EvaluateTransition(bool forceUpdate)
        {
            if (cameraTransform == null) return;

            float distance = Vector3.Distance(transform.position, cameraTransform.position);
            bool shouldShowProxy = distance >= transitionDistance;

            if (shouldShowProxy != isShowingProxy || forceUpdate)
            {
                isShowingProxy = shouldShowProxy;

                // Toggle visibility
                if (proxyRenderer != null)
                {
                    proxyRenderer.enabled = isShowingProxy;
                }

                foreach (var child in highDetailChildren)
                {
                    if (child != null)
                    {
                        // To save draw setup, disable active GameObjects entirely
                        child.SetActive(!isShowingProxy);
                    }
                }
            }
        }

        // Draw Gizmos in Editor to show cluster bounds and swap range
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 5.0f);
            
            // Draw transition sphere boundary wireframe
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, transitionDistance);
        }
    }
}
