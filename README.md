# MandatoryRCS

This plugin revisit the stock balance between the overpowered reaction wheels and RCS thrusters which are useless outside of docking situations. It does not propose a more realistic simulation of reaction wheels but is collection of tweaks aimed at limiting their functions and balancing the gameplay. Reaction wheels are turned into stabilizers, preventing your vessel to spin out of control and keeping it pointed at the direction you choose in the SAS autopilot. But you can't use them to initiate a rotation, this mean that you always need a few RCS thrusters to be able to control your vessel orientation.

## Features
### Reaction wheels nerf
- Reaction wheels provide no torque on pilot or SAS rotation requests.
- Reaction wheels provide full torque when SAS "Stability mode" is turned on.
- Reaction wheels provide full torque when the vessel is pointed toward the SAS selection.

### Rotation persistence trough timewarp and reloading
- Timewarping will not stop the vessel from rotating.
- Rotation is restored after timewarping, switching vessels or reloading.
- Rotation is not continuously calculated for unloaded vessels.

### SAS autopilot persistence trough timewarp and reloading
- The vessel will keep its orientation toward the SAS selection when timewarping, switching vessels or reloading.
- The SAS selection is remembered and restored when switching vessels or reloading.

### A part set of RCS thrusters and monopropellant tanks
- Tiny "Stratus-style" radial MP tank
- Double-size inline MP tanks
- Micro RCS thrusters for probes and small vessels
- Perpendicular variation of the place-anywhere block
- Variations of the RV-105 block (angled, 2-way, 3-way)
- Aerodynamic blocks
- Larger blocks 
- High thrust LFO blocks

## Planned features
- Rewrite / overhaul of the reaction wheels nerf to make them able to "help" RCS trusters, lowering the RCS fuel consumption.
- A set of RCS trusters
