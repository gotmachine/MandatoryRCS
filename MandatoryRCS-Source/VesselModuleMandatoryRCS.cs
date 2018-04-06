/* 
 * This file and all code it contains is released in the public domain
 */

using MandatoryRCS.Lib;
using System.Collections.Generic;
using UnityEngine;
using static MandatoryRCS.ComponentSASAttitude;

namespace MandatoryRCS
{
    // This is the main class, implemented as a KSP VesselModule
    // It contains all the common information used by the various components
    // Components are registered and called individually so we can control in wich order things are happening
    public class VesselModuleMandatoryRCS : VesselModule
    {
        #region Vessel state
        public enum VesselState
        {
            InPhysics,
            Packed,
            Unloaded
        }
        public VesselState currentState;
        public bool isInPhysicsFirstFrame;
        public bool playerControlled;
        public bool pilotIsIdle;
        public FlightCtrlState flightCtrlState;
        public ITargetable currentTarget;
        #endregion

        #region Vessel physics info
        // Angular velocity is saved
        [KSPField(isPersistant = true)]
        public Vector3d angularVelocity;
        public Vector3d MOI;
        public Vector3d torqueAvailable;
        public Vector3d angularDistanceToStop; // inertia in MechJeb VesselState
        public Vector3d torqueReactionSpeed;
        #endregion

        #region SAS
        // True if we are keeping the vessel oriented toward the SAS target
        [KSPField(isPersistant = true)]
        public bool autopilotEnabled = false;

        [KSPField(isPersistant = true)]
        public bool autopilotPersistentModeLock = false;

        [KSPField(isPersistant = true)]
        public SASMode autopilotMode = SASMode.KillRot;

        [KSPField(isPersistant = true)]
        public FlightGlobals.SpeedDisplayModes autopilotContext = FlightGlobals.SpeedDisplayModes.Orbit;

        [KSPField(isPersistant = true)]
        public bool lockedRollMode = false;

        [KSPField(isPersistant = true)]
        public int currentRoll = 0;

        [KSPField(isPersistant = true)]
        public bool rcsAutoMode = false;

        [KSPField(isPersistant = true)]
        public int velocityLimiter = 15;

        [KSPField(isPersistant = true)]
        public bool sunIsTarget = false;

        public Quaternion autopilotAttitudeWanted;
        public Vector3d autopilotDirectionWanted;
        public bool isRollRefDefined = true;
        public bool flyByWire = false;
        public bool autopilotModeHasChanged = false;
        public bool hasManeuverNode = false;
        public bool hasSAS = false;

        #endregion

        public bool rwLockedOnDirection;

        public bool ready = false;
        public int frameNumber = 0;

        public ComponentSASAutopilot sasAutopilot;
        public ComponentSASAttitude sasAttitude;
        public ComponentPersistantRotation persistentRotation;
        public ComponentRWTorqueControl torqueControl;

        public List<ComponentBase> components = new List<ComponentBase>();

        protected override void OnStart()
        {
            persistentRotation = new ComponentPersistantRotation();
            sasAutopilot = new ComponentSASAutopilot();
            sasAttitude = new ComponentSASAttitude();
            torqueControl = new ComponentRWTorqueControl();

            components.Add(persistentRotation);
            components.Add(sasAutopilot);
            components.Add(sasAttitude);
            components.Add(torqueControl);

            foreach (ComponentBase component in components)
            {
                component.vessel = Vessel;
                component.vesselModule = this;
                component.Start();
            }

            // Note : OnPreAutopilotUpdate seems to be called AFTER the vesselModule FixedUpdate();
            vessel.OnPreAutopilotUpdate -= LoadedUpdate;
            vessel.OnPreAutopilotUpdate += LoadedUpdate;

            GameEvents.onVesselLoaded.Add(onVesselLoaded);
                //new EventData<Vessel, Vessel>("onVesselSwitchingToUnloaded")
        }

        private void OnDestroy()
        {
            //vessel.OnPreAutopilotUpdate -= LoadedUpdate; // This cause a nullref at startup !?!
        }

        public override void OnUnloadVessel()
        {
            

        }

