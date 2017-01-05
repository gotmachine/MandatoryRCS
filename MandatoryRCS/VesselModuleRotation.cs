using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// TODO :
// - OK - Fix maneuver sas hold not being reset if maneuver node is modified during warp
// - OK - Restore SAS selection on loading
// - OK - Restore SAS selection on switching vessels in timewarp
// - TO TEST - Disable everything on prelaunch / landed / splashed
// - Add EC and RW torque conditions to SAS hold ?
// - Fix camera rotating when timewarping, need to find a reproductible case
// - use part.addforce instead of part.rigidbody.addforce, see KSP1.2 patchnotes --> not sure this is a good idea and things seemsto work fine as they are


namespace MandatoryRCS
{
    public class VesselModuleRotation : VesselModule
    {
        private const float lowVelocityThreesold = 0.025f;
        
        [KSPField(isPersistant = true)]
        public Vector3 angularVelocity;

        // True if we are keeping the vessel oriented roward the SAS target
        [KSPField(isPersistant = true)]
        public bool autopilotTargetHold;

        // Enum VesselAutopilot.AutopilotMode
        [KSPField(isPersistant = true)]
        public int autopilotMode; 

        // Enum FlightGlobals.SpeedDisplayModes
        // 0=orbit, 1=surface, 2=target, updated trough SpeedModeListener
        [KSPField(isPersistant = true)]
        public int autopilotContext;

        // Restore the angular velocity when loading / switching vessels
        private bool restoreAngularVelocity = false;

        // Apply the rotation toward the SAS selection when loading / switching vessels
        private bool restoreAutopilotTarget = false;

        // If set true by OnVesselChange event, we will try to restore the previous SAS selection
        public bool vesselSASHasChanged = false;

        // Var used to retry setting the SAS selection when loading / switching vessels
        private bool retrySAS = false;
        private int retrySASCount;
        private int setSASMode;

        // variable used to check if things have changed since last fixedUpdate
        // or when loading / switching vessels
        private double lastManeuverParameters;
        private object lastTarget = null;
        public int autopilotContextCurrent;

        private void FixedUpdate()
        {
            if (Vessel.loaded)
            {
                // Vessel is loaded but not in physics, either because 
                // - It is in the physics bubble but in non-psysics timewarp
                // - It has gone outside of the physics bubble
                // - It was just loaded, is in the physics bubble and will be unpacked in a few frames
                if (Vessel.packed) 
                {
                    // Check if target / maneuver is modified/deleted during timewarp
                    if (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRateIndex > 0)
                    {
                        autopilotTargetHold = TargetHoldValidity();
                    }
                    // We keep the vessel rotated toward the SAS target
                    if (autopilotTargetHold) 
                    {
                        RotateTowardTarget();
                    }
                    // We aren't holding a SAS target, rotate the vessel according to its angular velocity
                    else if (angularVelocity.magnitude > lowVelocityThreesold) 
                    {
                        RotatePacked();
                    }
                }
                else if (FlightGlobals.ready) // The vessel is in physics simulation and fully loaded
                {
                    // Restoring previous SAS selection after a vessel change
                    if (vesselSASHasChanged)
                    {
                        vesselSASHasChanged = false;
                        if (!RestoreSASMode(autopilotMode))
                        {
                            retrySAS = true;
                            setSASMode = autopilotMode;
                            retrySASCount = 10;
                        }
                    }

                    // Restoring angular velocity or rotation after entering physics
                    if (restoreAutopilotTarget) // Rotate to face SAS target
                    {
                        if (autopilotContext == autopilotContextCurrent) // Abort if the navball context (orbit/surface/target) has changed
                        {
                            Debug.Log("[US] " + Vessel.vesselName + " going OFF rails : applying rotation toward SAS target, autopilotMode=" + autopilotMode + ", targetMode=" + autopilotContext);
                            RotateTowardTarget();
                        }
                        restoreAutopilotTarget = false;
                    }
                    if (restoreAngularVelocity) // Restoring saved rotation if it was above the threesold
                    {
                        Debug.Log("[US] " + Vessel.vesselName + " going OFF rails : restoring angular velocity, angvel=" + angularVelocity.magnitude);
                        if (angularVelocity.magnitude > lowVelocityThreesold)
                        {
                            ApplyAngularVelocity();
                        }
                        restoreAngularVelocity = false;
                    }

                    // Sometimes the autopilot wasn't loaded fast enough, so we retry setting the SAS mode a few times
                    if (retrySAS)
                    {
                        if (retrySASCount > 0)
                        {
                            if (RestoreSASMode(setSASMode))
                            {
                                retrySAS = false;
                                Debug.Log("[US] autopilot mode " + setSASMode + " set at count " + retrySASCount);
                            }
                            retrySASCount--;
                        }
                        else
                        {
                            retrySAS = false;
                            Debug.Log("[US] can't set autopilot mode.");
                        }
                    }

                    // Saving angular velocity, SAS mode and checking if pointing toward target
                    // Do it in an else because some fixedupdate can happen before ?
                    SaveOffRailsStatus();
                }
            }

            // Saving the currrent target
            lastTarget = Vessel.targetObject;
            // Saving the current autopilot context
            autopilotContext = autopilotContextCurrent;
            // Saving the maneuver vector magnitude
            if (Vessel.patchedConicSolver != null)
            {
                if (Vessel.patchedConicSolver.maneuverNodes.Count > 0) 
                {
                    lastManeuverParameters = Vessel.patchedConicSolver.maneuverNodes[0].DeltaV.magnitude + Vessel.patchedConicSolver.maneuverNodes[0].UT; 
                }
            }
        }

