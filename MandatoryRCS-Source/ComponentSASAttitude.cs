using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MandatoryRCS
{
    public class ComponentSASAttitude : ComponentBase
    {

        
        public override void ComponentUpdate()
        {
            // Isn't called for unloaded vessels
            if (vesselModule.currentState == VesselModuleMandatoryRCS.VesselState.Unloaded) { return; }

            // First some sanity checks for the SAS modes
            if (vesselModule.SASContext == FlightGlobals.SpeedDisplayModes.Target && vessel.targetObject == null)
            {
                vesselModule.SASContext = FlightGlobals.SpeedDisplayModes.Orbit;
                vesselModule.SASMode = SASUI.SASFunction.KillRot;
            }
            if (vesselModule.SASMode == SASUI.SASFunction.Maneuver && vessel.patchedConicSolver.maneuverNodes.Count < 1)
            {
                vesselModule.SASMode = vesselModule.SASMode = SASUI.SASFunction.KillRot;
            }

            // Calculate requested attitude
            vesselModule.directionWanted = vessel.GetTransform().up;
            vesselModule.attitudeWanted = Quaternion.identity;

            Vector3d rollDirection = -vessel.GetTransform().forward;

            // Get direction vector
            switch (vesselModule.SASMode)
            {
                case SASUI.SASFunction.Prograde:
                case SASUI.SASFunction.Retrograde:
                    if (vesselModule.SASContext == FlightGlobals.SpeedDisplayModes.Orbit) // Orbit prograde
                    { vesselModule.directionWanted = vessel.obt_velocity; }
                    else if (vesselModule.SASContext == FlightGlobals.SpeedDisplayModes.Surface) // Surface prograde
                    { vesselModule.directionWanted = vessel.srf_velocity; }
                    else if (vesselModule.SASContext == FlightGlobals.SpeedDisplayModes.Target) // Target prograde
                    {
                        if (vessel.targetObject != null)
                        { vesselModule.directionWanted = -(vessel.targetObject.GetObtVelocity() - vessel.obt_velocity); }
                    }
                    if (vesselModule.SASMode == SASUI.SASFunction.Retrograde) // Invert vector for retrograde
                    {
                        vesselModule.directionWanted = -vesselModule.directionWanted;
                    }
                    break;
                case SASUI.SASFunction.Normal:
                case SASUI.SASFunction.AntiNormal:
                case SASUI.SASFunction.RadialOut:
                case SASUI.SASFunction.RadialIn:
                    // Get body up vector
                    Vector3d planetUp = (vessel.rootPart.transform.position - vessel.mainBody.position).normalized;
                    // Get normal vector
                    Vector3d normal = new Vector3();
                    if (vesselModule.SASContext == FlightGlobals.SpeedDisplayModes.Orbit) // Orbit
                    { normal = Vector3.Cross(vessel.obt_velocity, planetUp).normalized; }
                    else // Surface/Target (seems to be the same for normal/radial)
                    { normal = Vector3.Cross(vessel.srf_velocity, planetUp).normalized; }

                    // Return normal/antinormal or calculate radial
                    if (vesselModule.SASMode == SASUI.SASFunction.Normal) // Normal
                    { vesselModule.directionWanted = normal; }
                    else if (vesselModule.SASMode == SASUI.SASFunction.AntiNormal) // AntiNormal
                    { vesselModule.directionWanted = -normal; }
                    else
                    {
                        // Get RadialIn vector
                        Vector3d radial = new Vector3();
                        if (vesselModule.SASContext == FlightGlobals.SpeedDisplayModes.Orbit) // Orbit
                        { radial = Vector3.Cross(vessel.obt_velocity, normal).normalized; }
                        else // Surface/Target (seems to be the same for normal/radial)
                        { radial = Vector3.Cross(vessel.srf_velocity, normal).normalized; }

                        // Return radial vector
                        if (vesselModule.SASMode == SASUI.SASFunction.RadialIn) // Radial In
                        { vesselModule.directionWanted = radial; }
                        else if (vesselModule.SASMode == SASUI.SASFunction.RadialOut) // Radial Out
                        { vesselModule.directionWanted = -radial; }
                    }
                    break;
                case SASUI.SASFunction.Maneuver:
                    if (vessel.patchedConicSolver.maneuverNodes.Count < 1) { break; }
                    vesselModule.directionWanted = vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(vessel.orbit);
                    break;
                case SASUI.SASFunction.Target:
                    if (vessel.targetObject == null) { break; }
                    vesselModule.directionWanted = vessel.targetObject.GetTransform().position - vessel.transform.position;
                    break;
                case SASUI.SASFunction.AntiTarget:
                    if (vessel.targetObject == null) { break; }
                    vesselModule.directionWanted = -(vessel.targetObject.GetTransform().position - vessel.transform.position);
                    break;
                case SASUI.SASFunction.Parallel:
                    if (vessel.targetObject == null) { break; }
                    vesselModule.directionWanted = vessel.targetObject.GetTransform().up;
                    break;
                case SASUI.SASFunction.AntiParallel:
                    if (vessel.targetObject == null) { break; }
                    vesselModule.directionWanted = -(vessel.targetObject.GetTransform().up);
                    break;
                case SASUI.SASFunction.ProgradeCorrected:
                case SASUI.SASFunction.RetrogradeCorrected:
                    if (vessel.targetObject == null) { break; }
                    Vector3 targetDirInv = vessel.transform.position - vessel.targetObject.GetTransform().position;
                    Vector3 targetRelVel = vessel.GetObtVelocity() - vessel.targetObject.GetObtVelocity();

                    Vector3 correction = Vector3.ProjectOnPlane(-targetRelVel,targetDirInv);

                    // Avoid chasing the target when relative velocity is very low
                    if (correction.magnitude < 0.05)
                    {
                        vesselModule.directionWanted = -targetDirInv;
                        break;
                    }
                    // approch target direction
                    else
                    {
                        correction = correction * ((targetDirInv.magnitude / correction.magnitude) * Math.Max(correction.magnitude / targetDirInv.magnitude, 1.0f));
                    }

                    vesselModule.directionWanted = correction - targetDirInv;

                    if (vesselModule.SASMode == SASUI.SASFunction.RetrogradeCorrected)
                    {
                        vesselModule.directionWanted = -vesselModule.directionWanted;
                    }
                    break;

                    //Vector3 projection = targetPos.normalized + Vector3.ProjectOnPlane(targetvel, targetPos);
                    //float projectionMagn = projection.magnitude;
                    //projection = projectionMagn > 1.0f ? projection / projectionMagn * 1.0f : projection;
                    //direction = vessel.ReferenceTransform.InverseTransformDirection(projection.normalized);
            }

            // Get orientation
            switch (vesselModule.SASMode)
            {
                case SASUI.SASFunction.Hold:
                    vesselModule.attitudeWanted = Quaternion.LookRotation(vessel.GetTransform().up, -vessel.GetTransform().forward);
                    break;
                case SASUI.SASFunction.HoldSmooth:
                    vesselModule.attitudeWanted = Quaternion.LookRotation(vessel.GetTransform().up, -vessel.GetTransform().forward);
                    break;
                case SASUI.SASFunction.KillRot:
                    vesselModule.attitudeWanted = Quaternion.LookRotation(vessel.GetTransform().up, -vessel.GetTransform().forward);
                    break;
                default:
                    // Define the roll reference
                    Vector3 rollRef = Vector3.zero;
                    switch (vesselModule.SASContext)
                    {
                        // TODO : if directionwanted is perfectly aligned with the rollref, Quaternion.LookRotation can return a random rotation
                        // Typically this will happen when both radial/antiradial and locked roll are enabled
                        // Maybe we should use another vector as the rollref for radial/antiradial, maybe the sun since it make sense from a gameplay standpoint.
                        // Or simply disable locked roll for radial/antiradial
                        case FlightGlobals.SpeedDisplayModes.Orbit:
                            rollRef = (vessel.rootPart.transform.position - vessel.mainBody.position).normalized;
                            break;
                        case FlightGlobals.SpeedDisplayModes.Surface:
                            rollRef = (vessel.rootPart.transform.position - vessel.mainBody.position).normalized;
                            break;
                        case FlightGlobals.SpeedDisplayModes.Target:
                            rollRef = -vessel.targetObject.GetTransform().forward;
                            break;
                    }
                    vesselModule.attitudeWanted = Quaternion.LookRotation(vesselModule.directionWanted, rollRef);


                    if (vesselModule.lockedRollMode)
                    {
                        vesselModule.attitudeWanted *= Quaternion.Euler(0, 0, -vesselModule.currentRoll);
                    }
                    break;
            }

            // Checking if the timewarping / persistent hold mode should be enabled
            if (vesselModule.currentState == VesselModuleMandatoryRCS.VesselState.InPhysics)
            {
                if (vessel.Autopilot.Enabled
                    && vesselModule.SASMode != SASUI.SASFunction.KillRot
                    && vesselModule.SASMode != SASUI.SASFunction.Hold
                    && vesselModule.SASMode != SASUI.SASFunction.HoldSmooth
                    //&& (wheelsTotalMaxTorque > Single.Epsilon) // We have some reaction wheels
                    && vesselModule.angularVelocity.magnitude < Settings.velocityThreesold * 2 // The vessel isn't rotating too much
                    && Math.Max(Vector3.Dot(vesselModule.directionWanted.normalized, vessel.GetTransform().up.normalized), 0) > 0.975f) // 1.0 = toward target, 0.0 = target is at a 90° angle, previously 0.95
                {
                    vesselModule.SASModeLock = true;
                }
                else
                {
                    vesselModule.SASModeLock = false;
                }
            }
        }
    }
}
