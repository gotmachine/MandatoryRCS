using KSP.UI;
using KSP.UI.Screens.Flight;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static FlightGlobals;
using static MandatoryRCS.ComponentSASAttitude;

namespace MandatoryRCS.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class NavBallHandler : MonoBehaviour
    {
        #region Mode conversion dicts
        private Dictionary<SASMode, SASMarkerToggle> modeToToggle = new Dictionary<SASMode, SASMarkerToggle>();
        private Dictionary<SASMarkerToggle, SASMode> toggleToMode = new Dictionary<SASMarkerToggle, SASMode>();
        #endregion

        #region Sprites
        // Overlays
        Sprite spriteOnLocked;
        Sprite spriteOnNotLocked;
        Sprite spriteOff;
        // Markers
        Sprite spriteHold;
        Sprite spriteFlyByWire;
        Sprite spriteManeuver;
        Sprite spriteKillRot;
        Sprite spriteTarget;
        Sprite spriteAntiTarget;
        Sprite spritePrograde;
        Sprite spriteRetrograde;
        Sprite spriteNormal;
        Sprite spriteAntiNormal;
        Sprite spriteRadialIn;
        Sprite spriteRadialOut;
        Sprite spriteProgradeCorrected;
        Sprite spriteRetrogradeCorrected;
        Sprite spriteParallel;
        Sprite spriteAntiParallel;
        // Roll markers
        Sprite spriteFreeRoll;
        Sprite spriteRollRight;
        Sprite spriteRollLeft;
        Sprite spriteRoll0;
        Sprite spriteRoll45N;
        Sprite spriteRoll90N;
        Sprite spriteRoll135N;
        Sprite spriteRoll45;
        Sprite spriteRoll90;
        Sprite spriteRoll135;
        Sprite spriteRoll180;
        // Other markers
        Sprite spriteRCSAuto;
        Sprite spriteSun;
        Sprite spriteVel1;
        Sprite spriteVel2;
        Sprite spriteVel3;
        Sprite spriteVel4;
        Sprite spriteVel5;

        private void GetSprites()
        {
             spriteOnLocked            = UILib.GetSprite("OVERLAY_GREEN");
             spriteOnNotLocked         = UILib.GetSprite("OVERLAY_YELLOW");
             spriteOff                 = UILib.GetSprite("OVERLAY_RED");
            // Markers
             spriteHold                = UILib.GetSprite("HOLDSMOOTH");
             spriteFlyByWire           = UILib.GetSprite("FLYBYWIRE");
             spriteManeuver            = UILib.GetSprite("MANEUVER");
             spriteKillRot             = UILib.GetSprite("KILLROT");
             spriteTarget              = UILib.GetSprite("TARGET");
             spriteAntiTarget          = UILib.GetSprite("ANTITARGET");
             spritePrograde            = UILib.GetSprite("PROGRADE");
             spriteRetrograde          = UILib.GetSprite("RETROGRADE");
             spriteNormal              = UILib.GetSprite("NORMAL");
             spriteAntiNormal          = UILib.GetSprite("ANTINORMAL");
             spriteRadialIn            = UILib.GetSprite("RADIAL_IN");
             spriteRadialOut           = UILib.GetSprite("RADIAL_OUT");
             spriteProgradeCorrected   = UILib.GetSprite("PROGRADE_CORRECTED");
             spriteRetrogradeCorrected = UILib.GetSprite("RETROGRADE_CORRECTED");
             spriteParallel            = UILib.GetSprite("PARALLEL");
             spriteAntiParallel        = UILib.GetSprite("ANTIPARALLEL");
            // Roll markers
             spriteFreeRoll            = UILib.GetSprite("FREE_ROLL");
             spriteRollRight           = UILib.GetSprite("ROLL_RIGHT");
             spriteRollLeft            = UILib.GetSprite("ROLL_LEFT");
             spriteRoll0               = UILib.GetSprite("ROT0");
             spriteRoll45N             = UILib.GetSprite("ROT-45");
             spriteRoll90N             = UILib.GetSprite("ROT-90");
             spriteRoll135N            = UILib.GetSprite("ROT-135");
             spriteRoll45              = UILib.GetSprite("ROT45");
             spriteRoll90              = UILib.GetSprite("ROT90");
             spriteRoll135             = UILib.GetSprite("ROT135");
             spriteRoll180             = UILib.GetSprite("ROT180");
            // Other markers
             spriteRCSAuto             = UILib.GetSprite("RCSAUTO");
             spriteSun                 = UILib.GetSprite("SUN");
             spriteVel1                = UILib.GetSprite("VEL1");
             spriteVel2                = UILib.GetSprite("VEL2");
             spriteVel3                = UILib.GetSprite("VEL3");
             spriteVel4                = UILib.GetSprite("VEL4");
             spriteVel5                = UILib.GetSprite("VEL5");
        }
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
        private SASMarkerButton velLimiter;
        #endregion

        #region KSP UI GameObjects
        private NavBall navBall; // KSP class
        private GameObject navballFrame; // Top-level object for navball
        private GameObject collapseGroup; // Child of navballFrame, everything else is a child of this
        private GameObject autoPilotModes; // Top-level object for stock SAS marker toggles, we are disabling this
        private GameObject SAS; // SAS toggle
        private GameObject RCS; // RCS toggle
                                //private GameObject stability;
                                //private GameObject maneuver;
                                //private GameObject prograde;
                                //private GameObject retrograde;
                                //private GameObject normal;
                                //private GameObject antinormal;
                                //private GameObject radial;
                                //private GameObject antiradial;
                                //private GameObject target;
                                //private GameObject antitarget;
        #endregion

        #region UI state vars
        private bool guiInitialized = false;
        private bool guiEnabled = true;
        private SASMode currentMode;
        private SpeedDisplayModes currentContext;
        private bool lockedRollMode = false;
        private int currentRoll = 0;
        private bool rcsAutoMode = false;
        private int velocityLimiter = 15;
        private bool sunIsTarget = false;
        //private bool vesselHasChanged = false;

        private bool targetMarkersActive;
        private bool maneuverMarkerActive;
        private bool velocityMarkersActive;
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
            GetSprites();
            GameEvents.onVesselSwitching.Add(onVesselSwitching);
        }

        private void OnDestroy()
        {
            GameEvents.onVesselSwitching.Remove(onVesselSwitching);
        }

        // TODO : currently, this is our "scene just created" entry point
        // There is a noticeable delay between the stock UI display and our UI display
        private void LateUpdate()
        {
            // On scene load, gui will be initialized before 
            if (!FlightGlobals.ready 
                || FlightGlobals.ActiveVessel == null
                || FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() == 0
                || !FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First().ready)
            {
                return;
            }

            // Initialize the whole thing the first time
            if (!guiInitialized)
            {
                guiInitialized = CreateUI();
                if (!guiInitialized) return;
            }

            // Hide/show the panel according to SAS enabled state
            if (vesselModule.autopilotEnabled != guiEnabled)
            {
                guiEnabled = vesselModule.autopilotEnabled;
                SetMode(SASMode.KillRot, true);
                mainButtonPanel.SetActive(guiEnabled);
            }

            if (!guiEnabled) return;
            // Check if something has changed and update the UI
            // Note : ComponentSASAttitude should be responsible for all changes
            UpdateUIFromVesselModule();

            UpdateManeuverMarkers(vesselModule.hasManeuverNode);
            UpdateTargetMarkers(vesselModule.currentTarget != null);
            UpdateVelocityMarkers(FlightGlobals.GetDisplaySpeed() > 0.1);
            UpdateLockStatus();
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
        #endregion

        #region Init methods
        private bool CreateUI()
        {
            if (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null) return false;
            if (FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() == 0) return false;

            // Get a reference to the VesselModule
            vesselModule = FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();

            // Get references to the navball GameObjects
            navBall = FindObjectOfType<NavBall>();
            navballFrame = navBall.gameObject.transform.parent.parent.gameObject;
            collapseGroup = navballFrame.gameObject.GetChild("IVAEVACollapseGroup");
            autoPilotModes = navballFrame.gameObject.GetChild("AutopilotModes");
            SAS = navballFrame.gameObject.GetChild("SAS");
            RCS = navballFrame.gameObject.GetChild("RCS");

            // Disable the stock markers
            autoPilotModes.SetActive(false);

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
            velLimiter = new SASMarkerButton(this, "SAS aggressivity", new Vector2(xoffset + 9 + 0, yoffset - 55), mainButtonPanel, spriteVel3, spriteOff, spriteOnLocked);

            // Create SASMode<>Toggle dictionnaries
            CreateDictionnaries();

            // Update UI according to the vesselModule saved state
            UpdateUIFromVesselModule(true);
            UpdateManeuverMarkers(vesselModule.hasManeuverNode, true);
            UpdateTargetMarkers(vesselModule.currentTarget != null, true);
            UpdateVelocityMarkers(FlightGlobals.GetDisplaySpeed() > 0.1, true);

            guiEnabled = true;

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

        #region UI logic

        public void UpdateLockStatus()
        {
            SASMarkerToggle toggle;
            modeToToggle.TryGetValue(currentMode, out toggle);
            toggle.UpdateLockState(vesselModule.rwLockedOnDirection);
        }

        public void UpdateTargetMarkers(bool active, bool forceUpdate = false)
        {
            if (!forceUpdate && targetMarkersActive == active) return;
            targetMarkersActive = active;

            target.SetActive(active);
            antiTarget.SetActive(active);
            progradeCorrected.SetActive(active);
            retrogradeCorrected.SetActive(active);
            parallel.SetActive(active);
            antiParallel.SetActive(active);

            if (!active && (
                currentMode == SASMode.Target
                || currentMode == SASMode.AntiTarget
                || currentMode == SASMode.ProgradeCorrected
                || currentMode == SASMode.RetrogradeCorrected
                || currentMode == SASMode.Parallel
                || currentMode == SASMode.AntiParallel))
            {
                SetMode(SASMode.KillRot);
            }
        }

        public void UpdateManeuverMarkers(bool active, bool forceUpdate = false)
        {
            if (!forceUpdate && maneuverMarkerActive == active) return;
            maneuverMarkerActive = active;

            maneuver.SetActive(active);
            if (!active && currentMode == SASMode.Maneuver)
            {
                SetMode(SASMode.KillRot);
            }
        }

        public void UpdateVelocityMarkers(bool active, bool forceUpdate = false)
        {
            if (!forceUpdate && velocityMarkersActive == active) return;
            velocityMarkersActive = active;

            prograde.SetActive(active);
            retrograde.SetActive(active);
            progradeCorrected.SetActive(active);
            retrogradeCorrected.SetActive(active);

            if (!active && (
                currentMode == SASMode.Prograde
                || currentMode == SASMode.Retrograde
                || currentMode == SASMode.ProgradeCorrected
                || currentMode == SASMode.RetrogradeCorrected))
            {
                SetMode(SASMode.KillRot);
            }
        }
        #endregion

        #region SAS State update

        private void UpdateUIFromVesselModule(bool forceUpdate = false)
        {
            // Set context according to stock state
            SetContext(vesselModule.autopilotContext, forceUpdate);
            // Set the active mode
            SetMode(vesselModule.autopilotMode, false, forceUpdate);
            // Set roll mode
            SetRollMode(vesselModule.lockedRollMode, true, vesselModule.currentRoll, false, forceUpdate);
            // Set velocity limiter state
            SetVelocityLimiter(true, vesselModule.velocityLimiter, false, forceUpdate);
            // Set RCS auto mode
            SetRCSAuto(vesselModule.rcsAutoMode, false, forceUpdate);
            // Set sun target
            SetTargetSun(vesselModule.sunIsTarget, false, forceUpdate);
        }

        public void ToggleClick(SASMarkerToggle toggle, bool enabled)
        {
            if (toggle == freeRoll)
            {
                SetRollMode(enabled);
            }
            else if (toggle == rcsAuto)
            {
                SetRCSAuto(enabled);
            }
            else if (toggle == sunTarget)
            {
                SetTargetSun(enabled);
            }
            else if (enabled)
            {
                SASMode newMode;
                if (toggleToMode.TryGetValue(toggle, out newMode))
                {
                    SetMode(newMode);
                };
            }
        }

        public void ButtonClick(SASMarkerButton button)
        {
            if (button == velLimiter)
            {
                SetVelocityLimiter();
            }
            else if (lockedRollMode)
            {
                int newRoll = currentRoll;
                if (button == rollRight)
                {
                    newRoll += 45;
                    if (newRoll > 315) newRoll = 0;
                    SetRollAngle(newRoll);
                }
                if (button == rollLeft)
                {
                    newRoll -= 45;
                    if (newRoll < 0) newRoll = 315;
                    SetRollAngle(newRoll);
                }
            }
        }

        public void SetContext(SpeedDisplayModes context, bool forceUpdate = true)
        {
            if (!forceUpdate && context == currentContext) return;

            bool istarget = (context == SpeedDisplayModes.Target);

            progradeCorrected.SetVisible(istarget);
            retrogradeCorrected.SetVisible(istarget);
            parallel.SetVisible(istarget);
            antiParallel.SetVisible(istarget);

            normal.SetVisible(!istarget);
            antiNormal.SetVisible(!istarget);
            radialIn.SetVisible(!istarget);
            radialOut.SetVisible(!istarget);

            currentContext = context;
        }

        public void SetMode(SASMode mode, bool updateVesselModule = true, bool forceUpdate = true)
        {
            if (!forceUpdate && mode == currentMode) return;

            // Disable previously enabled toggle
            SASMarkerToggle oldToggle;
            modeToToggle.TryGetValue(currentMode, out oldToggle);
            oldToggle.SetToggleState(false, false);

            if (updateVesselModule)
            {
                vesselModule.autopilotMode = mode;
                vesselModule.autopilotModeHasChanged = true;
            }
            // Manually set the toggle to disabled state
            else
            { 
                SASMarkerToggle newToggle;
                modeToToggle.TryGetValue(mode, out newToggle);
                newToggle.SetToggleState(true, false);
            }
            // Update UI state var
            currentMode = mode;
        }

        public void SetVelocityLimiter(bool setValue = false, int value = 15, bool updateVesselModule = true, bool forceUpdate = true)
        {
            if (!forceUpdate && setValue && value == velocityLimiter) return;
            if (setValue) { velocityLimiter = value;}
            // Cycle trough possible values
            else
            {
                velocityLimiter += 5;
                if (velocityLimiter > 25) velocityLimiter = 5;
            }

            // Update symbol + value sanity check
            switch (velocityLimiter)
            {
                case 5: velLimiter.SetSymbolSprite(spriteVel1); break;
                case 10: velLimiter.SetSymbolSprite(spriteVel2); break;
                case 15: velLimiter.SetSymbolSprite(spriteVel3); break;
                case 20: velLimiter.SetSymbolSprite(spriteVel4); break;
                case 25: velLimiter.SetSymbolSprite(spriteVel5); break;
                default:
                    velocityLimiter = 15;
                    velLimiter.SetSymbolSprite(spriteVel3);
                    break;
            }

            if (updateVesselModule) vesselModule.velocityLimiter = velocityLimiter;
        }

        public void SetRCSAuto(bool enabled, bool updateVesselModule = true, bool forceUpdate = true)
        {
            if (!forceUpdate && enabled == rcsAutoMode) return;

            rcsAutoMode = enabled;
            rcsAuto.SetToggleState(rcsAutoMode, false);

            if (updateVesselModule) vesselModule.rcsAutoMode = rcsAutoMode;
        }

        public void SetTargetSun(bool enabled, bool updateVesselModule = true, bool forceUpdate = true)
        {
            if (!forceUpdate && enabled == sunIsTarget) return;

            sunIsTarget = enabled;
            sunTarget.SetToggleState(sunIsTarget, false);

            if (updateVesselModule) vesselModule.sunIsTarget = sunIsTarget;
        }

        public void SetRollMode(bool enabled, bool setRollAngle = false, int newRollAngle = 0, bool updateVesselModule = true, bool forceUpdate = true)
        {
            if (!forceUpdate && enabled == lockedRollMode && setRollAngle && newRollAngle == currentRoll) return;

            lockedRollMode = enabled;
            rollRight.SetActive(enabled);
            rollLeft.SetActive(enabled);
            freeRoll.SetToggleState(enabled, false);
            // -1 will set the sprite to freeroll
            SetRollAngle(enabled ? newRollAngle : -1, false, true);

            if (updateVesselModule)
            {
                vesselModule.lockedRollMode = lockedRollMode;
                vesselModule.currentRoll = currentRoll;
                vesselModule.autopilotModeHasChanged = true;
            }
        }

        public void SetRollAngle(int rollAngle, bool updateVesselModule = true, bool forceUpdate = true)
        {
            if (!forceUpdate && rollAngle == currentRoll) return;

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

            if (updateVesselModule)
            {
                vesselModule.currentRoll = currentRoll;
                vesselModule.autopilotModeHasChanged = true;
            }


        }
        #endregion
    }
}
