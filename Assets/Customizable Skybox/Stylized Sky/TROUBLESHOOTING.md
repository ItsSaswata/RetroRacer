# Skybox Shader with Planets - Troubleshooting Guide

## Common Issues and Solutions

### Shader Compilation Errors

#### Missing Texture References
**Issue**: Errors about missing textures when the shader compiles.

**Solution**: 
- Ensure all planet textures are assigned in the material inspector
- If you don't want to use all planets, you can use simple placeholder textures (like a white texture) for unused planets
- Unity's default "white" texture can be used as a placeholder: `"white" {}`

#### Precision Issues
**Issue**: Visual artifacts or precision errors in planet rendering.

**Solution**:
- Try changing shader precision from `fixed` to `half` or `float` for better precision
- Adjust planet sizes to avoid extremely small values that might cause precision issues

### Visual Issues

#### Planets Not Visible
**Issue**: Planets are not appearing in the skybox.

**Solution**:
- Verify that `_EnablePlanets` is set to 1 (enabled)
- Check planet positions to ensure they're in the visible area of the skybox
- Increase planet sizes if they're too small to be visible
- Ensure planet textures are properly assigned

#### Planets Look Distorted
**Issue**: Planets appear stretched or distorted.

**Solution**:
- Ensure planet textures are in equirectangular projection format
- Check that texture aspect ratio is 2:1 (width:height)
- Adjust UV mapping in the shader if necessary

#### Z-Fighting Between Planets
**Issue**: Planets appear to flicker when overlapping.

**Solution**:
- Adjust planet positions to avoid overlap
- Modify the blending order in the shader code if necessary

## Performance Optimization

### Reducing GPU Load

1. **Texture Resolution**: Use lower resolution textures for planets (1024×512 is often sufficient)

2. **Disable Unused Planets**: If you don't need all planets, modify the shader to skip rendering unused ones

3. **Simplify Saturn Rings**: The ring rendering is more complex - simplify or remove if performance is an issue

4. **LOD System**: Consider implementing a simple LOD system that reduces planet detail at a distance

### Memory Optimization

1. **Texture Compression**: Use appropriate texture compression in Unity's import settings

2. **Shared Textures**: For similar planets, consider reusing textures with different tints

3. **Mipmap Settings**: Disable mipmaps for planet textures if they're always viewed at a fixed size

## Advanced Customization

### Adding More Planets

To add additional planets:

1. Add new properties in the `Properties` block
2. Declare corresponding variables in the CGPROGRAM section
3. Create a new rendering function similar to the existing planet functions
4. Add the new planet to the blending sequence in the fragment shader

### Custom Effects

Some effects you can add:

1. **Atmospheric Glow**: Add a rim effect to planets for atmospheric glow
2. **Cloud Layers**: Add a second texture layer for clouds with independent rotation
3. **Time-Based Events**: Create special effects like eclipses based on time

## Contact and Support

If you encounter issues not covered in this guide, please check the Unity forums or contact the asset developer for assistance.