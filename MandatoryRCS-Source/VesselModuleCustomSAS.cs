using MandatoryRCS.MechJeb;
using MandatoryRCS.MechJebLib;
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

GENERAL MARKERS
Always available

- Hold : 
  Used to hold an orientation. The orientation is defined in two possible ways:
  - When hold mode activated, hold the current vessel orientation at time of activation
  - On pilot input key release, register the current orientation and then hold it
- HoldSmooth (Default mode):
  Same as Hold, but on input key release angular velocity is killed and once the vessel has stabilized, the reached orientation is registered.
- KillRot : 
  Don't hold any orientation, just keep angular velocity to zero.
- ForceRoll (3 horizontal markers) :
  - center marker : toggle that activate/deactivate force roll. Icon change to reflect the current selected roll angle
  - right / left markers : buttons with arrows icons, add / remove roll angle by 45° increments
  Roll angle is relative to the current reference (ORBIT, SURFACE OR TARGET)
- PitchOffset (3 horizontal markers + value) :
  Add a pitch offset to the current selected orientation. Disabled if Hold, HoldSmooth, KillRot or Maneuver mode is active.
  Usefull for managing attitude in atmosphere, expecially for reentry 
  - center marker : reset pitch offset and set mode to HoldSmooth
  - right / left markers : increase / decrease pitch offset (2.5° increments)
- Maneuver (if node exists)
  
ORBIT / SURFACE
- Prograde
- Retrograde
- Normal
- Antinormal
- Radial
- Antiradial

TARGET
- Target
- AntiTarget
- Prograde
- Retrograde
- ProgradeCorrected
  To target, correcting lateral velocity
  This is the orientation you need to burn to so Prograde become aligned with Target
  See TCA code for how to get the vector
- RetrogradeCorrected
  Against target, correcting lateral velocity
  This is the orientation you need to burn to so Retrograde become aligned with AntiTarget
  See TCA code for how to get the vector
- Parallel +
- Parallel -
 */


namespace MandatoryRCS
{
    public enum AttitudeReference
    {
        INERTIAL,          //world coordinate system.
        ORBIT,             //forward = prograde, left = normal plus, up = radial plus
        ORBIT_HORIZONTAL,  //forward = surface projection of orbit velocity, up = surface normal
        SURFACE_NORTH,     //forward = north, left = west, up = surface normal
        SURFACE_VELOCITY,  //forward = surface frame vessel velocity, up = perpendicular component of surface normal
        TARGET,            //forward = toward target, up = perpendicular component of vessel heading
        RELATIVE_VELOCITY, //forward = toward relative velocity direction, up = tbd
        TARGET_ORIENTATION,//forward = direction target is facing, up = target up
        MANEUVER_NODE,     //forward = next maneuver node direction, up = tbd
        SUN,               //forward = orbit velocity of the parent body orbiting the sun, up = radial plus of that orbit
        SURFACE_HORIZONTAL,//forward = surface velocity horizontal component, up = surface normal
    }

    public class VesselModuleCustomSAS : VesselModule
    {
        #region MECHJEB VARIABLES
        //private VesselState vesselState;

        public PIDControllerV3 pid;
        public Vector3d lastAct = Vector3d.zero;
        public Vector3d pidAction;  //info
        public Vector3d error;  //info
        protected float timeCount = 0;
        protected Part lastReferencePart;

        public bool RCS_auto = false;
        public bool attitudeRCScontrol = true;

        //[Persistent(pass = (int)Pass.Global)]
        public bool Tf_autoTune = true;

        //[Persistent(pass = (int)Pass.Global)]
        //public double Tf = 0; // not used any more but kept so it loads from old configs
        public Vector3d TfV = new Vector3d(0.3, 0.3, 0.3);
        private Vector3 TfVec = new Vector3(0.3f, 0.3f, 0.3f);  // use the serialize since Vector3d does not
        public double TfMin = 0.1;
        public double TfMax = 0.5;
        public bool lowPassFilter = true;

        public double kpFactor = 3;
        public double kiFactor = 6;
        public double kdFactor = 0.5;

        public double deadband = 0.0001;

        public double kWlimit = 0.15;

        public MovingAverage steeringError = new MovingAverage(); // unit = °

        public bool attitudeKILLROT = false;

        protected bool attitudeChanged = false;

        protected AttitudeReference _attitudeReference = AttitudeReference.INERTIAL;

        protected Vector3d _axisControl = Vector3d.one;

