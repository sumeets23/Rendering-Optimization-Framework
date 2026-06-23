# Skill: GPU Instancing

## 1. Purpose
This skill covers GPU Instancing configuration, Indirect/Procedural instancing, MaterialPropertyBlock usage, custom instancing shaders, and SRP Batcher interaction guidelines.

## 2. Technical Overview
GPU Instancing allows the GPU to render multiple identical meshes with variation (e.g. position, scale, color) in a single draw call. The CPU sends a single mesh definition and an array of transformation matrices instead of individual draw calls.

## 3. Unity APIs
- `UnityEngine.Graphics.RenderMeshInstanced`
- `UnityEngine.Material.enableInstancing`
- `UnityEngine.MaterialPropertyBlock`

## 4. Rendering Concepts
- **GPU Instancing:** Multi-instance single-draw-call geometry rendering.
- **Instance Buffer:** An array of per-instance data (transforms, colors, custom parameters) uploaded to the GPU.
- **SRP Batcher vs. GPU Instancing:** The SRP Batcher binds constant buffers once and draws different meshes. GPU Instancing draws the same mesh many times. They are mutually exclusive per draw call.

## 5. Optimization Strategies
- Enable "Enable Instancing" on materials where duplicate meshes exist.
- Use `MaterialPropertyBlock` to apply different properties (e.g. tint, offset) to instanced renderers.
- For massive foliage (e.g. grass), write a compute shader that generates instance matrices and render them via `Graphics.RenderMeshIndirect`.

## 6. Runtime Constraints
- Modifying a `MaterialPropertyBlock` on a renderer dirty-marks the renderer state and incurs a small CPU setup cost. Cache property block instances.

## 7. Memory Constraints
- Instance data arrays can consume substantial memory if drawing millions of instances. Keep per-instance struct sizes small.

## 8. VR Constraints
- In VR, GPU instancing is highly effective at reducing CPU-side stereo draw call overheads.

## 9. HDRP Notes
- Ensure materials that use GPU instancing are still compatible with HDRP's shadow map passes.

## 10. URP Notes
- GPU Instancing is widely supported on modern mobile devices (OpenGL ES 3.0+ and Vulkan).

## 11. Best Practices
- Group identical static props (barrels, columns, foliage) and enable instancing on their shared material.
- Combine transforms into a flat array before executing indirect instancing calls.

## 12. Common Pitfalls
- Creating new MaterialPropertyBlocks inside update loops, which leads to massive garbage collection allocations.

## 13. Performance Targets
- Batch sizes: Draw up to 10,000 instances in a single draw call.
- Draw call savings: 90%+ draw call reduction on repetitive objects.
