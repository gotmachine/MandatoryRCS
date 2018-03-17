using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MandatoryRCS
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class MandatoryRCSFlightEvents : MonoBehaviour
    {
        private bool vesselLoadOnSceneChange;

        private void Start()
        {
            GameEvents.onSetSpeedMode.Add(onSetSpeedMode);
            GameEvents.onVesselChange.Add(onVesselChange);
            GameEvents.onVesselStandardModification.Add(onVesselStandardModification);
            vesselLoadOnSceneChange = true;
        }

        // Detect active vessel changed by switching to an unloaded vessel (flight scene was rebuilt)
        private void FixedUpdate()
        {
            if (vesselLoadOnSceneChange && FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleRotation>().Count() > 0)
            {
                FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleRotation>().First().vesselSASHasChanged = true;
                vesselLoadOnSceneChange = false;
            }
        }

        // Update the total reaction wheels torque capacity for the vessel
        private void onVesselStandardModification(Vessel v)
        {
            if (v.vesselModules.OfType<VesselModuleRotation>().Count() > 0)
            {
                v.vesselModules.OfType<VesselModuleRotation>().First().updateWheelsTotalMaxTorque = true;
            }
        }

        // Detect active vessel change when switching vessel in the physics bubble
        private void onVesselChange(Vessel v)
        {
            if (v.vesselModules.OfType<VesselModuleRotation>().Count() > 0)
            {
                v.vesselModules.OfType<VesselModuleRotation>().First().vesselSASHasChanged = true;
            }
        }

        // Detect navball context (orbit/surface/target) changes
        private void onSetSpeedMode(FlightGlobals.SpeedDisplayModes mode)
        {
            if (FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleRotation>().Count() > 0)
            {
                FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleRotation>().First().autopilotContextCurrent = (int)mode;
            }
        }

        private void OnDestroy()
        {
            GameEvents.onSetSpeedMode.Remove(onSetSpeedMode);
            GameEvents.onVesselChange.Remove(onVesselChange);
            GameEvents.onVesselStandardModification.Remove(onVesselStandardModification);
        }
    }

    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    class MandatoryRCSGameScenesEvents : MonoBehaviour
    {
        private void Start()
        {
            GameEvents.onVesselSOIChanged.Add(onVesselSOIChanged);
        }

        // On SOI change, if target hold is in a body relative mode, set it to false and reset autopilot mode to stability assist
        private void onVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> data)
        {
            if (data.host.loaded)
            {
                if (data.host.vesselModules != null && data.host.vesselModules.OfType<VesselModuleRotation>().Count() > 0)
                {
                    if (data.host.vesselModules.OfType<VesselModuleRotation>().First().autopilotTargetHold
                        && data.host.vesselModules.OfType<VesselModuleRotation>().First().autopilotMode >= 1
                        && data.host.vesselModules.OfType<VesselModuleRotation>().First().autopilotMode <= 6)
                    {
                      data.host.vesselModules.OfType<VesselModuleRotation>().First().autopilotTargetHold = false;
                      data.host.vesselModules.OfType<VesselModuleRotation>().First().autopilotMode = 0;
                    }
                }
            }
            else
            {
                if (data.host.protoVessel.vesselModules == null || !data.host.protoVessel.vesselModules.HasNode("VesselModuleRotation"))
                { return; }
                bool autopilotTargetHoldCurrent = false;
                int autopilotModeCurrent = 0;
                if (!data.host.protoVessel.vesselModules.GetNode("VesselModuleRotation").TryGetValue("autopilotTargetHold", ref autopilotTargetHoldCurrent))
                { return;}
                if (!data.host.protoVessel.vesselModules.GetNode("VesselModuleRotation").TryGetValue("autopilotMode", ref autopilotModeCurrent))
                { return;}

                if (autopilotTargetHoldCurrent
                    && autopilotModeCurrent >= 1
                    && autopilotModeCurrent <= 6)
                {
                    data.host.protoVessel.vesselModules.GetNode("VesselModuleRotation").SetValue("autopilotTargetHold", false);
                    data.host.protoVessel.vesselModules.GetNode("VesselModuleRotation").SetValue("autopilotMode", 0);
                }
            }
        }

        private void OnDestroy()
        {
            GameEvents.onVesselSOIChanged.Remove(onVesselSOIChanged);
        }
    }
}