        // Vessel is leaving physics simulation
        // This is called only when timewarping (not on vessel unload)
        public override void OnGoOnRails()
        {
            Debug.Log("[US] " + Vessel.vesselName + " going ON rails, on target ? " + autopilotTargetHold + ", autopilotMode=" + autopilotMode + ", targetMode=" + autopilotContext + ", angvel=" + angularVelocity.magnitude);
        }

        // Vessel is entering physics simulation, either by being loaded or getting out of timewarp
        // Don't restore rotation/angular velocity here because the vessel isn't fully loaded
        // Mark it to be done in a latter FixedUpdate, where we can check for FlightGlobals.ready
        public override void OnGoOffRails()
        {
            restoreAutopilotTarget = autopilotTargetHold;
            restoreAngularVelocity = !autopilotTargetHold;
        }

        private void ApplyAngularVelocity()
        {
            if (Vessel.situation == Vessel.Situations.PRELAUNCH || Vessel.situation == Vessel.Situations.LANDED || Vessel.situation == Vessel.Situations.SPLASHED)
            {
                return;
            }

            Debug.Log("[US] Restoring " + Vessel.vesselName + "rotation after timewarp/load" );
            Vector3 COM = Vessel.CoM;
            Quaternion rotation = Vessel.ReferenceTransform.rotation;

            // Applying force on every part
            foreach (Part p in Vessel.parts)
            {
                if (!p.GetComponent<Rigidbody>()) continue;
                p.GetComponent<Rigidbody>().AddTorque(rotation * angularVelocity, ForceMode.VelocityChange);
                p.GetComponent<Rigidbody>().AddForce(Vector3.Cross(rotation * angularVelocity, (p.transform.position - COM)), ForceMode.VelocityChange);

                // This should (?) be done like this but I can't find how to convert the ForceMode.VelocityChange value into ForceMode.Force
                // p.AddTorque((rotation * angularVelocity) / (p.resourceMass + p.mass));
                // p.AddForce(Vector3.Cross(rotation * angularVelocity, (p.transform.position - COM)) / (p.resourceMass + p.mass));
            }
        }

        private void RotateTowardTarget()
        {
            if (Vessel.situation == Vessel.Situations.PRELAUNCH || Vessel.situation == Vessel.Situations.LANDED || Vessel.situation == Vessel.Situations.SPLASHED)
            {
                return;
            }

            Vessel.SetRotation(Quaternion.FromToRotation(Vessel.GetTransform().up, AutopilotTargetDirection()) * Vessel.transform.rotation, true);
        }

        private void RotatePacked()
        {
            if (Vessel.situation == Vessel.Situations.PRELAUNCH || Vessel.situation == Vessel.Situations.LANDED || Vessel.situation == Vessel.Situations.SPLASHED)
            {
                return;
            }

            Vessel.SetRotation(Quaternion.AngleAxis(angularVelocity.magnitude * TimeWarp.CurrentRate, Vessel.ReferenceTransform.rotation * angularVelocity) * Vessel.transform.rotation, true);
        }

        private void SaveOffRailsStatus()
        {
            // Saving the current angular velocity, zeroing it if negligeable
            if (Vessel.angularVelocity.magnitude < lowVelocityThreesold)
            {
                angularVelocity = Vector3.zero;
            }
            else
            {
                angularVelocity = Vessel.angularVelocity;
            }

            // Checking if the autopilot hold mode should be enabled
            if (Vessel.Autopilot.Enabled
                && !(Vessel.Autopilot.Mode.Equals(VesselAutopilot.AutopilotMode.StabilityAssist))
                && angularVelocity.magnitude < 0.05f // The vessel isn't rotating
                && Math.Max(Vector3.Dot(Vessel.Autopilot.SAS.targetOrientation.normalized, Vessel.GetTransform().up.normalized), 0) > 0.95f) // 1.0 = toward target, 0.0 = target is at a 90° angle
            {
                autopilotTargetHold = true;
            }
            else
            {
                autopilotTargetHold = false;
            }

            // Saving the current SAS mode
            autopilotMode = (int)Vessel.Autopilot.Mode;
        }

        private bool RestoreSASMode(int mode)
        {
            if (Vessel.Autopilot.Enabled)
            {
                return Vessel.Autopilot.SetMode((VesselAutopilot.AutopilotMode)mode);
            }
            else
            {
                return false;
            }

        }

