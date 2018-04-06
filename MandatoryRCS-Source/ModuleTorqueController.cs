/* 
 * This file and all code it contains is released in the public domain
 */

using UnityEngine;

namespace MandatoryRCS
{
    public class ModuleTorqueController : PartModule
    {
        [KSPField]
        public bool isControllable;

        private ModuleReactionWheel rwmodule;
        private Vector3 maxTorque;
        private float responseTime;

        public override void OnLoad(ConfigNode node)
        {
            // Does this RW respond to pilot/SAS input ?
            if (Settings.customizedWheels)
            {
                isControllable = true;
                return;
            }

            // If not defined in the module CFG, default rule is that pods and cockpits are not controllable, but RW parts and probes are.
            if (!node.HasValue("isControllable"))
            {
                isControllable = (part.CrewCapacity > 0) ? false : true;
            }
        }

        public void Start()
        {
            // Get the reaction wheel module
            rwmodule = part.Modules.GetModule<ModuleReactionWheel>();
            // Get the module config torque value
            maxTorque.x = rwmodule.PitchTorque;
            maxTorque.y = rwmodule.RollTorque;
            maxTorque.z = rwmodule.YawTorque;
            responseTime = rwmodule.torqueResponseSpeed;
            // Hide RW control modes and enable/disable toggle from GUI and action groups
            TweakUI();
        }

        public void LateUpdate()
        {
            //rwmodule.torqueResponseSpeed = responseTime;
        }

        public void SetTorque(float torqueFactor)
        {
            rwmodule.PitchTorque = maxTorque.x * torqueFactor;
            rwmodule.RollTorque = maxTorque.y * torqueFactor;
            rwmodule.YawTorque = maxTorque.z * torqueFactor;
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

        //private void SetWheelModuleTorque(Vector3 torque)
        //{
        //    if (torque.magnitude < Single.Epsilon)
        //    {
        //        rwmodule.RollTorque = 0;
        //        rwmodule.PitchTorque = 0;
        //        rwmodule.YawTorque = 0;
        //        rwmodule.enabled = false; // This reduce the "residual torque effect" that is driving me nuts.
        //        rwmodule.isEnabled = false;
        //        rwmodule.actuatorModeCycle = 1; // This prevent some weird behaviour of the SAS causing it to be unable to use RCS properly when reaction wheels have zero torque.
        //    }
        //    else
        //    {
        //        rwmodule.PitchTorque = torque.x;
        //        rwmodule.RollTorque = torque.y;
        //        rwmodule.YawTorque = torque.z;
        //        rwmodule.enabled = true;
        //        rwmodule.isEnabled = true;
        //        rwmodule.actuatorModeCycle = 0;
        //    }
        //}

    }
}