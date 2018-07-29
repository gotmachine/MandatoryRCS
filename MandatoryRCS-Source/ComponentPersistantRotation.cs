/* 
 * This file and all code it contains is released in the public domain
 */

using UnityEngine;
using static MandatoryRCS.VesselModuleMandatoryRCS;

namespace MandatoryRCS
{
    public class ComponentPersistantRotation : ComponentBase
    {
        public override void ComponentFixedUpdate()
        {
            // We rotate all packed vessels
            if (vesselModule.currentState == VesselState.PackedReady || vesselModule.currentState == VesselState.PackedLoadingFirstFrameReady)
            {
                // If the SAS is locked, we keep the vessel rotated toward the SAS selection
                if (vesselModule.sasPersistentModeLock)
                {
                    // We are calculating the SAS attitude in Update(), so we need to make sure Update() has run at least once before doing anything.
                    // TODO : probably due to the Update() situation, the movement is choppy, this need to be tested with a heavy load/low fps situation
                    // Maybe we can find a way to prevent this. We could try moving this part to Update(), but this seems risky.
                    //if (!vesselModule.ready) return;      

                    //if (vesselModule.lockedRollMode)
                    //{
                    //    SetVesselAttitude(vesselModule.sasAttitudeWanted * Quaternion.Euler(90, 0, 0));
                    //}
                    //else
                    //{
                    //    SetVesselAttitude(vesselModule.sasDirectionWanted);
                    //}
                }
                // else rotate the vessel according to its angular velocity
                else if (vesselModule.angularVelocity.magnitude > Settings.velocityThreesold)
                {
                    RotatePackedVessel(vesselModule.angularVelocity);
                }
            }

            // This is the first frame where the vessel is fully loaded and in physics
            // so we apply the saved angular velocity to the vessel.
            else if (vesselModule.currentState == VesselState.PhysicsVelocityFrame)
            {
                if (vesselModule.angularVelocity.magnitude > Settings.velocityThreesold)
                {
                    SetVesselAngularVelocity(vesselModule.angularVelocity);
                }
            }
        }

        public override void ComponentUpdate()
        {
            // We rotate all packed vessels
            if (vesselModule.sasPersistentModeLock 
                && vesselModule.ready
                && (vesselModule.currentState == VesselState.PackedReady || vesselModule.currentState == VesselState.PackedLoadingFirstFrameReady))
            {
                if (vesselModule.sasLockedRollMode)
                {
                    SetVesselAttitude(vesselModule.sasAttitudeWanted * Quaternion.Euler(90, 0, 0));
                }
                else
                {
                    SetVesselAttitude(vesselModule.sasDirectionWanted);
                }
               
            }
        }


        // Apply an angular velocity to the vessel
        // This should only be used on a fully loaded in physics vessel
        private void SetVesselAngularVelocity(Vector3 angularVelocity)
        {
            if (!vessel.loaded || !FlightGlobals.ready)
            { return; }

            if (vessel.situation == Vessel.Situations.PRELAUNCH || vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.SPLASHED)
            { return; }

            // Debug.Log("[US] Restoring " + Vessel.vesselName + "rotation after timewarp/load" );
            Vector3 COM = vessel.CoM;
            Quaternion rotation = vessel.ReferenceTransform.rotation;

            // Applying force on every part
            foreach (Part p in vessel.parts)
            {
                if (!p.GetComponent<Rigidbody>()) continue;
                p.GetComponent<Rigidbody>().AddTorque(rotation * angularVelocity, ForceMode.VelocityChange);
                p.GetComponent<Rigidbody>().AddForce(Vector3.Cross(rotation * angularVelocity, (p.transform.position - COM)), ForceMode.VelocityChange);
                // Note :
                // Doing this trough rigidbodies is depreciated but I can't find a way to use the 1.2 part.addforce/addtorque to provide reliable results
                // see 1.2 patchnotes and unity docs for ForceMode.VelocityChange/ForceMode.Force
            }
        }

        // Update the vessel rotation according to the provided angular velocity
        // Should only be used on a loaded and packed vessel (a vessel in non-physics timewarp)
        private void RotatePackedVessel(Vector3 angularVelocity)
        {
            if (!(vessel.loaded && vessel.packed))
            { return; }

            if (vessel.situation == Vessel.Situations.PRELAUNCH || vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.SPLASHED)
            { return; }

            //vessel.SetRotation(Quaternion.AngleAxis(angularVelocity.magnitude * TimeWarp.CurrentRate, vessel.ReferenceTransform.rotation * angularVelocity) * vessel.transform.rotation, true);
            vessel.SetRotation(Quaternion.AngleAxis(angularVelocity.magnitude * TimeWarp.CurrentRate, vessel.ReferenceTransform.rotation * angularVelocity) * vessel.transform.rotation, true);
        }

        private void SetVesselAttitude(Quaternion attitude)
        {
            if (vessel.situation == Vessel.Situations.PRELAUNCH || vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.SPLASHED)
            { return; }

            // vessel.SetRotation(attitude, true); // SetPos = false seems to break the game on some occasions...
            vessel.SetRotation(attitude, true);

        }

        private void SetVesselAttitude(Vector3 direction)
        {
            SetVesselAttitude(Quaternion.FromToRotation(vessel.vesselTransform.up, direction) * vessel.transform.rotation);
        }
    }
}


