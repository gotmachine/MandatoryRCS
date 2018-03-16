using MuMech;
using System;
using UnityEngine;

namespace MandatoryRCS
{


    public class VesselModuleRotation : VesselModule
    {
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

        // Store total torque avaible from the vessel reactions wheels
        public float wheelsTotalMaxTorque = 0.0f; // NOT THE TORQUE, the sum of all rw components magnitudes
        public float velSaturationTorqueFactor = 1.0f;
        public bool updateWheelsTotalMaxTorque = true;

        // SAS target direction is made available for the ModuleTorqueController
        public Vector3 targetDirection;

        public MRCSMechJebCore mechjeb;
        public MechJebModuleSmartASS SmartASS;

        public SASHandler.SASFunction customSASMode;



        protected override void OnStart()
        {
            base.OnStart();
            Vessel.OnPreAutopilotUpdate += new FlightInputCallback(DisableStockSAS);
        }

        private void DisableStockSAS(FlightCtrlState s)
        {
            // Get the mechjeb module
            if (mechjeb == null)
            {
                mechjeb = Vessel.GetMRCSMasterMechJeb();
                if (mechjeb != null)
                {
                    // cast the attitude instance to our customized child class that handle stock SAS re-enabling
                    mechjeb.attitude = (MRCSAttitudeController)mechjeb.attitude;

                    // get references to useful modules
                    SmartASS = mechjeb.GetComputerModule<MechJebModuleSmartASS>();
                }
            }

            if (mechjeb == null || SmartASS == null) return;

            if (!mechjeb.running) return;

            // Disable stock autopilot
            Vessel.Autopilot.SAS.DisconnectFlyByWire();

            // Disable smartASS if stock SAS is disabled
            if (!Vessel.Autopilot.Enabled)
            {
                if (SmartASS.target != MechJebModuleSmartASS.Target.OFF)
                {
                    SmartASS.target = MechJebModuleSmartASS.Target.OFF;
                    SmartASS.Engage();
                }
                return;
            }

            //
            switch (customSASMode)
            {
                case SASHandler.SASFunction.Hold:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.KILLROT, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.HoldSmooth:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.KILLROT, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.KillRot:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.KILLROT, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.Prograde:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.PROGRADE, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.Retrograde:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.RETROGRADE, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.Normal:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.NORMAL_PLUS, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.AntiNormal:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.NORMAL_MINUS, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.RadialOut:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.RADIAL_PLUS, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.RadialIn:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.RADIAL_MINUS, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.Target:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.TARGET_PLUS, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.AntiTarget:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.TARGET_MINUS, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.Maneuver:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.NODE, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.Parallel:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target.PARALLEL_PLUS, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.AntiParallel:
                    SetSMARTASSMode(MechJebModuleSmartASS.Target., FlightGlobals.speedDisplayMode);
                    break;
                default:
                    break;
            }
        }

        private void SetSMARTASSMode(MechJebModuleSmartASS.Target target, FlightGlobals.SpeedDisplayModes mode)
        {
            switch (mode)
            {
                case FlightGlobals.SpeedDisplayModes.Orbit:
                    SetSMARTASSMode(target, MechJebModuleSmartASS.Mode.ORBITAL);
                    break;
                case FlightGlobals.SpeedDisplayModes.Surface:
                    SetSMARTASSMode(target, MechJebModuleSmartASS.Mode.SURFACE);
                    break;
                case FlightGlobals.SpeedDisplayModes.Target:
                    SetSMARTASSMode(target, MechJebModuleSmartASS.Mode.TARGET);
                    break;
            }
        }

        private void SetSMARTASSMode(MechJebModuleSmartASS.Target target, MechJebModuleSmartASS.Mode mode)
        {
            if (SmartASS.target != target)
            {
                SmartASS.mode = mode;
                SmartASS.target = target;
                SmartASS.Engage();
            }
        }

