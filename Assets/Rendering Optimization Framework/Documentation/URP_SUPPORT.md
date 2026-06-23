# URP Support & Optimization

Universal Render Pipeline (URP) is designed to scale across multiple platforms, from mobile VR (Oculus Quest) to mid-range consoles and desktop computers. This guide focuses on URP-specific optimizations.

---

## URP Optimization Pillars

### 1. SRP Batcher Mappings
Unlike HDRP, which always uses the SRP Batcher, URP materials must be checked:
- Ensure all custom shaders store color, offset, and textures in the `UnityPerMaterial` constant buffer.
- Flag non-compatible materials in the Scene Analyzer.

### 2. Lightweight Shaders
- Mobile URP benefits from using simplified shading models.
- The framework supplies default lightweight shader templates (built via Shader Graph) for custom items, ensuring low ALU counts.
- Keep alpha clipping and transparency passes minimized to optimize mobile GPUs (tiled renderers struggle with alpha blending overdraw).

### 3. Compressed Texture Formats
- Mobile VR mandates ASTC compression (Adaptive Scalable Texture Compression).
- The Texture Streaming analyzer flags non-ASTC textures on mobile platforms.

---

## URP Specific Presets

- **Mobile VR Profile**: Minimal draw calls, disabled real-time shadows on tiny props, ASTC compression, active FFR.
- **Scalable Desktop Profile**: Enables soft shadows, high-quality mips, and basic post-processing features (bloom, vignette).
