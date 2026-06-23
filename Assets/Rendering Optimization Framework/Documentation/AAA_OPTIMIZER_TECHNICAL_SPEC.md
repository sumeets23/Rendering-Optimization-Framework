# AAAOptimizer: Technical Specification & Feature Reference Manual

This document provides a comprehensive technical overview of the **AAAOptimizer** framework. It describes what each optimization feature is, how it is implemented under the hood, and the specific Unity APIs, math, and rendering pipelines involved.

---

## Table of Contents
1. [Core Architecture & Rendering Pipeline Abstraction](#1-core-architecture--rendering-pipeline-abstraction)
2. [Scene Analyzer & Diagnostics](#2-scene-analyzer--diagnostics)
3. [Mesh Simplification & LOD Automation](#3-mesh-simplification--lod-automation)
4. [Draw Call Optimization](#4-draw-call-optimization)
5. [Hierarchical Level of Detail (HLOD) System](#5-hierarchical-level-of-detail-hlod-system)
6. [Impostor Billboard System](#6-impostor-billboard-system)
7. [Dynamic Texture Streaming](#7-dynamic-texture-streaming)
8. [World Partition Grid Streaming](#8-world-partition-grid-streaming)
9. [VR Performance Controller](#9-vr-performance-controller)
10. [Performance Guidelines & Zero-Allocation Rules](#10-performance-guidelines--zero-allocation-rules)

---

## 1. Core Architecture & Rendering Pipeline Abstraction

### What We Are Doing
To support both the **High Definition Render Pipeline (HDRP)** and **Universal Render Pipeline (URP)** in Unity 6, AAAOptimizer utilizes a unified config-driven architecture built around a pipeline bridge pattern.

### How We Are Doing It
* **Centralized Configuration (`AAAOptimizerConfig.cs`)**: A `ScriptableObject` containing all thresholds, budgets, ranges, and ratios. Changing config settings at runtime or edit-time alters the behavior of all systems without script edits.
* **Bridge Pattern (`IRenderPipelineBridge.cs`)**: An interface that abstracts pipeline-specific operations:
  * Dynamic resolution scaling APIs.
  * Setting shadow distances, cascade counts, and shadow-casting properties.
  * Adjusting volumetric fog resolutions and reflection probe parameters.
  * Material configurations (e.g., setting shader properties on HDRP/Lit vs URP/Lit).
* **Bake vs. Runtime Separation**: All heavy computations (mesh simplify, atlas generation, impostor captures) are done strictly in the Editor. Runtime components are lightweight swappers, timers, and budget monitors targeting zero heap allocation.

---

## 2. Scene Analyzer & Diagnostics

### What We Are Doing
The **Scene Analyzer** provides editor-time diagnostics to detect rendering bottlenecks, vertex budgets, excessive draw calls, shadow casters, and VRAM footprints.

### How We Are Doing It
* **Editor-Only Execution**: The entire analyzer pipeline is gated behind `#if UNITY_EDITOR` to guarantee zero runtime overhead or unexpected game freezes in production builds.
* **Hierarchy Traversal**: Scans all `MeshRenderer` and `SkinnedMeshRenderer` components in the active scene using `Object.FindObjectsByType`.
* **VRAM Estimation**: Evaluates texture properties (resolution, channel count, format) to estimate graphic memory footprint using:
  $$\text{VRAM Size} = \text{Width} \times \text{Height} \times \text{Bytes Per Pixel} \times 1.33$$
  *(where $1.33$ represents the mipmap chain overhead).*
* **SRP Batcher Compatibility**: Audits scene materials to check if shader properties are properly encapsulated inside the `UnityPerMaterial` constant buffer.
* **Duplicate Materials**: Identifies separate material assets that share identical texture maps and color parameters to suggest material merging.

---

## 3. Mesh Simplification & LOD Automation

### What We Are Doing
Automatically generates multi-level Level-of-Detail (LOD) hierarchies and decimates 3D static geometries at runtime or bake-time.

### How We Are Doing It
* **Vertex Clustering Simplification (`MeshSimplifierUtility.cs`)**:
  1. Computes the bounding box of the source mesh.
  2. Subdivides the bounding box into a 3D grid of cells. The resolution (grid density, from $6^3$ to $60^3$) is derived dynamically from the `reductionRatio` parameter:
     $$\text{gridResolution} = \text{Clamp}(\text{Round}(\text{reductionRatio} \times 60), 6, 60)$$
  3. Maps all vertices inside each cell into a single cluster.
  4. Averages the spatial coordinates, normal vectors (renormalized), and UV coordinates of all vertices in each cluster to output a single collapsed vertex.
  5. Discards degenerate triangles (triangles where multiple vertices collapse to the same grid cell).
  6. Rebuilds submesh indices to retain multi-material mappings.
* **LOD Automator (`LODGroupAutomator.cs`)**:
  * Runs in the Editor to generate 3 simplified meshes (LOD1, LOD2, LOD3) using configurable ratios (e.g. 60%, 30%, 10%).
  * Saves the generated assets in `Assets/AAAOptimizer/Data/LODs/`.
  * Creates child game objects (`_LOD1`, `_LOD2`, `_LOD3`) and configures a `LODGroup` component on the parent object.
  * Defines screen-space transition thresholds (e.g., LOD0 = 60%, LOD1 = 30%, LOD2 = 15%, LOD3 = 4%).

---

## 4. Draw Call Optimization

### What We Are Doing
Maximizes rendering batches by auto-configuring material properties and merging static mesh geometries.

### How We Are Doing It
* **GPU Instancing Utility (`DrawCallOptimizer.cs`)**:
  * Scans all scene renderers and sets `material.enableInstancing = true` via the active pipeline bridge. This groups identical meshes sharing the same material into single instanced draw calls.
* **Static Mesh Combiner**:
  * Collects static renderers under a parent object and groups them by their `sharedMaterial`.
  * Merges their vertex arrays, UVs, normals, and index buffers into a single combined mesh using `Mesh.CombineMeshes`.
  * Uses 32-bit indices (`IndexFormat.UInt32`) to support merged vertex counts exceeding 65,535.
  * Disables the original renderer components to redirect drawing to the single combined mesh object.

---

## 5. Hierarchical Level of Detail (HLOD) System

### What We Are Doing
Groups clusters of distant static geometries, combines their textures into an atlas, and simplifies the merged geometry to render a single, low-draw-call proxy mesh.

### How We Are Doing It
* **Spatial Clustering (`HLODClusterer.cs`)**:
  * Partitions the scene using a 3D grid cell size (e.g., 50 meters).
  * Collects all static `MeshRenderers` within each cell to form an `HLODCluster`.
* **Material Atlasing (`MaterialAtlasBuilder.cs`)**:
  * Accumulates unique materials used inside a cluster.
  * Converts compressed textures into readable ARGB formats and maps them into a manual `RenderTexture` grid pipeline.
  * Uses `Graphics.DrawTexture` with precise absolute pixel rects to pack the textures, completely eliminating the need for `Texture2D.PackTextures`.
  * Creates and serializes a new atlas material using the pipeline's Lit shader.
* **Mesh Combining & UV Remapping (`MeshCombiner.cs`)**:
  * Combines all cluster meshes into a single mesh.
  * Clamps submesh UV coordinates to half-texel inner boundaries to eliminate bilinear filtering bleed, then translates and scales them into their atlas `Rect`:
    ```csharp
    u = Mathf.Clamp(uv.x * scale.x + offset.x, halfTexel, 1f - halfTexel);
    v = Mathf.Clamp(uv.y * scale.y + offset.y, halfTexel, 1f - halfTexel);
    ```
* **LOD Proxy Swapping (`HLODController.cs`)**:
  * Monitors the main camera's distance to the cluster bounds center.
  * If the camera is farther than `transitionDistance`, the proxy renderer is enabled and the high-detail original children are completely deactivated.
  * Updates are throttled using `checkInterval` (e.g., 0.5s) to avoid frame spikes.

---

## 6. Impostor Billboard System

### What We Are Doing
Replaces highly complex 3D meshes at far distances with 2-triangle billboard cards that dynamically update their visual representation based on camera viewing angles.

### How We Are Doing It
* **Impostor Baker (`ImpostorBaker.cs`)**:
  * Sets up an orthographic camera centered on the target object.
  * Uses a **Hemi-Octahedron grid mapping** to dynamically capture 64 viewing angles (8x8) over the upper hemisphere, drastically improving vertical fidelity.
  * Captures orthographic color, object-space normals, and packed depth values.
  * Assembles these views into a 2D Texture Atlas:
    * **Base Map**: RGB (Color) + A (Opacity cutout).
    * **Normal Map**: RGB (Object-space normals) + A (Normalized scalar depth map).
* **Impostor Billboard Component (`ImpostorBillboard.cs` & Shaders)**:
  * Generates materials explicitly for `AlphaTest` and `ZWrite On` to completely eliminate depth-offset ordering issues.
  * Maps the view vector into Hemi-Octahedral UV space dynamically at runtime to select the corresponding UV frame in the texture atlas.
  * Interpolates (cross-fades) between adjacent frames to prevent visual jumps during camera rotation.

---

## 7. Dynamic Texture Streaming

### What We Are Doing
Regulates active GPU VRAM consumption by loading and unloading mipmap levels based on budget thresholds.

### How We Are Doing It
* **VRAM Tracker (`TextureStreamingController.cs`)**:
  * Hooks into Unity's built-in streaming mipmaps APIs:
    * `QualitySettings.streamingMipmapsActive = true`
    * `QualitySettings.streamingMipmapsMemoryBudget`
  * Monitors memory sizes using `Texture.desiredTextureMemory` and `Texture.currentTextureMemory` every second.
* **Dynamic Budgeting**:
  * If the current texture memory usage exceeds the defined budget, the controller increments `QualitySettings.globalTextureMipmapLimit`. This forces Unity to discard high-res mip levels (e.g., Mip 0) globally, immediately releasing VRAM.
  * If VRAM usage drops below 80% of the budget, it decrements the limit to restore texture sharpness.

---

## 8. World Partition Grid Streaming

### What We Are Doing
Divides expansive environments into grid cells and streams additive chunks (scenes or Addressable prefabs) based on player coordinates.

### How We Are Doing It
* **Grid Coordinates**:
  * Tracks player coordinates to calculate the active cell:
    $$\text{CellX} = \text{Floor}(\text{PlayerX} / \text{CellSize})$$
    $$\text{CellZ} = \text{Floor}(\text{PlayerZ} / \text{CellSize})$$
* **Predictive Loading**:
  * Evaluates player velocity:
    $$\text{Velocity} = \frac{\text{CurrentPosition} - \text{LastPosition}}{\Delta t}$$
  * Offsets the loading target position forward along the velocity vector (e.g., predicting 1.5 seconds ahead).
* **Additive Loading and Unloading**:
  * Maintains an active 3x3 cell matrix around the target coordinate.
  * Supports modern streaming via the Unity Addressables API (`USE_ADDRESSABLES` definition) via `Addressables.LoadSceneAsync`, gracefully falling back to standard `SceneManager.LoadSceneAsync` when Addressables is disabled.
  * Unloads out-of-range chunks automatically.
  * Limits loading queues to a maximum of 1 scene load/unload per frame to prevent CPU stuttering.
  * Implements an `unloadRadiusMargin` hysteresis buffer to avoid rapid flipping at boundary borders.

---

## 9. VR Performance Controller

### What We Are Doing
Maximizes framerate and controls rendering quality dynamically for virtual reality platforms (targeting standalone headsets like Meta Quest).

### How We Are Doing It
* **Stereoscopic Optimizations**:
  * Enforces **Single Pass Instanced (SPI)** rendering via configuration.
  * Overrides shadow configurations: sets shadow cascades to 1 and limits shadow distance to a tight range (e.g. 15-20 meters).
* **Dynamic Resolution Scaling (DRS)**:
  * Computes frame-time metrics at regular intervals.
  * If the frame time exceeds the target threshold (e.g., $13.8\text{ms}$ for $72\text{Hz}$):
    $$\text{Scale}_{\text{new}} = \text{Max}(\text{MinScale}, \text{Scale}_{\text{current}} - 0.05)$$
  * If the frame time is stable ($< 85\%$ of the target budget):
    $$\text{Scale}_{\text{new}} = \text{Min}(\text{MaxScale}, \text{Scale}_{\text{current}} + 0.05)$$
  * Applies the target scale dynamically via the pipeline bridge using `XRSettings` or render pipeline configurations.
* **Fixed Foveated Rendering (FFR)**:
  * Sets the FFR level (None, Low, Medium, High) using OpenXR platform API configurations, reducing pixel fill rate overhead at the edges of the VR lenses.

---

## 10. Performance Guidelines & Zero-Allocation Rules

### What We Are Doing
Maintains frame rate stability by adhering to high-performance C# coding conventions and CPU budget boundaries.

### How We Are Doing It
* **Zero Allocations in Update Loops**:
  * No use of `new`, LINQ queries, list instantiations, or string concatenations inside Update/Render frames.
  * Pre-allocates cached lists and arrays with defined capacities during initialization:
    ```csharp
    private List<GameObject> activeChunks = new List<GameObject>(32);
    ```
  * Uses non-allocating Unity APIs, e.g., `Physics.OverlapSphereNonAlloc` and `GetComponentsNonAlloc`.
* **Job System & Burst Compiler**:
  * Offloads heavy mathematics (like distance updates, grid calculations, bounding box intersections) to worker threads using the `IJobParallelFor` layout.
  * Compiles C# structures directly to optimized machine instructions using the `[BurstCompile]` attribute.
  * Utilizes `NativeArray` buffers for high-speed memory block copies.
