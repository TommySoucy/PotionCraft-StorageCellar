# PotionCraft-StorageCellar

A mod for Potion Craft that adds a new room in which minerals can grow

This custom room is below the garden and to the right of the basement. Every day, there is a small chance of minerals spawning in different spots inside the room.

## Installation

1. Download the latest BepinEx package corresponding to your operating system from [here](https://github.com/BepInEx/BepInEx/releases) and extract all files from the zip into your Potion Craft installation
2. Run the game once for BepinEx to generate its file system
3. Download and install latest [Custom Rooms release](https://github.com/TommySoucy/PotionCraft-StorageCellar/releases) as defined [here](https://github.com/TommySoucy/PotionCraft-CustomRooms)
4. Download latest of this mod from [releases](https://github.com/TommySoucy/PotionCraft-StorageCellar/releases)
5. Put all contents from the .zip of this mod into BepinEx/Plugins folder

## Config

NOTE: The config for this mod can be found in Rooms/StorageCellar.txt

The following config settings are available:

- **_spawnChancePerGrowingSpot_**: At each day start, each spot has spawnChancePerGrowingSpot probability to spawn a random mineral. (0.05 by default)

- **_mineralScale_**: A multiplier for the size of the minerals. They are small by default.

## Building

1. Clone repo
2. Open solution
3. Ensure all references are there
4. Build
5. DLL is now ready for install as explained in **Installation** section

## Used libraries

- Harmony
- BepinEx
