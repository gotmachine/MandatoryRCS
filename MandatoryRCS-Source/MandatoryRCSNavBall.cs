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

namespace MandatoryRCS
{

    public static class UILib
    {
        public static Sprite GetSprite(string textureName)
        {
            Texture2D texture = GetTexture(textureName);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(texture.width * 0.5f, texture.height * 0.5f));
        }

        public static Texture2D GetTexture(string textureName)
        {
            Debug.Log("loading : " + "MandatoryRCS/Resources/" + textureName);
            Texture2D texture = GameDatabase.Instance.GetTexture("MandatoryRCS/Resources/" + textureName, false);

            texture.filterMode = FilterMode.Bilinear; // FilterMode.Trilinear is too blurry
            return texture;
        }

        public static Texture2D LoadTexture(string FilePath)
        {

            // Load a PNG or JPG file from disk to a Texture2D
            // Returns null if load fails

            Texture2D Tex2D;
            byte[] FileData;

            if (File.Exists(FilePath))
            {
                FileData = File.ReadAllBytes(FilePath);
                Tex2D = new Texture2D(2, 2);           // Create new "empty" texture
                if (Tex2D.LoadImage(FileData))           // Load the imagedata into the texture (size is set automatically)
                    return Tex2D;                 // If data = readable -> return texture
            }
            return null;                     // Return null if load failed
        }

        // Get a texture2D, bypassing Unity asset read limitations
        public static Texture2D GetReadOnlyTexture(Texture2D source)
        {
            source.filterMode = FilterMode.Point;
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
            rt.filterMode = FilterMode.Point;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            Texture2D nTex = new Texture2D(source.width, source.height);
            nTex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            nTex.Apply();
            RenderTexture.active = null;
            return nTex;

        }

