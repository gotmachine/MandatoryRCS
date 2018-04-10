using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static MandatoryRCS.VesselModuleMandatoryRCS;

namespace MandatoryRCS
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GameEventsMandatoryRCS : MonoBehaviour
    {
        private GameObject blackScreen;
        private VesselModuleMandatoryRCS vesselModule;

        void Start()
        {
            GameEvents.onVesselSwitching.Add(OnVesselSwitching);
            GameEvents.onVesselGoOnRails.Add(OnVesselGoOnRails);
            GameEvents.onVesselGoOffRails.Add(OnVesselGoOffRails);
            GameEvents.onProtoVesselSave.Add(OnProtoVesselSave);

            blackScreen = new GameObject("MandatoryRCS loading screen");
            Canvas bsCanvas = blackScreen.AddComponent<Canvas>();
            Image bsImage = blackScreen.AddComponent<Image>();
            bsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            bsCanvas.sortingOrder = -1000;
            bsImage.color = Color.black;
            blackScreen.SetActive(false);
        }

        void OnDestroy()
        {
            GameEvents.onVesselSwitching.Remove(OnVesselSwitching);
            GameEvents.onVesselGoOnRails.Remove(OnVesselGoOnRails);
            GameEvents.onVesselGoOffRails.Remove(OnVesselGoOffRails);
            GameEvents.onProtoVesselSave.Remove(OnProtoVesselSave);
        }

        void Update()
        {
            if (FlightGlobals.ActiveVessel != null)
            {
                if (vesselModule == null && FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Any())
                {
                    vesselModule = FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();
                }
                else if (vesselModule != null && vesselModule.Vessel != FlightGlobals.ActiveVessel)
                {
                    vesselModule = FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();
                }
            }
            else
            {
                vesselModule = null;
            }

            if (blackScreen.activeInHierarchy)
            {
                if (vesselModule == null || vesselModule.currentState != VesselState.PackedLoadingUnloadedNotReady)
                {
                    blackScreen.SetActive(false);
                }
            }
            else
            {
                if (vesselModule != null && vesselModule.currentState == VesselState.PackedLoadingUnloadedNotReady)
                {
                    blackScreen.SetActive(true);
                }
            }
        }

        // Stock throw an exception if the Sun is the target, so we reset it before it happens
        private void OnProtoVesselSave(GameEvents.FromToAction<ProtoVessel, ConfigNode> data)
        {
            if (data.from.targetInfo.uniqueName == "Sun")
            {
                data.from.targetInfo.uniqueName = "";
                data.from.targetInfo.targetType = ProtoTargetInfo.Type.Null;
                Debug.Log("[MRCS] [" + data.from.vesselName + "] Removed sun as target in targetInfo (OnProtoVesselSave)");
            }

            if (data.to != null)
            {
                ConfigNode target = new ConfigNode();
                data.to.TryGetNode("TARGET", ref target);
                if (target.GetValue("tgtId") == "Sun")
                {
                    data.to.RemoveNode("TARGET");
                    Debug.Log("[MRCS] [" + data.from.vesselName + "] Removed sun as target in configNode (OnProtoVesselSave)");
                }
            }
        }

        // Detect active vessel change when switching vessel in the physics bubble
        // We neeed to know because the vessel target will be null at this point and restored only a few frames later
        private void OnVesselSwitching(Vessel fromVessel, Vessel toVessel)
        {

            if (fromVessel.targetObject != null)
            {
                fromVessel.targetObject = null;
                Debug.Log("[MRCS] [" + fromVessel.vesselName + "] Removed sun as target in targetObject (OnVesselSwitching)");
            }

            if (fromVessel.pTI != null && fromVessel.pTI.uniqueName == "Sun")
            {
                fromVessel.pTI.uniqueName = "";
                fromVessel.pTI.targetType = ProtoTargetInfo.Type.Null;
                Debug.Log("[MRCS] [" + fromVessel.vesselName + "] Removed sun as target in pTI (OnVesselSwitching)");
            }

            if (fromVessel.protoVessel.targetInfo != null && fromVessel.protoVessel.targetInfo.uniqueName == "Sun")
            {
                fromVessel.protoVessel.targetInfo.uniqueName = "";
                fromVessel.protoVessel.targetInfo.targetType = ProtoTargetInfo.Type.Null;
                Debug.Log("[MRCS] [" + fromVessel.vesselName + "] Removed sun as target in protoVessel.targetInfo (OnVesselSwitching)");
            }


            if (toVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() > 0)
            {
                VesselModuleMandatoryRCS vm = toVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();

                if (vm.currentState == VesselState.PhysicsReady)
                {
                    vm.vesselTargetDirtyFirstFrame = true;
                }

                vm.vesselTargetDirty = true;
                vm.targetDirtyFrameCounter = -1;
                Debug.Log("[MRCS] [" + toVessel.vesselName + "] Switching to " + toVessel.vesselName + ", target is dirty");
            }
        }

        // The vessel is going to packed state
        private void OnVesselGoOnRails(Vessel vessel)
        {
            if (vessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() > 0)
            {
                VesselModuleMandatoryRCS vm = vessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();
                if (vm.currentState == VesselState.Unloaded)
                {
                    vm.currentState = VesselState.PackedLoadingUnloadedNotReady;
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] is being packed (OnVesselGoOnRails), setting state from Unloaded to PackedLoadingUnloadedNotReady");
                }
                else
                {
                    vm.currentState = VesselState.PackedReady;
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] is being packed (OnVesselGoOnRails), setting state to PackedReady");
                }
            }
        }

        // The vessel is going to physics state
        private void OnVesselGoOffRails(Vessel vessel)
        {
            if (vessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() > 0)
            {
                VesselModuleMandatoryRCS vm = vessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();

                //if (vm.currentState == VesselState.PackedLoading)
                //{
                    vm.currentState = VesselState.PhysicsNotReady;
                    Debug.Log("[MRCS] [" + vessel.vesselName + "] is entering physics (OnVesselGoOffRails), setting state to PhysicsNotReady");
                //}
                //else
                //{
                //    vm.currentState = VesselState.PhysicsVelocityFrame;
                //    Debug.Log("[MRCS] [" + vessel.vesselName + "] is entering physics (OnVesselGoOffRails), setting state to PhysicsVelocityFrame");
                //}
            }
        }
    }
}
