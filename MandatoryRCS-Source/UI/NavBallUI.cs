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
        private Dictionary<SASMode, SASButton> modeToToggle = new Dictionary<SASMode, SASButton>();
        private Dictionary<SASButton, SASMode> toggleToMode = new Dictionary<SASButton, SASMode>();
        #endregion

        #region Toggles and buttons
        //private SASMarkerToggle hold;
        //private SASMarkerToggle flyByWire;
        //private SASMarkerToggle maneuver;
        //private SASMarkerToggle killRot;
        //private SASMarkerToggle target;
        //private SASMarkerToggle antiTarget;
        //private SASMarkerToggle prograde;
        //private SASMarkerToggle retrograde;
        //private SASMarkerToggle normal;
        //private SASMarkerToggle antiNormal;
        //private SASMarkerToggle radialIn;
        //private SASMarkerToggle radialOut;
        //private SASMarkerToggle progradeCorrected;
        //private SASMarkerToggle retrogradeCorrected;
        //private SASMarkerToggle parallel;
        //private SASMarkerToggle antiParallel;

        //private SASMarkerToggle freeRoll;
        //private SASMarkerButton rollRight;
        //private SASMarkerButton rollLeft;

        //private SASMarkerToggle sunTarget;
        //private SASMarkerToggle rcsAuto;
        //private SASMarkerSimple velLimiter;
        #endregion

        private SASButton hold;
        private SASButton flyByWire;
        private SASButton maneuver;
        private SASButton killRot;
        private SASButton target;
        private SASButton antiTarget;
        private SASButton prograde;
        private SASButton retrograde;
        private SASButton normal;
        private SASButton antiNormal;
        private SASButton radialIn;
        private SASButton radialOut;
        private SASButton parallel;
        private SASButton antiParallel;

        private SASButton freeRoll;
        private SASButton rollRight;
        private SASButton rollLeft;

        private SASButton sunTarget;
        private SASButton rcsAuto;
        private SASButton velLimiter;

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
        private bool hasVelocity = true;
        private int controlLevel = 0;
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
            // We don't do anything for EVA kerbals
            if (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.isEVA) return;

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

            /*
                TODO : STOCK INTEGRATION
                SAS control levels (vesselModule.Vessel.VesselValues.AutopilotSkill)  :
                    (0) : stability assist      -> killrot
                    (1) : + pro/retrograde      -> +pro/retrograde, hold, flybywire
                    (2) : + normal/radial       -> +normal, radial, roll lock
                    (3) : + target/maneuver     -> +target, maneuver
                Probe control (vesselModule.Vessel.CurrentControlLevel) :
                    - partial : change mode is allowed, sas is active
                    - none : changing mode not allowed, sas is active
            */


            // Update navball direction marker
            UpdateFlyByWireMarker();

            // Hide/show the whole SAS panel according to SAS button state
            if (vesselModule.sasEnabled != mainButtonPanel.activeInHierarchy)
            {
                guiEnabled = vesselModule.sasEnabled;
                mainButtonPanel.SetActive(guiEnabled);
            }

            // Check the SAS control level
            if (vesselModule.Vessel.VesselValues.AutopilotSkill.value != controlLevel)
            {
                ApplyControlVisibilityRule(vesselModule.Vessel.VesselValues.AutopilotSkill.value);
            }

            // has the context changed ?
            if (vesselModule.sasContext != currentContext)
                ApplyContextVisibilityRule(vesselModule.sasContext);

            // Do we still have a target ?
            if (vesselModule.currentTarget == null && hasTarget)
                ApplyTargetVisibilityRule(false);

            else if (vesselModule.currentTarget != null && !hasTarget)
                ApplyTargetVisibilityRule(true);

            // Do we have a maneuver node ?
            if (vesselModule.VesselHasManeuverNode() != maneuver.Active)
                maneuver.Active = vesselModule.VesselHasManeuverNode();

            // should velocity modes be enabled ?
            if (vesselModule.hasVelocity != hasVelocity)
                ApplyVelocityVisibilityRule(vesselModule.hasVelocity);

            // has the mode changed ?
            if (vesselModule.sasMode != currentMode)
                ChangeMode(vesselModule.sasMode);

            // has the roll mode changed ?
            if (vesselModule.sasLockedRollMode != lockedRollMode)
                SetRollLock(vesselModule.sasLockedRollMode);

            // should the freeroll button be enabled ?
            if (vesselModule.sasRollRefDefined != freeRoll.Active)
                SetRollLock(false, !vesselModule.sasRollRefDefined);

            // Has the velocity limiter value changed ?
            if (vesselModule.sasVelocityLimiter != velocityLimiter)
                CycleVelocityLimiter(false, vesselModule.sasVelocityLimiter);

            // Is the sun our target ?
            if (!sunTarget.ToggleState && vesselModule.currentTarget == (ITargetable)Sun.Instance.sun)
                sunTarget.ToggleState = true;
            else if (sunTarget.ToggleState && vesselModule.currentTarget != (ITargetable)Sun.Instance.sun)
                sunTarget.ToggleState = false;

            // Should RCS auto mode be enabled ?
            if (rcsAuto.ToggleState != vesselModule.sasRcsAutoMode)
                rcsAuto.ToggleState = vesselModule.sasRcsAutoMode;



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
                case 6: velLimiter.ChangeBackgroundSprite(spriteVel0); break;
                case 9: velLimiter.ChangeBackgroundSprite(spriteVel1); break;
                case 12: velLimiter.ChangeBackgroundSprite(spriteVel2); break;
                case 15: velLimiter.ChangeBackgroundSprite(spriteVel3); break;
                case 18: velLimiter.ChangeBackgroundSprite(spriteVel4); break;
                case 21: velLimiter.ChangeBackgroundSprite(spriteVel5); break;
                case 24: velLimiter.ChangeBackgroundSprite(spriteVel6); break;
                default:
                    velocityLimiter = 15;
                    velLimiter.ChangeBackgroundSprite(spriteVel3);
                    break;
            }

            if (updateModuleValue) vesselModule.sasVelocityLimiter = velocityLimiter;
        }

        private void SetRollLock(bool state, bool setInactive = false)
        {
            lockedRollMode = state;
            freeRoll.ToggleState = state;
            freeRoll.Active = !setInactive;
            rollRight.Active = state;
            rollLeft.Active = state;
            if (setInactive) state = false;
            SetRollAngle(true, state ? 0 : -1);
        }

        private void SetRollAngle(bool updateModuleValue, int rollAngle = -1)
        {
            switch (rollAngle)
            {
                case 0:
                    freeRoll.ChangeSymbolSprite(spriteRoll0);
                    break;
                case 45:
                    freeRoll.ChangeSymbolSprite(spriteRoll45);
                    break;
                case 90:
                    freeRoll.ChangeSymbolSprite(spriteRoll90);
                    break;
                case 135:
                    freeRoll.ChangeSymbolSprite(spriteRoll135);
                    break;
                case 180:
                    freeRoll.ChangeSymbolSprite(spriteRoll180);
                    break;
                case 225:
                    freeRoll.ChangeSymbolSprite(spriteRoll135N);
                    break;
                case 270:
                    freeRoll.ChangeSymbolSprite(spriteRoll90N);
                    break;
                case 315:
                    freeRoll.ChangeSymbolSprite(spriteRoll45N);
                    break;
                default:
                    freeRoll.ChangeSymbolSprite(spriteFreeRoll);
                    rollAngle = 0;
                    break;
            }

            currentRoll = rollAngle;

            if (updateModuleValue)
            {
                vesselModule.sasRollOffset = currentRoll;
                vesselModule.sasModeHasChanged = true;
            }
        }

        private void UpdateFlyByWireMarker()
        {
            if (vesselModule.sasMode == SASMode.FlyByWire || vesselModule.sasMode == SASMode.Hold)
            {
                if (!autopilotDirection.IsVisible())
                    autopilotDirection.SetVisible(true);

                autopilotDirection.Update(vesselModule.sasDirectionWanted);
            }
            else if (autopilotDirection.IsVisible())
            {
                autopilotDirection.SetVisible(false);
            }
        }

        private void UpdateLockStatus()
        {
            SASButton button;
            modeToToggle.TryGetValue(currentMode, out button);
            button.AltOnState = !vesselModule.rwLockedOnDirection;
        }

        private void ApplyControlVisibilityRule(int controlLevel)
        {
            this.controlLevel = controlLevel;
            foreach (KeyValuePair<SASMode, SASButton> entry in modeToToggle)
            {
                entry.Value.Visible = entry.Value.ControlLevel <= controlLevel ? true : false;
            }
            //ApplyContextVisibilityRule(vesselModule.autopilotContext);
        }


        private void ApplyContextVisibilityRule(SpeedDisplayModes newContext)
        {
            bool istarget = newContext == SpeedDisplayModes.Target;

            parallel.Visible = parallel.ControlLevel <= controlLevel ? istarget : false;
            antiParallel.Visible = parallel.ControlLevel <= controlLevel ? istarget : false;

            radialIn.Visible = parallel.ControlLevel <= controlLevel ? !istarget : false;
            radialOut.Visible = parallel.ControlLevel <= controlLevel ? !istarget : false;

            currentContext = newContext;
        }

        private void ApplyTargetVisibilityRule(bool hasTarget)
        {
            this.hasTarget = hasTarget;
            target.Active = hasTarget;
            antiTarget.Active = hasTarget;
            parallel.Active = hasTarget;
            antiParallel.Active = hasTarget;
        }

        private void ApplyVelocityVisibilityRule(bool hasVelocity)
        {
            this.hasVelocity = hasVelocity;
            prograde.Active = hasVelocity;
            retrograde.Active = hasVelocity;
        }

        private void ChangeMode(SASMode newMode)
        {
            // Disable previously enabled toggle
            SASButton oldButton;
            if (modeToToggle.TryGetValue(currentMode, out oldButton))
            {
                oldButton.ToggleState = false;
            }

            // And enable the new one
            SASButton newButton;
            modeToToggle.TryGetValue(newMode, out newButton);
            newButton.ToggleState = true;

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

            // NOTE: stock control levels :
            // (0) : stability assist       -> killrot
            // (1) : +pro / retrograde      -> + pro / retrograde, hold, flybywire
            // (2) : +normal / radial       -> + normal, radial, roll lock
            // (3) : +target / maneuver     -> + target, maneuver

            hold                = new SASButton(this, 1, "Hold",             SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 19 + 0,  yoffset + 125), mainButtonPanel, spriteButtonBackground, spriteHold);
            flyByWire           = new SASButton(this, 1, "Fly by wire",      SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 19 + 25, yoffset + 125), mainButtonPanel, spriteButtonBackground, spriteFlyByWire);
            maneuver            = new SASButton(this, 3, "Maneuver",         SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 8 + 0,   yoffset + 100), mainButtonPanel, spriteButtonBackground, spriteManeuver);
            killRot             = new SASButton(this, 0, "Kill rotation",    SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 8 + 25,  yoffset + 100), mainButtonPanel, spriteButtonBackground, spriteKillRot);
            target              = new SASButton(this, 3, "Target",           SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 3 + 0,   yoffset + 75), mainButtonPanel, spriteButtonBackground, spriteTarget);
            antiTarget          = new SASButton(this, 3, "AntiTarget",       SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 3 + 25,  yoffset + 75), mainButtonPanel, spriteButtonBackground, spriteAntiTarget);
            prograde            = new SASButton(this, 1, "Prograde",         SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 0 + 0,   yoffset + 50), mainButtonPanel, spriteButtonBackground, spritePrograde);
            retrograde          = new SASButton(this, 1, "Retrograde",       SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 0 + 25,  yoffset + 50), mainButtonPanel, spriteButtonBackground, spriteRetrograde);
            normal              = new SASButton(this, 2, "Normal",           SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 1 + 0,   yoffset + 25), mainButtonPanel, spriteButtonBackground, spriteNormal);
            antiNormal          = new SASButton(this, 2, "AntiNormal",       SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 1 + 25,  yoffset + 25), mainButtonPanel, spriteButtonBackground, spriteAntiNormal);
            radialIn            = new SASButton(this, 2, "RadialIn",         SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 4 + 0,   yoffset + 0), mainButtonPanel, spriteButtonBackground, spriteRadialIn);
            radialOut           = new SASButton(this, 2, "RadialOut",        SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 4 + 25,  yoffset + 0), mainButtonPanel, spriteButtonBackground, spriteRadialOut);
            parallel            = new SASButton(this, 3, "Parallel",         SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 4 + 0,   yoffset + 0), mainButtonPanel, spriteButtonBackground, spriteParallel);
            antiParallel        = new SASButton(this, 3, "AntiParallel",     SASButton.ButtonType.ToggleThreeState, new Vector2(xoffset + 4 + 25,  yoffset + 0), mainButtonPanel, spriteButtonBackground, spriteAntiParallel);
            freeRoll            = new SASButton(this, 2, "Free roll",        SASButton.ButtonType.ToggleTwoState,   new Vector2(xoffset - 3 + 25,  yoffset - 30), mainButtonPanel, spriteButtonBackground, spriteFreeRoll);
            rollRight           = new SASButton(this, 2, "Roll right",       SASButton.ButtonType.ButtonSymbol,     new Vector2(xoffset - 3 + 50,  yoffset - 30), mainButtonPanel, spriteButtonBackground, spriteRollRight);
            rollLeft            = new SASButton(this, 2, "Roll left",        SASButton.ButtonType.ButtonSymbol,     new Vector2(xoffset - 3 + 0,   yoffset - 30), mainButtonPanel, spriteButtonBackground, spriteRollLeft);
            sunTarget           = new SASButton(this, 0, "Target Sun",       SASButton.ButtonType.ToggleTwoState,   new Vector2(xoffset + 9 + 25,  yoffset - 55), mainButtonPanel, spriteButtonBackground, spriteSun);
            rcsAuto             = new SASButton(this, 0, "RCS auto",         SASButton.ButtonType.ToggleTwoState,   new Vector2(xoffset + 9 + 50,  yoffset - 55), mainButtonPanel, spriteButtonBackground, spriteRCSAuto);
            velLimiter          = new SASButton(this, 0, "SAS aggressivity", SASButton.ButtonType.ButtonBasic,      new Vector2(xoffset + 9 + 0,   yoffset - 55), mainButtonPanel, spriteVel3);

            // Create SASMode<-->Toggle dictionnaries
            CreateDictionnaries();

            guiEnabled = true;

            // We only need to set mode and visibility rules, everything else will be updated in the lateUpdate
            ChangeMode(vesselModule.sasMode);
            ApplyControlVisibilityRule(vesselModule.Vessel.VesselValues.AutopilotSkill.value);
            ApplyContextVisibilityRule(vesselModule.sasContext);

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
            toggleToMode.Add(parallel, SASMode.Parallel );
            toggleToMode.Add(antiParallel, SASMode.AntiParallel );
        }
        #endregion

        #region UI -> VesselModule
        public void ButtonClick(SASButton button)
        {
            if (vesselModule.Vessel.CurrentControlLevel == Vessel.ControlLevel.NONE) return;

            if (button == velLimiter)
            {
                CycleVelocityLimiter(true);
            }
            else if (button == rollRight || button == rollLeft)
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
            else if (button == freeRoll)
            {
                button.ToggleState = !button.ToggleState;
                vesselModule.sasLockedRollMode = button.ToggleState;
                SetRollAngle(true, button.ToggleState ? 0 : -1);
                vesselModule.sasModeHasChanged = true;
            }
            else if (button == rcsAuto)
            {
                button.ToggleState = !button.ToggleState;
                vesselModule.sasRcsAutoMode = button.ToggleState;
            }
            else if (button == sunTarget)
            {
                button.ToggleState = !button.ToggleState;
                vesselModule.SetTarget(button.ToggleState ? Sun.Instance.sun : null, true, true);
            }
            else
            {
                SASMode newMode;
                if (!toggleToMode.TryGetValue(button, out newMode)) return;

                button.ToggleState = true;
                vesselModule.SetSASMode(newMode);
            }
        }
        #endregion

    }
}


