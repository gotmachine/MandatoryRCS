# MandatoryRCS
Plugin for Kerbal Space Program

## Features

### Reaction wheels nerf
- Reaction wheels provide no torque on pilot or SAS rotation requests.
- Reaction wheels provide full torque when SAS "Stability mode" is turned on.
- Reaction wheels provide full torque when the vessel is pointed toward the SAS selection.

### Rotation persistance trough timewarp and reloading
- Timewarping will not stop the vessel from rotating.
- Rotation is restored after timewarping, switching vessels or reloading.
- Rotation is not continuously calculated for unloaded vessels.

### SAS autopilot persistance trough timewarp and reloading
- The vessel will keep its orientation toward the SAS selection when timewarping, switching vessels or reloading.
- The SAS selection is remembered and restored when switching vessels or reloading.
