using System;
using System.Reflection;
using UnityEngine;

namespace MandatoryRCS
{
  // Should be allscenes, but there is a bug if called from the main menu
  // temp fix : settings aren't available from the main menu
  [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    class Settings : MonoBehaviour
    {
        // RW feature master switch
        public static bool featureReactionWheels;

        // RW torque nerf
        public static float torqueOutputFactor;
        public static bool customizedWheels;

        // RW velocity saturation settings
        public static bool velocitySaturation;
        public static float saturationMinAngVel; // The angular velocity after witch wheels will begin too loose torque (rad/s)
        public static float saturationMaxAngVel; // Max angular velocity reaction wheels can fight against (rad/s)
        public static float saturationMinTorqueFactor; // Reaction wheels torque output at max angular velocity (%)

        // Rotation/SAS persiatnce master switch
        public static bool featureSASRotation;

        // Zero velocity threesold
        public static float velocityThreesold;

        // Other plugins checking
        public static bool isPluginPersistentRotation = false;
        public static bool isPluginSaturatableRW = false;

        // We need to apply settings
        public static bool firstLoad = true;

        private void Start()
        {
          foreach (var a in AssemblyLoader.loadedAssemblies)
          {
            if (a.name == "PersistentRotation")
            {
              isPluginPersistentRotation = true;
            }

            if (a.name == "SaturatableRW")
            {
              isPluginSaturatableRW = true;
            }
          }

          GameEvents.onLevelWasLoaded.Add(onLevelWasLoaded);
          GameEvents.onGameStatePostLoad.Add(onGameStatePostLoad);
          GameEvents.OnGameSettingsApplied.Add(OnGameSettingsApplied);
        }

        private void ApplySettings()
        {
            if (isPluginSaturatableRW)
            {
                featureReactionWheels = false;
            }
            else
            {
                featureReactionWheels = HighLogic.CurrentGame.Parameters.CustomParams<MandatoryRCSRWSettings>().reactionWheelsNerf;
            }

            switch (HighLogic.CurrentGame.Parameters.CustomParams<MandatoryRCSRWSettings>().torqueRatio)
            {
                case MandatoryRCSRWSettings.wheelsTorqueRatio.None:
                    torqueOutputFactor = 0;
                    break;
                case MandatoryRCSRWSettings.wheelsTorqueRatio.Realistic:
                    torqueOutputFactor = 0.005f;
                    break;
                case MandatoryRCSRWSettings.wheelsTorqueRatio.Easy:
                    torqueOutputFactor = 0.01f;
                    break;
                case MandatoryRCSRWSettings.wheelsTorqueRatio.Easier:
                    torqueOutputFactor = 0.05f;
                    break;
            }

            customizedWheels = HighLogic.CurrentGame.Parameters.CustomParams<MandatoryRCSRWSettings>().customizedWheels;

            velocitySaturation = HighLogic.CurrentGame.Parameters.CustomParams<MandatoryRCSRWSettings>().velocitySaturation;
            saturationMinAngVel = HighLogic.CurrentGame.Parameters.CustomParams<MandatoryRCSRWSettings>().minAngularVelocity * ((float)Math.PI / 180); // degree to radian
            saturationMaxAngVel = HighLogic.CurrentGame.Parameters.CustomParams<MandatoryRCSRWSettings>().maxAngularVelocity * ((float)Math.PI / 180); // degree to radian
            saturationMinTorqueFactor = HighLogic.CurrentGame.Parameters.CustomParams<MandatoryRCSRWSettings>().minTorqueFactor * 0.01f; // int % to float %

            if (isPluginPersistentRotation)
            {
                featureSASRotation = false;
            }
            else
            {
                featureSASRotation = HighLogic.CurrentGame.Parameters.CustomParams<MandatoryRCSRotationSettings>().rotationPersistance;
            }
            velocityThreesold = HighLogic.CurrentGame.Parameters.CustomParams<MandatoryRCSRotationSettings>().velocityThreesold * ((float)Math.PI / 180); // degree to radian
        }

        private void onLevelWasLoaded(GameScenes scene)
        {
            if (firstLoad && (scene == GameScenes.SPACECENTER || scene == GameScenes.TRACKSTATION || scene == GameScenes.EDITOR || scene == GameScenes.FLIGHT))
            {
                ApplySettings();
                firstLoad = false;
            }

            if (scene == GameScenes.MAINMENU)
            {
                firstLoad = true;
            }
        }

        private void onGameStatePostLoad(ConfigNode data) // This is called on quickloading
        {
            ApplySettings();
        }

        private void OnGameSettingsApplied() // This is called on getting out of the settings menu
        {
            ApplySettings();
        }

        private void OnDestroy()
        {
            GameEvents.onLevelWasLoaded.Remove(onLevelWasLoaded);
            GameEvents.onGameStatePostLoad.Remove(onGameStatePostLoad);
            GameEvents.OnGameSettingsApplied.Remove(OnGameSettingsApplied);
        }
    }


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
        public override string DisplaySection
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

        [GameParameters.CustomStringParameterUI("DISABLED ", autoPersistance = false, lines = 1)]
        public string autoDisabled = " the \"(Semi-)Saturatable Reaction Wheels\" plugin is loaded. If you want this feature, uninstall it and restart the game.";

        [GameParameters.CustomParameterUI("Enable reaction wheels rebalance",
             toolTip = "Disabling will revert to the stock behaviour.")]
        public bool reactionWheelsNerf = !Settings.isPluginSaturatableRW;

        [GameParameters.CustomStringParameterUI("About ", autoPersistance = false, lines = 6)]
        public string aboutRW0 = "Reaction wheels force is heavily reduced on pilot & SAS rotation requests. Full force is provided if the SAS in \"Stability Assist\" mode or if you have reached the active SAS marker.";

        [GameParameters.CustomParameterUI("Reaction wheels SAS & pilot control",
            toolTip = "Reaction wheels torque output on pilot/SAS rotation requests :\n\n\"None\" : Reaction wheels provide only stabilization, they can't be used\nto initiate a rotation (was the default in previous versions). \n\n\"Realistic\" : Reaction wheels torque output is scaled to a somewhat realistic value\naccording their ingame weight and size.\n\n\"Easy\" : 2 times the realistic torque.\n\n\"Easier\" : 10 times the realistic torque.")]
        public wheelsTorqueRatio torqueRatio = wheelsTorqueRatio.Realistic;
        public enum wheelsTorqueRatio { None, Realistic, Easy, Easier }

        [GameParameters.CustomParameterUI("SAS & pilot control customization",
            toolTip = "When enabled, reaction wheels integrated in pods and cockpits can't provide SAS & pilot control,\nonly independant reaction wheels parts and probe cores can. \n\nWhen disabled, all reaction wheels can provide SAS & pilot control.\n\nThis can be overriden by explicitly defining the ModuleTorqueController\nin the part config (or with an MM patch) and adding the property\nisControllable=true/false.")]
        public bool customizedWheels = true;

        [GameParameters.CustomParameterUI("Enable velocity saturation",
            toolTip = "When enabled, the faster the vessel rotate, the weaker reaction wheels are.")]
        public bool velocitySaturation = true;

        [GameParameters.CustomIntParameterUI("Saturation min thresold", minValue = 2, maxValue = 12, stepSize = 2, displayFormat = "0 deg/sec",
            toolTip = "Torque output will begin to decrease when the vessel is turning faster than this angular velocity.\nDefault value : 6 deg/sec.")]
        public int minAngularVelocity = 6; // 0,1047 rad/s

        [GameParameters.CustomIntParameterUI("Saturation max thresold", minValue = 15, maxValue = 90, stepSize = 15, displayFormat = "0 deg/sec",
            toolTip = "Torque output will reach its minimum when the vessel is turning faster than this angular velocity..\nDefault value : 45 deg/sec.")]
        public int maxAngularVelocity = 45; // 0,7853 rad/s

        [GameParameters.CustomIntParameterUI("Saturation minimum output", minValue = 0, maxValue = 25, stepSize = 5, displayFormat = @"0 \%",
            toolTip = "The minimum torque output of reaction wheels.\nSetting this to 0 % will result in loosing all control when the max thresold is reached.\nDefault value : 5 %.")]
        public int minTorqueFactor = 5;

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            if (Settings.isPluginSaturatableRW)
            {
                if (member.Name == "autoDisabled")
                { return true; }
                else
                { return false; }
            }

            if (reactionWheelsNerf == false && (
                member.Name == "autoDisabled" ||
                member.Name == "torqueRatio" ||
                member.Name == "customizedWheels" ||
                member.Name == "velocitySaturation" ||
                member.Name == "minAngularVelocity" ||
                member.Name == "maxAngularVelocity" ||
                member.Name == "minTorqueFactor"))
            {return false;}

            if (velocitySaturation == false && (
                member.Name == "autoDisabled" ||
                member.Name == "minAngularVelocity" ||
                member.Name == "maxAngularVelocity" ||
                member.Name == "minTorqueFactor"))
            { return false; }

            if (member.Name == "autoDisabled")
            { return false; }

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
        public override string DisplaySection
        {
            get { return "MandatoryRCS"; }
        }

        public override int SectionOrder
        {
            get { return 2; }
        }

        public override string Title
        {
            get { return "Rotation and SAS persistence"; }
        }

        [GameParameters.CustomStringParameterUI("DISABLED ", autoPersistance = false, lines = 3)]
        public string autoDisabled = "the \"Persistent Rotation\" plugin is loaded. If you want this feature, uninstall it and restart the game.";

        [GameParameters.CustomParameterUI("Enable rotation and SAS persistence",
             toolTip = "Disabling will revert to the stock behaviour.")]
        public bool rotationPersistance = !Settings.isPluginPersistentRotation;

        [GameParameters.CustomStringParameterUI("About ", autoPersistance = false, lines = 9)]
        public string aboutRSP = "This feature make the vessel rotation persistent through non-physics timewarps, when switching vessels and reloading. It also make the craft keep its orientation toward the SAS selection during timewarps. The SAS selection is remembered when switching vessels and reloading.";

        [GameParameters.CustomFloatParameterUI("Stability thresold (deg/sec)", minValue = 0.25f, maxValue = 4.0f, displayFormat = "F1",
             toolTip = "When the angular velocity (rotation speed) of the vessel is under this value, \nit is considered stable and will not rotate during timwarps or when unloaded.\nDefault value : 1.5 deg/sec")]
        public float velocityThreesold = 1.5f; // 0,02617 rad/s

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            if (Settings.isPluginPersistentRotation)
            {
                if (member.Name == "autoDisabled")
                { return true; }
                else
                { return false; }
            }

            if (rotationPersistance == false && (
                member.Name == "autoDisabled" ||
                member.Name == "velocityThreesold"))
            { return false; }

            if (member.Name == "autoDisabled")
            { return false; }

            return true;
        }
    }
}
