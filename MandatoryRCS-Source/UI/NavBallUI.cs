/* 
 * This file and all code it contains is released in the public domain
 */

using KSP.UI.Screens.Flight;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static FlightGlobals;
using static MandatoryRCS.ComponentSASAttitude;
using static MandatoryRCS.VesselModuleMandatoryRCS;
using static MandatoryRCS.UI.UISprites;

namespace MandatoryRCS.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class NavBallHandler : MonoBehaviour
    {
        #region Mode conversion dicts
        private Dictionary<SASMode, SASMarkerToggle> modeToToggle = new Dictionary<SASMode, SASMarkerToggle>();
        private Dictionary<SASMarkerToggle, SASMode> toggleToMode = new Dictionary<SASMarkerToggle, SASMode>();
        #endregion

        #region Toggles and buttons
        private SASMarkerToggle hold;
        private SASMarkerToggle flyByWire;
        private SASMarkerToggle maneuver;
        private SASMarkerToggle killRot;
        private SASMarkerToggle target;
        private SASMarkerToggle antiTarget;
        private SASMarkerToggle prograde;
        private SASMarkerToggle retrograde;
        private SASMarkerToggle normal;
        private SASMarkerToggle antiNormal;
        private SASMarkerToggle radialIn;
        private SASMarkerToggle radialOut;
        private SASMarkerToggle progradeCorrected;
        private SASMarkerToggle retrogradeCorrected;
        private SASMarkerToggle parallel;
        private SASMarkerToggle antiParallel;

        private SASMarkerToggle freeRoll;
        private SASMarkerButton rollRight;
        private SASMarkerButton rollLeft;

        private SASMarkerToggle sunTarget;
        private SASMarkerToggle rcsAuto;
        private SASMarkerSimple velLimiter;
        #endregion

        private NavBallvector autopilotDirection;

        #region KSP UI GameObjects
        private NavBall navBall; // KSP class
        private GameObject navballFrame; // Top-level object for navball
        private GameObject collapseGroup; // Child of navballFrame, everything else is a child of this
        private GameObject autoPilotModes; // Top-level object for stock SAS marker toggles, we are disabling this
        private GameObject navBallVectorsPivot; // Parent transform for the navball markers
        private GameObject SAS; // SAS toggle
        private GameObject RCS; // RCS toggle
        #endregion

        #region UI state vars
        private bool guiInitialized = false;
        private bool guiEnabled = true;
        private SASMode currentMode;
        private SpeedDisplayModes currentContext = SpeedDisplayModes.Target;
        private bool lockedRollMode = true;
        private int currentRoll = 0;
        private int velocityLimiter = 15;

        private bool hasTarget = true;
        private bool hasManeuver = true;
        private bool hasVelocity = true;
        #endregion

        // Reference to the currently piloted vessel
        // This can be set in two ways :
        // - In scene rebuild (switching to vessel from another scene)
        // - Trough the onVesselSwitching event (cycling trough multiple vessels in the physics bubble)
        public VesselModuleMandatoryRCS vesselModule;

        private GameObject mainButtonPanel;

        #region KSP Events

        private void Start()
        {
            GameEvents.onVesselSwitching.Add(onVesselSwitching);
            GameEvents.onVesselReferenceTransformSwitch.Add(OnVesselReferenceTransformSwitch);
        }

        private void OnDestroy()
        {
            GameEvents.onVesselSwitching.Remove(onVesselSwitching);
            GameEvents.onVesselReferenceTransformSwitch.Remove(OnVesselReferenceTransformSwitch);
        }

        private void OnVesselReferenceTransformSwitch(Transform from, Transform to)
        {
            if (guiInitialized && Vector3.Angle(from.up, to.up) > 5)
            {

            }
        }

        // Detect active vessel change when switching vessel in the physics bubble
        // Note : called before FlightGlobals.ActiveVessel is set, may lead to problems...
        private void onVesselSwitching(Vessel fromVessel, Vessel toVessel)
        {
            if (toVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() > 0)
            {
                vesselModule = toVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();
                //autopilotUI.UpdateUIState();
            }
        }

        private void LateUpdate()
        {
            // Get references to the navball GameObjects, if they are still null abort
            if (navBall == null)
            {
                navBall = FindObjectOfType<NavBall>();
                if (navBall == null) return;

                navballFrame = navBall.gameObject.transform.parent.parent.gameObject;
                collapseGroup = navballFrame.gameObject.GetChild("IVAEVACollapseGroup");
                autoPilotModes = navballFrame.gameObject.GetChild("AutopilotModes");
                navBallVectorsPivot = navballFrame.gameObject.GetChild("NavBallVectorsPivot");
                SAS = navballFrame.gameObject.GetChild("SAS");
                RCS = navballFrame.gameObject.GetChild("RCS");

                // Disable the stock markers
                autoPilotModes.SetActive(false);
            }

            // Initialize the whole thing the first time
            if (!guiInitialized)
            {
                guiInitialized = CreateUI();
                if (!guiInitialized) return;
            }

            // Abort if the vesselModule isn't ready
            // TODO : this won't work, we need to handle the user input when the vessel is packed / timewarping. --> Ok ?
            if (vesselModule == null || !(vesselModule.currentState == VesselState.PhysicsReady || vesselModule.currentState == VesselState.PackedReady))
                return;

            // It seems that disabling the autopilot also disable the stock code that revert 
            // back to the orbit speedDisplayMode if the target is null
            //if (vesselModule.currentTarget == null && FlightGlobals.speedDisplayMode == SpeedDisplayModes.Target)
            //    FlightGlobals.SetSpeedMode(SpeedDisplayModes.Orbit);
            // -> NOW DONE IN SASATTITUDE

            // Update navball direction marker
            UpdateFlyByWireMarker();

            // Hide/show the whole SAS panel according to SAS button state
            if (vesselModule.autopilotEnabled != mainButtonPanel.activeInHierarchy)
            {
                guiEnabled = vesselModule.autopilotEnabled;
                mainButtonPanel.SetActive(guiEnabled);
            }

            // has the context changed ?
            if (vesselModule.autopilotContext != currentContext)
                ApplyContextVisibilityRule(vesselModule.autopilotContext);

            // Do we still have a target ?
            if (vesselModule.currentTarget == null && hasTarget)
                ApplyTargetVisibilityRule(false);

            else if (vesselModule.currentTarget != null && !hasTarget)
                ApplyTargetVisibilityRule(true);

            // Do we have a maneuver node ?
            if (vesselModule.VesselHasManeuverNode() != maneuver.GetActive())
                maneuver.SetActive(vesselModule.VesselHasManeuverNode());

            // should velocity modes be enabled ?
            if (vesselModule.hasVelocity != hasVelocity)
                ApplyVelocityVisibilityRule(vesselModule.hasVelocity);

            // has the mode changed ?
            if (vesselModule.autopilotMode != currentMode)
                ChangeMode(vesselModule.autopilotMode);

            // has the roll mode changed ?
            if (vesselModule.lockedRollMode != lockedRollMode)
                SetRollLock(vesselModule.lockedRollMode);

            // should the freeroll button be enabled ?
            if (vesselModule.isRollRefDefined != freeRoll.GetActive())
                SetRollLock(false, !vesselModule.isRollRefDefined);

            // Has the velocity limiter value changed ?
            if (vesselModule.velocityLimiter != velocityLimiter)
                CycleVelocityLimiter(false, vesselModule.velocityLimiter);

            // Is the sun our target ?
            if (!sunTarget.GetToggleState() && vesselModule.currentTarget == (ITargetable)Sun.Instance.sun)
                sunTarget.SetToggleState(true, false);

            else if (sunTarget.GetToggleState() && vesselModule.currentTarget != (ITargetable)Sun.Instance.sun)
                sunTarget.SetToggleState(false, false);

            // Should RCS auto mode be enabled ?
            if (rcsAuto.GetToggleState() != vesselModule.rcsAutoMode)
                rcsAuto.SetToggleState(vesselModule.rcsAutoMode, false);

            UpdateLockStatus();

        }

        private void CycleVelocityLimiter(bool updateModuleValue, int value = -1)
        {
            // Force value if required
            if (value != -1)
            {
                velocityLimiter = value;
            }
            // Cycle trough possible values
            else
            {
                velocityLimiter += 3;
                if (velocityLimiter > 24) velocityLimiter = 6;
            }

            // Update symbol + value sanity check
            switch (velocityLimiter)
            {
                case 6: velLimiter.SetSprite(spriteVel0); break;
                case 9: velLimiter.SetSprite(spriteVel1); break;
                case 12: velLimiter.SetSprite(spriteVel2); break;
                case 15: velLimiter.SetSprite(spriteVel3); break;
                case 18: velLimiter.SetSprite(spriteVel4); break;
                case 21: velLimiter.SetSprite(spriteVel5); break;
                case 24: velLimiter.SetSprite(spriteVel6); break;
                default:
                    velocityLimiter = 15;
                    velLimiter.SetSprite(spriteVel3);
                    break;
            }

            if (updateModuleValue) vesselModule.velocityLimiter = velocityLimiter;
        }

        private void SetRollLock(bool state, bool setInactive = false)
        {
            lockedRollMode = state;
            freeRoll.SetToggleState(state, false);
            freeRoll.SetActive(!setInactive);
            rollRight.SetActive(state);
            rollLeft.SetActive(state);
            if (setInactive) state = false;
            SetRollAngle(true, state ? 0 : -1);
        }

        private void SetRollAngle(bool updateModuleValue, int rollAngle = -1)
        {
            switch (rollAngle)
            {
                case 0:
                    freeRoll.SetSymbolSprite(spriteRoll0);
                    break;
                case 45:
                    freeRoll.SetSymbolSprite(spriteRoll45);
                    break;
                case 90:
                    freeRoll.SetSymbolSprite(spriteRoll90);
                    break;
                case 135:
                    freeRoll.SetSymbolSprite(spriteRoll135);
                    break;
                case 180:
                    freeRoll.SetSymbolSprite(spriteRoll180);
                    break;
                case 225:
                    freeRoll.SetSymbolSprite(spriteRoll135N);
                    break;
                case 270:
                    freeRoll.SetSymbolSprite(spriteRoll90N);
                    break;
                case 315:
                    freeRoll.SetSymbolSprite(spriteRoll45N);
                    break;
                default:
                    freeRoll.SetSymbolSprite(spriteFreeRoll);
                    rollAngle = 0;
                    break;
            }

            currentRoll = rollAngle;

            if (updateModuleValue)
            {
                vesselModule.currentRoll = currentRoll;
                vesselModule.autopilotModeHasChanged = true;
            }
        }

        private void UpdateFlyByWireMarker()
        {
            if (vesselModule.autopilotMode == SASMode.FlyByWire || vesselModule.autopilotMode == SASMode.Hold)
            {
                if (!autopilotDirection.IsVisible())
                    autopilotDirection.SetVisible(true);

                autopilotDirection.Update(vesselModule.autopilotDirectionWanted);
            }
            else if (autopilotDirection.IsVisible())
            {
                autopilotDirection.SetVisible(false);
            }
        }

        private void UpdateLockStatus()
        {
            SASMarkerToggle toggle;
            modeToToggle.TryGetValue(currentMode, out toggle);
            toggle.UpdateLockState(vesselModule.rwLockedOnDirection);
        }

        private void ApplyContextVisibilityRule(SpeedDisplayModes newContext)
        {
            if (newContext == SpeedDisplayModes.Target || currentContext == SpeedDisplayModes.Target)
            {
                bool istarget = newContext == SpeedDisplayModes.Target;

                progradeCorrected.SetVisible(istarget);
                retrogradeCorrected.SetVisible(istarget);
                parallel.SetVisible(istarget);
                antiParallel.SetVisible(istarget);

                normal.SetVisible(!istarget);
                antiNormal.SetVisible(!istarget);
                radialIn.SetVisible(!istarget);
                radialOut.SetVisible(!istarget);
            }
            currentContext = newContext;
        }

        private void ApplyTargetVisibilityRule(bool hasTarget)
        {
            this.hasTarget = hasTarget;
            target.SetActive(hasTarget);
            antiTarget.SetActive(hasTarget);
            progradeCorrected.SetActive(hasTarget);
            retrogradeCorrected.SetActive(hasTarget);
            parallel.SetActive(hasTarget);
            antiParallel.SetActive(hasTarget);
        }

        private void ApplyVelocityVisibilityRule(bool hasVelocity)
        {
            this.hasVelocity = hasVelocity;
            prograde.SetActive(hasVelocity);
            retrograde.SetActive(hasVelocity);
            progradeCorrected.SetActive(hasVelocity);
            retrogradeCorrected.SetActive(hasVelocity);
        }

        private void ChangeMode(SASMode newMode)
        {
            // Disable previously enabled toggle
            SASMarkerToggle oldToggle;
            if (modeToToggle.TryGetValue(currentMode, out oldToggle))
            {
                oldToggle.SetToggleState(false, false);
            }

            // And enable the new one
            SASMarkerToggle newToggle;
            modeToToggle.TryGetValue(newMode, out newToggle);
            newToggle.SetToggleState(true, false);

            // Update UI state var
            currentMode = newMode;
        }




        #endregion

        #region Init
        private bool CreateUI()
        {
            if (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null)
                return false;
            if (FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() == 0)
                return false;

            // Get a reference to the VesselModule
            vesselModule = FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();

            if (vesselModule.currentState != VesselModuleMandatoryRCS.VesselState.PhysicsReady)
                return false;

            // Create our main panel
            mainButtonPanel = new GameObject("MRCS_SAS");
            mainButtonPanel.transform.SetParent(collapseGroup.transform);
            mainButtonPanel.layer = LayerMask.NameToLayer("UI");
            mainButtonPanel.transform.localPosition = new Vector3(-190, 78);

            // Create all the toggles and buttons
            int xoffset = 0;
            int yoffset = 5;
            hold = new SASMarkerToggle(this, "Hold", new Vector2(xoffset + 19 + 0, yoffset + 125), mainButtonPanel, spriteHold, spriteOff, spriteOnLocked, spriteOnNotLocked);
            flyByWire = new SASMarkerToggle(this, "Fly by wire", new Vector2(xoffset + 19 + 25, yoffset + 125), mainButtonPanel, spriteFlyByWire, spriteOff, spriteOnLocked, spriteOnNotLocked);
            maneuver = new SASMarkerToggle(this, "Maneuver", new Vector2(xoffset + 8 + 0, yoffset + 100), mainButtonPanel, spriteManeuver, spriteOff, spriteOnLocked, spriteOnNotLocked);
            killRot = new SASMarkerToggle(this, "Kill rotation", new Vector2(xoffset + 8 + 25, yoffset + 100), mainButtonPanel, spriteKillRot, spriteOff, spriteOnLocked, spriteOnNotLocked);
            target = new SASMarkerToggle(this, "Target", new Vector2(xoffset + 3 + 0, yoffset + 75), mainButtonPanel, spriteTarget, spriteOff, spriteOnLocked, spriteOnNotLocked);
            antiTarget = new SASMarkerToggle(this, "AntiTarget", new Vector2(xoffset + 3 + 25, yoffset + 75), mainButtonPanel, spriteAntiTarget, spriteOff, spriteOnLocked, spriteOnNotLocked);
            prograde = new SASMarkerToggle(this, "Prograde", new Vector2(xoffset + 0 + 0, yoffset + 50), mainButtonPanel, spritePrograde, spriteOff, spriteOnLocked, spriteOnNotLocked);
            retrograde = new SASMarkerToggle(this, "Retrograde", new Vector2(xoffset + 0 + 25, yoffset + 50), mainButtonPanel, spriteRetrograde, spriteOff, spriteOnLocked, spriteOnNotLocked);
            normal = new SASMarkerToggle(this, "Normal", new Vector2(xoffset + 1 + 0, yoffset + 25), mainButtonPanel, spriteNormal, spriteOff, spriteOnLocked, spriteOnNotLocked);
            antiNormal = new SASMarkerToggle(this, "AntiNormal", new Vector2(xoffset + 1 + 25, yoffset + 25), mainButtonPanel, spriteAntiNormal, spriteOff, spriteOnLocked, spriteOnNotLocked);
            radialIn = new SASMarkerToggle(this, "RadialIn", new Vector2(xoffset + 4 + 0, yoffset + 0), mainButtonPanel, spriteRadialIn, spriteOff, spriteOnLocked, spriteOnNotLocked);
            radialOut = new SASMarkerToggle(this, "RadialOut", new Vector2(xoffset + 4 + 25, yoffset + 0), mainButtonPanel, spriteRadialOut, spriteOff, spriteOnLocked, spriteOnNotLocked);
            progradeCorrected = new SASMarkerToggle(this, "Prograde corrected", new Vector2(xoffset + 1 + 0, yoffset + 25), mainButtonPanel, spriteProgradeCorrected, spriteOff, spriteOnLocked, spriteOnNotLocked);
            retrogradeCorrected = new SASMarkerToggle(this, "Retrograde corrected", new Vector2(xoffset + 1 + 25, yoffset + 25), mainButtonPanel, spriteRetrogradeCorrected, spriteOff, spriteOnLocked, spriteOnNotLocked);
            parallel = new SASMarkerToggle(this, "Parallel", new Vector2(xoffset + 4 + 0, yoffset + 0), mainButtonPanel, spriteParallel, spriteOff, spriteOnLocked, spriteOnNotLocked);
            antiParallel = new SASMarkerToggle(this, "AntiParallel", new Vector2(xoffset + 4 + 25, yoffset + 0), mainButtonPanel, spriteAntiParallel, spriteOff, spriteOnLocked, spriteOnNotLocked);
            freeRoll = new SASMarkerToggle(this, "Free roll", new Vector2(xoffset - 3 + 25, yoffset - 30), mainButtonPanel, spriteFreeRoll, spriteOff, spriteOnLocked, spriteOnNotLocked);
            rollRight = new SASMarkerButton(this, "Roll right", new Vector2(xoffset - 3 + 50, yoffset - 30), mainButtonPanel, spriteRollRight, spriteOff, spriteOnLocked);
            rollLeft = new SASMarkerButton(this, "Roll left", new Vector2(xoffset - 3 + 0, yoffset - 30), mainButtonPanel, spriteRollLeft, spriteOff, spriteOnLocked);
            sunTarget = new SASMarkerToggle(this, "Target Sun", new Vector2(xoffset + 9 + 25, yoffset - 55), mainButtonPanel, spriteSun, spriteOff, spriteOnLocked, spriteOnNotLocked);
            rcsAuto = new SASMarkerToggle(this, "RCS auto", new Vector2(xoffset + 9 + 50, yoffset - 55), mainButtonPanel, spriteRCSAuto, spriteOff, spriteOnLocked, spriteOnNotLocked);
            velLimiter = new SASMarkerSimple(this, "SAS aggressivity", new Vector2(xoffset + 9 + 0, yoffset - 55), mainButtonPanel, spriteVel3);

            // Create SASMode<-->Toggle dictionnaries
            CreateDictionnaries();

            guiEnabled = true;

            // We only need to set mode and context, everything else will be updated in the lateUpdate
            ChangeMode(vesselModule.autopilotMode);
            ApplyContextVisibilityRule(vesselModule.autopilotContext);

            autopilotDirection = new NavBallvector("autopilotDirection", navBallVectorsPivot, navBall, spriteFlyByWireNavBall, new Color32(30,216,40,255), true);
            UpdateFlyByWireMarker();

            return true;
        }

        private void CreateDictionnaries()
        {
            modeToToggle.Add(SASMode.Hold, hold);
            modeToToggle.Add(SASMode.FlyByWire, flyByWire);
            modeToToggle.Add(SASMode.Maneuver, maneuver);
            modeToToggle.Add(SASMode.KillRot, killRot);
            modeToToggle.Add(SASMode.Target, target);
            modeToToggle.Add(SASMode.AntiTarget, antiTarget);
            modeToToggle.Add(SASMode.Prograde, prograde);
            modeToToggle.Add(SASMode.Retrograde, retrograde);
            modeToToggle.Add(SASMode.Normal, normal);
            modeToToggle.Add(SASMode.AntiNormal, antiNormal);
            modeToToggle.Add(SASMode.RadialIn, radialIn);
            modeToToggle.Add(SASMode.RadialOut, radialOut);
            modeToToggle.Add(SASMode.ProgradeCorrected, progradeCorrected);
            modeToToggle.Add(SASMode.RetrogradeCorrected, retrogradeCorrected);
            modeToToggle.Add(SASMode.Parallel, parallel);
            modeToToggle.Add(SASMode.AntiParallel, antiParallel);

            toggleToMode.Add(hold, SASMode.Hold );
            toggleToMode.Add(flyByWire, SASMode.FlyByWire );
            toggleToMode.Add(maneuver, SASMode.Maneuver );
            toggleToMode.Add(killRot, SASMode.KillRot );
            toggleToMode.Add(target, SASMode.Target );
            toggleToMode.Add(antiTarget, SASMode.AntiTarget );
            toggleToMode.Add(prograde, SASMode.Prograde );
            toggleToMode.Add(retrograde, SASMode.Retrograde );
            toggleToMode.Add(normal, SASMode.Normal );
            toggleToMode.Add(antiNormal, SASMode.AntiNormal );
            toggleToMode.Add(radialIn, SASMode.RadialIn );
            toggleToMode.Add(radialOut, SASMode.RadialOut );
            toggleToMode.Add(progradeCorrected, SASMode.ProgradeCorrected );
            toggleToMode.Add(retrogradeCorrected, SASMode.RetrogradeCorrected );
            toggleToMode.Add(parallel, SASMode.Parallel );
            toggleToMode.Add(antiParallel, SASMode.AntiParallel );
        }
        #endregion

        #region UI -> VesselModule
        public void ToggleClick(SASMarkerToggle toggle, bool enabled)
        {
            if (toggle == freeRoll)
            {
                vesselModule.lockedRollMode = enabled;
                SetRollAngle(true, enabled ? 0 : -1);
                vesselModule.autopilotModeHasChanged = true;
            }
            else if (toggle == rcsAuto)
            {
                vesselModule.rcsAutoMode = enabled;
            }
            else if (toggle == sunTarget)
            {
                vesselModule.SetTarget(enabled ? Sun.Instance.sun : null, true, true);
            }
            else
            {
                SASMode newMode;

                if (!toggleToMode.TryGetValue(toggle, out newMode)) return;

                if (newMode != currentMode)
                {
                    vesselModule.autopilotMode = newMode;
                    vesselModule.autopilotModeHasChanged = true;
                }
                else
                {
                    toggle.SetToggleState(true, false);

                    if (newMode == SASMode.FlyByWire)
                    {
                        vesselModule.flyByWire = false;
                    }
                }
            }
        }

        public void ButtonClick(SASMarker button)
        {
            if (button == velLimiter)
            {
                CycleVelocityLimiter(true);
            }
            else if (lockedRollMode)
            {
                int newRoll = currentRoll;
                if (button == rollRight)
                {
                    newRoll += 45;
                    if (newRoll > 315) newRoll = 0;
                    SetRollAngle(true, newRoll);
                }
                if (button == rollLeft)
                {
                    newRoll -= 45;
                    if (newRoll < 0) newRoll = 315;
                    SetRollAngle(true, newRoll);
                }
            }
        }
        #endregion

    }
}


