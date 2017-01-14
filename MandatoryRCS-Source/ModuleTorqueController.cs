using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// TODO :
// - implement torque ON when RCS is ON, to reduce RCS fuel consumption

namespace MandatoryRCS
{
    public class ModuleTorqueController : PartModule
    {
        public static Keyframe[] curveKeys = new Keyframe[]
        {
            new Keyframe(0.000f, -1.000f),
            new Keyframe(0.950f, 0.000f),
            new Keyframe(0.960f, 0.025f),
            new Keyframe(0.970f, 0.150f),
            new Keyframe(0.985f, 0.850f),
            new Keyframe(0.990f, 0.950f),
            new Keyframe(0.995f, 0.990f),
            new Keyframe(1.000f, 1.000f)
        };

        public static FloatCurve torqueOutput = new FloatCurve(curveKeys);

        private static float realisticTorqueRatio = 0.0025f;

        internal ModuleReactionWheel rwmodule;
        public Vector3 maxTorque;

        private float physicsTorqueFactor = 1.0f;

        public Vector3 storedMomentum = Vector3.zero;
        public Vector3 saturationTorqueFactor = Vector3.zero;

        private static float saturationTorqueThreesold = 0.5f; // wheels begin to loose torque when saturation exceed this percentage
        private static float desaturationRate = 0.05f; // % of the reaction wheels maxtorque used for desaturation per second

        private bool callbackIsActive = false;

        public void Start()
        {
            // Get the reaction wheel module
            rwmodule = part.Modules.GetModule<ModuleReactionWheel>();

            // We so the torque calculations only when in flight
            if (HighLogic.LoadedSceneIsFlight)
            {
                maxTorque.x = rwmodule.PitchTorque;
                maxTorque.y = rwmodule.RollTorque;
                maxTorque.z = rwmodule.YawTorque;

                vessel.OnFlyByWire += new FlightInputCallback(UpdateTorque);
                callbackIsActive = true;
            }

            // Hide RW control modes and enable/disable toggle from GUI
            TweakUI();
        }

        // Apply torque in OnFlyByWire because the module FixedUpdate() is called too late, resulting in a 1 frame lag in torque updates, leading to having torque when you shouldn't.
        private void UpdateTorque(FlightCtrlState s)
        {
            if (!(FlightGlobals.ready && this.vessel.loaded && !this.vessel.packed))
            {
                return;
            }

            ApplySaturation();

            SetWheelsTorque();
        }

        public void OnDestroy()
        {
            if (callbackIsActive)
            {
                vessel.OnFlyByWire -= new FlightInputCallback(UpdateTorque);
            }
        }

        private void SetWheelsTorque()
        {
            // get the vessel rotation rate ratio
            physicsTorqueFactor = vessel.vesselModules.OfType<VesselModuleRotation>().First().wheelsPhysicsTorqueFactor;

            // On pilot rotation requests, use realistic torque output
            // TODO : Move this to a bool PilotInput the vesselmodule so this isn't evaluated for every partmodule
            // BEGIN MOVE
            if (GameSettings.PITCH_UP.GetKey()
                || GameSettings.PITCH_DOWN.GetKey()
                || GameSettings.YAW_LEFT.GetKey()
                || GameSettings.YAW_RIGHT.GetKey()
                || GameSettings.ROLL_LEFT.GetKey()
                || GameSettings.ROLL_RIGHT.GetKey())
            {
                SetWheelModuleTorque(Vector3.Scale(maxTorque, saturationTorqueFactor) * physicsTorqueFactor * realisticTorqueRatio);
            }
            else
            {
                // When SAS is enabled and is in "target" mode (not stability assist), enable torque if the target is near. 
                // Purpose : lower RCS fuel consumption and ability to keep the vessel in a direction (example : keep ship on node target during a LV-N long burn without wasting tons of MonoPropellant)
                if (this.vessel.Autopilot.Enabled)
                {
                    if (!(this.vessel.Autopilot.Mode.Equals(VesselAutopilot.AutopilotMode.StabilityAssist)))
                    {

                        // TODO : Move this to the vesselmodule so this isn't evaluated for every partmodule
                        // BEGIN MOVE
                        // 1.0 = toward target, 0.0 = target is at a 90° angle
                        float orientationDiff = Math.Max(Vector3.Dot(this.vessel.Autopilot.SAS.targetOrientation.normalized, this.vessel.GetTransform().up.normalized), 0);

                        // Torque output is maximal when on target and decrease to zero quickly
                        float orientationTorqueFactor = Math.Max(torqueOutput.Evaluate(orientationDiff), 0);

                        // END MOVE

                        if (orientationTorqueFactor < Single.Epsilon)
                        {
                            SetWheelModuleTorque(maxTorque * physicsTorqueFactor * realisticTorqueRatio);
                        }
                        else
                        {
                            SetWheelModuleTorque(maxTorque * orientationTorqueFactor * physicsTorqueFactor);
                        }
                    }
                    else
                    {
                        // When SAS is in stability assist mode, use stock torque output
                        SetWheelModuleTorque(Vector3.Scale(maxTorque, saturationTorqueFactor) * physicsTorqueFactor);
                    }
                }
                else
                {
                    // When SAS is disabled, use realistic torque output
                    SetWheelModuleTorque(Vector3.Scale(maxTorque, saturationTorqueFactor) * physicsTorqueFactor * realisticTorqueRatio);
                }
            }
        }

