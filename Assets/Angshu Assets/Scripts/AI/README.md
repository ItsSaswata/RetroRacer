# AI Racing System for RetroRacer

## Overview
This AI racing system allows AI-controlled vehicles to race around procedurally generated tracks. The system consists of several components that work together to create a realistic racing experience.

## Components

### 1. Racing Line Generator
The `RacingLine` class generates an optimal racing line for AI vehicles to follow. This is separate from the track's mesh generation spline and is specifically designed for racing.

- Automatically calculates corner cutting based on track geometry
- Determines appropriate speeds for different sections of the track
- Provides methods for AI to find and follow the racing line

### 2. AI Vehicle Controller
The `AIVehicleController` component makes vehicles follow the racing line with realistic driving behavior.

- Inherits from the existing `SimcadeVehicleController`
- Calculates steering, acceleration, and braking based on racing line
- Includes collision avoidance with other vehicles
- Configurable difficulty settings (skill level and aggressiveness)

### 3. AI Race Manager
The `AIRaceManager` handles spawning and managing AI racers on the track.

- Spawns a configurable number of AI racers
- Positions them at the starting line with appropriate spacing
- Assigns varying difficulty levels to create more interesting races

## How to Use

### Setting Up a Track with AI Racers

1. Make sure your track has a `TrackGenerator` component with "Generate Racing Line" enabled
2. Add the `AIRaceManager` component to any GameObject in your scene
3. Assign a vehicle prefab to the "AI Vehicle Prefab" field
   - This prefab should have a `SimcadeVehicleController` component
4. Configure the number of AI racers and their spacing
5. Start the game - AI racers will be spawned automatically once the track is generated

### Configuring AI Difficulty

You can adjust the following parameters in the `AIRaceManager`:

- **Min/Max Skill Level**: Controls how well AI drivers follow the racing line and manage speed
- **Min/Max Aggressiveness**: Controls how aggressively AI drivers take corners and overtake

For individual AI vehicles, you can also adjust these parameters directly on the `AIVehicleController` component.

### Visualizing the Racing Line

In the `TrackGenerator` component, enable "Show Racing Line" to visualize the racing line in the editor. This can be helpful for debugging and understanding AI behavior.

## Implementation Notes

- The racing line is generated when the track is created and saved with the track prefab
- AI vehicles automatically find and follow this racing line
- The system works with both procedurally generated tracks and manually created tracks
- AI difficulty can be adjusted to create more or less challenging opponents

## Tips for Best Results

- Make sure your vehicle prefab has proper colliders to enable collision avoidance
- Adjust the "Corner Cutting Factor" in the TrackGenerator to control how aggressively AI cuts corners
- For better performance, consider reducing the number of AI racers on lower-end devices
- The "Lookahead Points" parameter on AIVehicleController can be adjusted to make AI more or less responsive to upcoming corners