# Skill: HLOD Generation

## 1. Purpose
This skill covers spatial clustering of meshes, merging multiple mesh geometries, packing texture atlases, and generating low-resolution HLOD proxy meshes.

## 2. Technical Overview
Hierarchical Level of Detail (HLOD) combines multiple distant game objects into a single combined mesh and a single material. This reduces draw calls when objects are too far away for individual details to be noticeable.

## 3. Unity APIs
- `UnityEngine.Mesh.CombineMeshes`
- `UnityEngine.CombineInstance`
- `UnityEngine.Texture2D.PackTextures`

## 4. Rendering Concepts
- **Spatial Clustering:** Grouping objects by bounding-box proximity.
- **Proxy Geometry:** A combined, simplified mesh representing a collection of complex geometries.
- **Material Atlasing:** Stitching individual textures into a single texture map.

## 5. Optimization Strategies
- Use regular grid partitioning or octrees to generate clusters.
- Strip submesh definitions and convert all merged meshes to use a single submesh index.
- Calculate UV offsets to shift vertex coordinates to point to the correct atlas locations.

## 6. Runtime Constraints
- Do not compute HLOD configurations dynamically at runtime; they must be baked in the Editor and saved as prefabs.
- Load proxy prefabs asynchronously to prevent frame stutter.

## 7. Memory Constraints
- Combined atlas textures can consume significant memory. Downscale texture inputs (e.g. 512x512px max for proxy textures).

## 8. VR Constraints
- Ensure proper texture filtering and mipmaps are applied to atlases to avoid extreme aliasing (shimmering) in VR headsets.

## 9. HDRP Notes
- Ensure lightmap coordinates (UV2) are preserved and packed correctly if using static baked lighting.

## 10. URP Notes
- Keep vertex counts under 65k per cluster to remain compatible with older mobile devices that do not support 32-bit indices.

## 11. Best Practices
- Define cluster sizes that align with the scene loading cells or chunk grid sizing.
- Filter out small props (clutter) entirely from HLOD proxies.

## 12. Common Pitfalls
- Combining meshes that use wildly different shaders (e.g. skin shaders mixed with metallic metal shaders). Cluster by shader type.

## 13. Performance Targets
- Draw call reduction: Combine up to 100 draw calls into 1.
- Poly count reduction: Simpify cluster vertex counts by 75-90%.
