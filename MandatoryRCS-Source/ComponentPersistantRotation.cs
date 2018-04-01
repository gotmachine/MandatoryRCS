using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MandatoryRCS
{
    public class ComponentPersistantRotation : ComponentBase
    {
        public override void ComponentUpdate()
        {
            // Vessel is loaded but not in physics, either because 
            // - It is in the physics bubble but in non-psysics timewarp
            // - It has gone outside of the physics bubble
            // - It was just loaded, is in the physics bubble and will be unpacked in a few frames
            //if (vessel.loaded && vessel.packed)
            if (vesselModule.currentState == VesselModuleMandatoryRCS.VesselState.Packed)
            {
                // If the SAS is locked, we keep the vessel rotated toward the autopilot selection
                if (vesselModule.SASModeLock)
                {
                    if (vesselModule.lockedRollMode)
                    {
                        SetVesselAttitude(vesselModule.attitudeWanted * Quaternion.Euler(90, 0, 0));
                    }
                    else
                    {
                        SetVesselAttitude(vesselModule.directionWanted);
                    }
                }
                // else rotate the vessel according to its angular velocity
                else if (vesselModule.angularVelocity.magnitude > Settings.velocityThreesold)
                {
                    RotatePackedVessel(vesselModule.angularVelocity);
                }
            }

            // Vessel is fully loaded and in physics
            // else if (vessel.loaded && !vessel.packed && FlightGlobals.ready)
            else if (vesselModule.isInPhysicsFirstFrame)
            {
                // Restore the vessel attitude according to the autopilot selection
                if (vesselModule.SASModeLock)
                {
                    if (vesselModule.lockedRollMode)
                    {
                        SetVesselAttitude(vesselModule.attitudeWanted * Quaternion.Euler(90, 0, 0));
                    }
                    else
                    {
                        SetVesselAttitude(vesselModule.directionWanted);
                    }
                }

                // Restore the angular velocity
                if (vesselModule.angularVelocity.magnitude > Settings.velocityThreesold)
                {
                    SetVesselAngularVelocity(vesselModule.angularVelocity);
                }
            }
        }

        // Apply an angular momentum to the vessel
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

        // Update the vessel rotation according to the provided angular momentum
        // Should only be used on a loaded and packed vessel (a vessel in non-physics timewarp)
        private void RotatePackedVessel(Vector3 angularVelocity)
        {
            if (!(vessel.loaded && vessel.packed))
            { return; }

            if (vessel.situation == Vessel.Situations.PRELAUNCH || vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.SPLASHED)
            { return; }

            vessel.SetRotation(Quaternion.AngleAxis(angularVelocity.magnitude * TimeWarp.CurrentRate, vessel.ReferenceTransform.rotation * angularVelocity) * vessel.transform.rotation, true);
        }

        private void SetVesselAttitude(Quaternion attitude)
        {
            if (vessel.situation == Vessel.Situations.PRELAUNCH || vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.SPLASHED)
            { return; }

            vessel.SetRotation(attitude, true); // SetPos = false seems to break the game on some occasions...
        }

        private void SetVesselAttitude(Vector3 direction)
        {
            SetVesselAttitude(Quaternion.FromToRotation(vessel.GetTransform().up, direction) * vessel.transform.rotation);
        }
    }
}


