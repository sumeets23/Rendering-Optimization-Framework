# Performance Guidelines: AAAOptimizer

This document outlines the strict guidelines and coding standards implemented within the AAAOptimizer framework to maintain high-performance runtime execution.

---

## Memory Allocations & Garbage Collection

To achieve stable frame rates (especially in VR), runtime systems must not allocate memory during gameplay.
- **Zero Allocations in Update Loops**: Managers and swappers must not use `new`, `LINQ` operations, or string concatenations during active frames.
- **Pre-allocating Containers**: All Lists, Arrays, and Dictionaries must be initialized at startup with custom capacities:
  ```csharp
  private List<LODGroup> cachedGroups = new List<LODGroup>(256);
  ```
- **Non-allocating APIs**: Always use non-allocating alternatives when querying physical objects:
  - Use `Physics.OverlapSphereNonAlloc` instead of `Physics.OverlapSphere`.
  - Use `GetComponentsNonAlloc` instead of `GetComponents`.

---

## Job System & Burst Compiler

Heavy mathematical calculations (e.g. calculating mesh distances, grouping coordinates, analyzing bounding box intersections) should be offloaded to worker threads:
- Implement jobs using the `IJobParallelFor` interface.
- Enable the `[BurstCompile]` attribute to compile C# code directly into high-performance native machine instructions.
- Ensure all structures inside Job definitions use `NativeArray` with appropriate allocation lifetimes (`Allocator.Persistent` or `Allocator.TempJob`).

---

## Rendering Frame Budgets

Optimizations are budgeted to ensure they do not exceed frame times:
| Platform | Target FPS | Total Frame Budget | Optimizer CPU Budget |
| :--- | :--- | :--- | :--- |
| Standalone Mobile VR (Quest) | 72 FPS | 13.8ms | < 0.2ms |
| High-End Desktop (RTX 4090) | 144 FPS | 6.9ms | < 0.1ms |
| Console (PS5/Xbox) | 60 FPS | 16.6ms | < 0.25ms |
