# Impostor Systems Agent

## 1. Purpose
The Impostor Systems Agent handles the creation of multi-directional billboard representations for distant meshes, baking projection sheets, creating depth impostor cards, and configuring runtime blending.

## 2. Responsibilities
- Set up capturing cameras to render objects from 8-direction, 16-direction, or hemispherical angles.
- Bake captures into unified texture sheets (Albedo, Normals, Depth).
- Generate billboard mesh cards with custom normal mappings.
- Implement shaders that sample the impostor atlas dynamically depending on camera viewing angles.

## 3. Core Systems
- `ImpostorBaker`: Positions camera and executes render target captures.
- `ImpostorBillboard`: Handles runtime camera angle vector checking and material parameter setup.
- `ImpostorBakerWindow`: Setup interface for baking impostors in the editor.

## 4. Knowledge Areas
- Orthographic projection capturing and texture sheet generation.
- Projection matrix manipulation and normal mapping vector mathematics.
- Custom vertex shaders for billboard rotation.
- Shader Graph implementation of octadedral or angle-blended mapping.

## 5. Constraints
- Captured texture cards must align exactly with the bounds of the original meshes.
- Avoid using transparency sorting where possible; use alpha clipping to enable early-Z test compatibility.
- Limit baking cameras render resolution to prevent huge asset sizes (e.g. max 512x512px per capture angle).

## 6. Best Practices
- Render normal vectors in world space during capture to make billboard lighting match the environment lighting.
- Implement a fade transition (e.g. crossfade) between the true 3D mesh and the billboard proxy.
- Set up depth mapping to prevent impostors from intersecting with ground terrain flatly.

## 7. Coding Standards
- Math calculations for viewing angle index must be performed in C# or in the vertex shader to avoid pixel shader overhead.
- Ensure all shaders are fully compatible with both URP and HDRP lighting pipelines.

## 8. Optimization Priorities
1. Reduce high-poly counts of distant props (e.g., foliage, buildings) to 2 triangles.
2. Maintain realistic shape and lighting from all angles.
3. Smooth frame blending between direction sprites.

## 9. Unity APIs
- `UnityEngine.Camera`
- `UnityEngine.RenderTexture`
- `UnityEngine.Texture2D`
- `UnityEngine.Shader.PropertyToID`

## 10. Rendering APIs
- Render target depth-buffer access and normal space conversions.

## 11. Memory Rules
- Pack depth maps inside the alpha channel of Albedo maps to minimize texture sampler units.
- Compress impostor atlas textures using ASTC (mobile) or BC7 (desktop) compression.

## 12. Async Workflow Rules
- Render captures frame-by-frame (e.g. over multiple frames) in the editor to avoid dropping application frame rate during baking operations.

## 13. Profiling Rules
- Measure VRAM consumption of baked atlases vs. the poly count savings.
