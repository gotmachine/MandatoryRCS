using MandatoryRCS.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MandatoryRCS
{
    // Holds a reference to the active vessel MandatoryRCSVesselModule
    // so we don't have to search for it billions of times

    // THIS DOESN'T WORK : ALL VESSELS ARE USING THE ACTIVEVESSEL VESSELMODULE !!!!!!!
    //public static class VM
    //{
    //    public static VesselModuleMandatoryRCS active;
    //}

    //[KSPAddon(KSPAddon.Startup.Flight, false)]
    //public class MonoBehaviourFlight : MonoBehaviour
    //{
    //    private void FixedUpdate()
    //    {
    //        // Update the static reference to the active vessel VesselModule
    //        UpdateActiveVesselModule();
    //    }

    //    private void UpdateActiveVesselModule()
    //    {
    //        if (FlightGlobals.ActiveVessel == null)
    //        {
    //            VM.active = null;
    //            return;
    //        }
    //        IEnumerable<VesselModuleMandatoryRCS> vesselModules = FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>();
    //        if (vesselModules.Count() > 0)
    //        {
    //            VM.active = vesselModules.First();
    //        }
    //        else
    //        {
    //            VM.active = null;
    //        }
    //    }
    //}

    // This is the main class, implemented as a KSP VesselModule
    // It contains all the common information used by the various components
    // Components are registered and called individually so we can control in wich order things are happening
    public class VesselModuleMandatoryRCS : VesselModule
    {


        // Used by the persistent rotation component
        public bool isInPhysics;
        public bool isInPhysicsFirstFrame;

        #region Vessel info
        // Vessel Angular Momentum
        [KSPField(isPersistant = true)]
        public Vector3 angularMomentum;

        public Vector3d torqueAvailable;
        public Vector3d inertia;
        public Vector3d torqueReactionSpeed;
        #endregion

        #region SAS
        // True if we are keeping the vessel oriented roward the SAS target


        [KSPField(isPersistant = true)]
        public bool SASisEnabled = false;

        [KSPField(isPersistant = true)]
        public bool SASModeLock = false;

        [KSPField(isPersistant = true)]
        public SASUI.SASFunction SASMode = SASUI.SASFunction.KillRot;

        [KSPField(isPersistant = true)]
        public FlightGlobals.SpeedDisplayModes SASContext = FlightGlobals.SpeedDisplayModes.Orbit;

        [KSPField(isPersistant = true)]
        public bool lockedRollMode = false;

        [KSPField(isPersistant = true)]
        public bool pitchOffsetMode = false;

        [KSPField(isPersistant = true)]
        public int currentRoll = 0;

        [KSPField(isPersistant = true)]
        public int pitchOffset = 0;

        public Quaternion attitudeWanted;
        public Vector3 directionWanted;
        #endregion

        // Components :


        //public ComponentRWtweaks rw;

        // SAS is first
        public ComponentCustomSAS sas;

        public ComponentPersistantRotation pr;




        protected override void OnStart()
        {
            pr = new ComponentPersistantRotation();
            sas = new ComponentCustomSAS();

            pr.vessel = Vessel;
            sas.vessel = Vessel;

            pr.vesselModule = this;
            sas.vesselModule = this;

            pr.OnStart();
            sas.OnStart();
            //rw.vessel = Vessel;
        }

        private void FixedUpdate()
        {
            // Vessel information
            UpdateVesselTorque();
            UpdatePhysics();

            sas.FixedUpdate();
            pr.FixedUpdate();

        }

        private void UpdatePhysics()
        {
            // Vessel is loaded but not in physics, either because 
            // - It is in the physics bubble but in non-psysics timewarp
            // - It has gone outside of the physics bubble
            // - It was just loaded, is in the physics bubble and will be unpacked in a few frames
            if (Vessel.loaded && Vessel.packed)
            {
                isInPhysics = isInPhysicsFirstFrame = false;
            }

            // Vessel is fully loaded and in physics
            else if (Vessel.loaded && !Vessel.packed && FlightGlobals.ready)
            {
                // We are in physics
                if (!isInPhysics)
                {
                    isInPhysics = isInPhysicsFirstFrame = true;
                }
                // Not for the first time, we can now save the potentially updated angular velocity
                else
                {
                    isInPhysicsFirstFrame = false;
                    angularMomentum = Vessel.angularVelocity;
                }
            }
        }

        private void UpdateVesselTorque()
        {
            Vector6 torqueReactionWheel = new Vector6();
            Vector6 rcsTorqueAvailable = new Vector6();
            Vector6 torqueControlSurface = new Vector6();
            Vector6 torqueGimbal = new Vector6();
            Vector6 torqueOthers = new Vector6();
            Vector6 torqueReactionSpeed6 = new Vector6();

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part p = vessel.parts[i];

                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];
                    if (!pm.isEnabled)
                    {
                        continue;
                    }

                    ModuleReactionWheel rw = pm as ModuleReactionWheel;
                    if (rw != null)
                    {
                        Vector3 pos;
                        Vector3 neg;
                        rw.GetPotentialTorque(out pos, out neg); // GetPotentialTorque reports the same value for pos & neg on ModuleReactionWheel
                        torqueReactionWheel.Add(pos);
                        torqueReactionWheel.Add(-neg);
                    }
                    else if (pm is ModuleControlSurface) // also does ModuleAeroSurface
                    {
                        ModuleControlSurface cs = (pm as ModuleControlSurface);
                        Vector3 ctrlTorquePos;
                        Vector3 ctrlTorqueNeg;
                        cs.GetPotentialTorque(out ctrlTorquePos, out ctrlTorqueNeg);
                        torqueControlSurface.Add(ctrlTorquePos);
                        torqueControlSurface.Add(ctrlTorqueNeg);

                        torqueReactionSpeed6.Add(Mathf.Abs(cs.ctrlSurfaceRange) / cs.actuatorSpeed * Vector3d.Max(ctrlTorquePos.Abs(), ctrlTorqueNeg.Abs()));

                    }
                    else if (pm is ModuleGimbal)
                    {
                        ModuleGimbal g = (pm as ModuleGimbal);

                        if (g.engineMultsList == null)
                            g.CreateEngineList();
                        // GetPotentialTorque for ModuleGimbal fails with unity dev build but not with KSP.exe
                        // The try/catch should be removed for release
                        try
                        {
                            Vector3 pos;
                            Vector3 neg;
                            g.GetPotentialTorque(out pos, out neg);
                            // GetPotentialTorque reports the same value for pos & neg on ModuleGimbal
                            torqueGimbal.Add(pos);
                            torqueGimbal.Add(-neg);
                            if (g.useGimbalResponseSpeed)
                                torqueReactionSpeed6.Add((Mathf.Abs(g.gimbalRange) / g.gimbalResponseSpeed) * Vector3d.Max(pos.Abs(), neg.Abs()));
                        }
                        catch (Exception)
                        {
                            Debug.Log("Error : can't get potential torque from engine gimbal in " + p.partInfo.title);
                        }
                    }
                    else if (pm is ModuleRCS)
                    {
                        ModuleRCS rcs = (pm as ModuleRCS);
                        if (rcs == null)
                            continue;

                        if (!p.ShieldedFromAirstream && rcs.rcsEnabled && rcs.isEnabled && !rcs.isJustForShow)
                        {
                            Vector3 attitudeControl = new Vector3(rcs.enablePitch ? 1 : 0, rcs.enableRoll ? 1 : 0, rcs.enableYaw ? 1 : 0);

                            Vector3 translationControl = new Vector3(rcs.enableX ? 1 : 0f, rcs.enableZ ? 1 : 0, rcs.enableY ? 1 : 0);
                            for (int j = 0; j < rcs.thrusterTransforms.Count; j++)
                            {
                                Transform t = rcs.thrusterTransforms[j];
                                Vector3d thrusterPosition = t.position - vessel.CurrentCoM;
                                Vector3d thrustDirection = rcs.useZaxis ? -t.forward : -t.up;
                                float power = rcs.thrusterPower;
                                if (FlightInputHandler.fetch.precisionMode)
                                {
                                    if (rcs.useLever)
                                    {
                                        float lever = rcs.GetLeverDistance(t, thrustDirection, vessel.CurrentCoM);
                                        if (lever > 1)
                                        {
                                            power = power / lever;
                                        }
                                    }
                                    else
                                    {
                                        power *= rcs.precisionFactor;
                                    }
                                }
                                Vector3d thrusterThrust = thrustDirection * power;
                                Vector3d thrusterTorque = Vector3.Cross(thrusterPosition, thrusterThrust);
                                rcsTorqueAvailable.Add(Vector3.Scale(vessel.GetTransform().InverseTransformDirection(thrusterTorque), attitudeControl));
                            }
                        }
                    }
                    else if (pm is ITorqueProvider) // All mod that supports it. Including FAR
                    {
                        ITorqueProvider tp = pm as ITorqueProvider;
                        Vector3 pos;
                        Vector3 neg;
                        tp.GetPotentialTorque(out pos, out neg);
                        torqueOthers.Add(pos);
                        torqueOthers.Add(neg);
                    }

                }
            }

            Vector3d torqueAvailable = Vector3d.zero;
            torqueAvailable += Vector3d.Max(torqueReactionWheel.positive, torqueReactionWheel.negative);
            torqueAvailable += Vector3d.Max(rcsTorqueAvailable.positive, rcsTorqueAvailable.negative);
            torqueAvailable += Vector3d.Max(torqueControlSurface.positive, torqueControlSurface.negative);
            torqueAvailable += Vector3d.Max(torqueGimbal.positive, torqueGimbal.negative);
            torqueAvailable += Vector3d.Max(torqueOthers.positive, torqueOthers.negative);

            this.torqueAvailable = torqueAvailable;

            if (torqueAvailable.sqrMagnitude > 0)
            {
                torqueReactionSpeed = Vector3d.Max(torqueReactionSpeed6.positive, torqueReactionSpeed6.negative);
                torqueReactionSpeed.Scale(torqueAvailable.InvertNoNaN());
            }
            else
            {
                torqueReactionSpeed = Vector3d.zero;
            }

            Vector3d angularMomentum = Vector3d.zero;
            angularMomentum.x = (float)(vessel.MOI.x * vessel.angularVelocity.x);
            angularMomentum.y = (float)(vessel.MOI.y * vessel.angularVelocity.y);
            angularMomentum.z = (float)(vessel.MOI.z * vessel.angularVelocity.z);


            inertia = Vector3d.Scale(
                angularMomentum.Sign(),
                Vector3d.Scale(
                    Vector3d.Scale(angularMomentum, angularMomentum),
                    Vector3d.Scale(torqueAvailable, vessel.MOI).InvertNoNaN()
                    )
                );
        }
    }
}
