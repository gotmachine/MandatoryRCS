using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnadvancedStabilizers
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class GlobalTorqueController : MonoBehaviour
    {
        // This class alter the torque output of all loaded reaction wheels using the angular difference between the SAS target orientation and the vessel current orientation.
        // Full torque is provided when the difference is null and it is decreased quickly to zero if when the orientations differs
        // Consequently, the RW purpose is to provide stability once the SAS target has been reached, making RCS thrusters mandatory to control a vessel.
        // This is done in a KSPAddon instead of directly in the partModule because there is a weird interference with the SAS system if the RW torque is modified in a fixedUpdate.


        // Global list of loaded reaction wheels
        public static List<ReactionWheelReference> loadedReactionWheels = new List<ReactionWheelReference>();

        // Curve definition for torque nerfing
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

        // Utility class representing a RW
        public class ReactionWheelReference
        {
            public ModuleReactionWheel module;
            public float maxRollTorque;
            public float maxPitchTorque;
            public float maxYawTorque;
            public double maxEcRate;

            public ReactionWheelReference(ModuleReactionWheel mrw)
            {
                module = mrw;
                maxRollTorque = mrw.RollTorque;
                maxPitchTorque = mrw.PitchTorque;
                maxYawTorque = mrw.YawTorque;
                maxEcRate = mrw.resHandler.inputResources.First().rate;
            }
        }

        public void Start()
        {
            FlightGlobals.ActiveVessel.OnPreAutopilotUpdate += adjustSASTorque;
        }

        public void adjustSASTorque(FlightCtrlState state)
        {
            // When SAS is enabled and is in "target" mode (not stability assist), enable torque if the target is near. 
            // Purpose : lower RCS fuel consumption and ability to keep the vessel in a direction (example : keep ship on node target during a LV-N long burn without wasting tons of MonoPropellant)
            if (this.vessel.Autopilot.Enabled && !(this.vessel.Autopilot.Mode.Equals(VesselAutopilot.AutopilotMode.StabilityAssist)))
            {
                // 1.0 = toward target, 0.0 = target is at a 90° angle
                float orientationDiff = Math.Max(Vector3.Dot(this.vessel.Autopilot.SAS.targetOrientation.normalized, this.vessel.GetTransform().up.normalized), 0);

                // Torque output is maximal when on target and decrease to zero quickly
                float torqueRatio = Math.Max(torqueOutput.Evaluate(orientationDiff), 0);

                rwmodule.RollTorque = maxRollTorque * torqueRatio;
                rwmodule.PitchTorque = maxPitchTorque * torqueRatio;
                rwmodule.YawTorque = maxYawTorque * torqueRatio;
                rwmodule.resHandler.inputResources.First().rate = (torqueRatio < Single.Epsilon) ? 0.0f : maxEcRate; // EC rate
            }
            else
            {
                // Enable torque when SAS is in stability assist mode
                rwmodule.RollTorque = maxRollTorque;
                rwmodule.PitchTorque = maxPitchTorque;
                rwmodule.YawTorque = maxYawTorque;
                rwmodule.resHandler.inputResources.First().rate = maxEcRate;
            }
        }

    }

}
