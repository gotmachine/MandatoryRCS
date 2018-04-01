using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MandatoryRCS
{
    public class ComponentRWTorqueControl : ComponentBase
    {
        public enum TorqueMode
        {
            nerfed,
            locked,
            stock
        }
        public TorqueMode mode = TorqueMode.stock;
        private float torqueFactor = 0;

        private bool lockedOnDirection;

        const float torqueIncreaseTime = 3.0f;

        private static Keyframe[] curveKeys = new Keyframe[]
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

        private static FloatCurve torqueOutput = new FloatCurve(curveKeys);

        public override void ComponentUpdate()
        {
            // Get the saturation factor calculated from the vessel rotation
            //saturationFactor = vessel.vesselModules.OfType<VesselModuleRotation>().First().velSaturationTorqueFactor;
            // Determine the velocity saturation factor for reaction wheels (used by ModuleTorqueController)
            //        if (MandatoryRCSSettings.velocitySaturation)
            //        {
            //            velSaturationTorqueFactor = Math.Max(1.0f - Math.Min((Math.Max(angularVelocity.magnitude - MandatoryRCSSettings.saturationMinAngVel, 0.0f) * MandatoryRCSSettings.saturationMaxAngVel), 1.0f), MandatoryRCSSettings.saturationMinTorqueFactor);
            //        }
            //        else
            //        {
            //            velSaturationTorqueFactor = 1.0f;
            //        }

            float requestedTorqueFactor = 0;

            // On pilot rotation requests or if the SAS is disabled, use nerfed torque output
            if (!vesselModule.pilotIsIdle || !vesselModule.SASisEnabled)
            {
                mode = TorqueMode.nerfed;
            }

            // if the SAS is in a stability mode, use stock torque
            else if (vesselModule.SASMode == SASUI.SASFunction.Hold
                || vesselModule.SASMode == SASUI.SASFunction.HoldSmooth
                || vesselModule.SASMode == SASUI.SASFunction.KillRot)
            {
                mode = TorqueMode.stock;
            }
            else
            {
                // SAS is in target mode, enable full torque if the target is near. 
                // orientationDiff : 1.0 = toward target, 0.0 = target is at a 90° angle
                float orientationDiff = Math.Max(Vector3.Dot(vesselModule.directionWanted.normalized, vessel.ReferenceTransform.up.normalized), 0);

                //if (!lockedOnDirection && orientationDiff > 0.999f && vesselModule.angularVelocity.magnitude < 0.01)
                if (!lockedOnDirection && orientationDiff > 0.999f && vesselModule.angularVelocity.magnitude < 0.1)
                {
                    lockedOnDirection = true;
                }
                if (lockedOnDirection && orientationDiff < 0.90f)
                {
                    lockedOnDirection = false;
                }

                if (lockedOnDirection)
                {
                    // Torque output is maximal when on target and decrease to zero quickly according to the floatcurve
                    requestedTorqueFactor = Math.Max(torqueOutput.Evaluate(orientationDiff), 0);
                    if (requestedTorqueFactor < Single.Epsilon)
                    {
                        mode = TorqueMode.nerfed; // We are locked but too far
                    }
                    else
                    {
                        mode = TorqueMode.locked; // We are locked, apply the torque curve
                    }
                }
                else
                {
                    mode = TorqueMode.nerfed; // Target hasn't been reached
                }
            }

            switch (mode)
            {
                case TorqueMode.nerfed:
                    requestedTorqueFactor = Settings.torqueOutputFactor;
                    break;
                case TorqueMode.stock:
                    requestedTorqueFactor = 1;
                    break;
            }

            // Avoid an abrupt increase in the available torque, so the SAS can adjust to it
            if (torqueFactor < requestedTorqueFactor)
            {
                torqueFactor += (1.0f / torqueIncreaseTime) * TimeWarp.fixedDeltaTime;
                torqueFactor = Math.Min(torqueFactor, requestedTorqueFactor);
            }
            else
            {
                torqueFactor = requestedTorqueFactor;
            }
            

            // Apply the result to all reaction wheels
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part p = vessel.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleTorqueController rw = p.Modules[m] as ModuleTorqueController;
                    if (rw != null)
                    {
                        rw.SetTorque(torqueFactor);
                    }
                }
            }
        }
    }
}
