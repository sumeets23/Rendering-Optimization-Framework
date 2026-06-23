# Skill: Mesh Simplification

## 1. Purpose
This skill covers mesh decimation algorithms, vertex clustering, boundary/silhouette preservation, normal recalculation, and automatic creation of LOD hierarchies.

## 2. Technical Overview
Mesh simplification reduces the polygon count of a mesh while preserving its visual shape. By generating simplified versions (LOD1, LOD2, LOD3) of a detailed mesh, the GPU vertex load is reduced as objects move further from the camera.

## 3. Unity APIs
- `UnityEngine.Mesh`
- `UnityEditor.MeshUtility.Simplify` (internal editor APIs)
- `UnityEngine.LODGroup`

## 4. Rendering Concepts
- **Vertex Decimation:** Removing vertices and merging adjacent triangles.
- **Edge Collapse:** Contracting an edge between two vertices into a single vertex.
- **Silhouette Preservation:** Retaining the outer contours of a mesh to avoid visual shape distortion at distances.

## 5. Optimization Strategies
- Apply edge-collapse or vertex-clustering algorithms to reduce triangle count by target ratios (e.g. LOD1 = 60%, LOD2 = 30%, LOD3 = 10%).
- Protect boundary vertices (seams, UV boundaries) from collapse to avoid texture mapping issues.
- Automatically instantiate `LODGroup` components on parent game objects and assign the simplified meshes to corresponding LOD slots.

## 6. Runtime Constraints
- Simplification is a highly CPU-intensive geometric task. It should be performed in the Editor. If done at runtime, use simplified vertex clustering on separate worker threads.

## 7. Memory Constraints
- Generated LOD meshes increase the project's build size and memory load. Ensure LODs are recycled or streamed.

## 8. VR Constraints
- In VR, objects close to the viewer must use LOD0 with high density. LOD transitions must be conservative to prevent visible "popping" in the headset.

## 9. HDRP Notes
- Ensure simplified meshes preserve correct normal vectors and tangents to prevent lighting distortions when using complex HDRP materials.

## 10. URP Notes
- Extremely useful for target mobile platforms to keep vertex count under the vertex shader pipeline limit.

## 11. Best Practices
- Recalculate mesh normals and tangents after decimation to prevent flat-shading artifacts.
- Share the original materials across LOD levels where possible to avoid material duplicate overhead.

## 12. Common Pitfalls
- Over-simplifying thin or hollow geometry (like fences or ropes), causing them to completely disintegrate.

## 13. Performance Targets
- Poly count reduction: LOD1 (50% reduction), LOD2 (75% reduction), LOD3 (90% reduction).
- Setup automation: Instant creation of LOD setups for selected scene folders.
