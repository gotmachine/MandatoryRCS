using MandatoryRCS.Lib;
using MandatoryRCS.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static MandatoryRCS.ComponentSASAttitude;

namespace MandatoryRCS
{
    // This is the main class, implemented as a KSP VesselModule
    // It contains all the common information used by the various components
    // Components are registered and called individually so we can control in wich order things are happening
    public class VesselModuleMandatoryRCS : VesselModule
    {
        #region Vessel state
        public enum VesselState
        {
            InPhysics,
            Packed,
            Unloaded
        }
        public VesselState currentState;
        public bool isInPhysicsFirstFrame;
        public bool playerControlled;
        public bool pilotIsIdle;
        public FlightCtrlState flightCtrlState;
        public ITargetable currentTarget;
        #endregion

        #region Vessel physics info
        // Vessel Angular Momentum
        [KSPField(isPersistant = true)]
        public Vector3d angularVelocity;
        public Vector3d MOI;
        public Vector3d torqueAvailable;
        public Vector3d angularDistanceToStop; // inertia in MechJeb VesselState
        public Vector3d torqueReactionSpeed;
        #endregion

        #region SAS
        // True if we are keeping the vessel oriented toward the SAS target
        [KSPField(isPersistant = true)]
        public bool autopilotEnabled = false;

        [KSPField(isPersistant = true)]
        public bool autopilotPersistentModeLock = false;

        [KSPField(isPersistant = true)]
        public SASMode autopilotMode = SASMode.KillRot;

        [KSPField(isPersistant = true)]
        public FlightGlobals.SpeedDisplayModes autopilotContext = FlightGlobals.SpeedDisplayModes.Orbit;

        [KSPField(isPersistant = true)]
        public bool lockedRollMode = false;
        [KSPField(isPersistant = true)]
        public int currentRoll = 0;

        [KSPField(isPersistant = true)]
        public bool rcsAutoMode = false;
        [KSPField(isPersistant = true)]
        public int velocityLimiter = 15;
        [KSPField(isPersistant = true)]
        public bool sunIsTarget = false;

        public Quaternion autopilotAttitudeWanted;
        public Vector3d autopilotDirectionWanted;
        public bool flyByWire = false;
        public bool autopilotModeHasChanged = false;
        public bool hasManeuverNode = false;

        #endregion

        public bool rwLockedOnDirection;

        public bool ready = false;
        public int frameNumber = 0;

        // Components :


        //public ComponentRWtweaks rw;

        // SAS is first
        public ComponentSASAutopilot sasAutopilot;
        public ComponentSASAttitude sasAttitude;
        public ComponentPersistantRotation persistentRotation;
        public ComponentRWTorqueControl torqueControl;

        public List<ComponentBase> components = new List<ComponentBase>();

        protected override void OnStart()
        {
            persistentRotation = new ComponentPersistantRotation();
            sasAutopilot = new ComponentSASAutopilot();
            sasAttitude = new ComponentSASAttitude();
            torqueControl = new ComponentRWTorqueControl();

            components.Add(persistentRotation);
            components.Add(sasAutopilot);
            components.Add(sasAttitude);
            components.Add(torqueControl);

            foreach (ComponentBase component in components)
            {
                component.vessel = Vessel;
                component.vesselModule = this;
                component.Start();
            }

            // Note : OnPreAutopilotUpdate seems to be called AFTER the vesselModule FixedUpdate();
            vessel.OnPreAutopilotUpdate -= LoadedUpdate;
            vessel.OnPreAutopilotUpdate += LoadedUpdate;

            GameEvents.onVesselLoaded.Add(onVesselLoaded);
                //new EventData<Vessel, Vessel>("onVesselSwitchingToUnloaded")
        }

        private void OnDestroy()
        {
            //vessel.OnPreAutopilotUpdate -= LoadedUpdate; // This cause a nullref at startup !?!
        }