        public AttitudeReference attitudeReference
        {
            get
            {
                return _attitudeReference;
            }
            set
            {
                if (_attitudeReference != value)
                {
                    _attitudeReference = value;
                    attitudeChanged = true;
                }
            }
        }

        protected Quaternion _oldAttitudeTarget = Quaternion.identity;
        protected Quaternion _lastAttitudeTarget = Quaternion.identity;
        protected Quaternion _attitudeTarget = Quaternion.identity;
        public Quaternion attitudeTarget
        {
            get
            {
                return _attitudeTarget;
            }
            set
            {
                if (Math.Abs(Vector3d.Angle(_lastAttitudeTarget * Vector3d.forward, value * Vector3d.forward)) > 10)
                {
                    _oldAttitudeTarget = _attitudeTarget;
                    _lastAttitudeTarget = value;
                    AxisControl(true, true, true);
                    attitudeChanged = true;
                }
                _attitudeTarget = value;
            }
        }

        //private Quaternion requestedAttitude = Quaternion.identity;

        public bool attitudeRollMatters
        {
            get
            {
                return _axisControl.y > 0;
            }
        }

        public Vector3d AxisState
        {
            get { return new Vector3d(_axisControl.x, _axisControl.y, _axisControl.z); }
        }

        protected Quaternion lastSAS = new Quaternion();

        public double attitudeError;

        public Vector3d torque;
        public Vector3d inertia;
        #endregion

        #region MandatoryRCS variables

        public SASHandler.SASFunction SASMode;
        public bool lockedRollMode = false;
        public bool pitchOffsetMode = false;
        public int currentRoll = 0;
        public int pitchOffset = 0;
        public Quaternion requestedAttitude;

        public Vector3d torqueReactionSpeed;

        #endregion

        protected override void OnStart()
        {
            pid = new PIDControllerV3(Vector3d.zero, Vector3d.zero, Vector3d.zero, 1, -1);
            setPIDParameters();
            lastAct = Vector3d.zero;
            Vessel.OnPreAutopilotUpdate += new FlightInputCallback(SASUpdate);
            base.OnStart();
        }

        protected override void OnLoad(ConfigNode local)
        {
            base.OnLoad(local);
            TfV = TfVec;
        }

        protected override void OnSave(ConfigNode local)
        {
            TfVec = TfV;
            base.OnSave(local);
        }

        private void FixedUpdate()
        {
            //if (vesselState == null || vesselState.Vessel != Vessel)
            //{
            //    if (Vessel.vesselModules.OfType<VesselModuleRotation>().Count() == 0) { return; }
            //    vesselState = Vessel.vesselModules.OfType<VesselState>().First();
            //}

            steeringError.value = attitudeError = attitudeAngleFromTarget();



            Vector6 torqueReactionWheel = new Vector6();
            Vector6 rcsTorqueAvailable = new Vector6();
            Vector6 torqueControlSurface = new Vector6();
            Vector6 torqueGimbal = new Vector6();
            Vector6 torqueOthers = new Vector6();
            Vector6 torqueReactionSpeed6 = new Vector6();

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part p = vessel.parts[i];

                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];
                    if (!pm.isEnabled)
                    {
                        continue;
                    }

