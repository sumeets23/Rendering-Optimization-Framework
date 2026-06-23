# Rendering Optimization System for Unity 6

Rendering Optimization System is a modular environment virtualization and scene optimization toolkit for Unity 6 LTS. Supporting both the High Definition Render Pipeline (HDRP) and the Universal Render Pipeline (URP), it provides dashboard-driven editor workflows for scene auditing, HLOD/LOD generation, impostor baking, draw-call reduction, texture streaming, and world streaming.

---

## Key Features

1. **Scene Analyzer**: Complete diagnostic auditing of polygons, materials, textures, overdraw, shadow cascades, and batching opportunities.
2. **Runtime Mesh Simplification**: Fast edge-collapse and vertex-clustering geometry simplification utilities.
3. **HLOD (Hierarchical Level of Detail)**: Spatial clustering, mesh merging, and automatic material atlasing for distant environments.
4. **Impostor System**: 8-way, 16-way, and hemispherical orthographic captures rendered on 2-triangle billboards with normal/depth-mapped shaders.
5. **Draw Call Optimizer**: Auto-configuration of GPU instancing and SRP batcher compatibility mappings.
6. **Texture Streaming**: Runtime texture residency tracking and mip budgeting controller.
7. **World Partition Streaming**: Coordinates coordinate-based additive scene chunk loading and unloading.
9. **Debug Visualization**: Scene View gizmos overlays for chunk grids, active HLOD clusters, impostor bounds, and overdraw overlays.

---

## Directory Structure

```
Assets/AAAOptimizer/
├── Core/               # Configs and base manager classes
├── Editor/             # GUI Editor Windows and bakes
├── Runtime/            # Swappers, timers, monitors
├── Rendering/          # URP & HDRP abstract bridges
├── Streaming/          # Chunk loading system
├── HLOD/               # Mesh combiner & atlas pack
├── Impostors/          # Billboard baker
├── Simplification/     # Mesh simplification
├── TextureStreaming/   # Mip management
├── VR/                 # Foveated rendering configuration
├── HDRP/               # Desktop profiles
├── URP/                # Mobile VR profiles
├── Debug/              # Gizmos and overlays
├── Data/               # Configurations and presets
├── Resources/          # Default materials, shaders
├── UI/                 # HUD Canvas overlays
├── Documentation/      # System architecture manuals
├── Agents/             # AI optimization guides
├── Skills/             # Tech specifications manual
└── Shaders/            # Billboard and overlay HLSL graphs
```

---

## Getting Started

### 1. Installation
Ensure the following packages are installed in your Unity 6 project:
- **Burst** (`com.unity.burst`)
- **Mathematics** (`com.unity.mathematics`)
- **Collections** (`com.unity.collections`)
- **Addressables** (`com.unity.addressables`)
- **Shader Graph** (`com.unity.shadergraph`)
- **Jobs** (`com.unity.jobs`)

### 2. Basic Setup
1. Create a global configuration asset via the menu: `Assets -> Create -> Rendering Optimization System -> Configuration`.
2. Open the dashboard: `Tools -> Rendering Optimization System -> Dashboard`.
3. Press **Scan Scene** to inspect rendering bottlenecks, high-poly geometries, and duplicated material assets.
4. Use the dashboard tabs to configure HLODs, impostors, draw calls, and streaming parameters.

---

## System Documentation

Refer to the [Documentation/](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/) folder for technical specifications:
- [System Architecture](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/SYSTEM_ARCHITECTURE.md)
- [HLOD System Manual](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/HLOD_SYSTEM.md)
- [Impostor System Manual](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/IMPOSTOR_SYSTEM.md)
- [Texture Streaming Manual](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/TEXTURE_STREAMING.md)
- [World Streaming Manual](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/WORLD_STREAMING.md)
- [HDRP Optimization Guide](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/HDRP_SUPPORT.md)
- [URP Optimization Guide](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/URP_SUPPORT.md)
- [Performance Guidelines](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/PERFORMANCE_GUIDELINES.md)
- [Open Source References](file:///d:/Sumeet%20Projects/Rendering%20Optimization/Assets/AAAOptimizer/Documentation/OPEN_SOURCE_REFERENCES.md)
