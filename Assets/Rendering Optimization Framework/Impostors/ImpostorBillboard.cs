using UnityEngine;

namespace AAAOptimizer.Impostors
{
    [ExecuteAlways]
    public class ImpostorBillboard : MonoBehaviour
    {
        public enum BillboardRotationMode
        {
            PreserveBakedOrientation,
            FaceCameraYaw
        }

        [Header("Impostor Settings")]
        public Renderer impostorRenderer;
        public int directions = 8;
        public int columns = 3;
        public int rows = 3;
        public bool smoothTransition = true;
        public BillboardRotationMode rotationMode = BillboardRotationMode.FaceCameraYaw;

        [Header("Swap Settings")]
        public float transitionDistance = 150.0f;
        public float fadeZoneLength = 5.0f;
        public GameObject originalTarget;
        public string originalTargetName;
        public bool disableOriginalRenderersOnly = true;
        public float checkInterval = 0.3f;
        public Transform quadTransform;

        private Transform cameraTransform;
        private MaterialPropertyBlock propertyBlock;
        private Renderer[] originalTargetRenderers;
        private float nextCheckTime;
        private bool isShowingImpostor;
        private Quaternion baseRotation;

        private float currentTransitionAlpha = 1.0f;

        private void EnsureReferences()
        {
            ResolveOriginalTarget();

            if (impostorRenderer == null)
                impostorRenderer = GetComponentInChildren<Renderer>(true);

            if (quadTransform == null && impostorRenderer != null)
                quadTransform = impostorRenderer.transform;

            if (originalTarget != null && originalTargetRenderers == null)
                originalTargetRenderers = originalTarget.GetComponentsInChildren<Renderer>(true);
        }

        private void ResolveOriginalTarget()
        {
            if (originalTarget != null) return;

            string lookupName = !string.IsNullOrEmpty(originalTargetName)
                ? originalTargetName
                : name.Replace("_Impostor", string.Empty);

            if (string.IsNullOrEmpty(lookupName)) return;

            GameObject found = GameObject.Find(lookupName);
            if (found != null && found != gameObject && !found.transform.IsChildOf(transform))
            {
                originalTarget = found;
                originalTargetRenderers = null;   // force re-cache
            }
        }

        private Transform GetActiveCameraTransform()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying &&
                UnityEditor.SceneView.lastActiveSceneView?.camera != null)
            {
                return UnityEditor.SceneView.lastActiveSceneView.camera.transform;
            }
#endif
            if (Camera.main != null) return Camera.main.transform;
            return cameraTransform;
        }

        private Vector2 GetAtlasOffset(int frameIndex)
        {
            int safeColumns = Mathf.Max(1, columns);
            int safeRows = Mathf.Max(1, rows);
            int col = frameIndex % safeColumns;
            int row = frameIndex / safeColumns;
            int atlasRow = (safeRows - 1) - row;
            return new Vector2((float)col / safeColumns, (float)atlasRow / safeRows);
        }

        private void ApplyFrames(int frame1, int frame2, float blend, float transitionAlpha = 1.0f)
        {
            EnsureReferences();
            if (impostorRenderer == null) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            int safeColumns = Mathf.Max(1, columns);
            int safeRows = Mathf.Max(1, rows);
            float scaleX = 1.0f / safeColumns;
            float scaleY = 1.0f / safeRows;
            Vector2 offset1 = GetAtlasOffset(frame1);
            Vector2 offset2 = GetAtlasOffset(frame2);

            impostorRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetVector("_AtlasScale", new Vector4(scaleX, scaleY, 0, 0));
            propertyBlock.SetVector("_AtlasOffset", new Vector4(offset1.x, offset1.y, 0, 0));
            propertyBlock.SetVector("_NextAtlasOffset", new Vector4(offset2.x, offset2.y, 0, 0));
            propertyBlock.SetFloat("_FrameBlend", blend);
            propertyBlock.SetFloat("_TransitionAlpha", transitionAlpha);

            if (RenderSettings.sun != null)
            {
                Light sun = RenderSettings.sun;
                propertyBlock.SetVector("_ImpostorSunDir", -sun.transform.forward);
                propertyBlock.SetColor("_ImpostorLightColor", sun.color * sun.intensity);
                propertyBlock.SetFloat("_ImpostorLightStrength", 0.75f);
            }
            else
            {
                propertyBlock.SetVector("_ImpostorSunDir", Vector3.up);
                propertyBlock.SetColor("_ImpostorLightColor", Color.white);
                propertyBlock.SetFloat("_ImpostorLightStrength", 0.0f);
            }

            Color ambient = RenderSettings.ambientLight;
            if (ambient.maxColorComponent <= 0.001f)
                ambient = new Color(0.45f, 0.45f, 0.45f, 1.0f);
            propertyBlock.SetColor("_ImpostorAmbientColor", ambient);
            propertyBlock.SetFloat("_ImpostorAmbientStrength", 0.55f);

            var st = new Vector4(scaleX, scaleY, offset1.x, offset1.y);
            propertyBlock.SetVector("_BaseMap_ST", st);
            propertyBlock.SetVector("_MainTex_ST", st);
            propertyBlock.SetVector("_BaseColorMap_ST", st);
            propertyBlock.SetVector("_UnlitColorMap_ST", st);
            propertyBlock.SetVector("_BumpMap_ST", st);
            propertyBlock.SetVector("_NormalMap_ST", st);
            propertyBlock.SetVector("_HeightMap_ST", st);
            propertyBlock.SetVector("_ParallaxMap_ST", st);

            impostorRenderer.SetPropertyBlock(propertyBlock);
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            cameraTransform = GetActiveCameraTransform();
            baseRotation = transform.rotation;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            if (!Application.isPlaying)
            {
                EnsureReferences();
                if (impostorRenderer != null) impostorRenderer.enabled = true;
                ApplyFrames(0, 0, 0f, 1.0f);
            }
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                baseRotation = transform.rotation;
                EnsureReferences();
                if (impostorRenderer != null) impostorRenderer.enabled = true;
                ApplyFrames(0, 0, 0f, 1.0f);
            }
        }