        static KSP.UI.TooltipTypes.Tooltip_Text _tooltipPrefab = null;
        public static KSP.UI.TooltipTypes.Tooltip_Text tooltipPrefab
        {
            get
            {
                if (_tooltipPrefab == null)
                {
                    _tooltipPrefab = AssetBase.GetPrefab<KSP.UI.TooltipTypes.Tooltip_Text>("Tooltip_Text");
                }
                return _tooltipPrefab;
            }
        }
    }

    public class SASUI
    {
        public enum SASFunction
        {
            Hold,
            HoldSmooth,
            Maneuver,
            KillRot,
            Target,
            AntiTarget,
            Prograde,
            Retrograde,
            Normal,
            AntiNormal,
            RadialIn,
            RadialOut,
            ProgradeCorrected,
            RetrogradeCorrected,
            Parallel,
            AntiParallel,
            FreeRoll,
            RollRight,
            RollLeft,
            PitchReset,
            PitchUp,
            PitchDown
        }


        // SPRITES
        // Overlays
        Sprite spriteOn                  = UILib.GetSprite("OVERLAY_GREEN");
        Sprite spriteOff                 = UILib.GetSprite("OVERLAY_RED");
        // Markers
        Sprite spriteHold                = UILib.GetSprite("HOLD");
        Sprite spriteHoldSmooth          = UILib.GetSprite("HOLDSMOOTH");
        Sprite spriteManeuver            = UILib.GetSprite("MANEUVER");
        Sprite spriteKillRot             = UILib.GetSprite("KILLROT");
        Sprite spriteTarget              = UILib.GetSprite("TARGET");
        Sprite spriteAntiTarget          = UILib.GetSprite("ANTITARGET");
        Sprite spritePrograde            = UILib.GetSprite("PROGRADE");
        Sprite spriteRetrograde          = UILib.GetSprite("RETROGRADE");
        Sprite spriteNormal              = UILib.GetSprite("NORMAL");
        Sprite spriteAntiNormal          = UILib.GetSprite("ANTINORMAL");
        Sprite spriteRadialIn            = UILib.GetSprite("RADIAL_IN");
        Sprite spriteRadialOut           = UILib.GetSprite("RADIAL_OUT");
        Sprite spriteProgradeCorrected   = UILib.GetSprite("PROGRADE_CORRECTED");
        Sprite spriteRetrogradeCorrected = UILib.GetSprite("RETROGRADE_CORRECTED");
        Sprite spriteParallel            = UILib.GetSprite("PARALLEL");
        Sprite spriteAntiParallel        = UILib.GetSprite("ANTIPARALLEL");
        // Roll markers
        Sprite spriteFreeRoll            = UILib.GetSprite("FREE_ROLL");
        Sprite spriteRollRight           = UILib.GetSprite("ROLL_RIGHT");
        Sprite spriteRollLeft            = UILib.GetSprite("ROLL_LEFT");
        Sprite spriteRoll0               = UILib.GetSprite("ROT0");
        Sprite spriteRoll45N             = UILib.GetSprite("ROT-45");
        Sprite spriteRoll90N             = UILib.GetSprite("ROT-90");
        Sprite spriteRoll135N            = UILib.GetSprite("ROT-135");
        Sprite spriteRoll45              = UILib.GetSprite("ROT45");
        Sprite spriteRoll90              = UILib.GetSprite("ROT90");
        Sprite spriteRoll135             = UILib.GetSprite("ROT135");
        Sprite spriteRoll180             = UILib.GetSprite("ROT180");
        // Pitch markers
        Sprite spritePitchReset          = UILib.GetSprite("PITCH_RESET");
        Sprite spritePitchUp             = UILib.GetSprite("PITCH_UP");
        Sprite spritePitchDown           = UILib.GetSprite("PITCH_DOWN");
        Sprite spritePitchUpIndicator    = UILib.GetSprite("PITCH_UP_INDICATOR");
        Sprite spritePitchDownIndicator  = UILib.GetSprite("PITCH_DOWN_INDICATOR");

        // Buttons
        private SASToggle hold;
        private SASToggle holdSmooth;
        private SASToggle maneuver;
        private SASToggle killRot;
        private SASToggle target;
        private SASToggle antiTarget;
        private SASToggle prograde;
        private SASToggle retrograde;
        private SASToggle normal;
        private SASToggle antiNormal;
        private SASToggle radialIn;
        private SASToggle radialOut;
        private SASToggle progradeCorrected;
        private SASToggle retrogradeCorrected;
        private SASToggle parallel;
        private SASToggle antiParallel;

        private SASToggle freeRoll;
        private SASButton rollRight;
        private SASButton rollLeft;

        private SASToggle pitchReset;
        private SASButton pitchUp;
        private SASButton pitchDown;

        private List<SASToggle> toggles = new List<SASToggle>();

        private SASFunction currentMode;
        private FlightGlobals.SpeedDisplayModes currentContext;
        
        public bool lockedRollMode = false;
        public bool pitchOffsetMode = false;
        public int currentRoll = 0;
        public int pitchOffset = 0;

        public bool vesselHasChanged = false;

        public VesselModuleMandatoryRCS activeVM;

        private GameObject mainPanel;


        public SASUI(GameObject parentPanel, VesselModuleMandatoryRCS activeVM)
        {
            mainPanel = new GameObject("MRCS_SAS");
            mainPanel.transform.SetParent(parentPanel.transform);
            mainPanel.layer = LayerMask.NameToLayer("UI");
            mainPanel.transform.localPosition = new Vector3(-190, 78);

            int xoffset = 0; 
            int yoffset = 5;  

            holdSmooth          = new SASToggle(this, SASFunction.HoldSmooth,          new Vector2(xoffset + 19 + 0,  yoffset + 125), mainPanel, spriteHoldSmooth, spriteOff, spriteOn);
            hold                = new SASToggle(this, SASFunction.Hold,                new Vector2(xoffset + 19 + 25, yoffset + 125), mainPanel, spriteHold, spriteOff, spriteOn);
            maneuver            = new SASToggle(this, SASFunction.Maneuver,            new Vector2(xoffset + 8 + 0,   yoffset + 100), mainPanel, spriteManeuver, spriteOff, spriteOn);
            killRot             = new SASToggle(this, SASFunction.KillRot,             new Vector2(xoffset + 8 + 25,  yoffset + 100), mainPanel, spriteKillRot, spriteOff, spriteOn);
            target              = new SASToggle(this, SASFunction.Target,              new Vector2(xoffset + 3 + 0,   yoffset + 75),  mainPanel, spriteTarget, spriteOff, spriteOn);
            antiTarget          = new SASToggle(this, SASFunction.AntiTarget,          new Vector2(xoffset + 3 + 25,  yoffset + 75),  mainPanel, spriteAntiTarget, spriteOff, spriteOn);
            prograde            = new SASToggle(this, SASFunction.Prograde,            new Vector2(xoffset + 0 + 0,   yoffset + 50),  mainPanel, spritePrograde, spriteOff, spriteOn);
            retrograde          = new SASToggle(this, SASFunction.Retrograde,          new Vector2(xoffset + 0 + 25,  yoffset + 50),  mainPanel, spriteRetrograde, spriteOff, spriteOn);
            normal              = new SASToggle(this, SASFunction.Normal,              new Vector2(xoffset + 1 + 0,   yoffset + 25),  mainPanel, spriteNormal, spriteOff, spriteOn);
            antiNormal          = new SASToggle(this, SASFunction.AntiNormal,          new Vector2(xoffset + 1 + 25,  yoffset + 25),  mainPanel, spriteAntiNormal, spriteOff, spriteOn);
            radialIn            = new SASToggle(this, SASFunction.RadialIn,            new Vector2(xoffset + 4 + 0,   yoffset + 0),   mainPanel, spriteRadialIn, spriteOff, spriteOn);
            radialOut           = new SASToggle(this, SASFunction.RadialOut,           new Vector2(xoffset + 4 + 25,  yoffset + 0),   mainPanel, spriteRadialOut, spriteOff, spriteOn);
            progradeCorrected   = new SASToggle(this, SASFunction.ProgradeCorrected,   new Vector2(xoffset + 1 + 0,   yoffset + 25),  mainPanel, spriteProgradeCorrected, spriteOff, spriteOn);
            retrogradeCorrected = new SASToggle(this, SASFunction.RetrogradeCorrected, new Vector2(xoffset + 1 + 25,  yoffset + 25),  mainPanel, spriteRetrogradeCorrected, spriteOff, spriteOn);
            parallel            = new SASToggle(this, SASFunction.Parallel,            new Vector2(xoffset + 4 + 0,   yoffset + 0),   mainPanel, spriteParallel, spriteOff, spriteOn);
            antiParallel        = new SASToggle(this, SASFunction.AntiParallel,        new Vector2(xoffset + 4 + 25,  yoffset + 0),   mainPanel, spriteAntiParallel, spriteOff, spriteOn);
            freeRoll            = new SASToggle(this, SASFunction.FreeRoll,            new Vector2(xoffset - 3 + 25,  yoffset - 30),  mainPanel, spriteFreeRoll, spriteOff, spriteOn);
            rollRight           = new SASButton(this, SASFunction.RollRight,           new Vector2(xoffset - 3 + 50,  yoffset - 30),  mainPanel, spriteRollRight, spriteOff, spriteOn);
            rollLeft            = new SASButton(this, SASFunction.RollLeft,            new Vector2(xoffset - 3 + 0,   yoffset - 30),  mainPanel, spriteRollLeft, spriteOff, spriteOn);
            pitchReset          = new SASToggle(this, SASFunction.PitchReset,          new Vector2(xoffset + 9 + 25,  yoffset - 55),  mainPanel, spritePitchReset, spriteOff, spriteOn);
            pitchUp             = new SASButton(this, SASFunction.PitchUp,             new Vector2(xoffset + 9 + 50,  yoffset - 55),  mainPanel, spritePitchUp, spriteOff, spriteOn);
            pitchDown           = new SASButton(this, SASFunction.PitchDown,           new Vector2(xoffset + 9 + 0,   yoffset - 55),  mainPanel, spritePitchDown, spriteOff, spriteOn);

            toggles.Add(hold);
            toggles.Add(holdSmooth);
            toggles.Add(maneuver);
            toggles.Add(killRot);
            toggles.Add(target);
            toggles.Add(antiTarget);
            toggles.Add(prograde);
            toggles.Add(retrograde);
            toggles.Add(normal);
            toggles.Add(antiNormal);
            toggles.Add(radialIn);
            toggles.Add(radialOut);
            toggles.Add(progradeCorrected);
            toggles.Add(retrogradeCorrected);
            toggles.Add(parallel);
            toggles.Add(antiParallel);

            this.activeVM = activeVM;

            UpdateUIState();
        }

        public void EnableSASPanel(bool enabled)
        {
            mainPanel.SetActive(enabled);
        }

        public void UpdateUIState()
        {
            SetContext(FlightGlobals.SpeedDisplayModes.Surface);
            SetRollMode(activeVM.lockedRollMode, activeVM.currentRoll);
            SetPitchMode(activeVM.pitchOffsetMode, activeVM.pitchOffset);
            SetMode(activeVM.SASMode);
        }

        public FlightGlobals.SpeedDisplayModes GetContext() { return currentContext; }


        public void SetContext(FlightGlobals.SpeedDisplayModes context)
        {
            if (context == currentContext) return;

            bool istarget = (context == FlightGlobals.SpeedDisplayModes.Target);

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

        public void TargetActive(bool active)
        {
            target.SetActive(active);
            antiTarget.SetActive(active);
            progradeCorrected.SetActive(active);
            retrogradeCorrected.SetActive(active);
            parallel.SetActive(active);
            antiParallel.SetActive(active);

            if (!active && (
                currentMode == SASFunction.Target
                || currentMode == SASFunction.AntiTarget
                || currentMode == SASFunction.ProgradeCorrected
                || currentMode == SASFunction.RetrogradeCorrected
                || currentMode == SASFunction.Parallel
                || currentMode == SASFunction.AntiParallel))
            {
                SetMode(SASFunction.HoldSmooth);
            }
        }

        public void ManeuverActive(bool active)
        {
            maneuver.SetActive(active);
            if (!active && currentMode == SASFunction.Maneuver)
            {
                SetMode(SASFunction.HoldSmooth);
            }
        }

        public void VelocityActive(bool active)
        {
            prograde.SetActive(active);
            retrograde.SetActive(active);
            progradeCorrected.SetActive(active);
            retrogradeCorrected.SetActive(active);

            if (!active && (
                currentMode == SASFunction.Prograde
                || currentMode == SASFunction.Retrograde
                || currentMode == SASFunction.ProgradeCorrected
                || currentMode == SASFunction.RetrogradeCorrected))
            {
                SetMode(SASFunction.HoldSmooth);
            }
        }

        public void SetMode(SASFunction function)
        {
            foreach (SASToggle t in toggles)
            {
                t.SetToggleState(t.GetFunction() == function ? true : false, false);
            }
            currentMode = function;

            if (FlightGlobals.ActiveVessel != null)
            {
                activeVM.SASMode = function;
            }
        }

        public void SetRollMode(bool enabled, int newRollAngle = 0)
        {
            lockedRollMode = enabled;
            currentRoll = enabled ? newRollAngle : -1;
            rollRight.SetActive(enabled);
            rollLeft.SetActive(enabled);
            freeRoll.SetToggleState(enabled, false);
            UpdateRollSymbol();

            if (FlightGlobals.ActiveVessel != null)
            {
                activeVM.lockedRollMode = lockedRollMode;
                activeVM.currentRoll = currentRoll;
            }

        }

        public void UpdateRollSymbol()
        {
            switch (currentRoll)
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
                    break;
            }
        }

        public void SetPitchMode(bool enabled, int newPitchOffset = 0)
        {
            pitchOffsetMode = enabled;
            pitchOffset = newPitchOffset;
            pitchDown.SetActive(enabled);
            pitchUp.SetActive(enabled);
            pitchReset.SetToggleState(enabled, false);
            UpdatePitchSymbol();

            if (FlightGlobals.ActiveVessel != null)
            {
                activeVM.pitchOffsetMode = pitchOffsetMode;
                activeVM.pitchOffset = pitchOffset;
            }

        }

        public void UpdatePitchSymbol()
        {
            if (pitchOffset > 0)
            {
                pitchReset.SetSymbolSprite(spritePitchUpIndicator);
            }
            else if (pitchOffset < 0)
            {
                pitchReset.SetSymbolSprite(spritePitchDownIndicator);
            }
            else
            {
                pitchReset.SetSymbolSprite(spritePitchReset);
            }
        }

        public void ToggleAction(SASFunction function, bool enabled)
        {
            switch (function)
            {
                case SASFunction.FreeRoll:
                    SetRollMode(enabled);
                    break;
                case SASFunction.PitchReset:
                    SetPitchMode(enabled);
                    break;
                default:
                    if (currentMode == function && !enabled)
                    {
                        toggles.Find(p => p.GetFunction() == function).SetToggleState(true, false);
                    }
                    else
                    {
                        SetMode(function);
                    }
                    break;
            }
        }

        public void ButtonAction(SASFunction function)
        {
            if (lockedRollMode)
            {
                if (function == SASFunction.RollRight)
                {
                    currentRoll += 45;
                    if (currentRoll > 315) currentRoll = 0;
                    activeVM.currentRoll = currentRoll;
                    UpdateRollSymbol();
                }
                if (function == SASFunction.RollLeft)
                {
                    currentRoll -= 45;
                    if (currentRoll < 0) currentRoll = 315;
                    activeVM.currentRoll = currentRoll;
                    UpdateRollSymbol();
                }
            }

            if (pitchOffsetMode)
            {
                if (function == SASFunction.PitchUp)
                {
                    pitchOffset += 5;
                    if (pitchOffset > 15) pitchOffset = 15;
                    activeVM.pitchOffset = pitchOffset;
                    UpdatePitchSymbol();
                }
                if (function == SASFunction.PitchDown)
                {
                    pitchOffset -= 5;
                    if (pitchOffset < -15) pitchOffset = -15;
                    activeVM.pitchOffset = pitchOffset;
                    UpdatePitchSymbol();
                }
            }
        }
    }

    public class SASButton
    {
        //public Sprite spriteOn;
        //public Sprite spriteOff;
        //public Sprite spriteSymbol;

        public SASUI.SASFunction function; 

        private GameObject buttonObject;
        private GameObject button;
        private GameObject symbol;
        private Button buttonComponent;
        private KSP.UI.TooltipTypes.TooltipController_Text tooltip;
        private Image symbolImage;
        private Image offImage;

        private SASUI handler;

        public SASButton(SASUI handler, SASUI.SASFunction function, Vector2 position, GameObject parent, Sprite spriteSymbol, Sprite spriteOff, Sprite spriteOn)
        {
            this.handler = handler;
            this.function = function;

            buttonObject = new GameObject(function.ToString());
            buttonObject.transform.SetParent(parent.transform);
            buttonObject.layer = LayerMask.NameToLayer("UI");

            button = new GameObject("Button");
            button.transform.SetParent(buttonObject.transform);
            button.layer = LayerMask.NameToLayer("UI");

            offImage = button.AddComponent<Image>();
            offImage.sprite = spriteOff;
            offImage.type = Image.Type.Simple;

            symbol = new GameObject("Symbol");
            symbol.transform.SetParent(buttonObject.transform);
            symbol.layer = LayerMask.NameToLayer("UI");

            symbolImage = symbol.AddComponent<Image>();
            symbolImage.sprite = spriteSymbol;
            symbolImage.raycastTarget = false;
            symbolImage.type = Image.Type.Simple;

            buttonComponent = button.AddComponent<Button>();
            buttonComponent.transition = Selectable.Transition.SpriteSwap;
            buttonComponent.targetGraphic = offImage;
            SpriteState ss = new SpriteState();
            ss.pressedSprite = spriteOn;
            buttonComponent.spriteState = ss;

            buttonComponent.onClick.AddListener(OnClick);

            buttonObject.transform.localPosition = position;
            //buttonObject.transform.localScale = new Vector3(0.5f, 0.5f, 1.0f);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 24);
            symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(17, 17);

            tooltip = button.AddComponent<KSP.UI.TooltipTypes.TooltipController_Text>();
            tooltip.prefab = UILib.tooltipPrefab;
            tooltip.SetText(function.ToString());
        }

        private void OnClick()
        {
            //GameObject ButtonClicked = EventSystem.current.currentSelectedGameObject;
            handler.ButtonAction(function);
        }

        public void SetActive(bool active) { button.SetActive(active); }
        public bool GetActive() { return button.activeInHierarchy; }

        public void SetVisible(bool visible) { buttonObject.SetActive(visible); }
        public bool GetVisible() { return buttonObject.activeInHierarchy; }

    }

    public class SASToggle
    {
        //public Sprite spriteOn;
        //public Sprite spriteOff;
        //public Sprite spriteSymbol;
        public bool isOn;
        public bool overlayVisible;
        public bool buttonVisible;

        private GameObject toggleObject;
        private GameObject toggle;
        private GameObject symbol;
        private GameObject overlayOff;
        private GameObject overlayOn;
        private Toggle toggleComponent;
        private KSP.UI.TooltipTypes.TooltipController_Text tooltip;
        private Image symbolImage;

        private SASUI handler;
        private SASUI.SASFunction function;
        public SASUI.SASFunction GetFunction() { return function; }

        public SASToggle(SASUI handler, SASUI.SASFunction function, Vector2 position, GameObject parent, Sprite spriteSymbol, Sprite spriteOff, Sprite spriteOn)
        {
            this.handler = handler;
            this.function = function;

            toggleObject = new GameObject(function.ToString());
            toggleObject.transform.SetParent(parent.transform);
            toggleObject.layer = LayerMask.NameToLayer("UI");

            toggle = new GameObject(function.ToString());
            toggle.transform.SetParent(toggleObject.transform);
            toggle.layer = LayerMask.NameToLayer("UI");

            overlayOff = new GameObject("OverlayOff");
            overlayOff.transform.SetParent(toggle.transform);
            overlayOff.layer = LayerMask.NameToLayer("UI");

            overlayOn = new GameObject("OverlayOn");
            overlayOn.transform.SetParent(overlayOff.transform);
            overlayOn.layer = LayerMask.NameToLayer("UI");

            symbol = new GameObject("Symbol");
            symbol.transform.SetParent(toggleObject.transform);
            symbol.layer = LayerMask.NameToLayer("UI");

            symbolImage = symbol.AddComponent<Image>();
            symbolImage.sprite = spriteSymbol;
            symbolImage.raycastTarget = false;
            symbolImage.type = Image.Type.Simple;

            Image offImage = overlayOff.AddComponent<Image>();
            offImage.sprite = spriteOff;
            offImage.type = Image.Type.Simple;

            Image onImage = overlayOn.AddComponent<Image>();
            onImage.sprite = spriteOn;
            onImage.type = Image.Type.Simple;

            toggleComponent = toggle.AddComponent<Toggle>();
            toggleComponent.transition = Selectable.Transition.ColorTint;
            toggleComponent.targetGraphic = offImage;
            toggleComponent.isOn = false;
            toggleComponent.toggleTransition = Toggle.ToggleTransition.None; // fade ?
            toggleComponent.graphic = onImage;
            toggleComponent.onValueChanged.AddListener(OnToggleState);


            toggleObject.transform.localPosition = position;
            //toggleObject.transform.localScale = new Vector3(0.5f, 0.5f, 1.0f);
            toggle.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 24);
            //toggle.GetComponent<RectTransform>().anchoredPosition3D = position;
            //toggle.GetComponent<RectTransform>().localScale = new Vector3(0.5f, 0.5f, 1.0f);
            symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(17, 17);
            overlayOff.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 24);
            overlayOn.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 24);

            tooltip = toggle.AddComponent<KSP.UI.TooltipTypes.TooltipController_Text>();
            tooltip.prefab = UILib.tooltipPrefab;
            tooltip.SetText(function.ToString());
        }

        private void OnToggleState(bool enabled)
        {
            //GameObject ButtonClicked = EventSystem.current.currentSelectedGameObject;
            handler.ToggleAction(function, enabled);
        }

        public void SetActive(bool active) { toggle.SetActive(active); }
        public bool GetActive() { return toggle.activeInHierarchy; }

        public void SetVisible(bool visible) { toggleObject.SetActive(visible); }
        public bool GetVisible() { return toggleObject.activeInHierarchy; }

        public void SetTooltipText(string text)
        {
            tooltip.SetText(text);
        }

        public void SetTooltipEnabled(bool enabled)
        {
            tooltip.enabled = enabled;
        }

        public void SetToggleState(bool enabled, bool fireEvent = true)
        {
            if (!fireEvent) toggleComponent.onValueChanged.RemoveListener(OnToggleState);
            toggleComponent.isOn = enabled;
            if (!fireEvent) toggleComponent.onValueChanged.AddListener(OnToggleState);
        }

        public bool GetToggleState()
        {
            return toggleComponent.isOn;
        }

        public void SetSymbolSprite(Sprite sprite)
        {
            symbolImage.sprite = sprite;
        }

    }
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class NavBallHandler : MonoBehaviour
    {

        private NavBall navBall;

        private GameObject navballFrame;
        private GameObject autoPilotModes;
        private GameObject collapseGroup;

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
        //private GameObject SAS;
        //private GameObject RCS;

        private SASUI SASui;

        private bool customSASVisible = true;

        public VesselModuleMandatoryRCS activeVM;

        private void Start()
        {
            GameEvents.onVesselSwitching.Add(onVesselSwitching);
        }

        private void OnDestroy()
        {
            GameEvents.onVesselSwitching.Remove(onVesselSwitching);
        }

        private void FixedUpdate()
        {
            if (SASui == null) return;

            if (FlightGlobals.ActiveVessel.Autopilot.Enabled != customSASVisible)
            {
                SASui.EnableSASPanel(FlightGlobals.ActiveVessel.Autopilot.Enabled);
                customSASVisible = FlightGlobals.ActiveVessel.Autopilot.Enabled;
            }

            if (FlightGlobals.speedDisplayMode != SASui.GetContext())
            {
                SASui.SetContext(FlightGlobals.speedDisplayMode);
            }

            SASui.ManeuverActive(FlightGlobals.ActiveVessel.patchedConicSolver.maneuverNodes.Count != 0);
            SASui.TargetActive(FlightGlobals.ActiveVessel.targetObject != null);
            SASui.VelocityActive(FlightGlobals.GetDisplaySpeed() > 0.1);
        }

        private void LateUpdate()
        {
            if (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null) { return; }

            if (navBall == null)
            {
                if (FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() == 0) { return; }

                activeVM = FlightGlobals.ActiveVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();

                navBall = FindObjectOfType<NavBall>();
                navballFrame = navBall.gameObject.transform.parent.parent.gameObject;
                collapseGroup = navballFrame.gameObject.GetChild("IVAEVACollapseGroup");
                autoPilotModes = navballFrame.gameObject.GetChild("AutopilotModes");

                //stability = autoPilotModes.GetChild("Stability");
                //maneuver = autoPilotModes.GetChild("Maneuver");
                //prograde = autoPilotModes.GetChild("Prograde");
                //retrograde = autoPilotModes.GetChild("Retrograde");
                //normal = autoPilotModes.GetChild("Normal");
                //antinormal = autoPilotModes.GetChild("AntiNormal");
                //radial = autoPilotModes.GetChild("Radial");
                //antiradial = autoPilotModes.GetChild("AntiRadial");
                //target = autoPilotModes.GetChild("Target");
                //antitarget = autoPilotModes.GetChild("AntiTarget
                //SAS = navballFrame.gameObject.GetChild("SAS");
                //RCS = navballFrame.gameObject.GetChild("RCS");

                autoPilotModes.SetActive(false);

                SASui = new SASUI(collapseGroup, activeVM);
            }
        }

        // Detect active vessel change when switching vessel in the physics bubble
        // Note : called before FlightGlobals.ActiveVessel is set, may lead to problems...
        private void onVesselSwitching(Vessel fromVessel, Vessel toVessel)
        {
            if (toVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().Count() > 0)
            {
                activeVM = toVessel.vesselModules.OfType<VesselModuleMandatoryRCS>().First();
                SASui.activeVM = activeVM;
                SASui.UpdateUIState();
            }
        }
    }
}
