using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// TODO :
// - implement torque ON when RCS is ON, to reduce RCS fuel consumption

namespace UnadvancedStabilizers
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

        internal ModuleReactionWheel rwmodule;
        public float maxRollTorque;
        public float maxPitchTorque;
        public float maxYawTorque;

        public void Start()
        {
            // Get the part reaction wheel module
            rwmodule = part.Modules.GetModule<ModuleReactionWheel>();
            maxRollTorque = rwmodule.RollTorque;
            maxPitchTorque = rwmodule.PitchTorque;
            maxYawTorque = rwmodule.YawTorque;

            // Hide RW control modes and enable/disable toggle from GUI
            // RW torque can still be tweaked/disabled trough the renamed for clarity "Reaction Wheel Autority" GUI
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
                // Debug.Log("[MTC] RW Fields : guiName=" + f.guiName + ", name=" + f.name + ", guiActive=" + f.guiActive + ", guiActiveEditor=" + f.guiActiveEditor);
            }
            foreach (BaseEvent e in rwmodule.Events) 
            {
                if (e.name.Equals("OnToggle"))
                {
                    e.guiActive = false;
                    e.guiActiveEditor = false;
                }
                // Debug.Log("[MTC] RW Events : guiName=" + e.guiName + ", name=" + e.name + ", guiActive=" + e.guiActive + ", guiActiveEditor=" + e.guiActiveEditor);
            }
        }

        public void FixedUpdate()
        {
            if (!(HighLogic.LoadedSceneIsFlight && FlightGlobals.ready)) 
            { 
                return;
            }

            // Force SAS mode so reaction wheels don't respond to player input :
            rwmodule.actuatorModeCycle = 1; // 0 = Normal, 1 = SAS, 2 = Pilot

            // When SAS is enabled and is in "target" mode (not stability assist), enable torque if the target is near. 
            // Purpose : lower RCS fuel consumption and ability to keep the vessel in a direction (example : keep ship on node target during a LV-N long burn without wasting tons of MonoPropellant)
            if (this.vessel.Autopilot.Enabled && !(this.vessel.Autopilot.Mode.Equals(VesselAutopilot.AutopilotMode.StabilityAssist)))
            {
                // 1.0 = toward target, 0.0 = target is at a 90° angle
                float orientationDiff = Math.Max(Vector3.Dot(this.vessel.Autopilot.SAS.targetOrientation.normalized, this.vessel.GetTransform().up.normalized), 0);

                // Torque output is maximal when on target and decrease to zero quickly
                float torqueRatio = Math.Max(torqueOutput.Evaluate(orientationDiff), 0);

                // Disable the RW instead of applying zero torque because otherwise the SAS will still find a bit of "magical torque" out of nowhere...
                if (torqueRatio < Single.Epsilon)
                {
                    rwmodule.enabled = false;
                    rwmodule.isEnabled = false;
                }
                else 
                {
                    rwmodule.enabled = true;
                    rwmodule.isEnabled = true;
                    rwmodule.RollTorque = maxRollTorque * torqueRatio;
                    rwmodule.PitchTorque = maxPitchTorque * torqueRatio;
                    rwmodule.YawTorque = maxYawTorque * torqueRatio;
                }
            }
            else
            {
                // Enable torque when SAS is in stability assist mode
                rwmodule.enabled = true;
                rwmodule.isEnabled = true;
                rwmodule.RollTorque = maxRollTorque;
                rwmodule.PitchTorque = maxPitchTorque;
                rwmodule.YawTorque = maxYawTorque;
            }
        }
    }
}
