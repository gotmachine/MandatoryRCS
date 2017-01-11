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

        }
    }
}
