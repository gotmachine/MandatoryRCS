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
            ProgradeCorrected,
            RetrogradeCorrected,
            Parallel,
            AntiParallel
        }

        private void KeepCurrentAttitude()
        {
            vesselModule.autopilotDirectionWanted = vessel.GetTransform().up;
            vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, -vessel.GetTransform().forward);
        }

        private double maneuverNodeHash;

        public bool ModeInTargetContextOnly(SASMode mode)
        {
            return (mode == SASMode.Parallel
                || mode == SASMode.AntiParallel
                || mode == SASMode.ProgradeCorrected
                || mode == SASMode.RetrogradeCorrected);
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
                || mode == SASMode.AntiParallel
                || mode == SASMode.ProgradeCorrected
                || mode == SASMode.RetrogradeCorrected);
        }

        private bool UpdateTargetAndModeAndContext()
        {
            // Should the Sun be our target ?
            if (vesselModule.sunIsTarget)
            {
                // At this point currentTarget is the target from the last frame
                // if it's not the sun, the target was changed and isn't the sun anymore
                if (vessel.targetObject != vesselModule.currentTarget)
                {
                    vesselModule.sunIsTarget = false;
                }
                // The target should be the sun !
                else if (vessel.targetObject != (ITargetable)Sun.Instance.sun)
                {
                    FlightGlobals.fetch.SetVesselTarget(Sun.Instance.sun, true);
                    vesselModule.currentTarget = Sun.Instance.sun;
                }
            }
            else if (vessel.targetObject == (ITargetable)Sun.Instance.sun)
            {
                vessel.targetObject = null;
            }

            // It seems that disabling the autopilot also disable the stock code that revert 
            // back to the orbit speedDisplayMode if the target is null
            if (vessel.targetObject == null && FlightGlobals.speedDisplayMode == SpeedDisplayModes.Target)
                FlightGlobals.SetSpeedMode(SpeedDisplayModes.Orbit);

            // Now we handle here every possible change that could change the SAS mode and context
            // TODO : Revert to killrot if control part is intentionally changed (check reftransform ?)
            bool revertToKillRot = false;

            // Stock UI interactions
            if (vesselModule.playerControlled)
            {
                // If navball context is changed from target to orbit/surface, revert parallel/corrected to killrot
                if (FlightGlobals.speedDisplayMode != SpeedDisplayModes.Target
                    && vesselModule.autopilotContext == SpeedDisplayModes.Target
                    && ModeInTargetContextOnly(vesselModule.autopilotMode))
                {
                    revertToKillRot = true;
                    goto EndModeChecks;
                }

                // If navball context is changed from orbit/surface to target, revert radial/normal to killrot
                if (FlightGlobals.speedDisplayMode == SpeedDisplayModes.Target
                    && (vesselModule.autopilotContext == SpeedDisplayModes.Orbit || vesselModule.autopilotContext == SpeedDisplayModes.Surface)
                    && ModeInOrbSurfContextOnly(vesselModule.autopilotMode))
                {
                    revertToKillRot = true;
                    goto EndModeChecks;
                }
            }

            // If target has changed and mode is using target, revert to killrot
            if (vesselModule.currentTarget != vessel.targetObject && ModeUseTarget(vesselModule.autopilotMode))
            {
                revertToKillRot = true;
                goto EndModeChecks;
            }

            // If the maneuver node is no more, revert to killrot
            if (vesselModule.autopilotMode == SASMode.Maneuver && vessel.patchedConicSolver.maneuverNodes.Count < 1)
            {
                revertToKillRot = true;
                goto EndModeChecks;
            }
            // Check if maneuver node has changed
            // TODO: this doesn't work if maneuver is changed in the negative range, maybe remobe Math.Abs ?
            if (vessel.patchedConicSolver != null
                && vessel.patchedConicSolver.maneuverNodes.Count > 0 
                && vesselModule.hasManeuverNode
                && (Math.Abs(vessel.patchedConicSolver.maneuverNodes[0].DeltaV.magnitude + vessel.patchedConicSolver.maneuverNodes[0].UT) - Math.Abs(maneuverNodeHash) > 0.05))
            {
                revertToKillRot = true;
                goto EndModeChecks;
            }

            EndModeChecks:

            // Save Context and enabled status (stock UI interaction)
 
            if (vesselModule.playerControlled)
            {
                vesselModule.autopilotContext = FlightGlobals.speedDisplayMode;
                // Note : don't use FlightGlobals.ActiveVessel.Autopilot.Enabled because it will be set to false on load
                // despite the SAS toggle being enabled, use the action group instead
                vesselModule.autopilotEnabled = vessel.ActionGroups[KSPActionGroup.SAS];
            }
            // Save current target
            vesselModule.currentTarget = vessel.targetObject;
            // Save maneuver node
            vesselModule.hasManeuverNode = vessel.patchedConicSolver != null && vessel.patchedConicSolver.maneuverNodes.Count > 0;
            if (vesselModule.hasManeuverNode)
            {
                maneuverNodeHash = vessel.patchedConicSolver.maneuverNodes[0].DeltaV.magnitude + vessel.patchedConicSolver.maneuverNodes[0].UT;
            }
            
            return revertToKillRot;
        }

        private Vector3d GetDirectionVector()
        {
            // Get direction vector
            switch (vesselModule.autopilotMode)
            {
                case SASMode.Prograde:
                case SASMode.Retrograde:
                    if (vesselModule.autopilotContext == SpeedDisplayModes.Orbit) // Orbit prograde
                        return vesselModule.autopilotMode == SASMode.Prograde ? vessel.obt_velocity.normalized : (-vessel.obt_velocity).normalized;
                    else if (vesselModule.autopilotContext == SpeedDisplayModes.Surface) // Surface prograde
                        return vesselModule.autopilotMode == SASMode.Prograde ? vessel.srf_velocity.normalized : (-vessel.srf_velocity).normalized;
                    else // Target prograde
                    {
                        if (vessel.targetObject != null)
                        {
                            Vector3d velocity = vessel.obt_velocity - vessel.targetObject.GetObtVelocity();
                            return vesselModule.autopilotMode == SASMode.Prograde ? velocity.normalized : (-velocity).normalized;
                        }
                    }
                    break;
                case SASMode.Normal:
                case SASMode.AntiNormal:
                    Vector3d normal;
                    if (vesselModule.autopilotContext == SpeedDisplayModes.Surface)
                        normal = Vector3.Cross(vessel.srf_velocity, vessel.upAxis); 
                    else
                        normal = Vector3.Cross(vessel.obt_velocity, vessel.upAxis); 
                    return vesselModule.autopilotMode == SASMode.Normal ? normal.normalized : (-normal).normalized;
                case SASMode.RadialOut:
                    return vessel.upAxis.normalized;
                case SASMode.RadialIn:
                    return (-vessel.upAxis).normalized;
                case SASMode.Maneuver:
                    if (vessel.patchedConicSolver.maneuverNodes.Count < 1) break;
                    return vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(vessel.orbit).normalized;
                case SASMode.Target:
                    if (vessel.targetObject == null) break;
                    return (vessel.targetObject.GetTransform().position - vessel.transform.position).normalized;
                case SASMode.AntiTarget:
                    if (vessel.targetObject == null) break;
                    return (vessel.transform.position - vessel.targetObject.GetTransform().position).normalized;
                case SASMode.Parallel:
                case SASMode.AntiParallel:
                    if (vessel.targetObject == null) break;
                    Vector3d direction;

                    if (vessel.targetObject is ModuleDockingNode)
                        direction = vessel.targetObject.GetTransform().forward.normalized;
                    else
                        direction = vessel.targetObject.GetTransform().up.normalized;

                    return vesselModule.autopilotMode == SASMode.Parallel ? direction.normalized : (-direction).normalized;
                case SASMode.ProgradeCorrected:
                case SASMode.RetrogradeCorrected:
                    //TODO : this doesn't work well
                    if (vessel.targetObject == null) return vessel.ReferenceTransform.up.normalized;
                    Vector3 targetDirInv = vessel.transform.position - vessel.targetObject.GetTransform().position;
                    Vector3 targetRelVel = vessel.GetObtVelocity() - vessel.targetObject.GetObtVelocity();

                    Vector3 correction = Vector3.ProjectOnPlane(-targetRelVel,targetDirInv);

                    // Avoid chasing the target when relative velocity is very low
                    if (correction.magnitude < 0.05)
                    {
                        vesselModule.autopilotDirectionWanted = -targetDirInv;
                        break;
                    }
                    // approch target direction
                    else
                    {
                        correction = correction * ((targetDirInv.magnitude / correction.magnitude) * Math.Max(correction.magnitude / targetDirInv.magnitude, 1.0f));
                    }

                    vesselModule.autopilotDirectionWanted = correction - targetDirInv;

                    if (vesselModule.autopilotMode == SASMode.RetrogradeCorrected)
                    {
                        vesselModule.autopilotDirectionWanted *= -1;
                    }
                    break;

            }

            // In other cases, return the direction from the previous step
            return vesselModule.autopilotDirectionWanted;
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

            Vector3d rollRef = Vector3d.zero;

            if (vesselModule.autopilotContext == SpeedDisplayModes.Target
                || vesselModule.autopilotMode == SASMode.Target
                || vesselModule.autopilotMode == SASMode.AntiTarget)
            {
                if (vessel.targetObject is ModuleDockingNode)
                {
                    rollRef = vessel.targetObject.GetTransform().up;
                }
                else if (vessel.targetObject is CelestialBody)
                {
                    if (vesselModule.autopilotMode == SASMode.Parallel || vesselModule.autopilotMode == SASMode.AntiParallel)
                    {
                        rollRef = Vector3.Cross(vessel.targetObject.GetTransform().position - vessel.ReferenceTransform.position, vessel.targetObject.GetTransform().up);
                    }
                    else
                    {
                        rollRef = vessel.targetObject.GetTransform().up;
                    }
                }
                else
                {
                    rollRef = -vessel.targetObject.GetTransform().forward;
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
            double angleToRef = Vector3d.Angle(rollRef, vesselModule.autopilotDirectionWanted);
            if (angleToRef < 2.5 || angleToRef > 177.5)
            {
                vesselModule.isRollRefDefined = false;
                vesselModule.lockedRollMode = false;
                rollRef = -vessel.GetTransform().forward;
            }
            else
            {
                vesselModule.isRollRefDefined = true;
            }

            return rollRef;
        }

        private void UpdateAttitude(Vector3d rollRef)
        {
            switch (vesselModule.autopilotMode)
            {
                case SASMode.Hold:
                    if (!vesselModule.pilotIsIdle)
                    {
                        vesselModule.flyByWire = false;
                        KeepCurrentAttitude();
                    }
                    else if (!vesselModule.flyByWire)
                    {
                        double magnitudePitchYaw = Math.Sqrt(vesselModule.angularVelocity.x * vesselModule.angularVelocity.x + vesselModule.angularVelocity.z * vesselModule.angularVelocity.z);
                        double magnitudeRoll = Math.Sqrt(vesselModule.angularVelocity.y * vesselModule.angularVelocity.y);
                        // TODO : tweak the values
                        if (magnitudePitchYaw < 0.1 && magnitudeRoll < 0.4)
                        {
                            vesselModule.flyByWire = true;
                            vesselModule.autopilotDirectionWanted = vessel.GetTransform().up;
                            vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, rollRef);
                        }
                        else
                        {
                            vesselModule.autopilotDirectionWanted = vessel.GetTransform().up;
                            vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, rollRef);
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

                    vesselModule.autopilotDirectionWanted += vessel.ReferenceTransform.rotation * new Vector3(yawInput * TimeWarp.fixedDeltaTime * 0.5f, 0, -pitchInput * TimeWarp.fixedDeltaTime * 0.5f);
                    vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, rollRef);
                    // Reset pitch and yaw input
                    vesselModule.flightCtrlState.pitch = 0;
                    vesselModule.flightCtrlState.yaw = 0;
                    break;
                case SASMode.KillRot:
                    vesselModule.flyByWire = false;
                    vesselModule.lockedRollMode = false;
                    vesselModule.isRollRefDefined = false;
                    vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vessel.GetTransform().up, -vessel.GetTransform().forward);
                    break;
                default:
                    vesselModule.flyByWire = false;
                    vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, rollRef);
                    break;
            }

            // If locked roll is enabled, apply the roll offset
            if (vesselModule.lockedRollMode)
            {
                vesselModule.autopilotAttitudeWanted *= Quaternion.Euler(0, 0, -vesselModule.currentRoll);
            }
        }

        public override void ComponentUpdate()
        {
            // All this isn't called for unloaded vessels
            if (vesselModule.currentState == VesselState.Unloaded) { return; }

            // Update pilotIsIdle here because if mode == flybywire we may reset the ctrlState values
            if (vesselModule.currentState == VesselState.InPhysics && vesselModule.flightCtrlState != null) 
            {
                vesselModule.pilotIsIdle = vesselModule.flightCtrlState.isIdle;
            }
            else
            {
                vesselModule.pilotIsIdle = true;
            }

            // Update SAS mode, context and target then reset to KillRot if necessary
            if (UpdateTargetAndModeAndContext())
            {
                vesselModule.autopilotMode = SASMode.KillRot;
            }

            // Get direction vector
            vesselModule.autopilotDirectionWanted = GetDirectionVector();

            // Get rollref vector and update roll state
            Vector3d rollRef = UpdateRollRef();

            // Get requested attitude Quaternion using the direction and rollref vectors
            UpdateAttitude(rollRef);

            // Ensure we always use a normalized vector
            vesselModule.autopilotDirectionWanted = vesselModule.autopilotDirectionWanted.normalized;

            // Checking if the timewarping / persistent hold mode should be enabled
            if (vesselModule.currentState == VesselState.InPhysics)
            {
                if (vessel.Autopilot.Enabled
                    && vesselModule.autopilotMode != SASMode.KillRot
                    && vesselModule.autopilotMode != SASMode.Hold
                    && vesselModule.autopilotMode != SASMode.FlyByWire
                    //&& (wheelsTotalMaxTorque > Single.Epsilon) // We have some reaction wheels
                    && vesselModule.angularVelocity.magnitude < Settings.velocityThreesold * 2 // The vessel isn't rotating too much
                    && Math.Max(Vector3.Dot(vesselModule.autopilotDirectionWanted.normalized, vessel.GetTransform().up.normalized), 0) > 0.975f) // 1.0 = toward target, 0.0 = target is at a 90° angle, previously 0.95
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
