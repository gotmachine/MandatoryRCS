/* This file and all code it contains is licensed under the GNU General Public License v3.0
 * It is based on the code from the MechJeb2 plugin
 * MechJeb2 can be found at https://github.com/MuMech/MechJeb2
 */

using MandatoryRCS.Lib;
using System;
using System.Linq;
using UnityEngine;
using static MandatoryRCS.ComponentSASAttitude;

/* OTHER WANTED FEATURES :
    - Global RCS throttle -> RCS menu
    - RCS optimizer -> maybe at first something basic that disable non-optimized directions, this seems ok
    - Node executor -> dependency on betterburntime ?
    - Suicide burn executor -> dependency on betterburntime ?
 */

namespace MandatoryRCS
{

    public class ComponentSASAutopilot : ComponentBase
    {
        #region PID/ACTION
        public PIDControllerV3 pid;
        public Vector3d act;
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

        private readonly Vector3d defaultTfV = new Vector3d(0.3, 0.3, 0.3);

        #endregion

        public override void Start()
        {
            pid = new PIDControllerV3(Vector3d.zero, Vector3d.zero, Vector3d.zero, 1, -1);
            setPIDParameters();
            lastAct = Vector3d.zero;
            //vessel.OnPreAutopilotUpdate += new FlightInputCallback(SASUpdate);
        }

        public override void ComponentUpdate()
        {
            // Disable stock SAS
            vessel.Autopilot.SAS.DisconnectFlyByWire();

            Transform vesselTransform = vessel.ReferenceTransform;
            //Quaternion delta = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vesselTransform.rotation) * _requestedAttitude);

            // Find out the real shorter way to turn where we wan to.
            // Thanks to HoneyFox
            Vector3d tgtLocalUp = vesselTransform.transform.rotation.Inverse() * vesselModule.autopilotAttitudeWanted * Vector3d.forward;
            Vector3d curLocalUp = Vector3d.up;

            double turnAngle = Math.Abs(Vector3d.Angle(curLocalUp, tgtLocalUp));
            Vector2d rotDirection = new Vector2d(tgtLocalUp.x, tgtLocalUp.z);
            rotDirection = rotDirection.normalized * turnAngle;

            // And the lowest roll
            // Thanks to Crzyrndm
            Vector3 normVec = Vector3.Cross(vesselModule.autopilotAttitudeWanted * Vector3.forward, vesselTransform.up);
            Quaternion targetDeRotated = Quaternion.AngleAxis((float) turnAngle, normVec) * vesselModule.autopilotAttitudeWanted;
            float rollError = Vector3.Angle(vesselTransform.right, targetDeRotated * Vector3.right) *
                              Math.Sign(Vector3.Dot(targetDeRotated * Vector3.right, vesselTransform.forward));

            // From here everything should use MOI order for Vectors (pitch, roll, yaw)
            error = new Vector3d(
                -rotDirection.y * Mathf.Deg2Rad,
                rollError * Mathf.Deg2Rad,
                rotDirection.x * Mathf.Deg2Rad
                );

            if (!vesselModule.lockedRollMode) { error.y = 0; }

            Vector3d err = error + vesselModule.angularDistanceToStop;
            err = new Vector3d(
                Math.Max(-Math.PI, Math.Min(Math.PI, err.x)),
                Math.Max(-Math.PI, Math.Min(Math.PI, err.y)),
                Math.Max(-Math.PI, Math.Min(Math.PI, err.z)));

            // ( MoI / available torque ) factor:
            Vector3d NormFactor = Vector3d.Scale(vesselModule.MOI, vesselModule.torqueAvailable.InvertNoNaN());
           
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
            // vesselModule.velocityLimiter * 0.01 = kWlimit in MechJeb
            var Wlimit = new Vector3d(Math.Sqrt(NormFactor.x * Math.PI * vesselModule.velocityLimiter * 0.01),
                Math.Sqrt(NormFactor.y * Math.PI * vesselModule.velocityLimiter * 0.01),
                Math.Sqrt(NormFactor.z * Math.PI * vesselModule.velocityLimiter * 0.01));

            pidAction = pid.Compute(err, omega, Wlimit);

            // deadband
            pidAction.x = Math.Abs(pidAction.x) >= deadband ? pidAction.x : 0.0;
            pidAction.y = Math.Abs(pidAction.y) >= deadband ? pidAction.y : 0.0;
            pidAction.z = Math.Abs(pidAction.z) >= deadband ? pidAction.z : 0.0;

            // low pass filter,  wf = 1/Tf:
            act = lastAct;
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

            SetFlightCtrlState(act, deltaEuler, vesselModule.flightCtrlState, 1);

            act = new Vector3d(vesselModule.flightCtrlState.pitch, vesselModule.flightCtrlState.roll, vesselModule.flightCtrlState.yaw);

        }

