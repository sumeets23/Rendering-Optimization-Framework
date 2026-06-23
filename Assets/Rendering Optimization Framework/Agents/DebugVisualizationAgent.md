# Debug Visualization Agent

## 1. Purpose
The Debug Visualization Agent creates and maintains rendering utilities, gizmo drawing tools, editor overlays, and runtime diagnostic panels to analyze environment structures.

## 2. Responsibilities
- Implement visual indicators for chunk streaming boundaries.
- Render bounding boxes and clusters for HLOD grids in the Scene View.
- Build overdraw heatmap visualization tools.
- Design diagnostic HUD overlays showing memory graphs, draw calls, and FPS.

## 3. Core Systems
- `DebugVisualizer`: Coordinates gizmo drawing and shader overrides.

## 4. Knowledge Areas
- Unity `Gizmos` and `Handles` rendering pipelines.
- `GL` class immediate-mode rendering interface.
- Rendering command override buffers (`CommandBuffer.DrawRenderer`).
- Runtime GUI Canvas layouts and graphical graph rendering.

## 5. Constraints
- Disable all debug visualization rendering in production builds unless explicit configuration toggles are enabled.
- Avoid garbage collection allocations when creating text metrics.
- Keep gizmo rendering calculations optimized so high-poly scenes do not lag in the editor.

## 6. Best Practices
- Cache static strings for UI labels.
- Use wireframe primitives (cubes, spheres) to represent spatial clusters instead of rendering solid geometry.
- Draw bounding boxes only for visible renderers (frustum culling debug lines).

## 7. Coding Standards
- Debug code must be compiled out using `#if UNITY_EDITOR || DEVELOPMENT_BUILD` compiler directives.
- Use explicit colors to represent different metrics (e.g. Red = overdrawn/heavy, Green = light/optimal).

## 8. Optimization Priorities
1. Fast gizmo updates (avoiding dynamic array allocations).
2. Clean, understandable overlays in both editor and runtime.
3. No impact on regular runtime systems when disabled.

## 9. Unity APIs
- `UnityEngine.Gizmos`
- `UnityEngine.GL`
- `UnityEditor.Handles`
- `UnityEngine.Rendering.CommandBuffer`

## 10. Rendering APIs
- Immediate-mode drawing loops.

## 11. Memory Rules
- Use pre-allocated vertex arrays for dynamic debug mesh drawing.

## 12. Async Workflow Rules
- Perform spatial layout calculations (like cluster center locations) on worker threads using Jobs, then write the lines list to the main rendering pass.

## 13. Profiling Rules
- Ensure debug visualizers register under a separate "DebugVisuals" profiler marker to prevent skewing game optimization tests.
