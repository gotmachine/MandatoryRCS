using AT_Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MandatoryRCS.MRCSSAS
{

    public class VesselWrapper
    {
        Vessel vessel; // To be assigned

        // VesselWrapper.cs
        public void ApplyAutopilotSteering(FlightCtrlState s)
        {
            if (AutopilotSteering.IsZero()) return;
            s.pitch = Utils.Clamp(AutopilotSteering.x, -1, 1);
            s.roll = Utils.Clamp(AutopilotSteering.y, -1, 1);
            s.yaw = Utils.Clamp(AutopilotSteering.z, -1, 1);
        }

        public Vector3 AutopilotSteering;
        public bool AutopilotDisabled;
        public bool    HaveControlAuthority = true;

        // ControlProps.cs
        public void AddSteering(Vector3 steering)
        { AutopilotSteering = (AutopilotSteering + steering).ClampComponents(-1, 1); }

        public void SetAttitudeError(float error)
        {
            AttitudeError = error;
            // Aligned &= AttitudeError < GLB.ATCB.MaxAttitudeError;
            // Aligned |= AttitudeError < GLB.ATCB.AttitudeErrorThreshold;
            // CanWarp = CFG.WarpToNode && TimeWarp.WarpMode == TimeWarp.Modes.HIGH &&
            //    (WarpToTime > VSL.Physics.UT ||
            //     VSL.Controls.Aligned &&
            //     (VSL.Physics.NoRotation || VSL.Physics.ConstantRotation));
            // MinAlignmentTime = VSL.Torque.MaxCurrent.RotationTime2Phase(AttitudeError);
            // AlignmentFactor = Utils.ClampL(1 - AttitudeError / GLB.ATCB.MaxAttitudeError, 0);
            // InvAlignmentFactor = Utils.ClampH(AttitudeError / GLB.ATCB.MaxAttitudeError, 1);
        }

        


        public bool  Aligned = true;
        public bool  CanWarp = true;
        public float AttitudeError { get; private set; }
        public float MinAlignmentTime { get; private set; }
        public float AlignmentFactor { get; private set; }
        public float InvAlignmentFactor { get; private set; }

        // VesselProps
        // We need to update this, this is done is torqueprops
        public TorqueInfo MaxCurrent; //current maximum torque

        // TorqueProps
        public class TorqueInfo
        {
            public Vector3 AA { get; private set; } //angular acceleration vector in radians
        }

        // PhysicalProps
        public Vector3 MoI { get; private set; } = Vector3.one; //main diagonal of inertia tensor

        // TO BE DONE IN FIXEDUPDATE
        void FixedUpdate()
        {
            MoI = vessel.MOI;
            if (MoI.IsInvalid() || MoI.IsZero())
                MoI = Vector3.one;
        }



        // AttitudeControl.cs

        readonly MinimumF momentum_min = new MinimumF();
        Transform refT;
        Quaternion locked_attitude;
        bool attitude_locked;

        Vector3 lthrust, needed_lthrust;

        void OnAutopilotUpdate()
        {
            if (AutopilotDisabled)
            {
                Reset();
                return;
            }
            compute_rotation();
            compute_steering();
            set_authority_flag();
            AddSteering(steering);
        }

        protected Vector3 steering;
        protected Vector3 rotation_axis;
        protected float angular_error;

        protected readonly PidCascade pid_pitch = new PidCascade();
        protected readonly PidCascade pid_roll = new PidCascade();
        protected readonly PidCascade pid_yaw = new PidCascade();

        protected readonly Timer AuthorityTimer = new Timer();
        protected readonly DifferentialF ErrorDif = new DifferentialF();
        protected readonly LowPassFilterV MaxAA_Filter = new LowPassFilterV();

        void Reset()
        {
            pid_pitch.Reset();
            pid_roll.Reset();
            pid_yaw.Reset();
            MaxAA_Filter.Reset();
            MaxAA_Filter.Set(MaxCurrent.AA);
            MaxAA_Filter.Tau = 0;
            HaveControlAuthority = true;
            SetAttitudeError(180);
            rotation_axis = Vector3.zero;
            refT = null;
            momentum_min.Reset();
            attitude_locked = false;
            needed_lthrust = Vector3.zero;
            lthrust = Vector3.zero;
        }

        // To be wired to the SAS UI
        // Seems ok overall
        protected void compute_rotation()
        {
            Vector3 v;
            momentum_min.Update(vessel.angularMomentum.sqrMagnitude);
            lthrust = Vector3.zero; // VSL.LocalDir(VSL.Engines.CurrentDefThrustDir); TO UPDATE WITH VESSEL DIR
            steering = Vector3.zero;

            // Update with our SAS input
            switch (CFG.AT.state)
            {
                case Attitude.Custom:
                    if (CustomRotation.Equals(default(Rotation)))
                        goto case Attitude.KillRotation;
                    lthrust = CustomRotation.current;
                    needed_lthrust = CustomRotation.needed;
                    VSL.Engines.RequestNearestClusterActivation(needed_lthrust);
                    break;
                case Attitude.HoldAttitude:
                    if (refT != VSL.refT || !attitude_locked)
                    {
                        refT = VSL.refT;
                        locked_attitude = refT.rotation;
                        attitude_locked = true;
                    }
                    if (refT != null)
                    {
                        lthrust = Vector3.up;
                        needed_lthrust = refT.rotation.Inverse() * locked_attitude * lthrust;
                    }
                    break;
                case Attitude.KillRotation:
                    if (refT != VSL.refT || momentum_min.True)
                    {
                        refT = VSL.refT;
                        locked_attitude = refT.rotation;
                    }
                    if (refT != null)
                    {
                        lthrust = Vector3.up;
                        needed_lthrust = refT.rotation.Inverse() * locked_attitude * lthrust;
                    }
                    break;
                case Attitude.Prograde:
                case Attitude.Retrograde:
                    v = VSL.InOrbit ? VSL.vessel.obt_velocity : VSL.vessel.srf_velocity;
                    if (v.magnitude < GLB.THR.MinDeltaV)
                    {
                        CFG.AT.On(Attitude.KillRotation);
                        break;
                    }
                    if (CFG.AT.state == Attitude.Prograde) v *= -1;
                    needed_lthrust = VSL.LocalDir(v.normalized);
                    VSL.Engines.RequestNearestClusterActivation(needed_lthrust);
                    break;
                case Attitude.RelVel:
                case Attitude.AntiRelVel:
                    if (!VSL.HasTarget)
                    {
                        Message("No target");
                        CFG.AT.On(Attitude.KillRotation);
                        break;
                    }
                    v = VSL.InOrbit ?
                        VSL.Target.GetObtVelocity() - VSL.vessel.obt_velocity :
                        VSL.Target.GetSrfVelocity() - VSL.vessel.srf_velocity;
                    if (v.magnitude < GLB.THR.MinDeltaV)
                    {
                        CFG.AT.On(Attitude.KillRotation);
                        break;
                    }
                    if (CFG.AT.state == Attitude.AntiRelVel) v *= -1;
                    needed_lthrust = VSL.LocalDir(v.normalized);
                    VSL.Engines.RequestClusterActivationForManeuver(-v);
                    break;
                case Attitude.ManeuverNode:
                    var solver = VSL.vessel.patchedConicSolver;
                    if (solver == null || solver.maneuverNodes.Count == 0)
                    {
                        Message("No maneuver node");
                        CFG.AT.On(Attitude.KillRotation);
                        break;
                    }
                    v = -solver.maneuverNodes[0].GetBurnVector(VSL.orbit);
                    needed_lthrust = VSL.LocalDir(v.normalized);
                    VSL.Engines.RequestClusterActivationForManeuver(-v);
                    break;
                case Attitude.Normal:
                case Attitude.AntiNormal:
                case Attitude.Radial:
                case Attitude.AntiRadial:
                case Attitude.Target:
                case Attitude.AntiTarget:
                    VSL.Engines.RequestNearestClusterActivation(needed_lthrust);
                    break;
            }
            compute_rotation(lthrust.normalized, needed_lthrust.normalized);
            ResetCustomRotation();
        }

        public Rotation CustomRotation { get; private set; }

        public void ResetCustomRotation()
        {
            CustomRotation = default(Rotation);
        }

        [Persistent]
        public float AALowPassF = 1f;

        protected void compute_steering()
        {

            VSL.Controls.GimbalLimit = VSL.PreUpdateControls.mainThrottle > 0? 100 : 0;
            if (rotation_axis.IsZero()) return;
            var AV = get_angular_velocity();
            var AM = Vector3.Scale(AV, MoI);
            var abs_rotation_axis = rotation_axis.AbsComponents();
            var ErrV = angular_error * abs_rotation_axis;
            var iErr = Vector3.one - ErrV;
            var MaxAA = VSL.Torque.Slow ?
                VSL.Torque.Instant.AA + VSL.Torque.SlowMaxPossible.AA * Mathf.Min(VSL.PreUpdateControls.mainThrottle, VSL.OnPlanetParams.GeeVSF) :
                VSL.Torque.MaxCurrent.AA;
            MaxAA_Filter.Tau = (1 - Mathf.Sqrt(angular_error)) * AALowPassF;
            MaxAA = MaxAA_Filter.Update(MaxAA);
            var iMaxAA = MaxAA.Inverse(0);
            if (VSL.Torque.Slow)
            {
                pid_pitch.Tune(AV.x, AM.x, MaxAA.x, iMaxAA.x, ErrV.x, iErr.x,
                               MaxAA.x > 0 ? VSL.Torque.Instant.AA.x / MaxAA.x : 1,
                               VSL.Torque.EnginesResponseTime.x,
                               VSL.Torque.MaxPossible.SpecificTorque.x);
                pid_roll.Tune(AV.y, AM.y, MaxAA.y, iMaxAA.y, ErrV.y, iErr.y,
                              MaxAA.y > 0 ? VSL.Torque.Instant.AA.y / MaxAA.y : 1,
                              VSL.Torque.EnginesResponseTime.y,
                              VSL.Torque.MaxPossible.SpecificTorque.y);
                pid_yaw.Tune(AV.z, AM.z, MaxAA.z, iMaxAA.z, ErrV.z, iErr.z,
                             MaxAA.z > 0 ? VSL.Torque.Instant.AA.z / MaxAA.z : 1,
                             VSL.Torque.EnginesResponseTime.z,
                             VSL.Torque.MaxPossible.SpecificTorque.z);
            }
            else
            {
                pid_pitch.TuneFast(AV.x, AM.x, MaxAA.x, iMaxAA.x, iErr.x);
                pid_roll.TuneFast(AV.y, AM.y, MaxAA.y, iMaxAA.y, iErr.y);
                pid_yaw.TuneFast(AV.z, AM.z, MaxAA.z, iMaxAA.z, iErr.z);
            }
            pid_pitch.atPID.Update(ErrV.x * Mathf.PI, -AV.x * Mathf.Sign(rotation_axis.x));
            pid_roll.atPID.Update(ErrV.y * Mathf.PI, -AV.y * Mathf.Sign(rotation_axis.y));
            pid_yaw.atPID.Update(ErrV.z * Mathf.PI, -AV.z * Mathf.Sign(rotation_axis.z));
            var avErr = compute_av_error(AV, angular_error);
            steering = new Vector3(pid_pitch.UpdateAV(avErr.x), pid_roll.UpdateAV(avErr.y), pid_yaw.UpdateAV(avErr.z));
            //correct_steering(); // needed ??? seems to be linked to bearing control ??
        }

        // needed ???
        //protected void correct_steering()
        //{
        //    if (BRC != null && BRC.IsActive)
        //        steering = Vector3.ProjectOnPlane(steering, lthrust);
        //}

        protected void compute_rotation(Vector3 current, Vector3 needed)
        {
            var cur_inv = current.IsInvalid() || current.IsZero();
            var ned_inv = needed.IsInvalid() || needed.IsZero();
            if (cur_inv || ned_inv)
            {
                //Log("compute_steering: Invalid argumetns:\ncurrent {}\nneeded {}\ncurrent thrust {}",
                //current, needed, VSL.Engines.CurrentDefThrustDir);
                steering = Vector3.zero;
                return;
            }
            needed.Normalize();
            current.Normalize();
            SetAttitudeError(Utils.Angle2(needed, current));
            angular_error = AttitudeError / 180;
            if (AttitudeError > 0.001)
            {
                if (AttitudeError > 175)
                    rotation_axis = -MaxComponentV(MaxCurrent.AA.Exclude(current.MaxI()), 0.01f);
                else
                    rotation_axis = Vector3.Cross(current, needed);
            }
            else
                rotation_axis = -vessel.angularVelocity;
            if (rotation_axis.IsInvalid())
                rotation_axis = Vector3.zero;
            else
                rotation_axis.Normalize();
        }

        protected void compute_rotation(Quaternion rotation)
        {
            compute_rotation(Vector3.up, rotation * Vector3.up);
        }

        protected void compute_rotation(Rotation rotation)
        {
            compute_rotation(rotation.current, rotation.needed);
        }

        Vector3 MaxComponentV(Vector3 v, float threshold)
        {
            threshold += 1;
            int maxI = 0;
            float maxC = Math.Abs(v.x);
            for (int i = 1; i < 3; i++)
            {
                var c = Math.Abs(v[i]);
                if (maxC <= 0 || c / maxC > threshold)
                {
                    maxC = c;
                    maxI = i;
                }
            }
            var ret = new Vector3();
            ret[maxI] = maxC;
            return ret;
        }

        protected virtual Vector3 get_angular_velocity()
        {
            return vessel.angularVelocity;
        }

    }

    public struct Rotation
    {
        public Vector3 current, needed;

        public Rotation(Vector3 current, Vector3 needed)
        {
            this.current = current;
            this.needed = needed;
        }

        public static Rotation Local(Vector3 current, Vector3 needed, VesselWrapper VSL)
        {
            return new Rotation(VSL.LocalDir(current), VSL.LocalDir(needed));
        }

        public override string ToString()
        {
            return Utils.Format("[Rotation]: current {}, needed {}", current, needed);
        }
    }

    public class PidCascade
    {
        public PIDf_Controller2 atPID = new PIDf_Controller2();
        public PIDf_Controller3 avPID = new PIDf_Controller3();
        public LowPassFilterF avFilter = new LowPassFilterF();

        public void SetParams(float at_clamp, float av_clamp)
        {
            atPID.setClamp(0, at_clamp);
            avPID.setClamp(av_clamp);
        }

        public void Reset()
        {
            atPID.Reset();
            avPID.Reset();
        }

        void tune_at_pid_fast(Config.CascadeConfigFast cfg, float AV, float AM, float MaxAA, float iMaxAA, float iErr)
        {
            var atP_iErr = Mathf.Pow(Utils.ClampL(iErr - cfg.atP_ErrThreshold, 0), cfg.atP_ErrCurve);
            if (MaxAA >= 1)
            {
                atPID.P = Utils.ClampH(1
                + cfg.atP_HighAA_Scale * Mathf.Pow(MaxAA, cfg.atP_HighAA_Curve)
                + atP_iErr, cfg.atP_HighAA_Max);
                atPID.D = cfg.atD_HighAA_Scale * Mathf.Pow(iMaxAA, cfg.atD_HighAA_Curve) * Utils.ClampH(iErr + Mathf.Abs(AM), 1.2f);
            }
            else
            {
                atPID.P = (1
                + cfg.atP_LowAA_Scale * Mathf.Pow(MaxAA, cfg.atP_LowAA_Curve)
                + atP_iErr);
                atPID.D = cfg.atD_LowAA_Scale * Mathf.Pow(iMaxAA, cfg.atD_LowAA_Curve) * Utils.ClampH(iErr + Mathf.Abs(AM), 1.2f);
            }
            var atI_iErr = Utils.ClampL(iErr - ATCB.FastConfig.atI_ErrThreshold, 0);
            if (atI_iErr <= 0 || AV < 0)
            {
                atPID.I = 0;
                atPID.IntegralError = 0;
            }
            else
            {
                atI_iErr = Mathf.Pow(atI_iErr, cfg.atI_ErrCurve);
                atPID.I = cfg.atI_Scale * MaxAA * atI_iErr / (1 + Utils.ClampL(AV, 0) * cfg.atI_AV_Scale * atI_iErr);
            }
        }

        void tune_at_pid_slow()
        {
            atPID.P = 1; //Utils.Clamp(Mathf.Pow(MaxAA, ATCB.SlowConfig.atP_Curve), ATCB.SlowConfig.atP_Min, ATCB.SlowConfig.atP_Max);
            atPID.I = 0;
            atPID.D = 0;
        }

        void tune_av_pid_fast(Config.CascadeConfigFast cfg, float MaxAA)
        {
            avPID.P = Utils.ClampL(cfg.avP_MaxAA_Intersect -
            cfg.avP_MaxAA_Inclination * Mathf.Pow(MaxAA, cfg.avP_MaxAA_Curve),
                                   cfg.avP_Min);
            avPID.I = cfg.avI_Scale * avPID.P;
            avPID.D = 0;
        }

        float noise_scale_factor(Config.PIDCascadeConfig cfg, float AV, float Err)
        {
            return Utils.Clamp(Mathf.Pow(cfg.NoiseF_Scale * (Mathf.Abs(AV) + Err), cfg.NoiseF_Curve), cfg.NoiseF_Min, 1);
        }

        void tune_av_pid_mixed(float AV, float AM, float MaxAA, float InstantTorqueRatio, float Err)
        {
            var noise_scale = noise_scale_factor(ATCB.MixedConfig, AV, Err);
            //                TCAGui.AddDebugMessage("noise scale: {}", noise_scale);//debug
            avPID.P = ((ATCB.MixedConfig.avP_A / (Mathf.Pow(InstantTorqueRatio, ATCB.MixedConfig.avP_D) + ATCB.MixedConfig.avP_B) +
            ATCB.MixedConfig.avP_C) / Utils.ClampL(Mathf.Abs(AM), 1) / MaxAA) * noise_scale;
            avPID.D = ((ATCB.MixedConfig.avD_A / (Mathf.Pow(InstantTorqueRatio, ATCB.MixedConfig.avD_D) + ATCB.MixedConfig.avD_B) +
            ATCB.MixedConfig.avD_C) / Mathf.Pow(MaxAA, ATCB.MixedConfig.avD_MaxAA_Curve)) * noise_scale;
            avPID.I = ATCB.MixedConfig.avI_Scale * Utils.ClampH(MaxAA, 1);
        }

        void tune_av_pid_slow(float AV, float MaxAA, float EnginesResponseTime, float SpecificTorque, float Err)
        {
            var slowF = (1 + ATCB.SlowConfig.SlowTorqueF * EnginesResponseTime * SpecificTorque);
            var noise_scale = noise_scale_factor(ATCB.SlowConfig, AV, Err);
            //                TCAGui.AddDebugMessage("SlowF: {}\nnoise scale: {}", slowF, noise_scale);//debug
            if (MaxAA >= 1)
            {
                avPID.P = ATCB.SlowConfig.avP_HighAA_Scale / slowF * noise_scale;
                avPID.D = Utils.ClampL(ATCB.SlowConfig.avD_HighAA_Intersect - ATCB.SlowConfig.avD_HighAA_Inclination * MaxAA, ATCB.SlowConfig.avD_HighAA_Max) * noise_scale;
            }
            else
            {
                avPID.P = ATCB.SlowConfig.avP_LowAA_Scale / slowF * noise_scale;
                avPID.D = ATCB.SlowConfig.avD_LowAA_Intersect - ATCB.SlowConfig.avD_LowAA_Inclination * MaxAA * noise_scale;
            }
            avPID.I = 0;
        }

        public void Tune(float AV, float AM, float MaxAA, float iMaxAA, float Err, float iErr,
                         float InstantTorqueRatio, float EnginesResponseTime, float SpecificTorque)
        {
            if (InstantTorqueRatio > ATCB.FastThreshold)
            {
                tune_at_pid_fast(ATCB.FastConfig, AV, AM, MaxAA, iMaxAA, iErr);
                tune_av_pid_fast(ATCB.FastConfig, MaxAA);
            }
            else if (InstantTorqueRatio > ATCB.MixedThreshold)
            {
                tune_at_pid_fast(ATCB.MixedConfig, AV, AM, MaxAA, iMaxAA, iErr);
                tune_av_pid_fast(ATCB.MixedConfig, MaxAA);
            }
            else if (InstantTorqueRatio > ATCB.SlowThreshold)
            {
                tune_at_pid_slow();
                tune_av_pid_mixed(AV, AM, MaxAA, InstantTorqueRatio, Err);
            }
            else
            {
                tune_at_pid_slow();
                tune_av_pid_slow(AV, MaxAA, EnginesResponseTime, SpecificTorque, Err);
            }
        }

        public void TuneFast(float AV, float AM, float MaxAA, float iMaxAA, float iErr)
        {
            tune_at_pid_fast(ATCB.FastConfig, AV, AM, MaxAA, iMaxAA, iErr);
            tune_av_pid_fast(ATCB.FastConfig, MaxAA);
        }

        public float UpdateAV(float avError)
        {
            var tau = ATCB.avAction_Filter * TimeWarp.fixedDeltaTime;
            avFilter.Tau = tau;
            avPID.setTau(tau);
            avPID.Update(avError);
            avPID.Action = avFilter.Update(avPID.Action);
            return avPID.Action;
        }

        public override string ToString()
        {
            return Utils.Format("atPID: {}\navPID: {}", atPID, avPID);
        }
    }
}
