/* 
 * This file and all code it contains is released in the public domain
 */

using System;
using UnityEngine;
using static FlightGlobals;
using static MandatoryRCS.VesselModuleMandatoryRCS;

namespace MandatoryRCS
{
    public class ComponentSASAttitude : ComponentBase
    {
        private double maneuverNodeHash;

        public enum SASMode
        {
            Hold,
            FlyByWire,
            Maneuver,
            KillRot,
            Target,
            AntiTarget,
            Prograde,
            Retrograde,
            Normal,
            AntiNormal,
            RadialIn,
            RadialOut,
            Parallel,
            AntiParallel
        }

        private void KeepCurrentAttitude()
        {
            vesselModule.sasDirectionWanted = vessel.GetTransform().up;
            vesselModule.sasAttitudeWanted = Quaternion.LookRotation(vesselModule.sasDirectionWanted, -vessel.GetTransform().forward);
        }

        public bool ModeInTargetContextOnly(SASMode mode)
        {
            return (mode == SASMode.Parallel
                || mode == SASMode.AntiParallel);
        }

        public bool ModeInOrbSurfContextOnly(SASMode mode)
        {
            return (mode == SASMode.Normal
                || mode == SASMode.AntiNormal
                || mode == SASMode.RadialIn
                || mode == SASMode.RadialOut);
        }

        public bool ModeUseTarget(SASMode mode)
        {
            return (mode == SASMode.Target
                || mode == SASMode.AntiTarget
                || mode == SASMode.Parallel
                || mode == SASMode.AntiParallel);
        }

        public bool ModeUseVelocity(SASMode mode)
        {
            switch (mode)
            {
                case SASMode.Prograde:
                case SASMode.Retrograde:
                    return true;
                default:
                    return false;
            }

        }

        public void ResetToKillRot()
        {
            if (ModeUseTarget(vesselModule.autopilotMode) && vesselModule.autopilotContext == SpeedDisplayModes.Target)
            {
                vesselModule.SetContext(SpeedDisplayModes.Orbit, true, true);
            }
            vesselModule.autopilotMode = SASMode.KillRot;
            vesselModule.autopilotPersistentModeLock = false;
        }

        private double GetManeuverNodeHash()
        {
            if (vessel.patchedConicSolver != null && vessel.patchedConicSolver.maneuverNodes.Count > 0)
                return vessel.patchedConicSolver.maneuverNodes[0].DeltaV.magnitude + vessel.patchedConicSolver.maneuverNodes[0].UT;
            else
                return 0;
        }

