# Skill: Scene Analysis

## 1. Purpose
This skill covers traversing scene hierarchies, extracting rendering statistics, analyzing batching opportunities, identifying duplicate materials, estimating VRAM, and detecting high-poly bottlenecks.

## 2. Technical Overview
Optimization starts with accurate diagnostics. Scene analysis tools traverse active scene nodes, collect metadata from renderers, materials, and textures, and generate warnings for items that violate performance budgets.

## 3. Unity APIs
- `UnityEngine.Object.FindObjectsByType`
- `UnityEditor.TextureUtil` (internal editor APIs)
- `UnityEngine.Profiler`

## 4. Rendering Concepts
- **VRAM Estimation:** Calculating texture storage cost based on resolution, channels, and compression format.
- **Batching Opportunities:** Grouping mesh renderers that share identical material structures to maximize batch counts.
- **Shadow Casters:** Renderers that draw to the shadow map, doubling draw call counts.

## 5. Optimization Strategies
- Traverse all `MeshRenderer` and `SkinnedMeshRenderer` components in the scene.
- Group renderers by material reference to detect duplicate material instances.
- Estimate texture memory size: `width * height * bytesPerPixel * mipMultiplier` (approx. 1.33x for mips).
- Log warnings for objects with poly counts exceeding thresholds (e.g. > 50,000 vertices for mobile props).

## 6. Runtime Constraints
- Heavy traversal of active scene hierarchies causes severe CPU lag. Keep analysis strictly to editor workflows or run it incrementally over multiple frames at runtime.

## 7. Memory Constraints
- Store collected metrics databases in persistent ScriptableObjects so they can be reviewed and compared across bakes.

## 8. VR Constraints
- In VR, keep shadow casters under 50. Scene analyzer should flag any renderer with "Cast Shadows" enabled that is small or far away.

## 9. HDRP Notes
- Identify high reflection probe coverage and expensive volumetric regions during scene analysis.

## 10. URP Notes
- Audit SRP Batcher compatibility flags for materials during scene traversal.

## 11. Best Practices
- Focus optimization efforts on the "heavy hitters" (e.g. top 5% of meshes with highest poly count or draw call contribution).
- Automate analysis as an editor check before building the application.

## 12. Common Pitfalls
- Assuming static batching works automatically on meshes with different materials. Verify batching flags during scanning.

## 13. Performance Targets
- Scan execution time: < 2 seconds for moderate scenes (10,000 game objects).
- Zero runtime overhead when not actively running diagnostic tasks.
