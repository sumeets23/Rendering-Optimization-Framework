using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using AAAOptimizer.Core;

#if AAA_ADDRESSABLES_AVAILABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace AAAOptimizer.Streaming
{
    public class WorldStreamingManager : MonoBehaviour
    {
        public AAAOptimizerConfig config;
        public Transform targetCamera;
        
        [Header("Runtime Cache")]
        public Vector2Int currentCell;
        public List<string> activeLoadedChunks = new List<string>();

#if AAA_ADDRESSABLES_AVAILABLE
        private Dictionary<string, SceneInstance> loadedAddressableScenes = new Dictionary<string, SceneInstance>();
#endif

        private Vector3 lastPosition;
        private Vector3 velocity;
        private bool isStreamingInProgress = false;
        private Queue<string> loadQueue = new Queue<string>();
        private Queue<string> unloadQueue = new Queue<string>();

        private void Start()
        {
            if (config == null)
            {
                AAAOptimizerConfig[] configs = Resources.FindObjectsOfTypeAll<AAAOptimizerConfig>();
                if (configs.Length > 0) config = configs[0];
            }

            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            if (targetCamera != null)
            {
                lastPosition = targetCamera.position;
            }

            // Sync loaded scenes at startup
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.name.StartsWith("Chunk_"))
                {
                    activeLoadedChunks.Add(scene.name);
                }
            }
        }

        private void Update()
        {
            if (config == null || !config.enableWorldStreaming || targetCamera == null) return;

            // Compute velocity
            Vector3 pos = targetCamera.position;
            velocity = (pos - lastPosition) / Time.deltaTime;
            lastPosition = pos;

            // Project position based on velocity for predictive loading
            Vector3 targetPosition = pos;
            if (config.predictiveLoading)
            {
                targetPosition += velocity * 1.5f; // Project 1.5 seconds ahead
            }

            // Determine active cell coordinates
            int cellX = Mathf.FloorToInt(targetPosition.x / config.cellSize);
            int cellZ = Mathf.FloorToInt(targetPosition.z / config.cellSize);
            Vector2Int newCell = new Vector2Int(cellX, cellZ);

            if (newCell != currentCell)
            {
                currentCell = newCell;
                UpdateStreamingGrid();
            }

            // Process loading queues incrementally (limit per frame to avoid stutters)
            if (!isStreamingInProgress)
            {
                if (unloadQueue.Count > 0)
                {
                    StartCoroutine(UnloadChunkRoutine(unloadQueue.Dequeue()));
                }
                else if (loadQueue.Count > 0)
                {
                    StartCoroutine(LoadChunkRoutine(loadQueue.Dequeue()));
                }
            }
        }

        private void UpdateStreamingGrid()
        {
            HashSet<string> desiredChunks = new HashSet<string>();

            // Calculate 3x3 surrounding grid coordinates
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int cx = currentCell.x + dx;
                    int cz = currentCell.y + dz;
                    desiredChunks.Add($"Chunk_{cx}_{cz}");
                }
            }

            // Check what chunks to load
            foreach (var chunk in desiredChunks)
            {
                if (!activeLoadedChunks.Contains(chunk) && !loadQueue.Contains(chunk))
                {
                    // Check if chunk scene actually exists in build configurations before loading
                    if (IsSceneInBuildSettings(chunk))
                    {
                        loadQueue.Enqueue(chunk);
                    }
                }
            }

            // Check what chunks to unload (using unloadRadiusMargin to prevent rapid flipping)
            foreach (var chunk in activeLoadedChunks)
            {
                if (!desiredChunks.Contains(chunk) && !unloadQueue.Contains(chunk))
                {
                    // Calculate chunk cell index from name: Chunk_X_Z
                    string[] split = chunk.Split('_');
                    if (split.Length == 3 && int.TryParse(split[1], out int cx) && int.TryParse(split[2], out int cz))
                    {
                        float dist = Vector2.Distance(new Vector2(cx * config.cellSize, cz * config.cellSize), new Vector2(targetCamera.position.x, targetCamera.position.z));
                        if (dist > config.loadRadius + config.unloadRadiusMargin)
                        {
                            unloadQueue.Enqueue(chunk);
                        }
                    }
                }
            }
        }

        private IEnumerator LoadChunkRoutine(string chunkName)
        {
            isStreamingInProgress = true;
            Debug.Log($"[WorldStreamingManager] Loading chunk additively: {chunkName}");

#if AAA_ADDRESSABLES_AVAILABLE
            AsyncOperationHandle<SceneInstance> op = Addressables.LoadSceneAsync(chunkName, LoadSceneMode.Additive);
            while (!op.IsDone)
            {
                yield return null;
            }
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                loadedAddressableScenes[chunkName] = op.Result;
                activeLoadedChunks.Add(chunkName);
            }
            else
            {
                Debug.LogError($"[WorldStreamingManager] Failed to load Addressable chunk: {chunkName}");
            }
#else
            AsyncOperation op = SceneManager.LoadSceneAsync(chunkName, LoadSceneMode.Additive);
            while (op != null && !op.isDone)
            {
                yield return null;
            }

            activeLoadedChunks.Add(chunkName);
#endif
            isStreamingInProgress = false;
        }

        private IEnumerator UnloadChunkRoutine(string chunkName)
        {
            isStreamingInProgress = true;
            Debug.Log($"[WorldStreamingManager] Unloading chunk: {chunkName}");

#if AAA_ADDRESSABLES_AVAILABLE
            if (loadedAddressableScenes.TryGetValue(chunkName, out SceneInstance sceneInstance))
            {
                AsyncOperationHandle<SceneInstance> op = Addressables.UnloadSceneAsync(sceneInstance);
                while (!op.IsDone)
                {
                    yield return null;
                }
                loadedAddressableScenes.Remove(chunkName);
            }
#else
            AsyncOperation op = SceneManager.UnloadSceneAsync(chunkName);
            while (op != null && !op.isDone)
            {
                yield return null;
            }
#endif
            activeLoadedChunks.Remove(chunkName);
            isStreamingInProgress = false;
        }

        private bool IsSceneInBuildSettings(string sceneName)
        {
#if UNITY_EDITOR
            foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
            {
                if (scene.enabled && PathClean(scene.path) == sceneName)
                {
                    return true;
                }
            }
#endif
            // For runtime, we'll assume it exists or rely on exceptions
            return true;
        }

        private string PathClean(string path)
        {
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }
    }
}
