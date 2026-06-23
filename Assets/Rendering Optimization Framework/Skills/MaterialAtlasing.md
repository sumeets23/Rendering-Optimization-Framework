# Skill: Material Atlasing

## 1. Purpose
This skill covers consolidating multiple texture maps into a single texture atlas, mapping corresponding UV coordinates on combined meshes, managing texture bleeding, and blending shaders.

## 2. Technical Overview
Even if meshes are combined, they cannot be rendered in a single draw call if they use different materials (textures). Material Atlasing solves this by packing different textures (Albedo, Normal, Metallic) into unified maps, letting the combined mesh use a single material.

## 3. Unity APIs
- `UnityEngine.Texture2D.PackTextures`
- `UnityEngine.Mesh.uv`
- `UnityEngine.Rect`

## 4. Rendering Concepts
- **Texture Packing:** Arranging multiple smaller textures onto a larger texture grid.
- **UV Remapping:** Adjusting the vertex texture coordinates to map to the new packed rect coordinates.
- **Texture Bleeding:** Artifacts where pixels from adjacent textures leak into the rendering of a mesh due to texture mipmapping.

## 5. Optimization Strategies
- Collect all textures (Albedo, Normal, Mask) from target renderers.
- Use `Texture2D.PackTextures` to merge them into a single texture asset, generating an array of `Rect` structures containing the coordinates.
- Map the vertex coordinates of the combined mesh: `newUV = rect.x + oldUV * rect.width`.
- Add a border padding (e.g. 8-16 pixels) around texture blocks in the atlas to prevent mip bleeding.

## 6. Runtime Constraints
- Baking atlases dynamically at runtime is extremely slow due to texture reading and GPU upload latency. Bake atlases during editor configuration.

## 7. Memory Constraints
- Avoid baking 8K or larger textures; use 4K as the maximum atlas resolution to ensure compatibility with older GPUs and console VRAM footprints.

## 8. VR Constraints
- In VR, texture compression artifacts and mip bleed are highly visible because of magnification near the user's eyes. Use high-quality compression (BC7 / ASTC) and sufficient padding.

## 9. HDRP Notes
- Pack HDRP specific maps (Mask map: Metallic, Occlusion, Detail, Smoothness) consistently across the atlas.

## 10. URP Notes
- Pack URP maps (Metallic/Smoothness) using the correct target channels (usually Metallic in R and Smoothness in A or G).

## 11. Best Practices
- Standardize all texture inputs to the same color space (Linear vs. Gamma) before packing.
- Use matching resolution sizes for matching map channels.

## 12. Common Pitfalls
- Neglecting to pack Normal maps correctly (normal maps must not be packed with gamma correction; they must use linear texture configuration).

## 13. Performance Targets
- Draw call reduction: Combine multiple materials into a single material.
- Atlas bake time: < 3 seconds per cluster.
