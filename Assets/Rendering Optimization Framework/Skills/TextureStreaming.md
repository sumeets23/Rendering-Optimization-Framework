# Skill: Texture Streaming

## 1. Purpose
This skill covers configuring texture mipmap streaming, tracking active VRAM footprint, implementing texture memory budgets, and loading/unloading mip levels dynamically.

## 2. Technical Overview
Textures are typically the largest consumer of VRAM. Rather than loading full-resolution textures, texture streaming only uploads the specific mipmap levels required for the current camera distance, saving massive amounts of VRAM.

## 3. Unity APIs
- `UnityEngine.Texture2D.requestedMipmapLevel`
- `UnityEngine.Texture2D.streamingTextureForceMipLevels`
- `UnityEngine.QualitySettings.streamingMipmapsMemoryBudget`

## 4. Rendering Concepts
- **Mipmap:** Sequentially smaller, pre-filtered versions of a texture.
- **VRAM Residency:** The amount of texture data currently loaded into GPU memory.
- **Mip Capping:** Restricting a texture to only load up to a certain mip level (e.g. discard mip 0).

## 5. Optimization Strategies
- Enable "Streaming Mipmaps" on all environmental texture imports.
- Configure global memory budgets depending on target hardware (e.g., 512MB for mobile, 4GB for high-end desktop).
- Programmatically force lower mip levels on textures that are far from the camera or belong to secondary objects.

## 6. Runtime Constraints
- Forcing mip level changes requires a brief GPU texture upload operation. Batch updates to avoid performance hitches.

## 7. Memory Constraints
- Constantly monitor `Texture.desiredTextureMemory` against the budget. If it exceeds limits, lower the global mip quality settings dynamically.

## 8. VR Constraints
- Textures that are too blurry (due to overly aggressive streaming) are highly noticeable in VR headsets. Keep high-priority textures (near the player) at full mip quality.

## 9. HDRP Notes
- HDRP default settings have high VRAM demands. Correctly balance shadow maps, G-buffers, and texture streaming budgets.

## 10. URP Notes

## 11. Best Practices
- Enable texture streaming in project Quality settings.
- Adjust "Streaming Priority" on renderers that are close to the player.

## 12. Common Pitfalls
- Setting texture budgets too low, causing textures to rapidly cycle between blurry and sharp (mip thrashing).

## 13. Performance Targets
- VRAM savings: Up to 50% texture memory reduction.
- CPU overhead: < 0.1ms per frame.
