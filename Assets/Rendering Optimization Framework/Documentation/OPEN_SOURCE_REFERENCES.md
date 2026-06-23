# Open Source References & Inspirations

This document compiles the libraries, GitHub repositories, documentation pages, and articles that inspired the architecture of the AAAOptimizer framework.

---

## Mesh Simplification
- **UnityMeshSimplifier** by *Whinarn*  
  - *Link*: [GitHub Repo](https://github.com/Whinarn/UnityMeshSimplifier)  
  - *Inspiration*: Decimation algorithms using quadric error metrics. We wrap this library under `USE_UNITY_MESH_SIMPLIFIER` toggles to support advanced skin-mesh and static decimation in production projects.

## Impostors
- **Amplify Impostors** by *Amplify Creations*  
  - *Link*: [Asset Page](https://amplify.pt/unity/amplify-impostors/)  
  - *Inspiration*: Shape-preserving depth billboard structures and octahedral projection mappings.

## Mesh Baker & Mesh Combiner
- **Mesh Baker** by *Digital Opus*  
  - *Link*: [Asset Page](https://digitalopus.ca/site/mesh-baker-lod/)  
  - *Inspiration*: Mesh combining pipelines, material index mapping, and remapping vertex UVs to pack textures.

## Asset Streaming & Addressables
- **Unity Addressables Package**  
  - *Link*: [Unity Documentation](https://docs.unity3d.com/Packages/com.unity.addressables@latest)  
  - *Inspiration*: Async asset loading hooks, resource handles release cycles, and catalog allocations.

## World Streaming & Partition
- **Unreal Engine World Partition & HLOD**  
  - *Link*: [Unreal Engine Documentation](https://dev.epicgames.com/documentation/en-us/unreal-engine/hierarchical-level-of-detail-in-unreal-engine)  
  - *Inspiration*: Grid partitioning, spatial cell streaming hierarchies, and proxy level swaps.