                    ModuleReactionWheel rw = pm as ModuleReactionWheel;
                    if (rw != null)
                    {
                        Vector3 pos;
                        Vector3 neg;
                        rw.GetPotentialTorque(out pos, out neg); // GetPotentialTorque reports the same value for pos & neg on ModuleReactionWheel
                        torqueReactionWheel.Add(pos);
                        torqueReactionWheel.Add(-neg);
                    }
                    else if (pm is ModuleControlSurface) // also does ModuleAeroSurface
                    {
                        ModuleControlSurface cs = (pm as ModuleControlSurface);
                        Vector3 ctrlTorquePos;
                        Vector3 ctrlTorqueNeg;
                        cs.GetPotentialTorque(out ctrlTorquePos, out ctrlTorqueNeg);
                        torqueControlSurface.Add(ctrlTorquePos);
                        torqueControlSurface.Add(ctrlTorqueNeg);

                        torqueReactionSpeed6.Add(Mathf.Abs(cs.ctrlSurfaceRange) / cs.actuatorSpeed * Vector3d.Max(ctrlTorquePos.Abs(), ctrlTorqueNeg.Abs()));

                    }
                    else if (pm is ModuleGimbal)
                    {
                        //ModuleGimbal g = (pm as ModuleGimbal);
                        //Vector3 pos;
                        //Vector3 neg;
                        //g.GetPotentialTorque(out pos, out neg); // GetPotentialTorque reports the same value for pos & neg on ModuleGimbal
                        //torqueGimbal.Add(pos);
                        //torqueGimbal.Add(-neg);

                        //if (g.useGimbalResponseSpeed)
                        //{
                        //    torqueReactionSpeed6.Add((Mathf.Abs(g.gimbalRange) / g.gimbalResponseSpeed) * Vector3d.Max(pos.Abs(), neg.Abs()));
                        //}
                            
                    }
                    else if (pm is ModuleRCS)
                    {
                        ModuleRCS rcs = (pm as ModuleRCS);
                        if (rcs == null)
                            continue;

                        if (!p.ShieldedFromAirstream && rcs.rcsEnabled && rcs.isEnabled && !rcs.isJustForShow)
                        {
                            Vector3 attitudeControl = new Vector3(rcs.enablePitch ? 1 : 0, rcs.enableRoll ? 1 : 0, rcs.enableYaw ? 1 : 0);

                            Vector3 translationControl = new Vector3(rcs.enableX ? 1 : 0f, rcs.enableZ ? 1 : 0, rcs.enableY ? 1 : 0);
                            for (int j = 0; j < rcs.thrusterTransforms.Count; j++)
                            {
                                Transform t = rcs.thrusterTransforms[j];
                                Vector3d thrusterPosition = t.position - vessel.CurrentCoM;
                                Vector3d thrustDirection = rcs.useZaxis ? -t.forward : -t.up;
                                float power = rcs.thrusterPower;
                                if (FlightInputHandler.fetch.precisionMode)
                                {
                                    if (rcs.useLever)
                                    {
                                        float lever = rcs.GetLeverDistance(t, thrustDirection, vessel.CurrentCoM);
                                        if (lever > 1)
                                        {
                                            power = power / lever;
                                        }
                                    }
                                    else
                                    {
                                        power *= rcs.precisionFactor;
                                    }
                                }
                                Vector3d thrusterThrust = thrustDirection * power;
                                Vector3d thrusterTorque = Vector3.Cross(thrusterPosition, thrusterThrust);
                                rcsTorqueAvailable.Add(Vector3.Scale(vessel.GetTransform().InverseTransformDirection(thrusterTorque), attitudeControl));
                            }
                        }
                    }
                    else if (pm is ITorqueProvider) // All mod that supports it. Including FAR
                    {
                        ITorqueProvider tp = pm as ITorqueProvider;
                        Vector3 pos;
                        Vector3 neg;
                        tp.GetPotentialTorque(out pos, out neg);
                        torqueOthers.Add(pos);
                        torqueOthers.Add(neg);
                    }

                }
            }

            Vector3d torqueAvailable = Vector3d.zero;
            torqueAvailable += Vector3d.Max(torqueReactionWheel.positive, torqueReactionWheel.negative);
            torqueAvailable += Vector3d.Max(rcsTorqueAvailable.positive, rcsTorqueAvailable.negative);
            torqueAvailable += Vector3d.Max(torqueControlSurface.positive, torqueControlSurface.negative);
            torqueAvailable += Vector3d.Max(torqueGimbal.positive, torqueGimbal.negative);
            torqueAvailable += Vector3d.Max(torqueOthers.positive, torqueOthers.negative);

            torque = torqueAvailable;

            if (torqueAvailable.sqrMagnitude > 0)
            {
                torqueReactionSpeed = Vector3d.Max(torqueReactionSpeed6.positive, torqueReactionSpeed6.negative);
                torqueReactionSpeed.Scale(torqueAvailable.InvertNoNaN());
            }
            else
            {
                torqueReactionSpeed = Vector3d.zero;
            }

            Vector3d angularMomentum = Vector3d.zero;
            angularMomentum.x = (float)(vessel.MOI.x * vessel.angularVelocity.x);
            angularMomentum.y = (float)(vessel.MOI.y * vessel.angularVelocity.y);
            angularMomentum.z = (float)(vessel.MOI.z * vessel.angularVelocity.z);


            inertia = Vector3d.Scale(
                angularMomentum.Sign(),
                Vector3d.Scale(
                    Vector3d.Scale(angularMomentum, angularMomentum),
                    Vector3d.Scale(torque, vessel.MOI).InvertNoNaN()
                    )
                );
        }

        private void Update()
        {
            if (attitudeChanged)
            {
                if (attitudeReference != AttitudeReference.INERTIAL)
                {
                    attitudeKILLROT = false;
                }
                pid.Reset();
                lastAct = Vector3d.zero;

                attitudeChanged = false;
            }
        }

        // main callback
        private void SASUpdate(FlightCtrlState s)
        {
            if (FlightGlobals.ActiveVessel.Autopilot.Enabled)
            {
                // Disable stock SAS
                Vessel.Autopilot.SAS.DisconnectFlyByWire();

                // Update requested attitude
                requestedAttitude = GetRequestedAttitude();

                // Calculate needed action
                UpdateFlightInput(s);
            }

        }

        // Calculate requested attitude
        private Quaternion GetRequestedAttitude()
        {
            Vector3 direction = Vector3d.zero;
            Vector3 rollDirection = -Vessel.GetTransform().forward;
            Quaternion attitude = Quaternion.identity;

            switch (SASMode)
            {
                case SASHandler.SASFunction.Prograde:
                case SASHandler.SASFunction.Retrograde:
                    if (FlightGlobals.speedDisplayMode == FlightGlobals.SpeedDisplayModes.Orbit) // Orbit prograde
                    { direction = Vessel.obt_velocity; }
                    else if (FlightGlobals.speedDisplayMode == FlightGlobals.SpeedDisplayModes.Surface) // Surface prograde
                    { direction = Vessel.srf_velocity; }
                    else if (FlightGlobals.speedDisplayMode == FlightGlobals.SpeedDisplayModes.Target) // Target prograde
                    {
                        if (Vessel.targetObject != null)
                        { direction = -(Vessel.targetObject.GetObtVelocity() - Vessel.obt_velocity); }
                        else
                        { direction =  Vessel.GetTransform().up; }
                    }

                    if (SASMode == SASHandler.SASFunction.Retrograde) // Invert vector for retrograde
                    {
                        direction = -direction;
                    }
                    break;
                case SASHandler.SASFunction.Normal:
                case SASHandler.SASFunction.AntiNormal:
                case SASHandler.SASFunction.RadialOut:
                case SASHandler.SASFunction.RadialIn:
                    // Get body up vector
                    Vector3 planetUp = (Vessel.rootPart.transform.position - Vessel.mainBody.position).normalized;
                    // Get normal vector
                    Vector3 normal = new Vector3();
                    if (FlightGlobals.speedDisplayMode == FlightGlobals.SpeedDisplayModes.Orbit) // Orbit
                    { normal = Vector3.Cross(Vessel.obt_velocity, planetUp).normalized; }
                    else // Surface/Target (seems to be the same for normal/radial)
                    { normal = Vector3.Cross(Vessel.srf_velocity, planetUp).normalized; }

                    // Return normal/antinormal or calculate radial
                    if (SASMode == SASHandler.SASFunction.Normal) // Normal
                    { direction = normal; }
                    else if (SASMode == SASHandler.SASFunction.AntiNormal) // AntiNormal
                    { direction = -normal; }
                    else
                    {
                        // Get RadialIn vector
                        Vector3 radial = new Vector3();
                        if (FlightGlobals.speedDisplayMode == FlightGlobals.SpeedDisplayModes.Orbit) // Orbit
                        { radial = Vector3.Cross(Vessel.obt_velocity, normal).normalized; }
                        else // Surface/Target (seems to be the same for normal/radial)
                        { radial = Vector3.Cross(Vessel.srf_velocity, normal).normalized; }

                        // Return radial vector
                        if (SASMode == SASHandler.SASFunction.RadialIn) // Radial In
                        { direction = -radial; }
                        else if (SASMode == SASHandler.SASFunction.RadialOut) // Radial Out
                        { direction = radial; }
                    }
                    break;
                case SASHandler.SASFunction.Target:
                case SASHandler.SASFunction.AntiTarget:
                    if (Vessel.targetObject != null)
                    {
                        if (SASMode == SASHandler.SASFunction.Target) // Target
                        { direction = Vessel.targetObject.GetTransform().position - Vessel.transform.position; }

                        if (SASMode == SASHandler.SASFunction.AntiTarget) // AntiTarget
                        { direction = -(Vessel.targetObject.GetTransform().position - Vessel.transform.position); }
                    }
                    else
                    {
                        // No orientation keeping if target is null
                        direction = Vessel.GetTransform().up;
                    }
                    break;
                case SASHandler.SASFunction.Maneuver:
                    if (Vessel.patchedConicSolver.maneuverNodes.Count > 0)
                    {
                        direction = Vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(Vessel.orbit);
                    }
                    else
                    {
                        // No orientation keeping if there is no more maneuver node
                        direction = Vessel.GetTransform().up;
                    }
                    break;
                case SASHandler.SASFunction.Parallel:
                    direction = Vector3d.forward;
                    break;
                case SASHandler.SASFunction.AntiParallel:
                    direction = Vector3d.back;
                    break;
            }

            switch (SASMode)
            {
                case SASHandler.SASFunction.Hold:
                    //SetSMARTASSMode(MechJebModuleSmartASS.Target.KILLROT, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.HoldSmooth:
                    //SetSMARTASSMode(MechJebModuleSmartASS.Target.KILLROT, FlightGlobals.speedDisplayMode);
                    break;
                case SASHandler.SASFunction.KillRot:
                    attitude = Quaternion.LookRotation(Vessel.GetTransform().up, -Vessel.GetTransform().forward);
                    break;
                default:
                    Vector3 up = -Vessel.GetTransform().forward;
                    Vector3 dir = direction;
                    Quaternion rotref = Quaternion.LookRotation(Vessel.obt_velocity.normalized, (vessel.CoMD - vessel.mainBody.position).normalized);
                    up = attitudeWorldToReference(attitudeReferenceToWorld(rotref * Vector3.up));
                    Vector3.OrthoNormalize(ref dir, ref up);
                    attitude = Quaternion.LookRotation(dir, up);
                    break;
            }

            //attitude = Quaternion.LookRotation(Vessel.obt_velocity, -Vessel.GetTransform().forward);

            return attitude;
        }

        public Vector3d attitudeWorldToReference(Vector3d vector)
        {
            Quaternion rotref = Quaternion.LookRotation(Vessel.obt_velocity.normalized, (vessel.CoMD - vessel.mainBody.position).normalized);
            return Quaternion.Inverse(rotref) * vector;
        }

        public Vector3d attitudeReferenceToWorld(Vector3d vector)
        {
            Quaternion rotref = Quaternion.LookRotation(Vessel.obt_velocity.normalized, (vessel.CoMD - vessel.mainBody.position).normalized);
            return rotref * vector;
        }

        public void UpdateFlightInput(FlightCtrlState s)
        {
            // Direction we want to be facing
            //_requestedAttitude = attitudeGetReferenceRotation(attitudeReference) * attitudeTarget;
            // REFACTOR : requestedAttitudeVector
            Transform vesselTransform = vessel.ReferenceTransform;
            //Quaternion delta = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vesselTransform.rotation) * _requestedAttitude);

            // Find out the real shorter way to turn where we wan to.
            // Thanks to HoneyFox
            Vector3d tgtLocalUp = vesselTransform.transform.rotation.Inverse() * requestedAttitude * Vector3d.forward;
            Vector3d curLocalUp = Vector3d.up;

            double turnAngle = Math.Abs(Vector3d.Angle(curLocalUp, tgtLocalUp));
            Vector2d rotDirection = new Vector2d(tgtLocalUp.x, tgtLocalUp.z);
            rotDirection = rotDirection.normalized * turnAngle / 180.0;

            // And the lowest roll
            // Thanks to Crzyrndm
            Vector3 normVec = Vector3.Cross(requestedAttitude * Vector3.forward, vesselTransform.up);
            Quaternion targetDeRotated = Quaternion.AngleAxis((float)turnAngle, normVec) * requestedAttitude;
            float rollError = Vector3.Angle(vesselTransform.right, targetDeRotated * Vector3.right) * Math.Sign(Vector3.Dot(targetDeRotated * Vector3.right, vesselTransform.forward));

            // From here everything should use MOI order for Vectors (pitch, roll, yaw)

            error = new Vector3d(
                -rotDirection.y * Math.PI,
                rollError * Mathf.Deg2Rad,
                rotDirection.x * Math.PI
                );

            error.Scale(_axisControl);

            Vector3d err = error + inertia * 0.5;
            err = new Vector3d(
                Math.Max(-Math.PI, Math.Min(Math.PI, err.x)),
                Math.Max(-Math.PI, Math.Min(Math.PI, err.y)),
                Math.Max(-Math.PI, Math.Min(Math.PI, err.z)));

            // ( MoI / available torque ) factor:
            Vector3d NormFactor = Vector3d.Scale(vessel.MOI, torque.InvertNoNaN());

            err.Scale(NormFactor);

            // angular velocity:
            Vector3d omega = vessel.angularVelocity;
            //omega.x = vessel.angularVelocity.x;
            //omega.y = vessel.angularVelocity.z; // y <=> z
            //omega.z = vessel.angularVelocity.y; // z <=> y
            omega.Scale(NormFactor);

            if (Tf_autoTune)
                tuneTf(torque);
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

            // Feed the control torque to the differential throttle
            //if (core.thrust.differentialThrottleSuccess == MechJebModuleThrustController.DifferentialThrottleStatus.Success)
            //    core.thrust.differentialThrottleDemandedTorque = -Vector3d.Scale(act, vesselState.torqueDiffThrottle * vessel.ctrlState.mainThrottle);
        }

        private void SetFlightCtrlState(Vector3d act, Vector3d deltaEuler, FlightCtrlState s, float drive_limit)
        {
            bool userCommandingPitchYaw = (Mathfx.Approx(s.pitch, s.pitchTrim, 0.1F) ? false : true) || (Mathfx.Approx(s.yaw, s.yawTrim, 0.1F) ? false : true);
            bool userCommandingRoll = (Mathfx.Approx(s.roll, s.rollTrim, 0.1F) ? false : true);

            // Disable the new SAS so it won't interfere. But enable it while in timewarp for compatibility with PersistentRotation
            //if (TimeWarp.WarpMode != TimeWarp.Modes.HIGH || TimeWarp.CurrentRateIndex == 0)
            //    part.vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);


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

            //print("TfV O " + MuUtils.PrettyPrint(TfV));








            Vector3d delayFactor = Vector3d.one + 2 * torqueReactionSpeed;

            //print("del F " + MuUtils.PrettyPrint(delayFactor));

            TfV.Scale(delayFactor);


            TfV = TfV.Clamp(2.0 * TimeWarp.fixedDeltaTime, TfMax);
            TfV = TfV.Clamp(TfMin, TfMax);

            //print("TfV F " + MuUtils.PrettyPrint(TfV));

            //Tf = Mathf.Clamp((float)ratio.magnitude / 20f, 2 * TimeWarp.fixedDeltaTime, 1f);
            //Tf = Mathf.Clamp((float)Tf, (float)TfMin, (float)TfMax);
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

        public void AxisControl(bool pitch, bool yaw, bool roll)
        {
            _axisControl.x = pitch ? 1 : 0;
            _axisControl.y = roll ? 1 : 0;
            _axisControl.z = yaw ? 1 : 0;
        }
        /*
        public Quaternion attitudeGetReferenceRotation(AttitudeReference reference)
        {
            Vector3 fwd, up;
            Quaternion rotRef = Quaternion.identity;

            if (core.target.Target == null && (reference == AttitudeReference.TARGET || reference == AttitudeReference.TARGET_ORIENTATION || reference == AttitudeReference.RELATIVE_VELOCITY))
            {
                attitudeDeactivate();
                return rotRef;
            }

            if ((reference == AttitudeReference.MANEUVER_NODE) && (vessel.patchedConicSolver.maneuverNodes.Count == 0))
            {
                attitudeDeactivate();
                return rotRef;
            }

            switch (reference)
            {
                case AttitudeReference.ORBIT:
                    rotRef = Quaternion.LookRotation(vesselState.orbitalVelocity.normalized, vesselState.up);
                    break;
                case AttitudeReference.ORBIT_HORIZONTAL:
                    rotRef = Quaternion.LookRotation(Vector3d.Exclude(vesselState.up, vesselState.orbitalVelocity.normalized), vesselState.up);
                    break;
                case AttitudeReference.SURFACE_NORTH:
                    rotRef = vesselState.rotationSurface;
                    break;
                case AttitudeReference.SURFACE_VELOCITY:
                    rotRef = Quaternion.LookRotation(vesselState.surfaceVelocity.normalized, vesselState.up);
                    break;
                case AttitudeReference.TARGET:
                    fwd = (core.target.Position - vessel.GetTransform().position).normalized;
                    up = Vector3d.Cross(fwd, vesselState.normalPlus);
                    Vector3.OrthoNormalize(ref fwd, ref up);
                    rotRef = Quaternion.LookRotation(fwd, up);
                    break;
                case AttitudeReference.RELATIVE_VELOCITY:
                    fwd = core.target.RelativeVelocity.normalized;
                    up = Vector3d.Cross(fwd, vesselState.normalPlus);
                    Vector3.OrthoNormalize(ref fwd, ref up);
                    rotRef = Quaternion.LookRotation(fwd, up);
                    break;
                case AttitudeReference.TARGET_ORIENTATION:
                    Transform targetTransform = core.target.Transform;
                    if (core.target.CanAlign)
                    {
                        rotRef = Quaternion.LookRotation(targetTransform.forward, targetTransform.up);
                    }
                    else
                    {
                        rotRef = Quaternion.LookRotation(targetTransform.up, targetTransform.right);
                    }
                    break;
                case AttitudeReference.MANEUVER_NODE:
                    fwd = vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(orbit);
                    up = Vector3d.Cross(fwd, vesselState.normalPlus);
                    Vector3.OrthoNormalize(ref fwd, ref up);
                    rotRef = Quaternion.LookRotation(fwd, up);
                    break;
                case AttitudeReference.SUN:
                    Orbit baseOrbit = vessel.mainBody == Planetarium.fetch.Sun ? vessel.orbit : orbit.TopParentOrbit();
                    up = vesselState.CoM - Planetarium.fetch.Sun.transform.position;
                    fwd = Vector3d.Cross(-baseOrbit.GetOrbitNormal().xzy.normalized, up);
                    rotRef = Quaternion.LookRotation(fwd, up);
                    break;
                case AttitudeReference.SURFACE_HORIZONTAL:
                    rotRef = Quaternion.LookRotation(Vector3d.Exclude(vesselState.up, vesselState.surfaceVelocity.normalized), vesselState.up);
                    break;
            }
            return rotRef;
        }

        public Vector3d attitudeWorldToReference(Vector3d vector, AttitudeReference reference)
        {
            return Quaternion.Inverse(attitudeGetReferenceRotation(reference)) * vector;
        }

        public Vector3d attitudeReferenceToWorld(Vector3d vector, AttitudeReference reference)
        {
            return attitudeGetReferenceRotation(reference) * vector;
        }
                
        public bool attitudeTo(Quaternion attitude, AttitudeReference reference, object controller)
        {
            users.Add(controller);
            attitudeReference = reference;
            attitudeTarget = attitude;
            AxisControl(true, true, true);
            return true;
        }

        public bool attitudeTo(Vector3d direction, AttitudeReference reference, object controller)
        {
            //double ang_diff = Math.Abs(Vector3d.Angle(attitudeGetReferenceRotation(attitudeReference) * attitudeTarget * Vector3d.forward, attitudeGetReferenceRotation(reference) * direction));

            Vector3 up, dir = direction;

            if (!enabled)
            {
                up = attitudeWorldToReference(-vessel.GetTransform().forward, reference);
            }
            else
            {
                up = attitudeWorldToReference(attitudeReferenceToWorld(attitudeTarget * Vector3d.up, attitudeReference), reference);
            }
            Vector3.OrthoNormalize(ref dir, ref up);
            attitudeTo(Quaternion.LookRotation(dir, up), reference, controller);
            AxisControl(true, true, false);
            return true;
        }

        public bool attitudeTo(double heading, double pitch, double roll, object controller)
        {
            Quaternion attitude = Quaternion.AngleAxis((float)heading, Vector3.up) * Quaternion.AngleAxis(-(float)pitch, Vector3.right) * Quaternion.AngleAxis(-(float)roll, Vector3.forward);
            return attitudeTo(attitude, AttitudeReference.SURFACE_NORTH, controller);
        }
        */
        public bool attitudeDeactivate()
        {
            //users.Clear();
            attitudeChanged = true;

            return true;
        }

        //angle in degrees between the vessel's current pointing direction and the attitude target, ignoring roll
        public double attitudeAngleFromTarget()
        {
            Vector3 requestedDirection;
            float angle;
            requestedAttitude.ToAngleAxis(out angle, out requestedDirection);
            return enabled ? Math.Abs(Vector3d.Angle(requestedDirection, vessel.GetTransform().up)) : 0;
            //return enabled ? Math.Abs(Vector3d.Angle(attitudeGetReferenceRotation(attitudeReference) * attitudeTarget * Vector3d.forward, vesselState.forward)) : 0;
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