        private void UpdateSASState()
        {

            // Note : don't use FlightGlobals.ActiveVessel.Autopilot.Enabled because it will be set to false on load
            // despite the SAS toggle being enabled, use the action group instead
            if (vesselModule.autopilotEnabled != vessel.ActionGroups[KSPActionGroup.SAS])
            {
                Debug.Log("[MRCS] [" + vessel.vesselName + "] : SAS enabled status has changed");
                vesselModule.autopilotEnabled = vessel.ActionGroups[KSPActionGroup.SAS];
                ResetToKillRot();
            }


            //SAS control levels (vesselModule.Vessel.VesselValues.AutopilotSkill)  :
            //(0) : stability assist      -> killrot
            //(1) : + pro/retrograde      -> +pro/retrograde, hold, flybywire
            //(2) : + normal/radial       -> +normal, radial, roll lock
            //(3) : + target/maneuver     -> +target, maneuver
            switch (vesselModule.autopilotMode)
            {
                case SASMode.Hold:         if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 1) ResetToKillRot(); break;
                case SASMode.FlyByWire:    if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 1) ResetToKillRot(); break;
                case SASMode.Maneuver:     if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 3) ResetToKillRot(); break;
                case SASMode.KillRot:      if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 0) ResetToKillRot(); break;
                case SASMode.Target:       if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 3) ResetToKillRot(); break;
                case SASMode.AntiTarget:   if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 3) ResetToKillRot(); break;
                case SASMode.Prograde:     if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 1) ResetToKillRot(); break;
                case SASMode.Retrograde:   if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 1) ResetToKillRot(); break;
                case SASMode.Normal:       if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 2) ResetToKillRot(); break;
                case SASMode.AntiNormal:   if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 2) ResetToKillRot(); break;
                case SASMode.RadialIn:     if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 2) ResetToKillRot(); break;
                case SASMode.RadialOut:    if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 2) ResetToKillRot(); break;
                case SASMode.Parallel:     if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 3) ResetToKillRot(); break;
                case SASMode.AntiParallel: if (vesselModule.Vessel.VesselValues.AutopilotSkill.value < 3) ResetToKillRot(); break;
            }
            if (vesselModule.lockedRollMode && vesselModule.Vessel.VesselValues.AutopilotSkill.value < 2)
            {
                vesselModule.lockedRollMode = false;
                ResetToKillRot();
            }


            // Stock context and target state isn't reliable when loading, even if FlightGlobals.ready is true
            // because the stock target field is restored from the confignode by a coroutine.
            // It's the same for FlightGlobals.speedDisplayMode, it will be set to surface at load, and then changed a few dozen frames later.
            if (vesselModule.currentState == VesselState.PackedReady || vesselModule.currentState == VesselState.PhysicsReady)
            {
                // Maneuver nodes changes checks don't use the UI state so it is safe to always check for it

                // Target checks don't use the UI state, but we should not reset the SpeedDisplayModes of non-controlled vessels
                if (!vesselModule.vesselTargetDirty && vesselModule.PlayerControlled() && vesselModule.currentTarget != FlightGlobals.fetch.VesselTarget)
                {
                    // If we are orbiting the sun, stock won't allow it as its target. Unless another target has been selected, we stop tracking the stock target.
                    if ((object)vesselModule.currentTarget == Sun.Instance.sun && vessel.mainBody == Sun.Instance.sun && FlightGlobals.fetch.VesselTarget == null)
                    {
                        goto AbortTargetChange;
                    }
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] : target has changed, Old Target = " + vesselModule.currentTarget + ", New Target = " + vessel.targetObject);
                    vesselModule.SetTarget(vessel.targetObject, false, true);

                    // Enforce context change
                    if (vessel.targetObject == null && vesselModule.autopilotContext == SpeedDisplayModes.Target)
                    {
                        vesselModule.SetContext(SpeedDisplayModes.Orbit, vesselModule.PlayerControlled(), true);
                    }
                    // Reset SAS mode
                    if (ModeUseTarget(vesselModule.autopilotMode))
                    {
                        ResetToKillRot();
                    }
                    AbortTargetChange:;
                }

                // Should not happen but better safe than sorry
                if (vesselModule.currentTarget == null && ModeUseTarget(vesselModule.autopilotMode))
                {
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] Warning : autopilot mode was relative to target but target is null !");
                    if (vesselModule.autopilotContext == SpeedDisplayModes.Target)
                    {
                        vesselModule.SetContext(SpeedDisplayModes.Orbit, vesselModule.PlayerControlled(), true);
                    }
                    ResetToKillRot();
                }

                // Here we handle UI SpeedDisplayModes changes, so it's only for the controlled vessel
                // Note : FlightGlobals.speedDisplayMode seems to be valid on early load frames, even if FlightGlobals aren't ready
                if (!vesselModule.vesselTargetDirty && vesselModule.PlayerControlled() && vesselModule.autopilotContext != FlightGlobals.speedDisplayMode)
                {
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] : Context has changed, Old Context = " + vesselModule.autopilotContext + ", New Context = " + FlightGlobals.speedDisplayMode);
                    // If navball context is changed from target to orbit/surface, revert parallel/corrected to killrot
                    if (vesselModule.autopilotContext == SpeedDisplayModes.Target
                        && ModeInTargetContextOnly(vesselModule.autopilotMode))
                    {
                        ResetToKillRot();
                    }
                    // If navball context is changed from orbit/surface to target, revert radial/normal to killrot
                    else if (vesselModule.autopilotContext != SpeedDisplayModes.Target
                        && ModeInOrbSurfContextOnly(vesselModule.autopilotMode))
                    {
                        ResetToKillRot();
                    }
                    // Save new context
                    vesselModule.autopilotContext = FlightGlobals.speedDisplayMode;
                }

                
                if (vesselModule.autopilotMode == SASMode.Maneuver)
                {
                    // If the maneuver node is no more, revert to killrot
                    if (!vesselModule.VesselHasManeuverNode())
                    {
                        Debug.Log("[MRCS] [" + vessel.vesselName + "] : Node has changed");
                        ResetToKillRot();
                    }
                    // Also check if the node has changed
                    else
                    {
                        double newHash = GetManeuverNodeHash();
                        if (newHash - maneuverNodeHash > 0.01 || newHash - maneuverNodeHash < -0.01)
                        {
                            Debug.Log("[MRCS] [" + vessel.vesselName + "] : Node has changed");
                            ResetToKillRot();
                            maneuverNodeHash = 0;
                        }
                        maneuverNodeHash = newHash;
                    }
                }

                // Check if velocity is enough for the veolicty related modes to be active
                vesselModule.hasVelocity = FlightGlobals.GetDisplayVelocity().magnitude > 0.1f;
                if (ModeUseVelocity(vesselModule.autopilotMode))
                {
                    if (!vesselModule.hasVelocity)
                    {
                        ResetToKillRot();
                    }
                }
            }


        }

        private Vector3d GetDirectionVector()
        {
            // Get direction vector
            switch (vesselModule.autopilotMode)
            {
                case SASMode.Prograde:
                    switch (vesselModule.autopilotContext)
                    {
                        case SpeedDisplayModes.Orbit:
                            return vessel.obt_velocity.normalized;
                        case SpeedDisplayModes.Surface:
                            return vessel.srf_velocity.normalized;
                        case SpeedDisplayModes.Target:
                            if (vesselModule.currentTarget == null) break;
                            return (vessel.obt_velocity - vessel.targetObject.GetObtVelocity()).normalized;
                    }
                    break;
                case SASMode.Retrograde:
                    switch (vesselModule.autopilotContext)
                    {
                        case SpeedDisplayModes.Orbit:
                            return (-vessel.obt_velocity).normalized;
                        case SpeedDisplayModes.Surface:
                            return (-vessel.srf_velocity).normalized;
                        case SpeedDisplayModes.Target:
                            if (vesselModule.currentTarget == null) break;
                            return (-(vessel.obt_velocity - vessel.targetObject.GetObtVelocity())).normalized;
                    }
                    break;
                case SASMode.Normal:
                    switch (vesselModule.autopilotContext)
                    {
                        case SpeedDisplayModes.Orbit:
                            return vessel.orbit.h.xzy.normalized;
                        case SpeedDisplayModes.Surface:
                        case SpeedDisplayModes.Target:
                            return vessel.mainBody.RotationAxis.normalized;
                    }
                    break;
                case SASMode.AntiNormal:
                    switch (vesselModule.autopilotContext)
                    {
                        case SpeedDisplayModes.Orbit:
                            return (-vessel.orbit.h.xzy).normalized;
                        case SpeedDisplayModes.Surface:
                        case SpeedDisplayModes.Target:
                            return (-vessel.mainBody.RotationAxis).normalized;
                    }
                    break;
                case SASMode.RadialOut:
                    switch (vesselModule.autopilotContext)
                    {
                        case SpeedDisplayModes.Orbit:
                            return Vector3.Cross(vessel.obt_velocity, vessel.orbit.h.xzy).normalized;
                        case SpeedDisplayModes.Surface:
                        case SpeedDisplayModes.Target:
                            return (-vessel.upAxis).normalized;
                    }
                    break;
                case SASMode.RadialIn:
                    switch (vesselModule.autopilotContext)
                    {
                        case SpeedDisplayModes.Orbit:
                            return (-Vector3.Cross(vessel.obt_velocity, vessel.orbit.h.xzy)).normalized;
                        case SpeedDisplayModes.Surface:
                        case SpeedDisplayModes.Target:
                            return (vessel.upAxis).normalized;
                    }
                    break;
                case SASMode.Maneuver:
                    if (vessel.patchedConicSolver.maneuverNodes.Count < 1) break;
                    return vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(vessel.orbit).normalized;
                case SASMode.Target:
                    if (vesselModule.currentTarget == null) break;
                    return (vesselModule.currentTarget.GetTransform().position - vessel.transform.position).normalized;
                case SASMode.AntiTarget:
                    if (vesselModule.currentTarget == null) break;
                    return (vessel.transform.position - vesselModule.currentTarget.GetTransform().position).normalized;
                case SASMode.Parallel:
                case SASMode.AntiParallel:
                    if (vesselModule.currentTarget == null) break;
                    Vector3d direction;

                    if (vesselModule.currentTarget is ModuleDockingNode)
                        direction = vesselModule.currentTarget.GetTransform().forward.normalized;
                    else
                        direction = vesselModule.currentTarget.GetTransform().up.normalized;

                    return vesselModule.autopilotMode == SASMode.Parallel ? direction.normalized : (-direction).normalized;
            }

            // In other cases, return the direction from the previous step
            return vesselModule.sasDirectionWanted;
        }

        private Vector3d UpdateRollRef()
        {
            /*
            Roll handling is problematic. The conflicting facts are :
            (1) We want the roll attitude to make sense from a player standpoint when flying the vessel
            (2) The reference should result in a smooth and continous change of the roll attitude, no sudden inversions
            (2) A fixed reference (independant from the vessel attitude) WILL have a "dead zone" causing a sudden inversion of the roll attitude

            The current solution :
            - Disable roll lock when we enter the dead zone
            - Use predefined roll references for the different modes :
                - Radial/Antiradial use the main body north vector
                - Other body-relative modes use the up/radial vector
                - Target/antitarget use the target up/forward vector
                - Parallel/antiparallel use a "right" vector relative to the vessel forward vector
            */

            //if (!vesselModule.lockedRollMode) return -vessel.GetTransform().forward;

            Vector3d rollRef = Vector3d.zero;

            if (vesselModule.autopilotContext == SpeedDisplayModes.Target
                || vesselModule.autopilotMode == SASMode.Target
                || vesselModule.autopilotMode == SASMode.AntiTarget)
            {
                if (vesselModule.currentTarget is ModuleDockingNode)
                {
                    rollRef = vesselModule.currentTarget.GetTransform().up;
                }
                else if (vesselModule.currentTarget is CelestialBody)
                {
                    if (vesselModule.autopilotMode == SASMode.Parallel || vesselModule.autopilotMode == SASMode.AntiParallel)
                    {
                        rollRef = Vector3.Cross(vesselModule.currentTarget.GetTransform().position - vessel.vesselTransform.position, vesselModule.currentTarget.GetTransform().up);
                    }
                    else
                    {
                        rollRef = vesselModule.currentTarget.GetTransform().up;
                    }
                }
                else
                {
                    rollRef = -vesselModule.currentTarget.GetTransform().forward;
                }
            }
            else
            {
                // If SAS is set radial (Up), the roll ref is the mainbody north
                if (vesselModule.autopilotMode == SASMode.RadialIn || vesselModule.autopilotMode == SASMode.RadialOut)
                {
                    rollRef = Vector3d.Exclude(vessel.upAxis, vessel.mainBody.transform.up);
                }
                // Else the rollref is the radial/up vector
                else
                {
                    rollRef = vessel.upAxis;
                }
            }

            // Is the rollRef in the unstability zone ?
            double angleToRef = Vector3d.Angle(rollRef, vesselModule.sasDirectionWanted);
            if (angleToRef < 2.5 || angleToRef > 177.5)
            {
                vesselModule.isRollRefDefined = false;
                vesselModule.lockedRollMode = false;
                rollRef = -vessel.GetTransform().forward;
            }
            else if (vesselModule.autopilotMode == SASMode.KillRot)
            {
                vesselModule.lockedRollMode = false;
                vesselModule.isRollRefDefined = false;
            }
            else
            {
                vesselModule.isRollRefDefined = true;
            }

            return rollRef;
        }

        private void UpdateAttitude()
        {
            switch (vesselModule.autopilotMode)
            {
                case SASMode.Hold:
                    if (vesselModule.pilotRotationInput)
                    {
                        vesselModule.flyByWire = false;
                        KeepCurrentAttitude();
                    }
                    else if (!vesselModule.flyByWire)
                    {
                        double magnitudePitchYaw = Math.Sqrt(vesselModule.angularVelocity.x * vesselModule.angularVelocity.x + vesselModule.angularVelocity.z * vesselModule.angularVelocity.z);
                        //double magnitudeRoll = Math.Sqrt(vesselModule.angularVelocity.y * vesselModule.angularVelocity.y);
                        // TODO : tweak the values
                        if (magnitudePitchYaw < 0.1 ) //&& magnitudeRoll < 0.4)
                        {
                            vesselModule.flyByWire = true;
                            vesselModule.sasDirectionWanted = vessel.GetTransform().up;
                            vesselModule.sasAttitudeWanted = Quaternion.LookRotation(vesselModule.sasDirectionWanted, vesselModule.sasRollReference);
                        }
                        else
                        {
                            vesselModule.sasDirectionWanted = vessel.GetTransform().up;
                            vesselModule.sasAttitudeWanted = Quaternion.LookRotation(vesselModule.sasDirectionWanted, vesselModule.sasRollReference);
                        }
                    }
                    break;
                case SASMode.FlyByWire:
                    // Abort if flightCtrlstate is null
                    if (vesselModule.flightCtrlState == null)
                    {
                        vesselModule.flyByWire = false;
                        KeepCurrentAttitude();
                        break;
                    }
                    // If just activated, we need to register the current attitude
                    if (!vesselModule.flyByWire)
                    {
                        vesselModule.flyByWire = true;
                        KeepCurrentAttitude();
                    }

                    // Get pitch and yaw input
                    FlightCtrlState s = vesselModule.flightCtrlState;
                    float pitchInput = vesselModule.flightCtrlState.pitch;
                    float yawInput = vesselModule.flightCtrlState.yaw;
                    // Update direction with pitch and yaw input

                    vesselModule.sasDirectionWanted += vessel.GetTransform().rotation * new Vector3(yawInput * 0.01f, 0, -pitchInput * 0.01f);
                    vesselModule.sasAttitudeWanted = Quaternion.LookRotation(vesselModule.sasDirectionWanted, vesselModule.sasRollReference);
                    // Reset pitch and yaw input
                    vesselModule.flightCtrlState.pitch = 0;
                    vesselModule.flightCtrlState.yaw = 0;
                    break;
                case SASMode.KillRot:
                    vesselModule.flyByWire = false;
                    //vesselModule.lockedRollMode = false;
                    //vesselModule.isRollRefDefined = false;
                    vesselModule.sasAttitudeWanted = Quaternion.LookRotation(vessel.GetTransform().up, -vessel.GetTransform().forward);
                    break;
                default:
                    vesselModule.flyByWire = false;
                    vesselModule.sasAttitudeWanted = Quaternion.LookRotation(vesselModule.sasDirectionWanted, vesselModule.sasRollReference);
                    break;
            }

            // If locked roll is enabled, apply the roll offset
            if (vesselModule.lockedRollMode)
            {
                vesselModule.sasAttitudeWanted *= Quaternion.Euler(0, 0, -vesselModule.currentRoll);
            }
        }

        public override void ComponentUpdate()
        {
            // Don't update anything else in physics loading state, because SAS state isn't reliable at this time
            if (vesselModule.currentState == VesselState.PhysicsNotReady) return;

            // Check the validity of current SAS mode/context and react to possible stock UI interactions :
            // - SAS enabled/disabled
            // - Target changed
            // - Maneuver node changed
            // - Navball context changed
            // - Velocity becoming very low
            UpdateSASState();

            // Get direction vector
            vesselModule.sasDirectionWanted = GetDirectionVector();

            // Get rollref vector and update roll state
            vesselModule.sasRollReference = UpdateRollRef();
        }

        public override void ComponentFixedUpdate()
        {

            // All this isn't called for unloaded vessels
            if (vesselModule.currentState == VesselState.Unloaded) return;

            // Update pilotIsIdle here because if mode == flybywire we may reset the ctrlState values
            if (vesselModule.currentState == VesselState.PhysicsReady && vesselModule.flightCtrlState != null) 
            {
                if (vesselModule.flightCtrlState.isIdle)
                {
                    vesselModule.pilotRotationInput = false;
                    vesselModule.pilotTranslationInput = false;
                }
                else if (vesselModule.flightCtrlState.X == 0 && vesselModule.flightCtrlState.Y == 0 && vesselModule.flightCtrlState.Z == 0)
                {
                    vesselModule.pilotRotationInput = true;
                    vesselModule.pilotTranslationInput = false;
                }
                else
                {
                    vesselModule.pilotRotationInput = false;
                    vesselModule.pilotTranslationInput = true;
                }
            }
            else
            {
                vesselModule.pilotRotationInput = false;
                vesselModule.pilotTranslationInput = false;
            }

            // Don't update anything else in physics loading state, because SAS state isn't reliable at this time
            if (vesselModule.currentState == VesselState.PhysicsNotReady) return;

            // Get requested attitude Quaternion using the direction and rollref vectors
            UpdateAttitude();

            // Ensure we always use a normalized vector
            //vesselModule.autopilotDirectionWanted = vesselModule.autopilotDirectionWanted.normalized;

            // Checking if the timewarping / persistent hold mode should be enabled
            if (vesselModule.currentState == VesselState.PhysicsReady)
            {
                if (vessel.Autopilot.Enabled
                    && vesselModule.autopilotMode != SASMode.KillRot
                    && vesselModule.autopilotMode != SASMode.Hold
                    && vesselModule.autopilotMode != SASMode.FlyByWire
                    //&& (wheelsTotalMaxTorque > Single.Epsilon) // We have some reaction wheels
                    && vesselModule.angularVelocity.magnitude < Settings.velocityThreesold * 2 // The vessel isn't rotating too much
                    && Math.Max(Vector3.Dot(vesselModule.sasDirectionWanted.normalized, vessel.vesselTransform.up.normalized), 0) > 0.975f) // 1.0 = toward target, 0.0 = target is at a 90° angle, previously 0.95
                {
                    vesselModule.autopilotPersistentModeLock = true;
                }
                else
                {
                    vesselModule.autopilotPersistentModeLock = false;
                }
            }
        }
    }
}
