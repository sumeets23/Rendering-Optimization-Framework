# Impostor Colour Fix V8

Your material screenshot proves the shader is sampling an atlas, but the atlas assigned to the material preview is white silhouettes. That means the bake output itself is wrong or the wrong texture is assigned as the colour atlas.

## Correct diagnosis

If the material preview thumbnail is white:
- The issue is not tint or gamma.
- The issue is not just URP lighting.
- The impostor base texture is white/silhouette, not the final brown colour.

## Fix path

1. Add `AAAImpostorUnlitAtlasCutoutFixed.shader` to your project.
2. Use it on the impostor material.
3. Inspect the generated atlas PNG:
   - If it is white silhouettes, the baker is writing alpha/silhouette into the albedo atlas.
   - If it is brown chair frames, material assignment was wrong.
4. Use `ImpostorFinalColorCaptureUtility.BuildAlbedoFromWhiteBlack()` in your baker to build the albedo atlas from white/black captures.

## Why white/black capture solves it

Transparent RT capture is unreliable for final colour with alpha in Unity pipelines. Many objects render wrong RGB when alpha is transparent. White/black background reconstruction gives:
- correct alpha
- correct straight RGB colour
- no white halo
- no silhouette-only atlas

## Key formula

alpha = 1 - (whiteRGB - blackRGB)
colour = blackRGB / alpha

This reconstructs the real object colour.
