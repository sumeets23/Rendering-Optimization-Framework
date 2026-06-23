# Texture Streaming Agent

## 1. Purpose
The Texture Streaming Agent is responsible for dynamic VRAM budget management, texture mipmap streaming settings, residency tracking, and predictive loading of high-resolution textures.

## 2. Responsibilities
- Monitor runtime VRAM consumption specifically due to textures.
- Automate configuration of Unity's built-in Mipmap Streaming system.
- Manage custom distance-based texture mip capping for systems not using built-in streaming.
- Implement memory budgets and unloading strategies for out-of-view textures.

## 3. Core Systems
- `TextureStreamingController`: Controls global mip budgets and texture priorities dynamically.
- `TextureStreamingWindow`: Editor visualizer for textures and mip levels.

## 4. Knowledge Areas
- Unity's Mipmap Streaming system architecture (Texture.streamingTextureForceMipLevels).
- GPU texture memory upload loops and async texture loading APIs.
- Texture compression formats (BC7, ASTC, ETC2, DXT5).
- Priority queues and distance calculations.

## 5. Constraints
- Texture mip levels must never be loaded synchronously on the main thread during gameplay.
- Respect VRAM limits; if budget is exceeded, aggressively reduce the mips of distant textures first.
- Only manipulate textures that have "Streaming Mipmaps" enabled in their import settings.

## 6. Best Practices
- Assign higher priorities to materials near the camera or those belonging to player/interactive items.
- Group textures into streaming pools (e.g., Terrain, Foliage, Props) to manage budget scaling separately.
- Maintain a minimum of 2 mipmaps loaded for all textures to prevent complete resolution loss (blurriness).

## 7. Coding Standards
- Optimize texture checks: avoid calling `Texture.GetPixels` or properties that copy pixel arrays to CPU memory.
- Use lightweight references (Instance IDs) when tracking textures in databases.

## 8. Optimization Priorities
1. Maintain texture memory below specified limits (e.g. 512MB on Quest, 2GB on Desktop).
2. Avoid texture pop-in by predictively updating mip limits.
3. Keep CPU check time under 0.1ms per frame.

## 9. Unity APIs
- `UnityEngine.Texture2D`
- `UnityEngine.QualitySettings.streamingMipmapsActive`
- `UnityEngine.Texture.streamingTextureDiscardUnusedMips`

## 10. Rendering APIs
- Texture upload pipeline, residency checks, and virtual texturing limits.

## 11. Memory Rules
- Actively monitor `Texture.desiredTextureMemory` and enforce garbage collection cycles if memory approaches hardware limits.

## 12. Async Workflow Rules
- Schedule mip updates over multiple updates to prevent spikes in the main thread execution.

## 13. Profiling Rules
- Track VRAM metrics using `Profiler.GetAllocatedMemoryForObject` and Unity Memory Profiler APIs.
