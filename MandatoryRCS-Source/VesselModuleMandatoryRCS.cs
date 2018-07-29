/* 
 * This file and all code it contains is released in the public domain
 */

using MandatoryRCS.Lib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static FlightGlobals;
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
            PhysicsNotReady,
            PhysicsVelocityFrame,
            PhysicsReady,
            PackedLoadingUnloadedNotReady,
            PackedLoadingFirstFrameReady,
            PackedReady,
            Unloaded
        }
        public VesselState currentState;

        public bool pilotRotationInput;
        public bool pilotTranslationInput;
        public FlightCtrlState flightCtrlState;


        public bool PlayerControlled()
        {
            return (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel == vessel);
        }
        #endregion

        #region Vessel physics info
        // Angular velocity is saved
        [KSPField(isPersistant = true)]
        public Vector3 angularVelocity;
        public Vector3d MOI;
        public Vector3d torqueAvailable;
        public Vector3d angularDistanceToStop; // inertia in MechJeb VesselState
        public Vector3d torqueReactionSpeed;
        #endregion

        #region SAS
        // True if we are keeping the vessel oriented toward the SAS target
        [KSPField(isPersistant = true)]
        public bool sasEnabled = false;

        [KSPField(isPersistant = true)]
        public bool sasPersistentModeLock = false;

        [KSPField(isPersistant = true)]
        public SASMode sasMode = SASMode.KillRot;

        [KSPField(isPersistant = true)]
        public FlightGlobals.SpeedDisplayModes sasContext = FlightGlobals.SpeedDisplayModes.Orbit;

        [KSPField(isPersistant = true)]
        public bool sasLockedRollMode = false;

        [KSPField(isPersistant = true)]
        public int sasRollOffset = 0;

        [KSPField(isPersistant = true)]
        public bool sasRcsAutoMode = false;

        [KSPField(isPersistant = true)]
        public int sasVelocityLimiter = 15;

        public Quaternion sasAttitudeWanted;
        public Vector3d sasDirectionWanted;
        public Vector3d sasRollReference;
        public bool sasRollRefDefined = true;
        public bool hasVelocity = false;
        public bool sasFlyByWire = false;
        public bool sasModeHasChanged = false;
        public bool hasSAS = false;

        private VesselAutopilot.VesselSAS stockSAS;
        private bool useStockSAS = false;

        #endregion
        // TODO : check that we reset the direction lock when reset the SAS mode from the target handling code
        public bool rwLockedOnDirection;
        public bool ready = false;
        public int loadingFrameNumber = 0;
        private bool callbackFirstFrameSkipped = false;

        // Target info
        [KSPField(isPersistant = true)]
        public ProtoTargetInfo targetInfo = new ProtoTargetInfo();
        public ITargetable currentTarget;
        public bool targetNeedPhysics;
        public bool vesselTargetDirty;
        public bool vesselTargetDirtyFirstFrame;
        public int targetDirtyFrameCounter;

        public ComponentSASAutopilot sasAutopilot;
        public ComponentSASAttitude sasAttitude;
        public ComponentPersistantRotation persistentRotation;
        public ComponentRWTorqueControl torqueControl;

        public List<ComponentBase> components = new List<ComponentBase>();

        protected override void OnStart()
        {
            // For EVA kerbals, debris and flags, we only do persistent rotation, no need to add the other components
            if (vessel.vesselType == VesselType.Debris || vessel.vesselType == VesselType.Flag || vessel.vesselType == VesselType.EVA)
            {
                persistentRotation = new ComponentPersistantRotation();
                components.Add(persistentRotation);
            }
            else
            {
                persistentRotation = new ComponentPersistantRotation();
                sasAutopilot = new ComponentSASAutopilot();
                sasAttitude = new ComponentSASAttitude();
                torqueControl = new ComponentRWTorqueControl();

                components.Add(persistentRotation);
                components.Add(sasAutopilot);
                components.Add(sasAttitude);
                components.Add(torqueControl);
            }

            stockSAS = new VesselAutopilot.VesselSAS(vessel);

            foreach (ComponentBase component in components)
            {
                component.vessel = Vessel;
                component.vesselModule = this;
                component.Start();
            }

            // Note : OnPreAutopilotUpdate seems to be called AFTER the vesselModule FixedUpdate();
            vessel.OnPreAutopilotUpdate -= OnPreAutopilotUpdate;
            vessel.OnPreAutopilotUpdate += OnPreAutopilotUpdate;
            currentState = VesselState.Unloaded;
            callbackFirstFrameSkipped = false;
        }

        private void OnPreAutopilotUpdate(FlightCtrlState s)
        {
            // Excepted for EVA kerbals, disable stock SAS, we can't use it because the vessel.Autopilot will fight us by giving directions and resetting PID
            if (!vessel.isEVA)
            {
                vessel.Autopilot.SAS.DisconnectFlyByWire();
            }

            VesselModuleUpdate(true, s);

            // Reimplementation of the stock SAS in case the player want to use it instead of MechJeb SAS
            if (useStockSAS)
            {
                stockSAS.ConnectFlyByWire(false);
            }
            else
            {
                stockSAS.DisconnectFlyByWire();
            }
        }

        private void VesselModuleUpdate(bool fromCallback, FlightCtrlState ctrlState = null)
        {
            // In the frame transitionning from FixedUpdate to the OnPreAutopilotUpdate callback and vice-versa,
            // VesselModuleUpdate will be called twice : first by FixedUpdate, then by the callback.
            // We need to abort this second call
            if (!callbackFirstFrameSkipped && fromCallback)
            {
                callbackFirstFrameSkipped = true;
                return;
            }
            if (callbackFirstFrameSkipped && !fromCallback)
            {
                callbackFirstFrameSkipped = false;
            }

            // Update technical state (loaded, packed, etc) and some other global info
            UpdateVesselState();

            // Do nothing more for if vessel is unloaded or if the flightIntegrator is currently loading the vessel
            if (currentState == VesselState.Unloaded || currentState == VesselState.PackedLoadingUnloadedNotReady) return;

            // Get FlightCtrlState
            flightCtrlState = ctrlState;

            // Update angular velocity for in physics vessels
            // NOTE: for our purpose Vessel.angularVelocityD is not necessary
            if (currentState == VesselState.PhysicsReady)
            {
                angularVelocity = Vessel.angularVelocity;
            }

            // Update the SAS state according to the various rules that may alter it
            // and get the requested direction and attitude
            //if (autopilotModeHasChanged || !hasSAS) autopilotPersistentModeLock = false;
            if (hasSAS) sasAttitude.ComponentFixedUpdate();

            // Then rotate the vessel according to the angular velocity and the SAS state
            persistentRotation.ComponentFixedUpdate();

            // Update autopilot things
            if (hasSAS && currentState == VesselState.PhysicsReady)
            {
                // adjust the reaction wheels torque
                torqueControl.ComponentFixedUpdate();

                // Update the autopilot action
                if (sasEnabled && flightCtrlState != null)
                {
                    // We know how reaction wheels will behave, set RCS auto mode
                    // TODO : check if we need to reset the SAS PID on RCS enable/disable-> sasAutopilot.Reset()
                    RCSAutoSet();

                    if (useStockSAS)
                    {
                        stockSAS.lockedMode = sasMode == SASMode.KillRot;
                        stockSAS.SetTargetOrientation(sasDirectionWanted, sasModeHasChanged);
                    }
                    else
                    {
                        // Get MoI first, it isn't dependent on anything else
                        VesselPhysics.GetVesselMoI(vessel, out MOI);

                        // Get the available torque
                        VesselPhysics.GetVesselAvailableTorque(vessel, MOI, out torqueAvailable, out torqueReactionSpeed, out angularDistanceToStop);

                        // It's time to calculate how the autopilot should steer the vessel;
                        if (sasModeHasChanged) { sasAutopilot.Reset(); }
                        sasAutopilot.ComponentFixedUpdate();
                    }
                }
            }
            // Reset the SAS mode change flag
            if (sasModeHasChanged) { sasModeHasChanged = false; }


            
        }

        private void Update()
        {
            if (currentState == VesselState.Unloaded || currentState == VesselState.PackedLoadingUnloadedNotReady) return;
            if (sasModeHasChanged || !hasSAS) sasPersistentModeLock = false;
            if (hasSAS) sasAttitude.ComponentUpdate();

            // Ensure we have done all this at last once
            // Used by the navball UI, which should not be initialized before everything has been checked.
            ready = true;
            persistentRotation.ComponentUpdate();

        }

        private void FixedUpdate()
        {
            //// TODO : check again that LoadedUpdate() isn't called twice in the same frame at scene start or vessel switch
            if (vessel.loaded)
            {
                loadingFrameNumber += 1;
            }
            else
            {
                loadingFrameNumber = 0;
            }

            // Do the update on all vessels except the one that is currently active (if any), if it is in physics.
            // In this specific situation, the update is called by the OnPreAutopilotUpdate callback
            // Note : don't check for FlightGlobals.ready here, value may be inconsistent
            if (FlightGlobals.ActiveVessel == vessel && !vessel.packed) return;
            VesselModuleUpdate(false, vessel.ctrlState);
        }

        private void RCSAutoSet()
        {
            if (sasRcsAutoMode)
            {
                bool enabled;
                enabled = (!rwLockedOnDirection || pilotTranslationInput) && vessel.staticPressurekPa < 10;
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
                if (currentState != VesselState.Unloaded )
                {
                    currentState = VesselState.Unloaded;
                }
            }
            else if (Vessel.packed)
            {
                // OnVesselGoOnRails can lead to here in two possible states :
                // - PackedLoadingFirstFrame if we are coming from an unloaded vessel -> doesn't seem to happen
                // - PackedReady if we are coming from a in physics vessel

                // If we were unloaded, mark the vessel as PackedLoadingFromUnloaded
                if (currentState == VesselState.Unloaded)
                {
                    currentState = VesselState.PackedLoadingUnloadedNotReady;
                    vesselTargetDirty = true;
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] State change : Unloaded -> PackedLoadingUnloadedNotReady, target is dirty");
                }

                // If we were unloaded, check if we are in the flightIntegrator frame (usually only the first packed frame), then if not go to PackedLoadingFirstFrame
                // A PackedLoadingFirstFrame vessel will have its attitude checked and its rotation applied according to the SAS state
                if (currentState == VesselState.PackedLoadingUnloadedNotReady && !vessel.IsFirstFrame())
                {
                    currentState = VesselState.PackedLoadingFirstFrameReady;
                }
                // First frame is done, we switch to PackedReady
                else if (currentState == VesselState.PackedLoadingFirstFrameReady)
                {
                    currentState = VesselState.PackedReady;
                }

                // If we loaded a scene that have multiple vessels, only the active one needs to stay in the PackedLoading state
                // A PackedReady vessel will have its attitude checked and its persistent rotation applied 
                //if (currentState == VesselState.PackedLoading && FlightGlobals.ready && vessel != FlightGlobals.ActiveVessel)
                //{
                //    currentState = VesselState.PackedReady;
                //}
            }
            else
            {
                // OnVesselGoOnRails can lead to here in two possible states :
                // - PhysicsLoading if we are coming from an unloaded vessel
                // - PhysicsReady if we are coming from a vessel that was already in the scene
                if (currentState == VesselState.PhysicsNotReady && !FlightGlobals.ready)
                {
                    currentState = VesselState.PhysicsNotReady;
                }
                else if (currentState != VesselState.PhysicsReady && currentState != VesselState.PhysicsVelocityFrame)
                {
                    currentState = VesselState.PhysicsVelocityFrame;
                    vesselTargetDirtyFirstFrame = true;
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] Setting state to PhysicsVelocityFrame");
                }
                else
                {
                    currentState = VesselState.PhysicsReady;
                }
            }

            // Update nothing more for unloaded vessels

            if (currentState == VesselState.PackedLoadingFirstFrameReady || (currentState == VesselState.PhysicsVelocityFrame && targetNeedPhysics))
            {
                LoadTarget();
            }
            
            if (vesselTargetDirty && PlayerControlled() && (currentState == VesselState.PhysicsVelocityFrame || currentState == VesselState.PhysicsReady))
            {
                if (PlayerControlled())
                {
                    if (currentTarget != FlightGlobals.fetch.VesselTarget || sasContext != FlightGlobals.speedDisplayMode)
                    {
                        if (vesselTargetDirtyFirstFrame)
                        {
                            vesselTargetDirtyFirstFrame = false;
                            // Stock is using a coroutine to restore the target, so it usually happen only 3-4 frames after physics loading.
                            // We will wait a bit for this to happen, and after a few frames we will consider that something has gone wrong
                            // and sync the stock target to ours.
                            targetDirtyFrameCounter = 0;
                            Debug.Log("[MRCS] [" + vessel.vesselName + "] Waiting for target to be resumed. Saved target : " + (currentTarget == null ? "null" : currentTarget.GetDisplayName()) + " Saved context : " + sasContext);
                            Debug.Log("[MRCS] [" + vessel.vesselName + "] Stock target : " + (FlightGlobals.fetch.VesselTarget == null ? "null" : FlightGlobals.fetch.VesselTarget.GetDisplayName()) + " Stock context : " + FlightGlobals.speedDisplayMode);

                            // Theorically we have prevented the stock code from saving the target if the Sun was selected
                            // We are forced to do so because the stock code can't handle it (some coroutine if failing if the sun is restored as target)
                            // So we have to set it manually, after loading
                            if ((Object)currentTarget == Sun.Instance.sun)
                            {
                                SetTarget(Sun.Instance.sun, true, false);
                                if (sasContext == SpeedDisplayModes.Target)
                                {
                                    SetContext(SpeedDisplayModes.Target, true, false);
                                }
                            }
                        }


                        if (targetDirtyFrameCounter > 10)
                        {
                            Debug.Log("[MRCS] [" + vessel.vesselName + "] WARNING, saved target : " + (currentTarget == null ? "null" : currentTarget.GetDisplayName()) + " Saved context : " + sasContext);
                            Debug.Log("[MRCS] [" + vessel.vesselName + "] WARNING, stock target : " + (FlightGlobals.fetch.VesselTarget == null ? "null" : FlightGlobals.fetch.VesselTarget.GetDisplayName()) + " Stock context : " + FlightGlobals.speedDisplayMode);
                            vesselTargetDirty = false;
                            targetDirtyFrameCounter = -1;
                            SetTarget(currentTarget, true, false);
                            if (vessel == FlightGlobals.ActiveVessel)
                            {
                                SetContext(FlightGlobals.speedDisplayMode, true, false);
                            }
                            //sasAttitude.ResetToKillRot();
                        }
                        else
                        {
                            targetDirtyFrameCounter += 1;
                            Debug.Log("[MRCS] [" + vessel.vesselName + "] Target dirty since " + targetDirtyFrameCounter + " frames");
                        } 
                    }
                    else
                    {
                        vesselTargetDirty = false;
                        targetDirtyFrameCounter = -1;
                    }
                }
            }

            // EVA Kerbals, debris & flag : do only persistent rotation
            if (vessel.vesselType == VesselType.Debris || vessel.vesselType == VesselType.Flag || vessel.vesselType == VesselType.EVA)
            {
                hasSAS = false;
            }
            else
            {
                hasSAS = true;
            }

            if (vessel.vesselName == "DOCK POD TEST 2")
            {
                Debug.Log("Node: " + vessel.protoVessel.vesselModules.GetNode("VesselModuleMandatoryRCS").CountNodes + " - " + currentState);
            }
        }

        public bool VesselHasManeuverNode()
        {
            return vessel.patchedConicSolver != null && vessel.patchedConicSolver.maneuverNodes.Count > 0;
        }

        public void SetSASMode(SASMode mode)
        {
            if (mode != sasMode)
            {
                sasModeHasChanged = true;
                sasPersistentModeLock = false;
                sasMode = mode;
                switch (sasMode)
                {
                    case SASMode.Hold:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                        break;
                    case SASMode.Maneuver:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Maneuver);
                        break;
                    case SASMode.KillRot:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                        break;
                    case SASMode.Target:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Target);
                        break;
                    case SASMode.AntiTarget:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.AntiTarget);
                        break;
                    case SASMode.Prograde:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Prograde);
                        break;
                    case SASMode.Retrograde:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Retrograde);
                        break;
                    case SASMode.Normal:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Normal);
                        break;
                    case SASMode.AntiNormal:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Antinormal);
                        break;
                    case SASMode.RadialIn:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.RadialIn);
                        break;
                    case SASMode.RadialOut:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.RadialOut);
                        break;
                    default:
                        Vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
                        break;
                }
            }
            else
            {
                if (sasMode == SASMode.FlyByWire)
                {
                    sasFlyByWire = false;
                    rwLockedOnDirection = false;
                }
            }
        }

        public void SetTarget(ITargetable target, bool setFlightGlobals, bool setVesselModule)
        {

            if (setFlightGlobals)
            {
                if (!PlayerControlled())
                {
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] WARNING : trying to set stock vessel target from a non player controlled vessel");
                }
                else
                {
                    // Note : if we are orbiting the sun, stock won't allow to set it as its target.
                    // We still register it and we handle this special case in the target watcher
                    FlightGlobals.fetch.SetVesselTarget(target, true);
                    
                }  
            }

            if (setVesselModule)
            {
                currentTarget = target;
                targetInfo = new ProtoTargetInfo(target);

                if (currentTarget == null && sasContext == SpeedDisplayModes.Target)
                {
                    SetContext(SpeedDisplayModes.Orbit, PlayerControlled(), true);
                }
            }


        }

        public void SetContext(SpeedDisplayModes mode, bool setFlightGlobals, bool setVesselModule)
        {
            if (setFlightGlobals)
            {
                if (!PlayerControlled())
                {
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] WARNING : trying to set FlightGlobals.SpeedMode from a non player controlled vessel");
                }
                else
                {
                    FlightGlobals.SetSpeedMode(mode);
                }
                
            }
            if (setVesselModule)
            {
                sasContext = mode;
            }
            
        }

        private void LoadTarget()
        {
            // In the first fixedUpdate frame, when the vessel was just loaded and is still packed, we need to know what the target is to restore our attitude toward it
            // Since parts and partmodules don't exist yet, if the target if of this type, we won't be able to restore it
            // We use the targetNeedPhysics flag to know if should call this method again when we are in physics.

            // This shoould already be set, but better safe than sorry
            vesselTargetDirty = true;
            targetDirtyFrameCounter = -1;

            // Load targetInfo from ConfigNode
            targetInfo.Load(vessel.protoVessel.vesselModules.GetNode("VesselModuleMandatoryRCS").GetNode("targetInfo"));

            // Try to get the target Object
            ITargetable savedTarget = targetInfo.FindTarget();

            // If target is a Part or PartModule, it can't be accessed here because we are not in physics yet.
            // But the FindTarget() method is smart and will return the vessel instead, so we temporally use it as fallback, and we will restore the real target latter
            if (targetInfo.targetType == ProtoTargetInfo.Type.Part || targetInfo.targetType == ProtoTargetInfo.Type.PartModule)
            {
                targetNeedPhysics = true;
                currentTarget = savedTarget;
                Debug.Log("[MRCS] [" + vessel.vesselName + "] PackedLoadingFirstFrame : Found target of type : " + targetInfo.targetType + ", it will be restored when PhysicsReady");
            }
            // Maybe stock or other mods implement ITargetable, so we will try to restore this target latter but we reset the SAS state because we don't know how to 
            // get that target orientation & position, and we need that for SAS persistence
            else if (savedTarget == null && targetInfo.targetType != ProtoTargetInfo.Type.Null)
            {
                targetNeedPhysics = true;
                currentTarget = null;
                Debug.Log("[MRCS] [" + vessel.vesselName + "] PackedLoadingFirstFrame : Found target of unknown type, it will be restored when PhysicsReady");
            }
            // The target must be a Vessel or CelestialBody
            else
            {
                targetNeedPhysics = false;
                currentTarget = savedTarget;
                Debug.Log("[MRCS] [" + vessel.vesselName + "] PackedLoadingFirstFrame : Restoring target : " + (currentTarget != null ? currentTarget.GetDisplayName() : "null"));
            }

            // One way or another, if the target is null, we can't be in a target relative mode/copntext
            if (currentTarget == null)
            {
                Debug.Log("[MRCS] [" + vessel.vesselName + "] PackedLoadingFirstFrame : No target restored");
                if (sasAttitude.ModeInTargetContextOnly(sasMode))
                {
                    sasAttitude.ResetToKillRot();
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] PackedLoadingFirstFrame : the SAS was in a target dependant mode, resetting to KillRot");
                }
                if (sasContext == SpeedDisplayModes.Target)
                {
                    sasContext = SpeedDisplayModes.Orbit;
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] PackedLoadingFirstFrame : the SAS was in Target context, resetting to Orbit");
                }
            }

            // Force the flag to false if we are calling this from another state
            if (currentState != VesselState.PackedLoadingFirstFrameReady)
            {
                targetNeedPhysics = false;
            }
        }
    }
}
