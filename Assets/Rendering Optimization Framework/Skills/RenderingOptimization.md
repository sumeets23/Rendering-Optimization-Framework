# Skill: Rendering Optimization

## 1. Purpose
This skill covers rendering batching, draw call reduction, SetPass call minimizing, SRP Batcher optimization, and GPU instancing configuration.

## 2. Technical Overview
Rendering optimization in Unity 6 requires grouping objects to minimize hardware state changes. The GPU performs best when rendering large batches of geometry with identical shader variables and render state configurations.

## 3. Unity APIs
- `UnityEngine.Rendering.CommandBuffer`
- `UnityEngine.MaterialPropertyBlock`
- `UnityEngine.Mesh.CombineMeshes`

## 4. Rendering Concepts
- **Draw Call:** A command sent by the CPU to the GPU to draw a set of vertices.
- **SetPass Call:** A command that changes the active rendering pass (shader, blend modes, depth states).
- **SRP Batcher:** A built-in loop that uploads material properties to GPU constant buffers to reduce CPU draw setup overhead.

## 5. Optimization Strategies
- Structure custom shaders to store all material variables in the `UnityPerMaterial` constant buffer.
- Combine static, non-instanced meshes that share materials to eliminate CPU transform overhead.
- Automate GPU Instancing configuration for repeating static objects.

## 6. Runtime Constraints
- Combining meshes dynamically at runtime causes high CPU spikes due to vertex buffer allocations.
- Avoid setting material properties directly (e.g. `material.color = x`) as this creates duplicate material instances.

## 7. Memory Constraints
- Combined meshes increase memory usage as they create new unique vertex buffers. Balance memory vs. draw call counts.

## 8. VR Constraints
- Draw calls are twice as expensive in VR if Single Pass Instanced rendering is not active. Keep draw calls under 150 for mobile VR.

## 9. HDRP Notes
- HDRP relies heavily on the SRP Batcher. Avoid using MaterialPropertyBlocks as they break the SRP batcher loop in HDRP.

## 10. URP Notes
- URP supports both SRP Batcher and GPU Instancing. Mobile devices benefit significantly from GPU Instancing of dense foliage and clutter.

## 11. Best Practices
- Keep material count low.
- Rely on textures (atlases) to distinguish visual features instead of splitting meshes into separate materials.

## 12. Common Pitfalls
- Breaking SRP Batcher compatibility by using materials with different shader variants or modifying properties per object using `Renderer.material`.

## 13. Performance Targets
- Frame budget: 13.8ms (72 FPS) on VR headsets, 16.6ms (60 FPS) on standard displays.
- Draw calls: < 500 for high-end HDRP scenes, < 100 for URP mobile VR.