        private void FixedUpdate()
        {
            // Get the SAS target direction in any case, because ModuleTorqueControler need it
            if (Vessel.loaded)
            {
                targetDirection = AutopilotTargetDirection();
            }

            if (MandatoryRCSSettings.featureSASRotation)
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
                        if (autopilotTargetHold && TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRateIndex > 0)
                        {
                            autopilotTargetHold = TargetHoldValidity();
                        }
                        // We keep the vessel rotated toward the SAS target
                        if (autopilotTargetHold)
                        {
                            RotateTowardTarget();
                        }
                        // We aren't holding a SAS target, rotate the vessel according to its angular velocity
                        else if (angularVelocity.magnitude > MandatoryRCSSettings.velocityThreesold)
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
                                // Debug.Log("[US] " + Vessel.vesselName + " going OFF rails : applying rotation toward SAS target, autopilotMode=" + autopilotMode + ", targetMode=" + autopilotContext);
                                RotateTowardTarget();
                            }
                            restoreAutopilotTarget = false;
                        }
                        if (restoreAngularVelocity) // Restoring saved rotation if it was above the threesold
                        {
                            // Debug.Log("[US] " + Vessel.vesselName + " going OFF rails : restoring angular velocity, angvel=" + angularVelocity.magnitude);
                            if (angularVelocity.magnitude > MandatoryRCSSettings.velocityThreesold)
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
                                    // Debug.Log("[US] autopilot mode " + setSASMode + " set at count " + retrySASCount);
                                }
                                retrySASCount--;
                            }
                            else
                            {
                                retrySAS = false;
                                // Debug.Log("[US] can't set autopilot mode.");
                            }
                        }

                        // Saving angular velocity, SAS mode, calculate reaction wheels torque and check target hold status
                        SaveOffRailsStatus();
                    }
                }
                // Saving this FixedUpdate target, autopilot context and maneuver node, to check if they have changed in the next FixedUpdate
                SaveLastUpdateStatus();
            }

            // Rotation/SAS feature is disabled, we only update the things needed for the reaction wheels feature
            else
            {
                if (Vessel.loaded && !Vessel.packed && FlightGlobals.ready)
                {
                    // Saving angular velocity, SAS mode, calculate reaction wheels torque and check target hold status
                    SaveOffRailsStatus();
                }
            }
        }

        // Vessel is entering physics simulation, either by being loaded or getting out of timewarp
        // Don't restore rotation/angular velocity here because the vessel/scene isn't fully loaded
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

            // Debug.Log("[US] Restoring " + Vessel.vesselName + "rotation after timewarp/load" );
            Vector3 COM = Vessel.CoM;
            Quaternion rotation = Vessel.ReferenceTransform.rotation;

            // Applying force on every part
            foreach (Part p in Vessel.parts)
            {
                if (!p.GetComponent<Rigidbody>()) continue;
                p.GetComponent<Rigidbody>().AddTorque(rotation * angularVelocity, ForceMode.VelocityChange);
                p.GetComponent<Rigidbody>().AddForce(Vector3.Cross(rotation * angularVelocity, (p.transform.position - COM)), ForceMode.VelocityChange);

                // Doing this trough rigidbody is depreciated but I can't find a reliable way to use the 1.2 part.addforce/addtorque so they provide reliable results
                // see 1.2 patchnotes and unity docs for ForceMode.VelocityChange/ForceMode.Force
            }
        }

        private void RotateTowardTarget()
        {
            if (Vessel.situation == Vessel.Situations.PRELAUNCH || Vessel.situation == Vessel.Situations.LANDED || Vessel.situation == Vessel.Situations.SPLASHED)
            {
                return;
            }

            Vessel.SetRotation(Quaternion.FromToRotation(Vessel.GetTransform().up, targetDirection) * Vessel.transform.rotation, true); // SetPos=false seems to break the game on some occasions...
        }

        private void RotatePacked()
        {
            if (Vessel.situation == Vessel.Situations.PRELAUNCH || Vessel.situation == Vessel.Situations.LANDED || Vessel.situation == Vessel.Situations.SPLASHED)
            {
                return;
            }

            Vessel.SetRotation(Quaternion.AngleAxis(angularVelocity.magnitude * TimeWarp.CurrentRate, Vessel.ReferenceTransform.rotation * angularVelocity) * Vessel.transform.rotation, true); // false seems to fix the "infinite roll bug"
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

        private void SaveOffRailsStatus()
        {
            // Saving the current angular velocity, zeroing it if negligeable
            if (Vessel.angularVelocity.magnitude < MandatoryRCSSettings.velocityThreesold)
            {
                angularVelocity = Vector3.zero;
            }
            else
            {
                angularVelocity = Vessel.angularVelocity;
            }

            // The vessel has been loaded or changed (onVesselStandardModification), update the avaible max torque from reaction wheels
            if (updateWheelsTotalMaxTorque)
            {
                wheelsTotalMaxTorque = 0.0f;
                foreach (ModuleTorqueController mtc in Vessel.FindPartModulesImplementing<ModuleTorqueController>())
                {
                    wheelsTotalMaxTorque += mtc.maxTorque.magnitude; // Not exact but good enough
                }
                updateWheelsTotalMaxTorque = false;
            }

            // Determine the velocity saturation factor for reaction wheels (used by ModuleTorqueController)
            if (MandatoryRCSSettings.velocitySaturation)
            {
                velSaturationTorqueFactor = Math.Max(1.0f - Math.Min((Math.Max(angularVelocity.magnitude - MandatoryRCSSettings.saturationMinAngVel, 0.0f) * MandatoryRCSSettings.saturationMaxAngVel), 1.0f), MandatoryRCSSettings.saturationMinTorqueFactor);
            }
            else
            {
                velSaturationTorqueFactor = 1.0f;
            }
            

            // Checking if the autopilot hold mode should be enabled
            if (Vessel.Autopilot.Enabled
                && !(Vessel.Autopilot.Mode.Equals(VesselAutopilot.AutopilotMode.StabilityAssist))
                && (wheelsTotalMaxTorque > Single.Epsilon) // We have some reaction wheels
                && angularVelocity.magnitude < MandatoryRCSSettings.velocityThreesold * 2 // The vessel isn't rotating too much
                && Math.Max(Vector3.Dot(Vessel.Autopilot.SAS.targetOrientation.normalized, Vessel.GetTransform().up.normalized), 0) > 0.975f) // 1.0 = toward target, 0.0 = target is at a 90° angle, previously 0.95
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

        private void SaveLastUpdateStatus()
        {
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

            // 
            else
            {
                // Abort orientation keeping
                // autopilotTargetHold = false;
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
