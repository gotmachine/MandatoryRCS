# MandatoryRCS

This plugin revisit the balance between the overpowered reaction wheels and RCS thrusters which are useless outside of docking situations. It does not propose a more realistic simulation of reaction wheels but is collection of tweaks aimed at limiting their functions and balancing the gameplay. Reaction wheels are turned into stabilizers, preventing your vessel to spin out of control and keeping it pointed at the direction you choose in the SAS autopilot. But you can't use them to initiate a rotation, this mean that you always need a few RCS thrusters to be able to control your vessel orientation.

## Features

### Reaction wheels nerf
- Reaction wheels provide no torque on pilot or SAS rotation requests.
- Reaction wheels provide full torque when SAS "Stability mode" is turned on.
- Reaction wheels provide full torque when the vessel is pointed toward the SAS selection.

### Rotation persistence trough timewarp and reloading
- Timewarping will not stop the vessel from rotating.
- Rotation is restored after timewarping, switching vessels or reloading.
- Rotation is not continuously calculated for unloaded vessels, for minimal performance impact.

### SAS autopilot persistence trough timewarp and reloading
- The vessel will keep its orientation toward the SAS selection when timewarping, switching vessels or reloading.
- The SAS selection is remembered and restored when switching vessels or reloading.

## Notes

### Requirements
The plugin require ModuleManager to work. You can download it [here](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-121-module-manager-275-november-29th-2016-better-late-than-never/)

### Installation
Nothing special, drop the "MandatoryRCS" folder in your "GameData" folder.

### Incompatibilities
- [(Semi-)Saturatable Reaction Wheels](https://github.com/Crzyrndm/RW-Saturatable) - Partially realistic reaction wheels
- [Persistent Rotation](https://github.com/MarkusA380/PersistentRotation) - Bad thing will happen if you keep this one

### Licensing
This work of art is released under the [unlicense](http://unlicense.org/)

## Perhaps planned features

### Rewrite / overhaul of the reaction wheels nerf
- Reaction wheels can't be used to "snap" to a SAS selection, they provide torque only when the SAS selection has allready been reached.
- Make reaction wheels able to "help" RCS trusters by providing torque when they are in use, lowering the RCS fuel consumption.

### An (optional) part set of RCS thrusters and monopropellant tanks
- MonoPropellant tanks from RLA Stockalike
- Orbital MP engines from RLA Stockalike
- 0.25 kN RCS blocks, nozzle configurations :
  - 1x front
  - 1x down
  - 2x lateral
  - 2x lateral 45°
  - 2x lateral + 1x down
  - 2x lateral 45° + 1x down
- RV-105 block variations, nozzle configurations :
  - 1x down
  - 2x lateral
  - 2x lateral 45°
  - 2x lateral + 1x down
  - 2x lateral 45° + 1x down
  - 2x nozzles 45° + 1x down + 1x up
  - 2x lateral + 2x front
- 4 kN RCS blocks, nozzle configurations :
  - 1x front
  - 1x down (90° angle)
  - 2x lateral
  - 2x lateral 45°
  - 2x lateral + 2x front
- Aerodynamic 5-ways block
- Large RCS LFO blocks
