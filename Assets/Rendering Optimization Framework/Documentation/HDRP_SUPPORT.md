# HDRP Support & Optimization

High Definition Render Pipeline (HDRP) is built for high-fidelity desktop and console graphics. To optimize HDRP effectively, we focus on reflection probes, volumetric lights, ray-traced shadows, and streaming presets.

---

## HDRP Optimization Pillars

### 1. Volumetric & Fog Settings
Volumetric lighting is highly fill-rate intensive. The framework provides optimization presets:
- **Cinematic (Cinematic)**: High resolution volumetrics, ray-marching enabled, long distances.
- **Performant (Balanced)**: Half resolution volumetrics, reduced slice counts, shorter light range.
- **VR/Fast (Performant)**: Quarter resolution volumetrics or simple fog approximations.

### 2. Reflection Probe Budgets
Real-time reflection probes can tank frame rate. The framework enforces:
- Caching probe captures rather than rendering them every frame (individual updates over time).
- Downscaling probe resolutions (e.g. max 256x256 or 512x512).
- Restricting probe count per cluster.

### 3. Ray Tracing & Shadow Budgets
On RTX-class hardware, ray-traced shadows and ambient occlusion must be configured:
- Disable RT reflections for distant meshes.
- Switch distant meshes to simple orthographic shadow proxies.
- Dynamic resolution scaling integrates with HDRP's pipeline config.

---

## HDRP Specific Presets

The framework stores HDRP quality configurations:
- **Balanced Profile**: Suitable for digital twins and normal desktop setups.
- **High Fidelity Profile**: Maximizes visual qualities, disables aggressive mip reduction, and enables ray tracing.
- **Performant Profile**: Reduces reflection resolutions, volumetric slice steps, and caps texture mip levels.
