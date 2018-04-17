# MandatoryRCS

This plugin is an overhaul of KSP attitude control. It revisit the stock balance between the overpowered reaction wheels and RCS thrusters which are useless outside of docking situations. It does not propose a more realistic simulation of reaction wheels but is a collection of tweaks aimed at limiting their functions and balancing the gameplay. 

Reaction wheels are turned into stabilizers, preventing your vessel to spin out of control and keeping it pointed at the direction you choose in the SAS autopilot. But they have a very low (and somewhat realistic) torque power when the pilot or the SAS request a pitch, roll or yaw rotation.

This mean that like in real life, most crafts always need a few RCS thrusters (and propellant) to have orientation authority, but you still get most of the playability benefits of overpowered reaction wheels.

It also completly override the stock SAS code, using the much better PID controller coming straight from the well-known MechJeb plugin. Along with this comes a few new SAS modes and some other authority control features in a nice non-invasive UI that is as much stockalike as possible.

As a side benefit, the plugin also fix the "timewarps rotation stop" stock behaviour, and introduce a few related features like keeping the vessel oriented toward the SAS selection in timewarps and when switching vessels / reloading the game.

## Features

#### Reaction wheels nerf
- Reaction wheels have two different torque power, the stock one and a heavily nerfed, somewhat realistic one.
- Nerfed torque provided on pilot or SAS rotation requests.
- Stock torque when SAS "Kill rotation" is turned on.
- Stock torque when the vessel has reached the SAS selection (prograde, normal, target, etc).
- Hiding of irrelevant reaction wheels right-click menu options and action groups.

#### New SAS modes, UI and PID controller
- Custom SAS UI with a stockalike look and feel
- Four new SAS modes : kill rotation, fly by wire, parallel/antiparallel to target
- SAS is able to control the roll attitude (not recommended for atmospheric flight)
- Option to tweak the SAS aggressivity by limiting the maximum angular velocity
- Option to use the Sun as target
- RCS auto mode : RCS is auto-toggled on and off according to the needs of the pilot and the SAS.
- Implentation of the MechJeb PID controller instead of the stock one.

#### Rotation persistence trough timewarp and reloading
- Timewarping will not stop the vessel from rotating.
- Rotation is restored after timewarping, switching vessels or reloading.
- Rotation is not continuously calculated for unloaded (on rails) vessels, for minimal performance impact.

#### SAS autopilot and target persistence trough timewarp and reloading
- The vessel will keep its orientation toward the SAS selection when timewarping, switching vessels or reloading.
- The SAS selection is remembered and restored when switching vessels or reloading.
- The selected target is remembered and restored when switching vessels or reloading.

#### Customization
- Features can be enabled, disabled or tweaked in the ingame "Difficulty Settings" menu.

## Download & source

I highly recommend that you grab it from **CKAN** !