        private void SetFlightCtrlState(Vector3d act, Vector3d deltaEuler, FlightCtrlState s, float drive_limit)
        {
            bool userCommandingPitchYaw = (Mathfx.Approx(s.pitch, s.pitchTrim, 0.1F) ? false : true) || (Mathfx.Approx(s.yaw, s.yawTrim, 0.1F) ? false : true);
            bool userCommandingRoll = (Mathfx.Approx(s.roll, s.rollTrim, 0.1F) ? false : true);

            if (userCommandingPitchYaw || userCommandingRoll)
            {
                Reset();
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
        } // end of SetFlightCtrlState

        public void Reset()
        {
            pid.Reset();
        }

        public void tuneTf(Vector3d torque)
        {
            Vector3d ratio = new Vector3d(
                torque.x != 0 ? vesselModule.MOI.x / torque.x : 0,
                torque.y != 0 ? vesselModule.MOI.y / torque.y : 0,
                torque.z != 0 ? vesselModule.MOI.z / torque.z : 0
                );

            TfV = 0.05 * ratio;

            Vector3d delayFactor = Vector3d.one + 2 * vesselModule.torqueReactionSpeed;

            TfV.Scale(delayFactor);


            TfV = TfV.Clamp(2.0 * TimeWarp.fixedDeltaTime, TfMax);
            TfV = TfV.Clamp(TfMin, TfMax);
        }

        public void setPIDParameters()
        {
            Vector3d invTf = (Tf_autoTune ? TfV : defaultTfV).InvertNoNaN();

            pid.Kd = kdFactor * invTf;

            pid.Kp = (1 / (kpFactor * Math.Sqrt(2))) * pid.Kd;
            pid.Kp.Scale(invTf);

            pid.Ki = (1 / (kiFactor * Math.Sqrt(2))) * pid.Kp;
            pid.Ki.Scale(invTf);

            pid.intAccum = pid.intAccum.Clamp(-5, 5);
        }

        public void ResetConfig()
        {
            TfV = defaultTfV;
            TfMin = 0.1;
            TfMax = 0.5;
            kpFactor = 3;
            kiFactor = 6;
            kdFactor = 0.5;
            deadband = 0.0001;
            kWlimit = 0.15;
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

                if (vm.autopilotPersistentModeLock && 
                    (vm.autopilotMode == SASMode.Prograde ||
                    vm.autopilotMode == SASMode.Retrograde ||
                    vm.autopilotMode == SASMode.Normal ||
                    vm.autopilotMode == SASMode.AntiNormal ||
                    vm.autopilotMode == SASMode.RadialIn ||
                    vm.autopilotMode == SASMode.RadialOut))
                {
                    vm.autopilotPersistentModeLock = false;
                    vm.autopilotMode = SASMode.KillRot;
                }
            }
            // TODO : the vesselModule seems to exist in the Vessel even if unloaded, why are we going into the protovessel ?
            else if (data.host.protoVessel.vesselModules != null)
            {
                bool SASModeLock = false;
                SASMode sasMode = SASMode.KillRot;
                int SASModeInt = 3;
                if (!data.host.protoVessel.vesselModules.GetNode("VesselModuleMandatoryRCS").TryGetValue("SASModeLock", ref SASModeLock))
                { return; }
                if (!data.host.protoVessel.vesselModules.GetNode("VesselModuleMandatoryRCS").TryGetValue("SASMode", ref SASModeInt))
                { return; }

                sasMode = (SASMode)SASModeInt;

                if (SASModeLock &&
                    (sasMode == SASMode.Prograde ||
                    sasMode == SASMode.Retrograde ||
                    sasMode == SASMode.Normal ||
                    sasMode == SASMode.AntiNormal ||
                    sasMode == SASMode.RadialIn ||
                    sasMode == SASMode.RadialOut))
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



    public class PIDControllerV3 //: IConfigNode
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

        //public void Load(ConfigNode node)
        //{
        //    if (node.HasValue("Kp"))
        //    {
        //        Kp = ConfigNode.ParseVector3D(node.GetValue("Kp"));
        //    }
        //    if (node.HasValue("Ki"))
        //    {
        //        Ki = ConfigNode.ParseVector3D(node.GetValue("Ki"));
        //    }
        //    if (node.HasValue("Kd"))
        //    {
        //        Kd = ConfigNode.ParseVector3D(node.GetValue("Kd"));
        //    }
        //}

        //public void Save(ConfigNode node)
        //{
        //    node.SetValue("Kp", Kp.ToString());
        //    node.SetValue("Ki", Ki.ToString());
        //    node.SetValue("Kd", Kd.ToString());
        //}
    }

}