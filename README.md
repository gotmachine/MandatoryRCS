# MandatoryRCS

This plugin revisit the stock balance between the overpowered reaction wheels and RCS thrusters which are useless outside of docking situations. It does not propose a more realistic simulation of reaction wheels but is a collection of tweaks aimed at limiting their functions and balancing the gameplay. 

Reaction wheels are turned into stabilizers, preventing your vessel to spin out of control and keeping it pointed at the direction you choose in the SAS autopilot. But they have a very low (and somewhat realistic) torque power when the pilot or the SAS request a pitch, roll or yaw rotation.

This mean that like in real life, most crafts always need a few RCS thrusters (and propellant) to have orientation authority, but you still get most of the playability benefits of overpowered reaction wheels.

As a side benefit, the plugin also fix the "timewarps rotation stop" stock behaviour, and introduce a few related features like keeping the vessel oriented toward the SAS selection in timewarps and when switching vessels / reloading the game.

## Features

#### Reaction wheels nerf
- Reaction wheels have two different torque power, the stock one and a heavily nerfed, somewhat realistic one.
- Nerfed torque provided on pilot or SAS rotation requests.
- Stock torque when SAS "Stability mode" is turned on.
- Stock torque when the vessel has reached the SAS selection (prograde, normal, target, etc).
- Torque output is affected by the vessel angular velocity : the faster the vessel rotate, the weaker reaction wheels are.
- Pods and cockpits built-in reaction wheels can't be controlled (they don't respond to pilot/SAS input) but still provide SAS stabilization.
- Reaction wheels in probes cores and independent parts can be controlled.
- Hiding of irrelevant reaction wheels right-click menu options and action groups.

#### Rotation persistence trough timewarp and reloading
- Timewarping will not stop the vessel from rotating.
- Rotation is restored after timewarping, switching vessels or reloading.
- Rotation is not continuously calculated for unloaded (on rails) vessels, for minimal performance impact.

#### SAS autopilot persistence trough timewarp and reloading
- The vessel will keep its orientation toward the SAS selection when timewarping, switching vessels or reloading.
- The SAS selection is remembered and restored when switching vessels or reloading.

#### Customization
- Features can be enabled, disabled or tweaked in the ingame "Difficulty Settings" menu.

## Instructions & notes

#### Download & source

I highly recommend that you grab it from **CKAN** !

But you can also get the [latest release and source](https://github.com/gotmachine/MandatoryRCS/releases/latest) from github

#### Requirements
This **requires the ModuleManager plugin** to work. You can download it [here](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-121-module-manager-275-november-29th-2016-better-late-than-never/)

#### Incompatibilities
- [(Semi-)Saturatable Reaction Wheels](https://github.com/Crzyrndm/RW-Saturatable) : can still be used but the reaction wheels features will be automatically disabled.
- [Persistent Rotation](https://github.com/MarkusA380/PersistentRotation) : can still be used but the SAS and rotation persistence features will be automatically disabled.

#### Recommendations
- [RCS Build Aid](https://github.com/m4v/RCSBuildAid) ([Forum post](http://forum.kerbalspaceprogram.com/index.php?/topic/33124-12-rcs-build-aid-v091/)) - Editor plugin to help you place your RCS thrusters efficiently.
- [RLA StockAlike](https://github.com/deimos790/RLA_Continued) ([Pictures](https://imgur.com/a/xJFxC)) - A light part packs featuring (among other things) some super useful small RCS thrusters, monopropellant tanks and engines.

## Disclaimer
This is my first plugin and I'm far from a skilled programmer, so the code for this may be ugly. As far as I know, it does the job and doesn't break the game. However, keep in mind that *I don't really know what I'm doing*. If anybody has the time to review and comment my code, I'm open to suggestions and pull requests :)

## Thanks
@MarkusA380 for figuring out how to make vessels rotate, you saved me a lot of time !

The whole KSP community for its awesomeness !

## Licensing
This masterful work of art is released under the [unlicense](http://unlicense.org/). 

So public domain, feel free to do anything, especially updating this plugin if I'm not around.

## Changelog and bugs

#### Known bugs and glitches
- Getting out of timewarps with the SAS direction hold activated input a large roll "kick", most visible at high timewarp levels. I tried a lot of things to find out why this happen or fix it, and failed.
- When switching to an unloaded vessel with its SAS in "target", "antitarget" or "maneuver" mode, the orientation change is applied a few frames after the vessel is unpacked, leading to the rotation event being visible to the player. Won't fix as this is minor, purely cosmetic and fixing would require large modifications.

#### v1.2 for KSP 1.2.2
- (feature) Ingame settings menu with compatibility checks for SSRW and PR plugins. They can now be used alongside the plugin, incompatible features are auto-disabled.
- (feature) Torque output on pilot/SAS input is now very low instead of disabled.
- (feature) Torque output on pilot/SAS input is still disabled for all reaction wheels in manned parts. This can be overridden/customized trough a "isControllable" parameter available in the module config (see the default patch CFG for more about that).
- (improvement) In SAS target mode, reaction wheels provide torque only if they have closely reached the target first (less "magnet effect")
- (improvement) Refactored several things in ModuleTorqueController, now a bit less dirty.
- (improvement) SAS target hold in body-relative modes (pro/retrograde, radial, normal) is now disabled when the vessel SOI change.

#### v1.1 for KSP 1.2.2
- (bugfix) Fixed SAS orientation being applied when not reached on initiating timewarp (woops)
- (bugfix) Tweaked a few things to prevent the perpetual SAS roll overshoot.

#### v1.0 for KSP 1.2.2
- (feature) The torque output from reaction wheels is now affected by the vessel angular velocity : the torque output decrease when the angular velocity increase, down to a minimum of 5% when the angular velocity reach 45° / second.
- (bugfix) Fixed reaction wheels providing a bit of torque when switching SAS from stability assist mode to a target hold mode after loading a vessel (fixed by forcing module deactivation every fixedupdate)
- (bugfix) Fixed SAS overshooting its target when using RCS (Fixed by explicitly setting reaction wheels torque to 0 when the module is disabled)
- (bugfix) Irrelevant reaction wheels action groups options are now hidden

#### Perhaps planned features

- Reaction wheels saturation over time when landed.
- (Maybe) Make reaction wheels able to "help" RCS thrusters by providing torque when they are activated, lowering the RCS fuel consumption.
- A RCS thrusters part pack
  
#### Realism notes
  
The way reaction wheels work with this plugin isn't realistic. This said, in lowering their torque output to realistic values for maneuvering, this plugin make the playstyle a lot closer to the reality without cutting too much on playability. In real life, reaction wheels and control moment gyroscopes (CMR) can provide only very small amounts of torque. For example, each CMR on the ISS is rougly 1.2 meter wide, weight about 280 kg ([source](http://www.boeing.com/assets/pdf/defense-space/space/spacestation/systems/docs/ISS%20Motion%20Control%20System.pdf)) and provide only 0.258 kNm of torque ([source](https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/20100021932.pdf)). Now compare that to the 5 kNm provided by the 0.625m, 50 kg small reaction wheel in KSP.
  
