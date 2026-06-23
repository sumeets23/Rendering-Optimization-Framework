# Skill: Runtime Streaming

## 1. Purpose
This skill covers grid-based chunk division, asynchronous scene loading/unloading, addressable asset streaming, and predictive camera velocity-based preloading.

## 2. Technical Overview
Large environments cannot reside entirely in memory. Dynamic streaming loads nearby chunks while unloading distant ones based on camera coordinate checks, keeping CPU usage and memory footprint steady.

## 3. Unity APIs
- `UnityEngine.SceneManagement.SceneManager.LoadSceneAsync`
- `UnityEngine.AddressableAssets.Addressables.LoadAssetAsync`
- `UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle`

## 4. Rendering Concepts
- **Grid Partitioning:** Splitting coordinates into uniform columns/rows.
- **Additive Loading:** Merging scene hierarchy trees dynamically at runtime.
- **Predictive Preloading:** Initiating asset loads in the direction of player movement before the player enters the grid cell.

## 5. Optimization Strategies
- Divide the world coordinates into a 2D grid.
- Track player position and check active grid cells.
- Maintain a loading queue to process requests incrementally, preventing CPU stuttering.
- Pre-cache Addressable keys for instant lookup.

## 6. Runtime Constraints
- Loading a scene or instantiating large prefabs creates main-thread CPU spikes. Distribute object instantiation across frames using a queue.

## 7. Memory Constraints
- Clean up memory after unloading by calling `Resources.UnloadUnusedAssets` and garbage collection.

## 8. VR Constraints
- Any CPU stuttering (>1ms spike) in VR can cause simulation sickness. Loading must be completely asynchronous and decoupled from the main thread.

## 9. HDRP Notes
- Ensure lightmap assets and reflection probes associated with the chunks are loaded and bound correctly.

## 10. URP Notes
- Keep asset bundle sizes small (under 10MB per chunk) to ensure rapid loading on cellular networks or mobile devices.

## 11. Best Practices
- Implement a hysteresis buffer around cell boundaries (e.g. 5-10 meters overlap) to prevent load/unload spam.
- Hide loading chunks using environmental occluders, fog, or distance-based clipping.

## 12. Common Pitfalls
- Unloading a chunk and loading it back in within a few frames. Use a cooldown timer on unloading operations.

## 13. Performance Targets
- Frame rate variance during streaming: < 3ms.
- Maximum active loaded grid size: 3x3 cells.
