using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MandatoryRCS
{
    class DebugInfo : PartModule
    {
        VesselModuleRotation vm;
        ModuleTorqueController tc;
        ModuleReactionWheel rw;

        [KSPField(guiActive = true, guiName = "Ang. Velocity", guiFormat = "00.00")]
        public float angularVelocity;

        [KSPField(guiActive = true, guiName = "rollTorque", guiFormat = "00.00")]
        public float rollTorque;

        [KSPField(guiActive = true, guiName = "pitchTorque", guiFormat = "00.00")]
        public float pitchTorque;

        [KSPField(guiActive = true, guiName = "yawTorque", guiFormat = "00.00")]
        public float yawTorque;



        [KSPField(guiActive = true, guiName = "inputVectorDelta")]
        public string inputVectorDelta;

        [KSPField(guiActive = true, guiName = "storedMomentum")]
        public string storedMomentum;

        [KSPField(guiActive = true, guiName = "saturationTorqueFactor")]
        public string saturationTorqueFactor;

        [KSPField(guiActive = true, guiName = "rollSaturated")]
        public bool rollSaturated;


        public void FixedUpdate()
        {
            if (!(HighLogic.LoadedSceneIsFlight && FlightGlobals.ready))
            {
                return;
            }

            

            vm = vessel.vesselModules.OfType<VesselModuleRotation>().First();
            tc = part.Modules.GetModule<ModuleTorqueController>();
            rw = part.Modules.GetModule<ModuleReactionWheel>();

            angularVelocity = vessel.angularVelocity.magnitude;
            rollTorque = rw.RollTorque;
            pitchTorque = rw.PitchTorque;
            yawTorque = rw.YawTorque;

            inputVectorDelta = (rw.inputVector * TimeWarp.fixedDeltaTime).ToString("0.0000");
            storedMomentum = tc.storedMomentum.ToString("0.000");
            rollSaturated = tc.rollSaturated;
            saturationTorqueFactor = tc.saturationTorqueFactor.ToString("0.000");
        }
    }
}
