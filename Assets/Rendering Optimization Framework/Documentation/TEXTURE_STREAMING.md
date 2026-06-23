# Texture Streaming System

The Texture Streaming System manages VRAM consumption by loading only the necessary mipmap levels of textures based on camera distance and active budgets.

---

## How It Works

1. **Mipmap Import Configurations**: All environmental textures are imported with "Streaming Mipmaps" checked. This separates the texture data into individual mip files inside Unity's asset pipeline.
2. **VRAM Tracker**: The system checks the graphics memory footprints:
   - `Texture.currentTextureMemory`: Currently allocated VRAM for textures.
   - `Texture.desiredTextureMemory`: Total texture memory that would be loaded if budgets were infinite.
3. **Priority & Capping Manager**:
   - Nearby renderers increase their associated materials' streaming priority.
   - For distant or out-of-frustum textures, the system forces a higher mip quality cap (e.g. discarding mips 0 and 1, forcing mip 2 or lower).
   - This releases physical VRAM on the GPU, avoiding paging stalls or out-of-memory crashes.

---

## Configuration Settings

- **VRAM Memory Budget**: Maximum allowed texture VRAM in megabytes (e.g. 512MB for Quest VR, 2048MB for Desktop performant).
- **Max Level of Reduction**: The maximum number of mipmaps that can be discarded for distant textures (e.g. limit to 4 to prevent textures from turning into single pixels).
- **Fade Speed**: Rate at which mip levels load in when the camera moves closer, preventing sharp popping.
- **Priority Rules**: Multipliers assigned based on object categories (e.g. Player = 2.0x, Props = 1.0x, Far Terrain = 0.5x).
