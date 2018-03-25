
/* LICENSE INFORMATION
 * This file and all code it contains is licensed under the GNU General Public License v3.0
 * It is derived from MechJeb2 Copyright (C) 2013 
 * MechJeb2 can be found at https://github.com/MuMech/MechJeb2
 */

using MandatoryRCS.Lib;
using System;
using System.Linq;
using UnityEngine;

/* MECHJEB INTEGRATION GOALS :
 * - SAS Menu
 *   - "Force roll"
 *   - "Maximum relative angular velocity" -> slider in the SAS menu
 *   - "autoexecute maneuver" -> to be made compliant with control levels
 * - RCS Menu
 *   - "RCS auto mode" -> needs to be tweaked ?
 *   - "RCS balancer" + overdrive level -> test overdrive 
 *   - global RCS throttle : currently not implemented in mechjeb.
 * - SAS markers revamp :
 * 

    TODO :
    // Disable target hold if navball context is changed
    // Disable target hold if target was modified
    // Disable target hold if the maneuver node was modified or deleted

    old code :
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

    in a flight events class :
            private void Start()
        {
            GameEvents.onSetSpeedMode.Add(onSetSpeedMode);
            }
            // Detect navball context (orbit/surface/target) changes
        private void onSetSpeedMode(FlightGlobals.SpeedDisplayModes mode)
        {
            FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleRotation>().First().autopilotContextCurrent = (int)mode;
        }
                private void OnDestroy()
        {
            GameEvents.onSetSpeedMode.Remove(onSetSpeedMode);
            }


 */


namespace MandatoryRCS
{

    public class ComponentCustomSAS : MandatoryRCSComponent
    {
        #region PID/ACTION
        public PIDControllerV3 pid;
        public Vector3d lastAct = Vector3d.zero;
        public Vector3d pidAction;  //info
        public Vector3d error;  //info

        public bool Tf_autoTune = true;

        public Vector3d TfV = new Vector3d(0.3, 0.3, 0.3);
        private Vector3 TfVec = new Vector3(0.3f, 0.3f, 0.3f);  // use the serialize since Vector3d does not
        public double TfMin = 0.1;
        public double TfMax = 0.5;
        public bool lowPassFilter = true;

        public double kpFactor = 3;
        public double kiFactor = 6;
        public double kdFactor = 0.5;

        public double deadband = 0.0001;

        public double kWlimit = 0.15; // max angular velocity, we need to add a UI for that

        protected Vector3d axisControl = Vector3d.one;
        #endregion

        public override void OnStart()
        {
            pid = new PIDControllerV3(Vector3d.zero, Vector3d.zero, Vector3d.zero, 1, -1);
            setPIDParameters();
            lastAct = Vector3d.zero;
            vessel.OnPreAutopilotUpdate += new FlightInputCallback(SASUpdate);
            base.OnStart();
        }

        //public override void OnLoad(ConfigNode c)
        //{
        //    base.OnLoad(c);
        //    TfV = TfVec;
        //}

        //public override void OnSave(ConfigNode c)
        //{
        //    TfVec = TfV;
        //    base.OnSave(c);
        //}

        public override void FixedUpdate()
        {
            // Update attitude in timewarp, needed for the persistant rotation component
            // so it can follow the SAS target
            if (FlightGlobals.ActiveVessel != null && vessel.loaded && vessel.packed)
            {
                UpdateRequestedAttitude();
            }
            
        }


        // main callback
        private void SASUpdate(FlightCtrlState s)
        {
            // Update context and enabled status
            if (FlightGlobals.ActiveVessel == vessel)
            {
                vesselModule.SASContext = FlightGlobals.speedDisplayMode;
                vesselModule.SASisEnabled = FlightGlobals.ActiveVessel.Autopilot.Enabled;
            }

            if (vesselModule.SASisEnabled)
            {
                // Disable stock SAS
                vessel.Autopilot.SAS.DisconnectFlyByWire();

                // Update requested attitude
                UpdateRequestedAttitude();

                // Calculate needed action
                UpdateSASAction(s);
            }

        }

