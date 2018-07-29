/* This file and all code it contains is licensed under the GNU General Public License v3.0
 * 
 * It is based on the code from the MechJeb2 plugin
 * MechJeb2 can be found at https://github.com/MuMech/MechJeb2
 * 
 * And on the code from the KOS plugin
 * KOS can be found at https://github.com/KSP-KOS
 */

using System;
using UnityEngine;

namespace MandatoryRCS.Lib
{
    public static class VesselPhysics
    {
        public static void GetVesselAvailableTorque(Vessel vessel, Vector3d MOI, out Vector3d torque, out Vector3d reactionSpeed, out Vector3d angularDistanceToStop)
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
                                rcsTorqueAvailable.Add(Vector3.Scale(vessel.vesselTransform.InverseTransformDirection(thrusterTorque), attitudeControl));
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
            torque = Vector3d.zero;
            torque += Vector3d.Max(torqueReactionWheel.positive, torqueReactionWheel.negative);
            torque += Vector3d.Max(rcsTorqueAvailable.positive, rcsTorqueAvailable.negative);
            torque += Vector3d.Max(torqueOthers.positive, torqueOthers.negative);
            torque += torqueAvailableControlSurface;
            torque += torqueAvailableGimbal;

            // Set reaction speed
            reactionSpeed = Vector3d.zero;
            reactionSpeed += reactionSpeedControlSurface;
            reactionSpeed += reactionSpeedGimbal;

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
                    Vector3d.Scale(torque, MOI).InvertNoNaN()
                    )
                );
        }


        public static void GetVesselMoI(Vessel vessel, out Vector3d MoI)
        {
            /// <summary>
            /// This is a replacement for the stock API Property "vessel.MOI", which seems buggy when used
            /// with "control from here" on parts other than the default control part.
            /// <br/>
            /// Right now the stock Moment of Inertia Property returns values in inconsistent reference frames that
            /// don't make sense when used with "control from here".  (It doesn't merely rotate the reference frame, as one
            /// would expect "control from here" to do.)
            /// </summary>   
            /// TODO: This has been fixed in KSP 1.4.3 !

            // Found that the default rotation has top pointing forward, forward pointing down, and right pointing starboard.
            // This fixes that rotation.
            Transform vesselTransform = vessel.ReferenceTransform;
            Quaternion vesselRotation = vesselTransform.rotation * Quaternion.Euler(-90, 0, 0);
            Vector3d centerOfMass = vessel.CoMD;

            var tensor = Matrix4x4.zero;
            Matrix4x4 partTensor = Matrix4x4.identity;
            Matrix4x4 inertiaMatrix = Matrix4x4.identity;
            Matrix4x4 productMatrix = Matrix4x4.identity;
            foreach (var part in vessel.Parts)
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
            MoI = KSPUtil.Diag(tensor);
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
