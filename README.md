# MandatoryRCS

This plugin revisit the balance between the overpowered reaction wheels and RCS thrusters which are useless outside of docking situations. It does not propose a more realistic simulation of reaction wheels but is collection of tweaks aimed at limiting their functions and balancing the gameplay. Reaction wheels are turned into stabilizers, preventing your vessel to spin out of control and keeping it pointed at the direction you choose in the SAS autopilot. But you can't use them to initiate a rotation, this mean that you always need a few RCS thrusters to be able to control your vessel orientation.

## Features

#### Reaction wheels nerf
- No torque provided on pilot or SAS rotation requests.
- Full torque when SAS "Stability mode" is turned on.
- Full torque when the vessel is pointed toward the SAS selection.
- Torque output is affected by the vessel angular velocity : the faster the vessel rotate, the weaker reaction wheels are.
- Hiding of irrelevant reactions wheels right-click menu options.

#### Rotation persistence trough timewarp and reloading
- Timewarping will not stop the vessel from rotating.
- Rotation is restored after timewarping, switching vessels or reloading.
- Rotation is not continuously calculated for unloaded (on rails) vessels, for minimal performance impact.

#### SAS autopilot persistence trough timewarp and reloading
- The vessel will keep its orientation toward the SAS selection when timewarping, switching vessels or reloading.
- The SAS selection is remembered and restored when switching vessels or reloading.

## Instructions & notes

#### Requirements
The plugin **requires the ModuleManager plugin** to work. You can download it [here](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-121-module-manager-275-november-29th-2016-better-late-than-never/)

#### Installation
Nothing special, drop the "MandatoryRCS" folder in your "GameData" folder.

#### Incompatibilities
- [(Semi-)Saturatable Reaction Wheels](https://github.com/Crzyrndm/RW-Saturatable) - May work but will probably mess up the reaction wheels. Features overlap anyway so there is no point keeping it.
- [Persistent Rotation](https://github.com/MarkusA380/PersistentRotation) - Does more or less the same thing that this plugin. Bad thing will happen if you keep this one.

#### Recommandations
- [RCS Build Aid](https://github.com/m4v/RCSBuildAid) ([Forum post](http://forum.kerbalspaceprogram.com/index.php?/topic/33124-12-rcs-build-aid-v091/)) - Editor plugin to help you place your RCS thrusters efficiently.
- [RLA StockAlike](https://github.com/deimos790/RLA_Continued) ([Pictures](https://imgur.com/a/xJFxC)) - A light part packs featuring (among other things) some super useful RCS thrusters, monopropellant tanks and engines.

## Thanks
@MarkusA380 for figuring out how to make vessels rotate, you saved me a lot of time !
The whole KSP community for its awesomeness !

## Licensing
This masterful work of art is released under the [unlicense](http://unlicense.org/). So public domain, feel free to do anything, especially updating this plugin if I'm not around.

## Changelog and bugs

#### Known bugs and glitches
- Irrelevant reaction wheels action group options are still present

#### v1.0-pre2 for KSP 1.2.2
- (feature) The torque output from reaction wheels is now affected by the vessel angular velocity : the torque output decrease when the angular velocity increase, down to a minimum of 5% when the angular velocity reach 45° / second.
- (bugfix) Fixed reaction wheels providing a bit of torque when switching SAS from stability assist mode to a target hold mode after loading a vessel (fixed by forcing module desactivation every fixedupdate)
- (bugfix) Fixed SAS overshooting its target when using RCS (Fixed by explicitly setting reaction wheels torque to 0 when the module is disabled)
- (bugfix) Unexpectedly fixed vessels getting some roll velocity when loading / getting out of timewarp, probably because of one of the above fix

#### v1.0-pre1 for KSP 1.2.2
Initial release

## Perhaps planned features

#### Ingame settings in the difficulty options menu

#### Modification of the reaction wheels nerf
- Make reaction wheels able to "help" RCS thrusters by providing torque when they are in use, lowering the RCS fuel consumption.

#### An (optional) part set of RCS thrusters, MP tanks and orbital MP engines
- Decrease of stock MP tanks dry mass
- Slight increase of the ISP of MP engines and RCS blocks
- Monopropellant tanks from RLA Stockalike
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
