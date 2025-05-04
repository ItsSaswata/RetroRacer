# Enhanced Stylized Sky Shader with Planets

## Overview
The Stylized Sky shader has been enhanced to include realistic planets alongside the existing stars. This creates a more immersive outer space environment for your game.

## Features Added
- Support for multiple planets (Earth, Jupiter, Saturn with rings, and Mars)
- Realistic planet textures with proper mapping
- Customizable planet positions, sizes, and rotation speeds
- Saturn with special ring rendering
- Toggle to enable/disable planets

## How to Use

### Required Textures
You'll need to assign texture maps for each planet in the Material Inspector:

1. **Earth Texture**: A texture map of Earth showing continents, oceans, and clouds
2. **Jupiter Texture**: A texture map showing Jupiter's distinctive bands and the Great Red Spot
3. **Saturn Texture**: A texture map for Saturn's body
4. **Saturn Ring Texture**: A texture map for Saturn's rings
5. **Mars Texture**: A texture map showing Mars' reddish surface and polar caps

### Recommended Texture Sources
You can find high-quality planet textures from:
- NASA's public domain images (https://www.nasa.gov/multimedia/imagegallery/)
- Solar System Scope (https://www.solarsystemscope.com/textures/)
- Texture Haven or similar texture sites

### Adjusting Planet Properties
In the Material Inspector, you can customize:

- **Enable Planets**: Toggle to show/hide all planets
- **Planet Position**: Vector3 position in the skybox (values between -1 and 1)
- **Planet Size**: Controls the apparent size of each planet
- **Rotation Speed**: Controls how fast each planet rotates
- **Saturn Ring Size**: Controls the size of Saturn's rings relative to the planet

## Performance Considerations
The shader is optimized for performance, but adding multiple planets with high-resolution textures may impact performance on lower-end devices. If you experience performance issues:

1. Use lower resolution planet textures
2. Reduce the number of visible planets
3. Simplify the Saturn ring rendering

## Troubleshooting
If planets aren't visible:
- Ensure planet textures are assigned
- Check that planet positions are within the visible area of the skybox
- Verify that planet sizes are large enough to be visible
- Make sure "Enable Planets" is toggled on

Enjoy your enhanced space skybox!