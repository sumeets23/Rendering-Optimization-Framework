# Rendering Optimization Framework

AAAOptimizer is a Unity 6 rendering optimization toolkit for large scenes, combining scene analysis, LOD generation, HLOD proxy building, impostor baking, texture streaming, and world streaming in one modular workflow.

## Features

- Scene analysis and rendering diagnostics
- Mesh simplification and automated LOD generation
- HLOD clustering, proxy mesh creation, and material atlasing
- Impostor baking with atlas-based billboard rendering
- Draw-call optimization and batching support
- Texture streaming and mip budget control
- Grid-based world streaming
- HDRP and URP compatibility via pipeline bridges

## Technical Highlights

- Editor-time baking for heavy operations
- Runtime swapping for lightweight performance
- Generated assets stored in `Assets/AAAOptimizer/Data/`
- Alpha-tested impostor rendering with normal/depth atlases
- Distance-based proxy selection for large environments

## Repository Structure

- `Assets/AAAOptimizer/Core/` - configuration, diagnostics, and pipeline abstraction
- `Assets/AAAOptimizer/HLOD/` - cluster building, atlas packing, proxy mesh generation
- `Assets/AAAOptimizer/Impostors/` - impostor baker and runtime billboard controller
- `Assets/AAAOptimizer/Simplification/` - mesh simplification and LOD automation
- `Assets/AAAOptimizer/Streaming/` - chunk and world streaming logic
- `Assets/AAAOptimizer/TextureStreaming/` - texture residency control
- `Assets/AAAOptimizer/Editor/` - Unity editor windows and bake tools
- `Assets/AAAOptimizer/Shaders/` - impostor and optimization shaders

## Workflow

1. Scan the scene to identify optimization targets.
2. Generate LODs or HLOD proxies for static objects.
3. Bake impostors for distant complex meshes.
4. Tune streaming and rendering budgets from the configuration asset.
5. Test the resulting scene in Play Mode.

## Demo Notes

This repository includes source code and generated demo assets that show how the optimization systems work in practice. The project is intended as a technical demonstration of modular rendering optimization in Unity 6.
