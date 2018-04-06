/* 
 * This file and all code it contains is released in the public domain
 */

using KSP.UI.Screens.Flight;
using MandatoryRCS.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MandatoryRCS
{
    public class SASMarker
    {
        public string name;
    }

    public class SASMarkerSimple : SASMarker
    {
        //public Sprite spriteOn;
        //public Sprite spriteOff;
        //public Sprite spriteSymbol;

        private GameObject buttonObject;
        private GameObject button;
        private Button buttonComponent;
        private KSP.UI.TooltipTypes.TooltipController_Text tooltip;
        private Image symbolImage;

        private NavBallHandler handler;

        public SASMarkerSimple(NavBallHandler handler, string name, Vector2 position, GameObject parent, Sprite spriteSymbol)
        {
            this.handler = handler;
            this.name = name;

            buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent.transform);
            buttonObject.layer = LayerMask.NameToLayer("UI");

            button = new GameObject("Button");
            button.transform.SetParent(buttonObject.transform);
            button.layer = LayerMask.NameToLayer("UI");

            symbolImage = button.AddComponent<Image>();
            symbolImage.sprite = spriteSymbol;
            symbolImage.type = Image.Type.Simple;

            buttonComponent = button.AddComponent<Button>();
            buttonComponent.transition = Selectable.Transition.None;

            buttonComponent.onClick.AddListener(OnClick);

            buttonObject.transform.localPosition = position;
            //buttonObject.transform.localScale = new Vector3(0.5f, 0.5f, 1.0f);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 24);

            tooltip = button.AddComponent<KSP.UI.TooltipTypes.TooltipController_Text>();
            tooltip.prefab = UILib.tooltipPrefab;
            tooltip.SetText(name);
        }

        private void OnClick()
        {
            //GameObject ButtonClicked = EventSystem.current.currentSelectedGameObject;
            handler.ButtonClick(this);
        }

        public void SetActive(bool active) { button.SetActive(active); }
        public bool GetActive() { return button.activeInHierarchy; }

        public void SetVisible(bool visible) { buttonObject.SetActive(visible); }
        public bool GetVisible() { return buttonObject.activeInHierarchy; }

        public void SetSprite(Sprite symbol)
        {
            symbolImage.sprite = symbol;
        }

    }

    public class SASMarkerButton : SASMarker
    {
        //public Sprite spriteOn;
        //public Sprite spriteOff;
        //public Sprite spriteSymbol;

        private GameObject buttonObject;
        private GameObject button;
        private GameObject symbol;
        private Button buttonComponent;
        private KSP.UI.TooltipTypes.TooltipController_Text tooltip;
        private Image symbolImage;
        private Image offImage;

        private NavBallHandler handler;

        public SASMarkerButton(NavBallHandler handler, string name, Vector2 position, GameObject parent, Sprite spriteSymbol, Sprite spriteOff, Sprite spriteOn)
        {
            this.handler = handler;
            this.name = name;

            buttonObject = new GameObject(name);
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
            tooltip.SetText(name);
        }

        private void OnClick()
        {
            //GameObject ButtonClicked = EventSystem.current.currentSelectedGameObject;
            handler.ButtonClick(this);
        }

        public void SetActive(bool active) { button.SetActive(active); }
        public bool GetActive() { return button.activeInHierarchy; }

        public void SetVisible(bool visible) { buttonObject.SetActive(visible); }
        public bool GetVisible() { return buttonObject.activeInHierarchy; }

        public void SetSymbolSprite(Sprite symbol)
        {
            symbolImage.sprite = symbol;
        }

    }

    public class SASMarkerToggle : SASMarker
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
        private Image onImage;
        private Image offImage;
        private Sprite spriteOnLocked;
        private Sprite spriteOnNotLocked;

        private NavBallHandler handler;

        public SASMarkerToggle(NavBallHandler handler, string name, Vector2 position, GameObject parent, Sprite spriteSymbol, Sprite spriteOff, Sprite spriteOnLocked, Sprite spriteOnNotLocked)
        {
            this.handler = handler;
            this.name = name;

            toggleObject = new GameObject(name);
            toggleObject.transform.SetParent(parent.transform);
            toggleObject.layer = LayerMask.NameToLayer("UI");

            toggle = new GameObject(name + " toggle");
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

            offImage = overlayOff.AddComponent<Image>();
            offImage.sprite = spriteOff;
            offImage.type = Image.Type.Simple;

            this.spriteOnLocked = spriteOnLocked;
            this.spriteOnNotLocked = spriteOnNotLocked;

            onImage = overlayOn.AddComponent<Image>();
            onImage.sprite = this.spriteOnLocked;
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
            toggle.GetComponent<RectTransform>().sizeDelta = new Vector2(25, 25); //24,24
            //toggle.GetComponent<RectTransform>().anchoredPosition3D = position;
            //toggle.GetComponent<RectTransform>().localScale = new Vector3(0.5f, 0.5f, 1.0f);
            symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(18, 18); // 17,17
            overlayOff.GetComponent<RectTransform>().sizeDelta = new Vector2(25, 25); //24,24
            overlayOn.GetComponent<RectTransform>().sizeDelta = new Vector2(25, 25); //24,24

            tooltip = toggle.AddComponent<KSP.UI.TooltipTypes.TooltipController_Text>();
            tooltip.prefab = UILib.tooltipPrefab;
            tooltip.SetText(name);
        }

        private void OnToggleState(bool enabled)
        {
            //GameObject ButtonClicked = EventSystem.current.currentSelectedGameObject;
            handler.ToggleClick(this, enabled);
            offImage.enabled = !enabled;
        }

        public void UpdateLockState(bool locked)
        {
            onImage.sprite = locked ? spriteOnLocked : spriteOnNotLocked;
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
            offImage.enabled = !enabled;
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


    public class NavBallvector
    {
        private GameObject vectorObject;
        private GameObject indicationArrow;
        private NavBall navBall;
        private bool visible;

        public NavBallvector(string name, GameObject navBallVectorsPivot, NavBall navBall, Sprite sprite, Color32 color, bool hasArrow)
        {
            this.navBall = navBall;
            vectorObject = new GameObject(name);
            vectorObject.transform.SetParent(navBallVectorsPivot.transform);
            vectorObject.layer = LayerMask.NameToLayer("UI");

            Image image = vectorObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;

            ((RectTransform)vectorObject.transform).sizeDelta = new Vector2(40, 40);

            if (hasArrow)
            {
                indicationArrow = GameObject.Instantiate(navBallVectorsPivot.GetChild("BurnVectorArrow"));
                indicationArrow.transform.SetParent(navBallVectorsPivot.transform);
                indicationArrow.GetComponent<MeshRenderer>().materials[0].SetColor("_TintColor", color);
            }

            visible = true;
        }

        public bool IsVisible()
        {
            return visible;
        }

        public void SetVisible(bool visible)
        {
            this.visible = visible;

            if (!visible)
            {
                vectorObject.SetActive(false);
                if (indicationArrow != null)
                {
                    indicationArrow.SetActive(false);
                }
            }
        }

        public void Update(Vector3 direction)
        {
            if (visible)
            {
                vectorObject.transform.localPosition = navBall.attitudeGymbal * (direction.normalized * navBall.VectorUnitScale);

                if (vectorObject.transform.localPosition.z >= navBall.VectorUnitCutoff)
                {
                    if (!vectorObject.activeSelf)
                    {
                        vectorObject.SetActive(true);
                    }
                    if (indicationArrow != null && indicationArrow.activeSelf)
                    {
                        indicationArrow.SetActive(false);
                    }
                    return;
                }
                else
                {
                    if (vectorObject.activeSelf)
                    {
                        vectorObject.SetActive(false);
                    }
                    if (indicationArrow != null && !indicationArrow.activeSelf)
                    {
                        indicationArrow.SetActive(true);
                    }
                }

                if (indicationArrow != null)
                {
                    Vector3 localPosition = vectorObject.transform.localPosition;
                    Vector3 vector = localPosition - Vector3.Dot(localPosition, Vector3.forward) * Vector3.forward;
                    vector.Normalize();
                    vector *= navBall.VectorUnitScale * 0.6f;
                    indicationArrow.transform.localPosition = vector;
                    float num = 57.29578f * Mathf.Acos(vector.x / Mathf.Sqrt(vector.x * vector.x + vector.y * vector.y));
                    if (vector.y < 0f)
                    {
                        num += 2f * (180f - num);
                    }
                    if (float.IsNaN(num))
                    {
                        num = 0f;
                    }
                    Quaternion localRotation = Quaternion.Euler(num + 90f, 270f, 90f);
                    indicationArrow.transform.localRotation = localRotation;
                }
            }
            else
            {
                if (vectorObject.activeSelf)
                {
                    vectorObject.SetActive(false);
                }
                if (indicationArrow != null && indicationArrow.activeSelf)
                {
                    indicationArrow.SetActive(false);
                }
            }
        }
    }
}
