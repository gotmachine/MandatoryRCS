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

        private bool isControllable;

        internal ModuleReactionWheel rwmodule;

        public Vector3 maxTorque;
        private bool isOnTarget;
        private bool pilotInput = false;
        private float saturationFactor = 1.0f;
        private bool callbackIsActive = false;

        public void Start()
        {
            // Get the reaction wheel module
            rwmodule = part.Modules.GetModule<ModuleReactionWheel>();
            // Get the module config torque value
            maxTorque.x = rwmodule.PitchTorque;
            maxTorque.y = rwmodule.RollTorque;
            maxTorque.z = rwmodule.YawTorque;

            if (MandatoryRCSSettings.featureReactionWheels)
            {
                // Does this RW respond to pilot/SAS input ?
                if (MandatoryRCSSettings.customizedWheels)
                {
                    bool isDefinedInCFG = false;
                    foreach (ConfigNode node in part.partInfo.partConfig.GetNodes("MODULE"))
                    {
                        if (node.GetValue("name") == "ModuleTorqueController")
                        {
                            isDefinedInCFG = node.TryGetValue("isControllable", ref isControllable);
                            break;
                        }
                    }
                    // If not defined in the module CFG, default rule is that pods and cockpits are not controllable, but RW parts and probes are.
                    if (!isDefinedInCFG)
                    {
                        isControllable = (part.CrewCapacity > 0) ? false : true;
                    }
                }
                else
                {
                    isControllable = true;
                }

                // We do the torque calculations only when in flight
                if (HighLogic.LoadedSceneIsFlight)
                {
                    vessel.OnFlyByWire += new FlightInputCallback(UpdateTorque);
                    vessel.OnPreAutopilotUpdate += new FlightInputCallback(GetPilotInput);
                    callbackIsActive = true;
                }
            }
            // Hide RW control modes and enable/disable toggle from GUI and action groups
            TweakUI();
        }

        private void GetPilotInput(FlightCtrlState st)
        {
            pilotInput = !st.isIdle;
        }

        public void OnDestroy()
        {
            if (callbackIsActive)
            {
                vessel.OnFlyByWire -= new FlightInputCallback(UpdateTorque);
                vessel.OnPreAutopilotUpdate -= new FlightInputCallback(GetPilotInput);
            }
        }
        
        // Apply torque in OnFlyByWire because the module FixedUpdate() is called too late, resulting in a 1 frame lag in torque updates, leading to having torque when you shouldn't.
        private void UpdateTorque(FlightCtrlState st)
        {
            if (FlightGlobals.ready && vessel.loaded && !vessel.packed)
            {
                // Get the saturation factor calculated from the vessel rotation
                saturationFactor = vessel.vesselModules.OfType<VesselModuleRotation>().First().velSaturationTorqueFactor;

                // On pilot rotation requests, use nerfed torque output
                if (pilotInput)
                {
                    SetNerfedTorque(); // SAS is disabled
                }
                else
                {
                    if (vessel.Autopilot.Enabled)
                    {
                        if (vessel.Autopilot.Mode.Equals(VesselAutopilot.AutopilotMode.StabilityAssist))
                        {
                            SetStockTorque(); // SAS is in stability assist mode
                        }
                        else
                        {
                            // SAS is in target mode, enable full torque if the target is near. 
                            // orientationDiff : 1.0 = toward target, 0.0 = target is at a 90° angle
                            // float orientationDiff = Math.Max(Vector3.Dot(vessel.Autopilot.SAS.targetOrientation.normalized, vessel.GetTransform().up.normalized), 0);
                            float orientationDiff = Math.Max(Vector3.Dot(vessel.vesselModules.OfType<VesselModuleRotation>().First().targetDirection.normalized, vessel.GetTransform().up.normalized), 0);

                            if (!isOnTarget && orientationDiff > 0.999f)
                            {
                                isOnTarget = true;
                            }
                            if (isOnTarget && orientationDiff < 0.90f)
                            {
                                isOnTarget = false;
                            }

                            if (isOnTarget)
                            {
                                // Torque output is maximal when on target and decrease to zero quickly according to the floatcurve
                                float orientationTorqueFactor = Math.Max(torqueOutput.Evaluate(orientationDiff), 0);
                                if (orientationTorqueFactor < Single.Epsilon || orientationTorqueFactor <= saturationFactor * MandatoryRCSSettings.torqueOutputFactor)
                                {
                                    SetNerfedTorque(); // We are locked but too far
                                }
                                else
                                {
                                    SetTargetTorque(orientationTorqueFactor); // We are locked, apply the torque curve
                                }
                            }
                            else
                            {
                                SetNerfedTorque(); // Target hasn't been reached
                            }
                            
                        }
                    }
                    else
                    {
                        SetNerfedTorque(); // SAS is disabled
                    }
                }
            }
        }

        private void SetStockTorque()
        {
            SetWheelModuleTorque(maxTorque * saturationFactor);
        }

        private void SetNerfedTorque()
        {
            if (isControllable)
            {
                SetWheelModuleTorque(maxTorque * saturationFactor * MandatoryRCSSettings.torqueOutputFactor);
            }
            else
            {
                SetWheelModuleTorque(Vector3.zero);
            }
        }

        private void SetTargetTorque(float orientationTorqueFactor)
        {
            SetWheelModuleTorque(maxTorque * orientationTorqueFactor * saturationFactor);
        }
        
        private void SetWheelModuleTorque(Vector3 torque)
        {
            if (torque.magnitude < Single.Epsilon)
            {
                rwmodule.RollTorque = 0;
                rwmodule.PitchTorque = 0;
                rwmodule.YawTorque = 0;
                rwmodule.enabled = false; // This reduce the "residual torque effect" that is driving me nuts.
                rwmodule.isEnabled = false;
                rwmodule.actuatorModeCycle = 1; // This prevent some weird behaviour of the SAS causing it to be unable to use RCS properly when reaction wheels have zero torque.
            }
            else
            {
                rwmodule.PitchTorque = torque.x;
                rwmodule.RollTorque = torque.y;
                rwmodule.YawTorque = torque.z;
                rwmodule.enabled = true;
                rwmodule.isEnabled = true;
                rwmodule.actuatorModeCycle = 0;
            }
        }

        // Disable all UI buttons and action groups, except the authority limiter slider.
        // RW torque can still be tweaked/disabled trough the renamed for clarity "Reaction Wheel Autority" GUI
        // TODO : reenable Normal/SAS/Pilot mode switch if strictMode is disabled
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