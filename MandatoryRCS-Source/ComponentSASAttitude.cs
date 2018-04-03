using MandatoryRCS.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            if (vessel.patchedConicSolver.maneuverNodes.Count > 0 
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
            vesselModule.hasManeuverNode = vessel.patchedConicSolver.maneuverNodes.Count > 0;
            if (vesselModule.hasManeuverNode)
            {
                maneuverNodeHash = vessel.patchedConicSolver.maneuverNodes[0].DeltaV.magnitude + vessel.patchedConicSolver.maneuverNodes[0].UT;
            }
            
            return revertToKillRot;
        }

        public override void ComponentUpdate()
        {
            // Isn't called for unloaded vessels
            if (vesselModule.currentState == VesselState.Unloaded) { return; }

            // Update SAS mode, context and target then reset to KillRot if necessary
            if (UpdateTargetAndModeAndContext())
            {
                vesselModule.autopilotMode = SASMode.KillRot;
            }


            // Calculate requested attitude


            // Get direction vector
            // TODO : Use less code for this
            switch (vesselModule.autopilotMode)
            {
                case SASMode.Prograde:
                case SASMode.Retrograde:
                    if (vesselModule.autopilotContext == FlightGlobals.SpeedDisplayModes.Orbit) // Orbit prograde
                    { vesselModule.autopilotDirectionWanted = vessel.obt_velocity; }
                    else if (vesselModule.autopilotContext == FlightGlobals.SpeedDisplayModes.Surface) // Surface prograde
                    { vesselModule.autopilotDirectionWanted = vessel.srf_velocity; }
                    else if (vesselModule.autopilotContext == FlightGlobals.SpeedDisplayModes.Target) // Target prograde
                    {
                        if (vessel.targetObject != null)
                        { vesselModule.autopilotDirectionWanted = -(vessel.targetObject.GetObtVelocity() - vessel.obt_velocity); }
                    }
                    if (vesselModule.autopilotMode == SASMode.Retrograde) // Invert vector for retrograde
                    {
                        vesselModule.autopilotDirectionWanted = -vesselModule.autopilotDirectionWanted;
                    }
                    break;
                case SASMode.Normal:
                case SASMode.AntiNormal:
                case SASMode.RadialOut:
                case SASMode.RadialIn:
                    // Get body up vector
                    Vector3d planetUp = (vessel.rootPart.transform.position - vessel.mainBody.position).normalized;
                    // Get normal vector
                    Vector3d normal = new Vector3();
                    if (vesselModule.autopilotContext == FlightGlobals.SpeedDisplayModes.Orbit) // Orbit
                    { normal = Vector3.Cross(vessel.obt_velocity, planetUp).normalized; }
                    else // Surface/Target (seems to be the same for normal/radial)
                    { normal = Vector3.Cross(vessel.srf_velocity, planetUp).normalized; }

                    // Return normal/antinormal or calculate radial
                    if (vesselModule.autopilotMode == SASMode.Normal) // Normal
                    { vesselModule.autopilotDirectionWanted = normal; }
                    else if (vesselModule.autopilotMode == SASMode.AntiNormal) // AntiNormal
                    { vesselModule.autopilotDirectionWanted = -normal; }
                    else
                    {
                        // Get RadialIn vector
                        Vector3d radial = new Vector3();
                        if (vesselModule.autopilotContext == FlightGlobals.SpeedDisplayModes.Orbit) // Orbit
                        { radial = Vector3.Cross(vessel.obt_velocity, normal).normalized; }
                        else // Surface/Target (seems to be the same for normal/radial)
                        { radial = Vector3.Cross(vessel.srf_velocity, normal).normalized; }

                        // Return radial vector
                        if (vesselModule.autopilotMode == SASMode.RadialIn) // Radial In
                        { vesselModule.autopilotDirectionWanted = radial; }
                        else if (vesselModule.autopilotMode == SASMode.RadialOut) // Radial Out
                        { vesselModule.autopilotDirectionWanted = -radial; }
                    }
                    break;
                case SASMode.Maneuver:
                    if (vessel.patchedConicSolver.maneuverNodes.Count < 1) { break; }
                    vesselModule.autopilotDirectionWanted = vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(vessel.orbit);
                    break;
                case SASMode.Target:
                    if (vessel.targetObject == null) { break; }
                    vesselModule.autopilotDirectionWanted = vessel.targetObject.GetTransform().position - vessel.transform.position;
                    break;
                case SASMode.AntiTarget:
                    if (vessel.targetObject == null) { break; }
                    vesselModule.autopilotDirectionWanted = -(vessel.targetObject.GetTransform().position - vessel.transform.position);
                    break;
                case SASMode.Parallel:
                case SASMode.AntiParallel:
                    if (vessel.targetObject == null) { break; }
                    // vessel.targetObject.GetFwdVector() -> not consistent
                    if (vessel.targetObject is ModuleDockingNode)
                    {
                        vesselModule.autopilotDirectionWanted = vessel.targetObject.GetTransform().forward;
                    }
                    else
                    {
                        vesselModule.autopilotDirectionWanted = vessel.targetObject.GetTransform().up;
                    }
                    if (vesselModule.autopilotMode == SASMode.AntiParallel)
                    {
                        vesselModule.autopilotDirectionWanted *= -1;
                    }
                    break;

                case SASMode.ProgradeCorrected:
                case SASMode.RetrogradeCorrected:
                    //TODO : this doesn't work well
                    if (vessel.targetObject == null) { break; }
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

                    //Vector3 projection = targetPos.normalized + Vector3.ProjectOnPlane(targetvel, targetPos);
                    //float projectionMagn = projection.magnitude;
                    //projection = projectionMagn > 1.0f ? projection / projectionMagn * 1.0f : projection;
                    //direction = vessel.ReferenceTransform.InverseTransformDirection(projection.normalized);
            }

            Vector3.Normalize(vesselModule.autopilotDirectionWanted);

            // Define the roll reference
            /*
            Roll handling is problematic. The conflicting facts are :
            (1) We want the roll attitude to make sense from a player standpoint when flying the vessel
            (2) The reference should result in a smooth and continous change of the roll attitude, no sudden inversions
            (2) A fixed reference (independant from the vessel attitude) WILL have a "dead zone" causing a sudden inversion of the roll attitude

            In most cases, the player will want the roll reference to be relative to the "horizon line" (the blue/brown separation on the navball)
            The roll reference in this case is the "UP/DOWN" also called "RADIAL/ANTIRADIAL" direction.
            The issue is that as the vessel is pointing closer toward the radial vector, this reference become 
            unstable (bad for gameplay) and then undefined when the vessel is aligned toward radial/antiradial (bad for the code).
            In a lesser way, this problem also appear when the roll reference is the target.
            
            How we can handle this "roll reference is undefined" issue :
            - Disable roll lock when we enter the unstable zone
                - Easy solution to the issue
                - This mean that roll lock can't be used in some situations
            - Don't disable roll lock but don't apply the orientation in the unstable zone
                - This doesn't solve the "sudden 180° turn while in timewarp" issue
                    > The timewarp issue could maybe be lessened by lerping between the start and end points of the unstable zone
                    > This still will be stromboscopic at high timewarp rates
            - Enable the player to switch between different roll references
                - Possible roll references : mainbody up, mainbody north, target up, sun.
                - Notify the player if he has selected a potentially undefined orientation/rollref combination
                - Extra complexity, not very usefull in most situations
                - The issue can still appear and be handled somehow

            Possible references :
            Up/radial = vessel.upAxis
            North = Vector3d.Exclude(vessel.upAxis, vessel.mainBody.transform.up);
            */

            Vector3d rollRef = Vector3d.zero;

            if (vesselModule.autopilotContext == SpeedDisplayModes.Target
                || vesselModule.autopilotMode == SASMode.Target
                || vesselModule.autopilotMode == SASMode.AntiTarget)
            {
                if (vessel.targetObject is ModuleDockingNode || vessel.targetObject is CelestialBody)
                {
                    rollRef = vessel.targetObject.GetTransform().up;
                }
                else
                {
                    rollRef = -vessel.targetObject.GetTransform().forward;
                }
            }
            else
            {
                    rollRef = vessel.upAxis;
            }
            // Is the rollRef in the unstability zone ?
            // TODO : deactivate the RollLock marker when we are in the unstability zone
            double angleToRef = Vector3d.Angle(rollRef, vesselModule.autopilotDirectionWanted);
            if (angleToRef < 2.5 || angleToRef > 177.5)
            {
                vesselModule.lockedRollMode = false;
                rollRef = - vessel.GetTransform().forward;
            }

            // Get orientation
            switch (vesselModule.autopilotMode)
            {
                case SASMode.Hold:
                    if (!vesselModule.pilotIsIdle)
                    {
                        vesselModule.flyByWire = false;
                        vesselModule.autopilotDirectionWanted = vessel.GetTransform().up;
                        vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, -vessel.GetTransform().forward);
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
                            vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, -vessel.GetTransform().forward);
                        }
                    }
                    break;
                case SASMode.FlyByWire:
                    // TODO: fix this, add navball marker
                    if (!vesselModule.flyByWire)
                    {
                        vesselModule.flyByWire = true;
                        vesselModule.autopilotDirectionWanted = vessel.GetTransform().up;
                        vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, -vessel.GetTransform().forward);
                    }
                    FlightCtrlState s = vesselModule.flightCtrlState;

                    float pitchInput = vesselModule.flightCtrlState.pitch;
                    float yawInput = vesselModule.flightCtrlState.yaw;
                    vesselModule.autopilotDirectionWanted = Quaternion.Euler(pitchInput, yawInput, 0) * vesselModule.autopilotDirectionWanted;
                    vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, -vessel.GetTransform().forward);
                    vesselModule.flightCtrlState.pitch = 0;
                    vesselModule.flightCtrlState.yaw = 0;
                    break;
                case SASMode.KillRot:
                    vesselModule.flyByWire = false;
                    vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vessel.GetTransform().up, -vessel.GetTransform().forward);
                    break;
                default:
                    vesselModule.flyByWire = false;
                    vesselModule.autopilotAttitudeWanted = Quaternion.LookRotation(vesselModule.autopilotDirectionWanted, rollRef);
                    if (vesselModule.lockedRollMode)
                    {
                        vesselModule.autopilotAttitudeWanted *= Quaternion.Euler(0, 0, -vesselModule.currentRoll);
                    }
                    break;
            }

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
