# Impostor Billboard System

The Impostor system replaces complex 3D meshes that are far from the player with flat 2-triangle billboards. By rendering the target asset from multiple angles at edit-time and storing the results in a texture atlas, the system dynamically renders the correct view depending on the camera's orientation.

---

## Technical Concept

```
     [Camera Views]
        \  |  /
      o--o--o--o--o  (Angles)
      |  Target   |
      o--o--o--o--o
            |
    [Baked Texture Atlas]
     +---+---+---+---+
     | 1 | 2 | 3 | 4 |
     +---+---+---+---+
     | 5 | 6 | 7 | 8 |
     +---+---+---+---+
            |
    [Runtime Billboard]
   Calculates view angle ->
   Selects UV frame ->
   Dynamic lighting (normals)
```

---

## Key Components

### 1. Impostor Baker (`ImpostorBaker.cs`)
- Deploys an orthographic camera centered on the target asset.
- Rotates either the camera or the asset across 8, 16, or octahedral coordinates.
- Captures colors, depth values, and world-space normal vectors.
- Assemblies them into:
  - **Base Map**: Color (RGB) + Transparency/Alpha Clip (A)
  - **Normal Map**: World Space Normal (RGB) + Depth offset (A)

### 2. Billboard Shader Graph
The impostor shader uses vertex math to rotate the quad mesh to billboard-face the camera position. In the pixel shader:
- It computes the relative angle vector: `Angle = atan2(ViewDir.z, ViewDir.x)`.
- It maps the angle to a discrete grid index corresponding to the texture atlas columns and rows.
- It interpolates between the two nearest frames to prevent sharp jumps when rotation changes.
- It writes the packed depth value back to the depth buffer to handle intersections with the environment.

---

## Features
- **Depth Impostors**: Allows correct intersection with ground surfaces and other meshes.
- **Normal-map Lighting**: World-space normal captures ensure that shadows, specular shines, and lighting direction dynamically update on the billboard.
- **Cross-fade Transitions**: Smooth dithering fade when transitioning between the 3D model and the billboard mesh.