        public override void OnUnloadVessel()
        {
            

        }

        private void onVesselLoaded(Vessel vessel)
        {
            // On load, stock throw an exception if the Sun is the target, so we reset it before unloading
            if (vessel.protoVessel.targetInfo.uniqueName == "Sun")
            {
                vessel.protoVessel.targetInfo.uniqueName = "";
                vessel.protoVessel.targetInfo.targetType = ProtoTargetInfo.Type.Null;
            }
        }

        private void LoadedUpdate(FlightCtrlState s = null)
        {
            // Save FlightCtrlState
            flightCtrlState = s;

            // Update physics and control state
            UpdateVesselState();

            // Update angular velocity for in physics vessels
            if (currentState == VesselState.InPhysics && !isInPhysicsFirstFrame)
            {
                angularVelocity = Vessel.angularVelocityD;
            }

            // Update the SAS state and get the requested attitude
            if (autopilotModeHasChanged) { autopilotPersistentModeLock = false; }
            sasAttitude.ComponentUpdate();
            // Then rotate the vessel according to the angular velocity or the SAS request
            persistentRotation.ComponentUpdate();

            // Update autopilot things
            if (currentState == VesselState.InPhysics)
            {
                // Is the player currently steering this vessel ?
                // TODO : if flybywire mode is enabled, we have oveeriden the flightcontrolstate piutch and yaw value in ComponentSASAttitude
                // We need to set pilotIsIdle in SASattitude probably
                pilotIsIdle = s != null && s.isIdle;
                // Now that we know what the pilot is doing, adjust the reaction wheels torque
                torqueControl.ComponentUpdate();

                if (autopilotEnabled && s != null)
                {
                    // Update MoI first, it isn't dependent on anything else
                    UpdateMoI();
                    // We know how reaction wheels will behave, set RCS auto mode
                    // TODO : check if we need to reset the SAS PID on RCS enable/disable-> sasAutopilot.Reset()
                    RCSAutoSet();
                    // Finally calculate the available torque
                    UpdateTorque();
                    // It's time to calculate how the autopilot should steer the vessel;
                    if (autopilotModeHasChanged) { sasAutopilot.Reset(); }
                    sasAutopilot.ComponentUpdate();
                }
            }
            // Reset the SAS mode change flag
            if (autopilotModeHasChanged) { autopilotModeHasChanged = false; }

            // Ensure we have done all this at last once
            // Used by the navball UI, which should not be initialized before everything has been checked.
            ready = true;
        }

        private void FixedUpdate()
        {
            if (!vessel.loaded)
            {
                return;
            }

            
            frameNumber += 1;

            // Note : don't check for FlightGlobals.ready here
            if (FlightGlobals.ActiveVessel == vessel && !vessel.packed) return;


            //frameNumber += 1;
            //// Do the update on all vessels except the one that is currently active (if any), if it is in physics.
            //// In this specific situation, this update is handled in the OnPreAutopilotUpdate callback
            //if (vessel.loaded // It is loaded
            //    && !vessel.packed // It is in physics
            //    && FlightGlobals.ready // Physics are ready
            //    && FlightGlobals.ActiveVessel != null // there is an active vessel
            //    && FlightGlobals.ActiveVessel == vessel) // and it is the current one
            //{
            //    return;
            //}

            LoadedUpdate();
        }

        private void RCSAutoSet()
        {
            if (rcsAutoMode)
            {
                bool enabled;
                enabled = !rwLockedOnDirection && vessel.staticPressurekPa < 10;
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, enabled);
            }
        }