        private bool TargetHoldValidity()
        {
            // Disable target hold if navball context is changed
            if (autopilotContextCurrent != autopilotContext)
            {
                return false;
            }

            // Disable target hold if target was modified
            if ((autopilotMode == 7 || autopilotMode == 8 || autopilotContext == 2) && Vessel.targetObject != lastTarget)
            {
                return false;
            }

            // Disable target hold if the maneuver node was modified or deleted
            if (autopilotMode == 9)
            {
                if (Vessel.patchedConicSolver.maneuverNodes.Count == 0)
                {
                    return false;
                }
                else if (Math.Abs(Vessel.patchedConicSolver.maneuverNodes[0].DeltaV.magnitude + Vessel.patchedConicSolver.maneuverNodes[0].UT) - Math.Abs(lastManeuverParameters) > 0.01f)
                {
                    return false;
                }
            }
            return true;
        }


        // Return the orientation vector of the saved SAS mode and context
        private Vector3 AutopilotTargetDirection()
        {
            Vector3 target = new Vector3();

            // Prograde/Retrograde
            if (autopilotMode == 1 || autopilotMode == 2) 
            {
                if (autopilotContext == 0) // Orbit prograde
                {target = Vessel.obt_velocity;} 
                else if (autopilotContext == 1) // Surface prograde
                {target = Vessel.srf_velocity;} 
                else if (autopilotContext == 2) // Target prograde
                {
                    if (Vessel.targetObject != null) 
                    { target = -(Vessel.targetObject.GetObtVelocity() - Vessel.obt_velocity); }
                    else 
                    { return Vessel.GetTransform().up; }
                }

                if (autopilotMode == 2) // Invert vector for retrograde
                {
                    target = -target;
                }
            }

            // Normal/Radial
            else if (autopilotMode == 3 || autopilotMode == 4 || autopilotMode == 5 || autopilotMode == 6) 
            {
                // Get body up vector
                Vector3 planetUp = (Vessel.rootPart.transform.position - Vessel.mainBody.position).normalized;

                // Get normal vector
                Vector3 normal = new Vector3();
                if (autopilotContext == 0) // Orbit
                {normal = Vector3.Cross(Vessel.obt_velocity, planetUp).normalized;}
                else if (autopilotContext == 1 || autopilotContext == 2) // Surface/Target (seems to be the same for normal/radial)
                {normal = Vector3.Cross(Vessel.srf_velocity, planetUp).normalized;}

                // Return normal/antinormal or calculate radial
                if (autopilotMode == 3) // Normal
                {target = normal;}
                else if (autopilotMode == 4) // AntiNormal
                {target = -normal;}
                else
                {
                    // Get RadialIn vector
                    Vector3 radial = new Vector3();
                    if (autopilotContext == 0) // Orbit
                    {radial = Vector3.Cross(Vessel.obt_velocity, normal).normalized;}
                    else if (autopilotContext == 1 || autopilotContext == 2) // Surface/Target (seems to be the same for normal/radial)
                    {radial = Vector3.Cross(Vessel.srf_velocity, normal).normalized;}

                    // Return radial vector
                    if (autopilotMode == 5) // Radial In
                    {target = -radial;}
                    else if (autopilotMode == 6) // Radial Out
                    {target = radial;}
                }
            }

            // Target/Antitarget
            else if (autopilotMode == 7 || autopilotMode == 8) 
            {
                if (Vessel.targetObject != null)
                {
                    if (autopilotMode == 7) // Target
                    {target = Vessel.targetObject.GetTransform().position - Vessel.transform.position;}
                    
                    if (autopilotMode == 8) // AntiTarget
                    {target = -(Vessel.targetObject.GetTransform().position - Vessel.transform.position);}
                }
                else
                {
                    // No orientation keeping if target is null
                    return Vessel.GetTransform().up;
                }
            }

            // Maneuver
            else if (autopilotMode == 9)
            {
                if (Vessel.patchedConicSolver.maneuverNodes.Count > 0)
                {
                    target = Vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(Vessel.orbit);
                }
                else
                {
                    // No orientation keeping if there is no more maneuver node
                    return Vessel.GetTransform().up;
                }
            }

            // This shouldn't happen
            else
            {
                // Abort orientation keeping
                autopilotTargetHold = false;
                return Vessel.GetTransform().up;
            }

            return target;
        }

        // Copypasted from PersistentRotation main.cs
        private Quaternion FromToRotation(Vector3d fromv, Vector3d tov) //Stock FromToRotation() doesn't work correctly
        {
            Vector3d cross = Vector3d.Cross(fromv, tov);
            double dot = Vector3d.Dot(fromv, tov);
            double wval = dot + Math.Sqrt(fromv.sqrMagnitude * tov.sqrMagnitude);
            double norm = 1.0 / Math.Sqrt(cross.sqrMagnitude + wval * wval);
            return new QuaternionD(cross.x * norm, cross.y * norm, cross.z * norm, wval * norm);
        }
    }
}
