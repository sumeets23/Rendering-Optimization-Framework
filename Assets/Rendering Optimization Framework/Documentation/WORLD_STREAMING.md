# World Streaming & Partition System

The World Streaming System divides large environments into coordinate-based grid cells. As the camera moves through the environment, cells are loaded and unloaded additively, maintaining a stable memory footprint and CPU frame cycle.

---

## Spatial Grid Partitioning

The world space is divided into cells of size $S \times S$ (along the X and Z axes).

```
+-----------+-----------+-----------+
|  Cell 0,2 |  Cell 1,2 |  Cell 2,2 |
+-----------+-----------+-----------+
|  Cell 0,1 |  Player   |  Cell 2,1 |
|           |  (1,1)    |           |
+-----------+-----------+-----------+
|  Cell 0,0 |  Cell 1,0 |  Cell 2,0 |
+-----------+-----------+-----------+
```

As the player moves, a 3x3 array of cells centered on the player's active grid is kept loaded. Outer cells are unloaded.

---

## Key Features

### 1. Additive Async Loading
All chunks are stored as additive Unity scene assets or Addressable prefabs. Loading uses `SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive)` or `Addressables.LoadAssetAsync<GameObject>(key)`.

### 2. Loading Limits
To prevent frame rate drops when crossing cell borders, the loading manager restricts tasks to a configurable limit (e.g. max 1 chunk loading or instantiating per frame).

### 3. Velocity-Based Preloading
The manager calculates player velocity: `Velocity = (CurrentPosition - LastPosition) / Time.deltaTime`. By checking the velocity direction, it preloads cells in the path of travel before the player actually crosses the boundary.

---

## Editor Settings
- **Cell Size**: Coordinate size of grid columns (e.g. 100 units).
- **Loading Range**: Radius around camera to trigger loads.
- **Unloading Margin**: Additional radius required to unload chunks (hysteresis buffer) to avoid load/unload spam.
- **Addressables Mode**: Toggles between Scene-based streaming and Prefab-based Addressable streaming.
