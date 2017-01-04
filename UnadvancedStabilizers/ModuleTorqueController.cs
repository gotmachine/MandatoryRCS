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

        // DEBUG
        public Vector3d planetUp;
        public Vector3 obtNormal;
        public Vector3 obtRadial;
        public Vector3 srfNormal;
        public Vector3 srfRadial;


        [KSPField(guiActive = true, guiName = "Rot X")]
        public float rotx;
        [KSPField(guiActive = true, guiName = "Rot Y")]
        public float roty;
        [KSPField(guiActive = true, guiName = "Rot Z")]
        public float rotz;
        [KSPField(guiActive = true, guiName = "SavAngVelocity")]
        public float savedAngularVelocity;
        [KSPField(guiActive = true, guiName = "CurAngVelocity")]
        public float currentAngularVelocity;
        [KSPField(guiActive = true, guiName = "Target keep")]
        public bool targetKeeping;
        [KSPField(guiActive = true, guiName = "Target")]
        public string targetString;
        [KSPField(guiActive = true, guiName = "Heading")]
        public string headingString;

        // END DEBUG

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

        [KSPEvent(guiName = "Prograde orb", active = true, guiActive = true)]
        public void porb()
        {
            this.vessel.SetRotation(Quaternion.FromToRotation(vessel.GetTransform().up, vessel.obt_velocity) * vessel.transform.rotation, true);
        }

        [KSPEvent(guiName = "Prograde srf", active = true, guiActive = true)]
        public void psurf()
        {
            this.vessel.SetRotation(Quaternion.FromToRotation(vessel.GetTransform().up, vessel.srf_velocity) * vessel.transform.rotation, true);
        }

        [KSPEvent(guiName = "Normal orb", active = true, guiActive = true)]
        public void norb()
        {
            planetUp = (vessel.rootPart.transform.position - vessel.mainBody.position).normalized;
            obtNormal = Vector3.Cross(vessel.obt_velocity, planetUp).normalized;

            this.vessel.SetRotation(Quaternion.FromToRotation(vessel.GetTransform().up, obtNormal) * vessel.transform.rotation, true);
        }

        [KSPEvent(guiName = "Normal srf", active = true, guiActive = true)]
        public void nsrf()
        {
            planetUp = (vessel.rootPart.transform.position - vessel.mainBody.position).normalized;
            srfNormal = Vector3.Cross(vessel.srf_velocity, planetUp).normalized;

            this.vessel.SetRotation(Quaternion.FromToRotation(vessel.GetTransform().up, srfNormal) * vessel.transform.rotation, true);
        }

        [KSPEvent(guiName = "RadialIn orb", active = true, guiActive = true)]
        public void rorb()
        {
            planetUp = (vessel.rootPart.transform.position - vessel.mainBody.position).normalized;
            obtNormal = Vector3.Cross(vessel.obt_velocity, planetUp).normalized;
            obtRadial = Vector3.Cross(vessel.obt_velocity, obtNormal).normalized;

            this.vessel.SetRotation(Quaternion.FromToRotation(vessel.GetTransform().up, obtRadial) * vessel.transform.rotation, true);
        }

        [KSPEvent(guiName = "RadialIn srf", active = true, guiActive = true)]
        public void rsrf()
        {
            planetUp = (vessel.rootPart.transform.position - vessel.mainBody.position).normalized;
            srfNormal = Vector3.Cross(vessel.srf_velocity, planetUp).normalized;
            srfRadial = Vector3.Cross(vessel.srf_velocity, srfNormal).normalized;

            this.vessel.SetRotation(Quaternion.FromToRotation(vessel.GetTransform().up, srfRadial) * vessel.transform.rotation, true);
        }

        [KSPEvent(guiName = "Maneuver", active = true, guiActive = true)]
        public void man()
        {
            Vector3d maneuver = vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(vessel.orbit);

            this.vessel.SetRotation(Quaternion.FromToRotation(vessel.GetTransform().up, maneuver) * vessel.transform.rotation, true);
        }

        [KSPEvent(guiName = "Target", active = true, guiActive = true)]
        public void targ()
        {
            Vector3d target = (vessel.targetObject.GetTransform().position - vessel.transform.position);

            this.vessel.SetRotation(Quaternion.FromToRotation(vessel.GetTransform().up, target) * vessel.transform.rotation, true);
        }

        [KSPEvent(guiName = "Target retro", active = true, guiActive = true)]
        public void targPro()
        {
            Vector3d targetpro = vessel.targetObject.GetObtVelocity() - vessel.obt_velocity;

            this.vessel.SetRotation(Quaternion.FromToRotation(vessel.GetTransform().up, targetpro) * vessel.transform.rotation, true);
        }

        

        // Note : things were working in Update()
        public void FixedUpdate()
        {
            if (!(HighLogic.LoadedSceneIsFlight && FlightGlobals.ready)) 
            { 
                return;
            }

            // DEBUG
            rotx = this.vessel.GetTransform().rotation.x;
            roty = this.vessel.GetTransform().rotation.y;
            rotz = this.vessel.GetTransform().rotation.z;
            currentAngularVelocity = this.vessel.angularVelocity.magnitude;
            savedAngularVelocity = this.vessel.vesselModules.OfType<VesselModuleRotation>().First().angularVelocity.magnitude;
            targetKeeping = this.vessel.vesselModules.OfType<VesselModuleRotation>().First().autopilotTargetHold;
            targetString = "X" + vessel.Autopilot.SAS.targetOrientation.x.ToString("0.00") + " Y" + vessel.Autopilot.SAS.targetOrientation.y.ToString("0.00") + " Z" + vessel.Autopilot.SAS.targetOrientation.z.ToString("0.00");
            headingString = "X" + vessel.transform.up.x.ToString("0.00") + " Y" + vessel.transform.up.y.ToString("0.00") + " Z" + vessel.transform.up.z.ToString("0.00");
       

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
