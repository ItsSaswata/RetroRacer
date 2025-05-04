# Planet Textures Guide

## Texture Requirements

To use the enhanced skybox shader with planets, you'll need to create or download texture maps for each planet. Here are the specifications for optimal results:

### Recommended Texture Formats
- **File Format**: PNG or JPG
- **Resolution**: 2048×1024 or 4096×2048 (equirectangular projection)
- **Color Space**: sRGB

## Texture Sources

### NASA Resources (Public Domain)
- [NASA Visible Earth](https://visibleearth.nasa.gov/)
- [NASA Solar System Exploration](https://solarsystem.nasa.gov/resources/)

### Other Free Resources
- [Solar System Scope](https://www.solarsystemscope.com/textures/)
- [Celestia Motherlode](http://www.celestiamotherlode.net/)

## Texture Setup in Unity

When importing planet textures into Unity:

1. Set **Texture Type** to "Default"
2. Enable **Generate Mip Maps**
3. Set **Wrap Mode** to "Repeat"
4. For best results, use **Compression** set to "High Quality"

## Planet-Specific Recommendations

### Earth
- Use a texture with visible continents, oceans, and cloud layers
- Consider a texture with a slight blue atmospheric glow at the edges

### Jupiter
- Look for textures showing the distinctive orange and white bands
- Ensure the Great Red Spot is visible

### Saturn
- For the planet body, use a texture with visible atmospheric bands
- For rings, use a separate texture with transparent background
- Ring texture should be a radial gradient from inner to outer rings

### Mars
- Use textures with the characteristic reddish-orange surface
- Look for textures showing polar ice caps and major surface features

## Creating Custom Planet Textures

If you want to create your own planet textures, consider using software like:
- Photoshop with equirectangular projection
- GIMP with appropriate plugins
- Specialized planet generators like SpaceEngine (for exports)

Remember that the shader uses equirectangular mapping, so your textures should be in this projection format for proper display on the spherical planets.