But you can also get the [latest release and source](https://github.com/gotmachine/MandatoryRCS/releases/latest) from github

## Instructions & notes

### SAS user manual

**Reaction wheels lock**

![](https://raw.githubusercontent.com/gotmachine/MandatoryRCS/dev-features/Showcase/UI%20Showcase/YELLOWLOCK.png)

The background color of the selected mode will change depending on the reaction wheels state. When the background is yellow, this mean that the reaction wheels are not yet locked on the required direction and or providing a very low, semi-realistic torque. When the background turn green, the reaction wheels have acquired a lock and will provide theire full torque power. It also mean that the SAS direction will be kept trough timewarps and reloads.

**Roll control**

![](https://raw.githubusercontent.com/gotmachine/MandatoryRCS/dev-features/Showcase/UI%20Showcase/ROLLLOCK.png)

Clicking on the middle button will enable **Roll Lock**. This force the SAS to hold the roll attitude relative to a predefined reference. Each click on the left and right buttons will offset this roll attitude by 45°, and the middle marker will update its icon to refelct that. The roll references depend on the current mode and on the selected navball context :
- Orbit/Surface context :
  - In RadialIn/RadialOut modes, the reference is the main body north direction.
  - In other modes, the reference is the radial out direction. This will keep your vessel in the same orientation relative the main body horizon.
- Target context :
  - In Parallel/AntiParallel modes if a body is selected, the refrence is the body "east" direction.
  - In other cases, the reference is the target north direction for bodies, and the radial direction for vessels and parts (exact direction depend on the part/vessel).
  
A few things to note :
- Roll lock is disabled in the Kill Rotation mode.
- Roll lock will disable itself if your vessel is near aligned with the roll reference. This is to prevent a sudden and nasty 180° roll turn when going from one side of the reference to the other side.

| **Modes** | |
|:---:|---|
|![](https://raw.githubusercontent.com/gotmachine/MandatoryRCS/dev-features/Showcase/UI%20Showcase/KILLROT.png)  | **Kill rotation :** In this mode, the SAS only action is to counteract any angular velocity, it does not try to hold an attitude. The most efficient mode for RCS fuel consumption, and also the one where your reaction wheels will help you the most.|
|![](https://raw.githubusercontent.com/gotmachine/MandatoryRCS/dev-features/Showcase/UI%20Showcase/FLYBYWIRE.png)  | **Fly By Wire :** This mode is whole new way of controlling your vessel. When activated, a green marker will appear on the navball at your current attitude. As long as the Fly By Wire mode stays activated, your pitch/yaw input (using the WASD keys) will no longer directly control your vessel but will instead move this marker on the navball. The SAS will then control your vessel to align it with the marker. Note that if you click again on the Fly By wire SAS button, your current attitude will be registered.
|![](https://raw.githubusercontent.com/gotmachine/MandatoryRCS/dev-features/Showcase/UI%20Showcase/PARALLEL.png)  | **Parallel / Antiparallel :** Only available when the "target" navball context is selected, this mode will maintain your vessel parallel to you target. Great for docking, or for keeping your solar panels toward the Sun if used in conjunction with the roll lock mode.|
| **Options** | |
|![](https://raw.githubusercontent.com/gotmachine/MandatoryRCS/dev-features/Showcase/UI%20Showcase/SASLIMIT.png)  | **SAS Aggressivity :** Clicking on this button will cycle the SAS settings from a low to high angular velocity limit. A lower limit will make the SAS turn less quickly, improving precision and lowering RCS fuel consumption when in space. A higher limit may be usefull in atmospheric flight.|
|![](https://raw.githubusercontent.com/gotmachine/MandatoryRCS/dev-features/Showcase/UI%20Showcase/TARGETSUN.png)  | **Target Sun :** Allow you to set the Sun as your target. Alongside with roll lock and the target context, this will allow you to keep those solar panels perfectly aligned with the Sun.|
|![](https://raw.githubusercontent.com/gotmachine/MandatoryRCS/dev-features/Showcase/UI%20Showcase/RCSAUTO.png)  | **RCS Auto :** In this mode, the RCS toggle is in the hands of the SAS. It will enable it when the pilot request a pitch/roll/yaw or translation command, and at the discretion of the SAS when reaction wheels aren't locked on their target|

### Installation and support

#### Requirements
This **requires the ModuleManager plugin** to work. You can download it [here](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-121-module-manager-275-november-29th-2016-better-late-than-never/)

#### Incompatibilities
- [(Semi-)Saturatable Reaction Wheels](https://github.com/Crzyrndm/RW-Saturatable) : Will conflict with the reaction wheels nerf.
- [Persistent Rotation](https://github.com/MarkusA380/PersistentRotation) :  ~~can still be used but the SAS and rotation persistence features will be automatically disabled.~~ Currently incompatible, ask for support and I may consider adding it.

#### Recommendations
- [RCS Build Aid](https://github.com/m4v/RCSBuildAid) ([Forum post](http://forum.kerbalspaceprogram.com/index.php?/topic/33124-12-rcs-build-aid-v091/)) - Editor plugin to help you place your RCS thrusters efficiently.
- [Better Burn Time](https://forum.kerbalspaceprogram.com/index.php?/topic/126111-141-betterburntime-v161-accurate-burn-time-indicator-on-navball-no-more-na/) - Great overhaul of the right side of the navball ;)

## Disclaimer
This is my first plugin and I'm far from a skilled programmer, so the code for this may be ugly. As far as I know, it does the job and doesn't break the game. However, keep in mind that *I don't really know what I'm doing*. If anybody has the time to review and comment my code, I'm open to suggestions and pull requests :)

## Thanks
@MarkusA380 for figuring out the basics of persistent rotation.
@Sarbian and others contributors to MechJeb for the attitude PID controller.

The whole KSP community for its awesomeness !

## Licensing
Due to the integration of MechJeb-derived code, this plugin is released with mixed licensing.
The plugin as a whole is released under the [unlicense](http://unlicense.org/), meaning public domain, meaning do as you wish.
Individual source files contains a header indicating their license situation. 
Most files are released in the public domain, at the exception of the following source files that contains code derived from the MechJeb plugin and are licensed under the GNU General Public License v3.0 :
- ComponentSASAutopilot.cs
- Lib\MathExtensions.cs
- Lib\Vector6.cs
- Lib\VesselPhysics.cs

## Changelog and bugs

#### Known bugs and glitches
- None yet !

#### v2.0 beta 1 for KSP 1.4.2
Beta release with extra logging of debug information the the KSP.log
- Complete rewrite of the whole plugin
- The reaction wheels nerf has been simplified, control variations are no more since this was confusing and not very relevant
- Target persistence
- Stock SAS PID controller is replaced by a MechJeb derived PID-controller
- Complete in-house reimplementation of the stock SAS UI, with new modes and simple options within the UI.
- Added a way to target the Sun
- Added a RCS-auto mode 
- Many bugs !

#### v1.5 for KSP 1.4.1
- Recompiled for KSP 1.4.1
- (bugfix) Fixed NRE on asteroids changing SOI
- (bugfix) Fixed NRE on planting flags

#### v1.4 for KSP 1.3.1
- Recompiled for KSP 1.3.1
- Tweaks to the settings menu

#### v1.3 for KSP 1.3.0
- Recompiled for KSP 1.3.0 (thx Linuxgurugamer)
- (bugfix) Fixed typo in settings (issue #1)

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

- Reaction wheels overhaul with saturation simulation, automatic RCS desaturation, and possibly other means of attitude keeping like magnetorquers.
- Integration with Better Burn Time to provide a basic maneuver node executor autopilot.
  
#### Realism notes
  
The way reaction wheels work with this plugin isn't realistic. This said, in lowering their torque output to realistic values for maneuvering, this plugin make the playstyle a lot closer to the reality without cutting too much on playability. In real life, reaction wheels and control moment gyroscopes (CMR) can provide only very small amounts of torque. For example, each CMR on the ISS is rougly 1.2 meter wide, weight about 280 kg ([source](http://www.boeing.com/assets/pdf/defense-space/space/spacestation/systems/docs/ISS%20Motion%20Control%20System.pdf)) and provide only 0.258 kNm of torque ([source](https://ntrs.nasa.gov/archive/nasa/casi.ntrs.nasa.gov/20100021932.pdf)). Now compare that to the 5 kNm provided by the 0.625m, 50 kg small reaction wheel in KSP.
  