        private void UpdateVesselState()
        {
            // Situations in which the vessel can be loaded but not in physics : 
            // - It is in the physics bubble but in non-psysics timewarp
            // - It has just gone outside of the physics bubble
            // - It was just loaded, is in the physics bubble and will be unpacked in a few frames

            if (!Vessel.loaded)
            {
                currentState = VesselState.Unloaded;
            }
            else if (Vessel.packed)
            {
                currentState = VesselState.Packed;
            }
            else if (FlightGlobals.ready)
            {
                if (!isInPhysicsFirstFrame && (currentState == VesselState.Packed || currentState == VesselState.Unloaded))
                {
                    isInPhysicsFirstFrame = true;
                }
                else
                {
                    isInPhysicsFirstFrame = false;
                }
                currentState = VesselState.InPhysics;
            }

            if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel == vessel)
            {
                playerControlled = true;
            }
            else
            {
                playerControlled = false;
            }
        }

        private void UpdateTorque()
        {
            Vector6 torqueReactionWheel = new Vector6();
            Vector6 rcsTorqueAvailable = new Vector6();
            Vector6 torqueControlSurface = new Vector6();
            Vector6 torqueGimbal = new Vector6();
            Vector6 torqueOthers = new Vector6();
            //Vector6 torqueReactionSpeed6 = new Vector6();
            Vector6 reactionSpeedControlSurface6 = new Vector6();
            Vector6 reactionSpeedGimbal6 = new Vector6();

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

                        //torqueReactionSpeed6.Add(Mathf.Abs(cs.ctrlSurfaceRange) / cs.actuatorSpeed * Vector3d.Max(ctrlTorquePos.Abs(), ctrlTorqueNeg.Abs()));
                        reactionSpeedControlSurface6.Add(Mathf.Abs(cs.ctrlSurfaceRange) / cs.actuatorSpeed * Vector3.Max(ctrlTorquePos.Abs(), ctrlTorqueNeg.Abs()));

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
                                reactionSpeedGimbal6.Add((Mathf.Abs(g.gimbalRange) / g.gimbalResponseSpeed) * Vector3d.Max(pos.Abs(), neg.Abs()));
                        }
                        catch (Exception)
                        {
                            Debug.Log("Error : can't get potential torque from engine gimbal in " + p.partInfo.title);
                        }
                    }
                    else if (pm is ModuleRCS)
                    {
                        if (!vessel.ActionGroups[KSPActionGroup.RCS])
                            continue;

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

            // Torque and Reaction speed for control surfaces
            Vector3d torqueAvailableControlSurface = Vector3d.Max(torqueControlSurface.positive, torqueControlSurface.negative);
            Vector3d reactionSpeedControlSurface = Vector3d.Max(reactionSpeedControlSurface6.positive, reactionSpeedControlSurface6.negative);
            reactionSpeedControlSurface.Scale(torqueAvailableControlSurface.InvertNoNaN());

            // Torque and Reaction speed for gimbal
            Vector3d torqueAvailableGimbal = Vector3d.Max(torqueGimbal.positive, torqueGimbal.negative);
            Vector3d reactionSpeedGimbal = Vector3d.Max(reactionSpeedGimbal6.positive, reactionSpeedGimbal6.negative);
            reactionSpeedGimbal.Scale(torqueAvailableGimbal.InvertNoNaN());

            // Set torque
            torqueAvailable = Vector3d.zero;
            torqueAvailable += Vector3d.Max(torqueReactionWheel.positive, torqueReactionWheel.negative);
            torqueAvailable += Vector3d.Max(rcsTorqueAvailable.positive, rcsTorqueAvailable.negative);
            torqueAvailable += Vector3d.Max(torqueOthers.positive, torqueOthers.negative);
            torqueAvailable += torqueAvailableControlSurface;
            torqueAvailable += torqueAvailableGimbal;

            // Set reaction speed
            torqueReactionSpeed = Vector3d.zero;
            torqueReactionSpeed += reactionSpeedControlSurface;
            torqueReactionSpeed += reactionSpeedGimbal;

            // original MechJeb ReactionSpeed code
            //if (torqueAvailable.sqrMagnitude > 0)
            //{
            //    torqueReactionSpeed = Vector3d.Max(torqueReactionSpeed6.positive, torqueReactionSpeed6.negative);
            //    torqueReactionSpeed.Scale(torqueAvailable.InvertNoNaN());
            //}
            //else
            //{
            //    torqueReactionSpeed = Vector3d.zero;
            //}

            Vector3d angularMomentum = Vector3d.zero;
            angularMomentum.x = (float)(MOI.x * vessel.angularVelocity.x);
            angularMomentum.y = (float)(MOI.y * vessel.angularVelocity.y);
            angularMomentum.z = (float)(MOI.z * vessel.angularVelocity.z);

            // Inertia in MechJeb code
            angularDistanceToStop = 0.5 * Vector3d.Scale(
                angularMomentum.Sign(),
                Vector3d.Scale(
                    Vector3d.Scale(angularMomentum, angularMomentum),
                    Vector3d.Scale(torqueAvailable, MOI).InvertNoNaN()
                    )
                );
        }

        private void UpdateMoI()
        {
            /// <summary>
            /// This is a replacement for the stock API Property "vessel.MOI", which seems buggy when used
            /// with "control from here" on parts other than the default control part.
            /// <br/>
            /// Right now the stock Moment of Inertia Property returns values in inconsistent reference frames that
            /// don't make sense when used with "control from here".  (It doesn't merely rotate the reference frame, as one
            /// would expect "control from here" to do.)
            /// </summary>   
            /// TODO: Check this again after each KSP stock release to see if it's been changed or not.

            // Found that the default rotation has top pointing forward, forward pointing down, and right pointing starboard.
            // This fixes that rotation.
            Transform vesselTransform = vessel.ReferenceTransform;
            Quaternion vesselRotation = vesselTransform.rotation * Quaternion.Euler(-90, 0, 0);
            Vector3d centerOfMass = vessel.CoMD;

            var tensor = Matrix4x4.zero;
            Matrix4x4 partTensor = Matrix4x4.identity;
            Matrix4x4 inertiaMatrix = Matrix4x4.identity;
            Matrix4x4 productMatrix = Matrix4x4.identity;
            foreach (var part in Vessel.Parts)
            {
                if (part.rb != null)
                {
                    KSPUtil.ToDiagonalMatrix2(part.rb.inertiaTensor, ref partTensor);

                    Quaternion rot = Quaternion.Inverse(vesselRotation) * part.transform.rotation * part.rb.inertiaTensorRotation;
                    Quaternion inv = Quaternion.Inverse(rot);

                    Matrix4x4 rotMatrix = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one);
                    Matrix4x4 invMatrix = Matrix4x4.TRS(Vector3.zero, inv, Vector3.one);

                    // add the part inertiaTensor to the ship inertiaTensor
                    KSPUtil.Add(ref tensor, rotMatrix * partTensor * invMatrix);

                    Vector3 position = vesselTransform.InverseTransformDirection(part.rb.position - centerOfMass);

                    // add the part mass to the ship inertiaTensor
                    KSPUtil.ToDiagonalMatrix2(part.rb.mass * position.sqrMagnitude, ref inertiaMatrix);
                    KSPUtil.Add(ref tensor, inertiaMatrix);

                    // add the part distance offset to the ship inertiaTensor
                    OuterProduct2(position, -part.rb.mass * position, ref productMatrix);
                    KSPUtil.Add(ref tensor, productMatrix);
                }
            }
            MOI = KSPUtil.Diag(tensor);
        }

        public static void OuterProduct2(Vector3 left, Vector3 right, ref Matrix4x4 m)
        {
            m.m00 = left.x * right.x;
            m.m01 = left.x * right.y;
            m.m02 = left.x * right.z;
            m.m10 = left.y * right.x;
            m.m11 = left.y * right.y;
            m.m12 = left.y * right.z;
            m.m20 = left.z * right.x;
            m.m21 = left.z * right.y;
            m.m22 = left.z * right.z;
        }
    }
}