        //public void FixedUpdate()
        //{
        //    if (!(HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && this.vessel.loaded && !this.vessel.packed))
        //    {
        //        return;
        //    }

        //    ApplySaturation();

        //    // Force SAS mode so reaction wheels don't respond to player input :
        //    rwmodule.actuatorModeCycle = 0; // 0 = Normal, 1 = SAS, 2 = Pilot

        //    physicsTorqueFactor = vessel.vesselModules.OfType<VesselModuleRotation>().First().wheelsPhysicsTorqueFactor;

        //    SetWheelsTorque();
        //}

        private void SetWheelModuleTorque(Vector3 torque)
        {
            rwmodule.PitchTorque = torque.x;
            rwmodule.RollTorque = torque.y;
            rwmodule.YawTorque = torque.z;
        }

        private void ApplySaturation()
        {
            // Apply the desaturation rate
            Vector3 desaturation = maxTorque * desaturationRate * TimeWarp.fixedDeltaTime;

            if (Math.Abs(storedMomentum.x) > desaturation.x)
            {
                storedMomentum.x -= Math.Sign(storedMomentum.x) * desaturation.x;
            }
            if (Math.Abs(storedMomentum.y) > desaturation.y)
            {
                storedMomentum.y -= Math.Sign(storedMomentum.y) * desaturation.y;
            }
            if (Math.Abs(storedMomentum.z) > desaturation.z)
            {
                storedMomentum.z -= Math.Sign(storedMomentum.z) * desaturation.z;
            }

            // Update the momentum stored by this reaction wheel
            storedMomentum += TimeWarp.fixedDeltaTime * rwmodule.inputVector;

            // Update the torque reduction factor due to saturation
            saturationTorqueFactor.x = GetSaturatedTorque(maxTorque.x, storedMomentum.x);
            saturationTorqueFactor.y = GetSaturatedTorque(maxTorque.y, storedMomentum.y);
            saturationTorqueFactor.z = GetSaturatedTorque(maxTorque.z, storedMomentum.z);
        }

        
        // Simple formula to determine the torque output ratio according to a momentum threeshold
        private float GetSaturatedTorque(float maxTorque, float storedMomentum)
        {
            if (maxTorque > Single.Epsilon)
            {
                return Math.Min((-Math.Abs(storedMomentum) + maxTorque) / (maxTorque * (1 - saturationTorqueThreesold)), 1.0f);
            }
            else
            {
                return 1.0f;
            }
        }

        // Disable all UI buttons and action groups, except the authority limiter slider.
        // RW torque can still be tweaked/disabled trough the renamed for clarity "Reaction Wheel Autority" GUI
        private void TweakUI()
        {
            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
            {
                foreach (BaseField f in rwmodule.Fields)
                {
                    if (f.name.Equals("actuatorModeCycle") || f.name.Equals("stateString"))
                    {
                        f.guiActive = false;
                        f.guiActiveEditor = false;
                    }
                    if (f.name.Equals("authorityLimiter"))
                    {
                        f.guiName = "Reaction Wheel Authority";
                    }
                    // Debug.Log("RW Fields : guiName=" + f.guiName + ", name=" + f.name + ", guiActive=" + f.guiActive + ", guiActiveEditor=" + f.guiActiveEditor);
                }
                foreach (BaseEvent e in rwmodule.Events)
                {
                    if (e.name.Equals("OnToggle"))
                    {
                        e.guiActive = false;
                        e.guiActiveEditor = false;
                    }
                    // Debug.Log("RW Events : guiName=" + e.guiName + ", name=" + e.name + ", guiActive=" + e.guiActive + ", guiActiveEditor=" + e.guiActiveEditor);
                }
                foreach (BaseAction a in rwmodule.Actions)
                {
                    a.active = false;
                    // Debug.Log("RW Actions : guiName=" + a.guiName + ", name=" + a.name + ", active=" + a.active);
                }
            }
        }
    }
}