        // Calculate requested attitude
        private void UpdateRequestedAttitude()
        {
            
            vesselModule.directionWanted = vessel.GetTransform().up;
            vesselModule.attitudeWanted = Quaternion.identity;

            Vector3 rollDirection = -vessel.GetTransform().forward;



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
                    Vector3 planetUp = (vessel.rootPart.transform.position - vessel.mainBody.position).normalized;
                    // Get normal vector
                    Vector3 normal = new Vector3();
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
                        Vector3 radial = new Vector3();
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
                        correction = correction * ((targetDirInv.magnitude / correction.magnitude) * Math.Max(correction.magnitude / targetDirInv.magnitude, 1.0f)) ;
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
                        axisControl.y = 1;
                    }
                    else
                    {
                        axisControl.y = 0;
                    }
                    break;
            }
        }

        public void UpdateSASAction(FlightCtrlState s)
        {
            Transform vesselTransform = vessel.ReferenceTransform;
            //Quaternion delta = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vesselTransform.rotation) * _requestedAttitude);

            // Find out the real shorter way to turn where we wan to.
            // Thanks to HoneyFox
            Vector3d tgtLocalUp = vesselTransform.transform.rotation.Inverse() * vesselModule.attitudeWanted * Vector3d.forward;
            Vector3d curLocalUp = Vector3d.up;

            double turnAngle = Math.Abs(Vector3d.Angle(curLocalUp, tgtLocalUp));
            Vector2d rotDirection = new Vector2d(tgtLocalUp.x, tgtLocalUp.z);
            rotDirection = rotDirection.normalized * turnAngle / 180.0;

            // And the lowest roll
            // Thanks to Crzyrndm
            Vector3 normVec = Vector3.Cross(vesselModule.attitudeWanted * Vector3.forward, vesselTransform.up);
            Quaternion targetDeRotated = Quaternion.AngleAxis((float)turnAngle, normVec) * vesselModule.attitudeWanted;
            float rollError = Vector3.Angle(vesselTransform.right, targetDeRotated * Vector3.right) * Math.Sign(Vector3.Dot(targetDeRotated * Vector3.right, vesselTransform.forward));

            // From here everything should use MOI order for Vectors (pitch, roll, yaw)
            error = new Vector3d(
                -rotDirection.y * Math.PI,
                rollError * Mathf.Deg2Rad,
                rotDirection.x * Math.PI
                );

            error.Scale(axisControl);

            Vector3d err = error + vesselModule.inertia * 0.5;
            err = new Vector3d(
                Math.Max(-Math.PI, Math.Min(Math.PI, err.x)),
                Math.Max(-Math.PI, Math.Min(Math.PI, err.y)),
                Math.Max(-Math.PI, Math.Min(Math.PI, err.z)));

            // ( MoI / available torque ) factor:
            Vector3d NormFactor = Vector3d.Scale(vessel.MOI, vesselModule.torqueAvailable.InvertNoNaN());

            err.Scale(NormFactor);

            // angular velocity:
            Vector3d omega = vessel.angularVelocity;
            //omega.x = vessel.angularVelocity.x;
            //omega.y = vessel.angularVelocity.z; // y <=> z
            //omega.z = vessel.angularVelocity.y; // z <=> y
            omega.Scale(NormFactor);

            if (Tf_autoTune)
                tuneTf(vesselModule.torqueAvailable);
            setPIDParameters();

            // angular velocity limit:
            var Wlimit = new Vector3d( Math.Sqrt(NormFactor.x * Math.PI * kWlimit),
                                        Math.Sqrt(NormFactor.y * Math.PI * kWlimit),
                                        Math.Sqrt(NormFactor.z * Math.PI * kWlimit));

            pidAction = pid.Compute(err, omega, Wlimit);

            // deadband
            pidAction.x = Math.Abs(pidAction.x) >= deadband ? pidAction.x : 0.0;
            pidAction.y = Math.Abs(pidAction.y) >= deadband ? pidAction.y : 0.0;
            pidAction.z = Math.Abs(pidAction.z) >= deadband ? pidAction.z : 0.0;

            // low pass filter,  wf = 1/Tf:
            Vector3d act = lastAct;
            if (lowPassFilter)
            {
                act.x += (pidAction.x - lastAct.x) * (1.0 / ((TfV.x / TimeWarp.fixedDeltaTime) + 1.0));
                act.y += (pidAction.y - lastAct.y) * (1.0 / ((TfV.y / TimeWarp.fixedDeltaTime) + 1.0));
                act.z += (pidAction.z - lastAct.z) * (1.0 / ((TfV.z / TimeWarp.fixedDeltaTime) + 1.0));
            }
            else
            {
                act = pidAction;
            }
            lastAct = act;

            Vector3d deltaEuler = error * UtilMath.Rad2Deg;

            SetFlightCtrlState(act, deltaEuler, s, 1);

            act = new Vector3d(s.pitch, s.roll, s.yaw);

        }

        private void SetFlightCtrlState(Vector3d act, Vector3d deltaEuler, FlightCtrlState s, float drive_limit)
        {
            bool userCommandingPitchYaw = (Mathfx.Approx(s.pitch, s.pitchTrim, 0.1F) ? false : true) || (Mathfx.Approx(s.yaw, s.yawTrim, 0.1F) ? false : true);
            bool userCommandingRoll = (Mathfx.Approx(s.roll, s.rollTrim, 0.1F) ? false : true);

            //if (attitudeKILLROT)
            //{
            //    if (lastReferencePart != vessel.GetReferenceTransformPart() || userCommandingPitchYaw || userCommandingRoll)
            //    {
            //        attitudeTo(Quaternion.LookRotation(vessel.GetTransform().up, -vessel.GetTransform().forward), AttitudeReference.INERTIAL, null);
            //        lastReferencePart = vessel.GetReferenceTransformPart();
            //    }
            //}
            if (userCommandingPitchYaw || userCommandingRoll)
            {
                pid.Reset();
            }

            if (!userCommandingRoll)
            {
                if (!double.IsNaN(act.y)) s.roll = Mathf.Clamp((float)(act.y), -drive_limit, drive_limit);
            }

            if (!userCommandingPitchYaw)
            {
                if (!double.IsNaN(act.x)) s.pitch = Mathf.Clamp((float)(act.x), -drive_limit, drive_limit);
                if (!double.IsNaN(act.z)) s.yaw = Mathf.Clamp((float)(act.z), -drive_limit, drive_limit);
            }

            //// RCS and SAS control:
            //Vector3d absErr;            // Absolute error (exag º)
            //absErr.x = Math.Abs(deltaEuler.x);
            //absErr.y = Math.Abs(deltaEuler.y);
            //absErr.z = Math.Abs(deltaEuler.z);

            //if ((absErr.x < 0.4) && (absErr.y < 0.4) && (absErr.z < 0.4))
            //{
            //    if (timeCount < 50)
            //    {
            //        timeCount++;
            //    }
            //    else
            //    {
            //        if (RCS_auto)
            //        {
            //            if (attitudeRCScontrol && core.rcs.users.Count == 0)
            //            {
            //                part.vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
            //            }
            //        }
            //    }
            //}
            //else if ((absErr.x > 1.0) || (absErr.y > 1.0) || (absErr.z > 1.0))
            //{
            //    timeCount = 0;
            //    if (RCS_auto && ((absErr.x > 3.0) || (absErr.y > 3.0) || (absErr.z > 3.0)))
            //    {
            //        if (attitudeRCScontrol)
            //        {
            //            part.vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
            //        }
            //    }
            //}
        } // end of SetFlightCtrlState

        public void tuneTf(Vector3d torque)
        {
            Vector3d ratio = new Vector3d(
                torque.x != 0 ? vessel.MOI.x / torque.x : 0,
                torque.y != 0 ? vessel.MOI.y / torque.y : 0,
                torque.z != 0 ? vessel.MOI.z / torque.z : 0
                );

            TfV = 0.05 * ratio;

            Vector3d delayFactor = Vector3d.one + 2 * vesselModule.torqueReactionSpeed;

            TfV.Scale(delayFactor);


            TfV = TfV.Clamp(2.0 * TimeWarp.fixedDeltaTime, TfMax);
            TfV = TfV.Clamp(TfMin, TfMax);
        }

        public void setPIDParameters()
        {
            Vector3d invTf = TfV.InvertNoNaN();
            pid.Kd = kdFactor * invTf;

            pid.Kp = (1 / (kpFactor * Math.Sqrt(2))) * pid.Kd;
            pid.Kp.Scale(invTf);

            pid.Ki = (1 / (kiFactor * Math.Sqrt(2))) * pid.Kp;
            pid.Ki.Scale(invTf);

            pid.intAccum = pid.intAccum.Clamp(-5, 5);
        }

        public void ResetConfig()
        {
            TfMin = 0.1;
            TfMax = 0.5;
            kpFactor = 3;
            kiFactor = 6;
            kdFactor = 0.5;
        }

    }

    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    class PersistantRotationAllGameScenesEvents : MonoBehaviour
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
                if (data.host.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() == 0)
                { return; }

                VesselModuleMandatoryRCS vm = data.host.vesselModules.OfType<VesselModuleMandatoryRCS>().First();

                if (vm.SASModeLock && 
                    (vm.SASMode == SASUI.SASFunction.Prograde ||
                    vm.SASMode == SASUI.SASFunction.Retrograde ||
                    vm.SASMode == SASUI.SASFunction.Normal ||
                    vm.SASMode == SASUI.SASFunction.AntiNormal ||
                    vm.SASMode == SASUI.SASFunction.RadialIn ||
                    vm.SASMode == SASUI.SASFunction.RadialOut))
                {
                    vm.SASModeLock = false;
                    vm.SASMode = SASUI.SASFunction.KillRot;
                }
            }
            else
            {
                bool SASModeLock = false;
                SASUI.SASFunction SASMode = SASUI.SASFunction.KillRot;
                int SASModeInt = 3;
                if (!data.host.protoVessel.vesselModules.GetNode("VesselModuleMandatoryRCS").TryGetValue("SASModeLock", ref SASModeLock))
                { return; }
                if (!data.host.protoVessel.vesselModules.GetNode("VesselModuleMandatoryRCS").TryGetValue("SASMode", ref SASModeInt))
                { return; }

                SASMode = (SASUI.SASFunction)SASModeInt;

                if (SASModeLock &&
                    (SASMode == SASUI.SASFunction.Prograde ||
                    SASMode == SASUI.SASFunction.Retrograde ||
                    SASMode == SASUI.SASFunction.Normal ||
                    SASMode == SASUI.SASFunction.AntiNormal ||
                    SASMode == SASUI.SASFunction.RadialIn ||
                    SASMode == SASUI.SASFunction.RadialOut))
                {
                    data.host.protoVessel.vesselModules.GetNode("VesselModuleMandatoryRCS").SetValue("SASModeLock", false);
                    data.host.protoVessel.vesselModules.GetNode("VesselModuleMandatoryRCS").SetValue("SASMode", 0);
                }
            }
        }

        private void OnDestroy()
        {
            GameEvents.onVesselSOIChanged.Remove(onVesselSOIChanged);
        }
    }



    public class PIDControllerV3 : IConfigNode
    {
        public Vector3d Kp, Ki, Kd, intAccum, derivativeAct, propAct;
        public double max, min;

        public PIDControllerV3(Vector3d Kp, Vector3d Ki, Vector3d Kd, double max = double.MaxValue, double min = double.MinValue)
        {
            this.Kp = Kp;
            this.Ki = Ki;
            this.Kd = Kd;
            this.max = max;
            this.min = min;
            Reset();
        }

        public Vector3d Compute(Vector3d error, Vector3d omega, Vector3d Wlimit)
        {
            derivativeAct = Vector3d.Scale(omega, Kd);
            Wlimit = Vector3d.Scale(Wlimit, Kd);

            // integral actíon + Anti Windup
            intAccum.x = (Math.Abs(derivativeAct.x) < 0.6 * max) ? intAccum.x + (error.x * Ki.x * TimeWarp.fixedDeltaTime) : 0.9 * intAccum.x;
            intAccum.y = (Math.Abs(derivativeAct.y) < 0.6 * max) ? intAccum.y + (error.y * Ki.y * TimeWarp.fixedDeltaTime) : 0.9 * intAccum.y;
            intAccum.z = (Math.Abs(derivativeAct.z) < 0.6 * max) ? intAccum.z + (error.z * Ki.z * TimeWarp.fixedDeltaTime) : 0.9 * intAccum.z;

            propAct = Vector3d.Scale(error, Kp);

            Vector3d action = propAct + intAccum;

            // Clamp (propAct + intAccum) to limit the angular velocity:
            action = new Vector3d(Math.Max(-Wlimit.x, Math.Min(Wlimit.x, action.x)),
                                  Math.Max(-Wlimit.y, Math.Min(Wlimit.y, action.y)),
                                  Math.Max(-Wlimit.z, Math.Min(Wlimit.z, action.z)));

            // add. derivative action 
            action += derivativeAct;

            // action clamp
            action = new Vector3d(Math.Max(min, Math.Min(max, action.x)),
                                  Math.Max(min, Math.Min(max, action.y)),
                                  Math.Max(min, Math.Min(max, action.z)));
            return action;
        }

        public void Reset()
        {
            intAccum = Vector3d.zero;
        }

        public void Load(ConfigNode node)
        {
            if (node.HasValue("Kp"))
            {
                Kp = ConfigNode.ParseVector3D(node.GetValue("Kp"));
            }
            if (node.HasValue("Ki"))
            {
                Ki = ConfigNode.ParseVector3D(node.GetValue("Ki"));
            }
            if (node.HasValue("Kd"))
            {
                Kd = ConfigNode.ParseVector3D(node.GetValue("Kd"));
            }
        }

        public void Save(ConfigNode node)
        {
            node.SetValue("Kp", Kp.ToString());
            node.SetValue("Ki", Ki.ToString());
            node.SetValue("Kd", Kd.ToString());
        }
    }

}