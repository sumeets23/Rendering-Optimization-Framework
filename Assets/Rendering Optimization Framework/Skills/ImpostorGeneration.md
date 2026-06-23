# Skill: Impostor Generation

## 1. Purpose
This skill covers capturing 3D geometry from multiple directions into 2D billboard sprite sheets, generating depth maps, and writing custom shaders to render them based on viewing angle.

## 2. Technical Overview
Impostors replace complex 3D meshes (like trees or distant buildings) with a simple 2-triangle billboard. By capturing the object from multiple angles and selecting the appropriate frame at runtime, the illusion of a 3D shape is maintained.

## 3. Unity APIs
- `UnityEngine.Camera.Render`
- `UnityEngine.RenderTexture`
- `UnityEngine.Texture2D.ReadPixels`

## 4. Rendering Concepts
- **Hemispherical Capture:** Positioning camera viewpoints over a dome centered on the object.
- **Normal Mapping Impostors:** Storing world-space normal vectors in a capture texture to enable dynamic billboard lighting.
- **Depth Impostors:** Storing camera depth values to allow proper scene intersections and shadow casting.

## 5. Optimization Strategies
- Capture objects from 8 or 16 horizontal angles.
- Pack color and depth into one atlas texture, and normal and smoothness into another.
- Implement a custom shader that checks the dot product of the camera view direction and the billboard forward vector to index the texture atlas sheet.

## 6. Runtime Constraints
- Swapping between the full 3D mesh and the impostor billboard can cause visual popping. Use transparency clipping fade or cross-fading to smooth the transition.

## 7. Memory Constraints
- An atlas sheet of 8x8 views at 256x256 pixels requires a 2048x2048 texture. Limit capture resolution to 128x128px per frame for small objects.

## 8. VR Constraints
- In VR, flat billboards suffer from stereo disparity (the user's eyes detect that the object is flat). Impostors must only be used at far distances where stereo disparity becomes negligible.

## 9. HDRP Notes
- Ensure depth outputs from the impostor shader write directly to the depth buffer (`SV_Depth`) if displacement/intersection is needed.

## 10. URP Notes
- Use simplified pixel shader operations. For mobile VR, disable depth writing from impostor shaders if performance is bound by fill rate.

## 11. Best Practices
- Render captures with orthographic projection cameras to eliminate perspective distortion in the sprite sheet.
- Clean transparency alpha edges to avoid halo or black border artifacts.

## 12. Common Pitfalls
- Capturing object normals in tangent space rather than world space, leading to incorrect lighting rotations as the camera rotates.

## 13. Performance Targets
- Triangle count reduction: Reduce 10,000+ triangles to 2 triangles.
- Vertex count reduction: 99.9% reduction per model.