        private void onVesselLoaded(Vessel vessel)
        {
            // On load, stock throw an exception if the Sun is the target, so we reset it before it happens
            if (vessel.protoVessel.targetInfo.uniqueName == "Sun")
            {
                vessel.protoVessel.targetInfo.uniqueName = "";
                vessel.protoVessel.targetInfo.targetType = ProtoTargetInfo.Type.Null;
            }
        }

        private void LoadedUpdate(FlightCtrlState s = null)
        {

            // Update physics and control state
            UpdateVesselState();

            // Do nothing more for unloaded vessels
            if (!vessel.loaded) return;

            // Get FlightCtrlState
            flightCtrlState = s;

            // Update angular velocity for in physics vessels but not in the
            // first frame because the persistent rotation component may want to restore it.
            if (currentState == VesselState.InPhysics && !isInPhysicsFirstFrame)
            {
                angularVelocity = Vessel.angularVelocityD;
            }

            // Update the SAS state according to the various rules that may alter it
            // and get the requested direction and attitude
            if (autopilotModeHasChanged || !hasSAS) autopilotPersistentModeLock = false;
            if (hasSAS) sasAttitude.ComponentUpdate();

            // Then rotate the vessel according to the angular velocity and the SAS state
            persistentRotation.ComponentUpdate();

            // Update autopilot things
            if (hasSAS && currentState == VesselState.InPhysics)
            {
                // adjust the reaction wheels torque
                torqueControl.ComponentUpdate();

                // For now, non-player controlled vessels won't have their autopilot active
                if (autopilotEnabled && s != null)
                {
                    // Get MoI first, it isn't dependent on anything else
                    VesselPhysics.GetVesselMoI(vessel, out MOI);
                    // We know how reaction wheels will behave, set RCS auto mode
                    // TODO : check if we need to reset the SAS PID on RCS enable/disable-> sasAutopilot.Reset()
                    RCSAutoSet();
                    // Get the available torque
                    VesselPhysics.GetVesselAvailableTorque(vessel, MOI, out torqueAvailable, out torqueReactionSpeed, out angularDistanceToStop);

                    // It's time to calculate how the autopilot should steer the vessel;
                    if (autopilotModeHasChanged) { sasAutopilot.Reset(); }
                    sasAutopilot.ComponentUpdate();
                }
            }
            // Reset the SAS mode change flag
            if (autopilotModeHasChanged) { autopilotModeHasChanged = false; }

            // Ensure we have done all this at last once
            // Used by the navball UI, which should not be initialized before everything has been checked.
            ready = true;
        }

        private void FixedUpdate()
        {
            // TODO : check again that LoadedUpdate() isn't called twice in the same frame at scene start or vessel switch
            // If okay, remove this
            frameNumber += 1;

            // Do the update on all vessels except the one that is currently active (if any), if it is in physics.
            // In this specific situation, the update is called by the OnPreAutopilotUpdate callback
            // Note : don't check for FlightGlobals.ready here, value may be inconsistent
            if (FlightGlobals.ActiveVessel == vessel && !vessel.packed) return;
            LoadedUpdate();
        }

        private void RCSAutoSet()
        {
            if (rcsAutoMode)
            {
                bool enabled;
                enabled = !rwLockedOnDirection && vessel.staticPressurekPa < 10;
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, enabled);
            }
        }

        private void UpdateVesselState()
        {
            // Situations in which the vessel can be loaded but not in physics : 
            // - It is in the physics bubble but in non-psysics timewarp
            // - It has just gone outside of the physics bubble
            // - It was just loaded, is in the physics bubble and will be unpacked in a few frames

            if (!Vessel.loaded)
            {
                currentState = VesselState.Unloaded;
            }
            else if (Vessel.packed)
            {
                currentState = VesselState.Packed;
            }
            else if (FlightGlobals.ready)
            {
                if (!isInPhysicsFirstFrame && (currentState == VesselState.Packed || currentState == VesselState.Unloaded))
                {
                    isInPhysicsFirstFrame = true;
                }
                else
                {
                    isInPhysicsFirstFrame = false;
                }
                currentState = VesselState.InPhysics;
            }

            if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel == vessel)
            {
                playerControlled = true;
            }
            else
            {
                playerControlled = false;
            }

            // Do only persistent rotation for debris & flags...
            if (vessel.vesselType == VesselType.Debris || vessel.vesselType == VesselType.Flag)
            {
                hasSAS = false;
            }
            else
            {
                hasSAS = true;
            }

        }
    }
}