#else
        private void OnEnable()
        {
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }
#endif

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                   UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                cameraTransform = GetActiveCameraTransform();
                baseRotation = transform.rotation;
                EnsureReferences();
                if (impostorRenderer != null) impostorRenderer.enabled = true;
                ApplyFrames(0, 0, 0f, 1.0f);
                return;
            }
#endif
            cameraTransform = GetActiveCameraTransform();
            baseRotation = transform.rotation;

            EnsureReferences();
            ApplyFrames(0, 0, 0f, 1.0f);
            EvaluateTransition(true);
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                cameraTransform = GetActiveCameraTransform();
                baseRotation = transform.rotation;
                EnsureReferences();
                if (impostorRenderer != null) impostorRenderer.enabled = true;

                if (rotationMode == BillboardRotationMode.FaceCameraYaw)
                    FaceCamera();

                UpdateUVFrame();
                return;
            }
#endif
            cameraTransform = GetActiveCameraTransform();
            if (cameraTransform == null) return;

            if (rotationMode == BillboardRotationMode.FaceCameraYaw)
                FaceCamera();

            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + Mathf.Max(0.02f, checkInterval);
                EvaluateTransition(false);
            }

            if (isShowingImpostor)
                UpdateUVFrame();
        }

        private void FaceCamera()
        {
            if (cameraTransform == null) return;
            Transform rotTarget = quadTransform != null ? quadTransform : transform;
            Vector3 toCamera = cameraTransform.position - rotTarget.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.001f)
                rotTarget.rotation = Quaternion.LookRotation(-toCamera);
        }

        private void EvaluateTransition(bool forceUpdate)
        {
            if (cameraTransform == null) return;

            if (originalTarget == null) ResolveOriginalTarget();

            if (originalTarget == null)
            {
                if (!isShowingImpostor || forceUpdate)
                {
                    isShowingImpostor = true;
                    EnsureReferences();
                    if (impostorRenderer != null) impostorRenderer.enabled = true;
                }
                return;
            }

            float distance = Vector3.Distance(transform.position, cameraTransform.position);
            float fadeStartDist = Mathf.Max(0f, transitionDistance - fadeZoneLength);
            
            bool shouldShowImpostor = distance >= fadeStartDist;
            bool shouldShowMesh = distance < transitionDistance;
            
            float newAlpha = 0f;
            if (distance >= transitionDistance) newAlpha = 1.0f;
            else if (distance >= fadeStartDist && fadeZoneLength > 0f) newAlpha = (distance - fadeStartDist) / fadeZoneLength;
            
            bool alphaChanged = Mathf.Abs(newAlpha - currentTransitionAlpha) > 0.01f;
            currentTransitionAlpha = newAlpha;

            if (shouldShowImpostor != isShowingImpostor || forceUpdate || alphaChanged)
            {
                isShowingImpostor = shouldShowImpostor;
                EnsureReferences();

                if (impostorRenderer != null)
                {
                    impostorRenderer.enabled = isShowingImpostor;
                }

                if (originalTargetRenderers != null && originalTargetRenderers.Length > 0)
                {
                    foreach (var r in originalTargetRenderers)
                    {
                        if (r != null && r.enabled != shouldShowMesh) r.enabled = shouldShowMesh;
                    }
                }
                else if (!disableOriginalRenderersOnly)
                {
                    if (originalTarget.activeSelf != shouldShowMesh)
                        originalTarget.SetActive(shouldShowMesh);
                }

                if (shouldShowImpostor || alphaChanged) UpdateUVFrame();
            }
        }

        private void UpdateUVFrame()
        {
            if (impostorRenderer == null) return;

            if (rotationMode == BillboardRotationMode.PreserveBakedOrientation || cameraTransform == null)
            {
                ApplyFrames(0, 0, 0f, currentTransitionAlpha);
                return;
            }

            int safeDir = Mathf.Max(1, directions);

            Vector3 centerPos = quadTransform != null ? quadTransform.position : transform.position;
            Vector3 toCamera = cameraTransform.position - centerPos;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude <= 0.001f) return;

            Vector3 localDir = Quaternion.Inverse(transform.rotation) * toCamera.normalized;
            float angle = Mathf.Atan2(localDir.x, localDir.z);
            if (angle < 0f) angle += Mathf.PI * 2.0f;

            float exactFrame = (angle / (Mathf.PI * 2.0f)) * safeDir;
            int frameIndex1 = Mathf.FloorToInt(exactFrame);
            int frameIndex2 = frameIndex1 + 1;

            float blend = exactFrame - frameIndex1;

            frameIndex1 %= safeDir;
            frameIndex2 %= safeDir;
            if (frameIndex1 < 0) frameIndex1 += safeDir;
            if (frameIndex2 < 0) frameIndex2 += safeDir;

            if (!smoothTransition)
            {
                blend = Mathf.Clamp01((blend - 0.5f) * 2f + 0.5f);
                blend = Mathf.SmoothStep(0f, 1f, blend);
            }

            ApplyFrames(frameIndex1, frameIndex2, blend, currentTransitionAlpha);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, transitionDistance);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (transitionDistance + 0.5f),
                $"Impostor @ {transitionDistance}m | {rotationMode}");
#endif
        }
    }
}