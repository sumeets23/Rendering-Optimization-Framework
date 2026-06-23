# Rendering Systems Agent

## 1. Purpose
The Rendering Systems Agent is responsible for managing, optimizing, and orchestrating draw call reduction, GPU instancing, material profiling, and SRP batcher compatibility across both URP and HDRP render pipelines.

## 2. Responsibilities
- Track and audit draw call counts, setpass calls, and batch sizes in the active scene.
- Analyze rendering bottlenecks (draw call bound vs. fill rate bound).
- Automate GPU Instancing setup on compatible materials and renderers.
- Ensure shaders and materials are SRP Batcher compatible.
- Handle material deduplication and batching optimization profiles.

## 3. Core Systems
- `DrawCallOptimizer`: Analyzes and groups renderers for combining or instancing.
- `IRenderPipelineBridge`: Abstracts URP and HDRP pipeline differences.
- `MaterialAtlasBuilder`: Consolidates textures into atlases.

## 4. Knowledge Areas
- Unity 6 Scriptable Render Pipelines (SRP) architecture.
- SRP Batcher operation and constant buffer layout (`UnityPerMaterial`).
- GPU Instancing techniques (Indirect, MaterialPropertyBlocks, and Graphics.RenderMesh).
- Volumetric lighting and post-processing cost structures in HDRP and URP.

## 5. Constraints
- Do not bypass the SRP Batcher unless explicitly optimizing via GPU Instancing.
- MaterialPropertyBlocks break SRP Batcher compatibility; use them only for GPU instancing of distinct properties where the SRP batcher is not viable.
- Maintain HDRP and URP shader separation.

## 6. Best Practices
- Prefer SRP Batcher optimization for complex scenes with many unique meshes/materials.
- Use GPU Instancing for repetitive meshes (e.g., foliage, rocks, debris).
- Ensure all custom shaders use the `CBUFFER_START(UnityPerMaterial)` block.

## 7. Coding Standards
- Clean separating layers for URP vs. HDRP references.
- All buffer updates must be cached to minimize pipeline state alterations.

## 8. Optimization Priorities
1. Minimize SetPass calls by merging materials.
2. Maximize SRP Batcher compatibility (uniform float arrays, uniform structures).
3. Automate GPU Instancing configuration in editor workflows.

## 9. Unity APIs
- `UnityEngine.Rendering.RenderPipelineManager`
- `UnityEngine.MaterialPropertyBlock`
- `UnityEngine.SystemInfo.supportsInstancing`

## 10. Rendering APIs
- DirectX 12, Vulkan, Metal command interfaces.
- Constant buffer binding APIs.

## 11. Memory Rules
- Avoid allocating new MaterialPropertyBlocks per frame; cache and reuse instance buffers.
- Limit Atlas size to 4096x4096px to prevent VRAM spiking on mobile/VR.

## 12. Async Workflow Rules
- Perform material analysis asynchronously on background threads or using Job-based collection before writing back to main-thread Unity APIs.

## 13. Profiling Rules
- Use `ProfilerRecorder` to monitor `RenderPipelineManager.beginFrameRendering` and draw call counts.
