using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MandatoryRCS
{
    class MandatoryRCSRWSettings : GameParameters.CustomParameterNode
    {
        public override GameParameters.GameMode GameMode
        {
            get { return GameParameters.GameMode.ANY; }
        }

        public override bool HasPresets
        {
            get { return false; }
        }

        public override string Section
        {
            get { return "MandatoryRCS"; }
        }

        public override int SectionOrder
        {
            get { return 1; }
        }

        public override string Title
        {
            get { return "Reaction wheels rebalance"; }
        }

        [GameParameters.CustomParameterUI("Enable reaction wheels rebalance",
             toolTip = "Disabling will revert to the stock behaviour.")]
        public bool reactionWheelsNerf = true;

        [GameParameters.CustomStringParameterUI("About ", autoPersistance = false, lines = 6)]
        public string aboutRW0 = "reduce the reaction wheels torque output on pilot or SAS rotation requests. Full torque is still provided when the SAS is standing by in \"Stability Assist\" mode or when the craft orientation is near the current SAS selection.";

        [GameParameters.CustomParameterUI("Strict mode",
            toolTip = "When enabled, reaction wheels provide no torque at all on SAS or pilot input,\n they are used only for stabilization.")]
        public bool strictMode = false;

        [GameParameters.CustomParameterUI("Reaction wheels torque", 
            toolTip = "RW torque output on pilot/SAS rotation requests.\nThe \"Normal\" setting is near realistic values.")]
        public wheelsTorqueRatio torqueRatio = wheelsTorqueRatio.Normal;
        public enum wheelsTorqueRatio { Hard, Normal, Easy, Easier }

        [GameParameters.CustomParameterUI("Enable pseudo-saturation",
            toolTip = "When enabled, the faster the vessel rotate, the weaker reaction wheels are.")]
        public bool pseudoSaturation = true;

        [GameParameters.CustomIntParameterUI("Saturation min threesold", minValue = 2, maxValue = 12, stepSize = 2, displayFormat = "0 deg/sec",
            toolTip = "Torque output will begin to decrease when the vessel is turning faster than this angular velocity.\nDefault value : 6 deg/sec.")]
        public int minAngularVelocity = 6; // 0,1047 rad/s

        [GameParameters.CustomIntParameterUI("Saturation max threesold", minValue = 15, maxValue = 90, stepSize = 15, displayFormat = "0 deg/sec",
            toolTip = "Torque output will reach its minimum when the vessel is turning faster than this angular velocity..\nDefault value : 45 deg/sec.")]
        public int maxAngularVelocity = 45; // 0,7853 rad/s

        [GameParameters.CustomIntParameterUI("Saturation minimum output", minValue = 0, maxValue = 25, stepSize = 5, displayFormat = @"0 \%",
            toolTip = "The minimum torque output of reaction wheels.\nSetting this to 0 % will result in loosing all control when the max threesold is reached.\nDefault value : 5 %.")]
        public int minTorqueFactor = 5;


        public float wheelsMinAngularVelocity = 0.1f; // The angular velocity after witch wheels will begin too loose torque
        public float wheelsMaxAngularVelocity = 0.785f; // Max angular velocity reaction wheels can fight against (rad/s), 0.785 = 45°/sec
        public float wheelsMinTorqueFactor = 0.05f; // Reaction wheels torque output at max angular velocity (%)

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            if (reactionWheelsNerf == false && (
                member.Name == "strictMode" ||
                member.Name == "torqueRatio" ||
                member.Name == "pseudoSaturation" ||
                member.Name == "minAngularVelocity" ||
                member.Name == "maxAngularVelocity" ||
                member.Name == "minTorqueFactor"
                )) { return false; }

            if (strictMode == true && (
                member.Name == "torqueRatio"
                )) { return false; }

            if (pseudoSaturation == false && (            
                member.Name == "minAngularVelocity" ||
                member.Name == "maxAngularVelocity" ||
                member.Name == "minTorqueFactor"
                )) { return false; }

            return true;
        }
    }

    class MandatoryRCSRotationSettings : GameParameters.CustomParameterNode
    {
        public override GameParameters.GameMode GameMode
        {
            get { return GameParameters.GameMode.ANY; }
        }

        public override bool HasPresets
        {
            get { return false; }
        }

        public override string Section
        {
            get { return "MandatoryRCS"; }
        }

        public override int SectionOrder
        {
            get { return 2; }
        }

        public override string Title
        {
            get { return "Rotation and SAS persistance"; }
        }

        [GameParameters.CustomParameterUI("Enable rotation and SAS persistance",
             toolTip = "Disabling will revert to the stock behaviour.")]
        public bool rotationPersistance = true;

        [GameParameters.CustomStringParameterUI("About ", autoPersistance = false, lines = 9)]
        public string aboutRSP = "This feature make the vessel rotation persistant trough non-physics timewarps, when switching vessels and reloading. It also make the craft keep its orientation toward the SAS selection during timewarps. The SAS selection is remembered when switching vessels and reloading.";

        [GameParameters.CustomFloatParameterUI("Stability threesold (deg/sec)", minValue = 0.1f, maxValue = 5.0f, displayFormat = "F1",
             toolTip = "When the angular velocity (rotation speed) of the vessel is under this value, \nit is considered stable and will not rotate during timwarps or when unloaded.\nDefault value : 1.5 deg/sec")]
        public float velocityThreesold = 1.5f; // 0,02617 rad/s

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            if (rotationPersistance == false && (
                member.Name == "velocityThreesold"
                )) { return false; }

            return true;
        }

    }
}
