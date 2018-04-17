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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MandatoryRCS
{

    public class SASButton
    {
        public enum ButtonType
        {
            ButtonBasic,
            ButtonSymbol,
            ToggleTwoState,
            ToggleThreeState
        }


        private GameObject buttonObject;
        private GameObject symbolObject;
        private GameObject backgroundObject;
        private Image backgroundImage;
        private Image symbolImage;
        private Sprite spriteBackground;
        private Sprite spriteSymbol;
        private EventTrigger trigger;
        private KSP.UI.TooltipTypes.TooltipController_Text tooltip;
        private NavBallHandler handler;
        private ButtonType type;
        public ButtonType Type() { return type; }

        private Color inactiveColor = new Color32(255,255,255,50);
        private Color offColor = new Color32(255,118,114,255);
        private Color onColor = new Color32(90,255,90,255);
        private Color onAltColor = new Color32(255,251,58,255);
        public int ControlLevel { get; private set; } = -1;

        public SASButton(NavBallHandler handler, int controlLevel, string name, ButtonType type, Vector2 position, GameObject parent, Sprite spriteBackground, Sprite spriteSymbol = null)
        {
            this.handler = handler;
            ControlLevel = controlLevel;
            this.type = type;
            this.spriteBackground = spriteBackground;
            this.spriteSymbol = spriteSymbol;

            buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent.transform);
            buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 24);
            buttonObject.layer = LayerMask.NameToLayer("UI");
            
            backgroundObject = new GameObject("backgroundObject", typeof(RectTransform));
            backgroundObject.transform.SetParent(buttonObject.transform);
            backgroundObject.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 24);
            backgroundObject.layer = LayerMask.NameToLayer("UI");

            backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.sprite = this.spriteBackground;
            backgroundImage.type = Image.Type.Simple;

            backgroundImage.color = type == ButtonType.ButtonBasic ? Color.white : offColor;

            if (spriteSymbol != null)
            {
                symbolObject = new GameObject("symbolObject", typeof(RectTransform));
                symbolObject.transform.SetParent(buttonObject.transform);
                symbolObject.GetComponent<RectTransform>().sizeDelta = new Vector2(18, 18);
                symbolObject.layer = LayerMask.NameToLayer("UI");

                symbolImage = symbolObject.AddComponent<Image>();
                symbolImage.sprite = this.spriteSymbol;
                symbolImage.type = Image.Type.Simple;  
            }

            trigger = buttonObject.AddComponent<EventTrigger>();

            EventTrigger.Entry mouseDown = new EventTrigger.Entry();
            mouseDown.eventID = EventTriggerType.PointerClick;
            mouseDown.callback.AddListener((data) => { OnPointerDownDelegate((PointerEventData)data); });
            trigger.triggers.Add(mouseDown);

            EventTrigger.Entry mouseUp = new EventTrigger.Entry();
            mouseDown.eventID = EventTriggerType.PointerUp;
            mouseDown.callback.AddListener((data) => { OnPointerUpDelegate((PointerEventData)data); });
            trigger.triggers.Add(mouseUp);

            tooltip = buttonObject.AddComponent<KSP.UI.TooltipTypes.TooltipController_Text>();
            tooltip.prefab = UILib.tooltipPrefab;
            tooltip.SetText(name);

            buttonObject.transform.localPosition = position;

            visible = true;
            active = true;
            toggleState = false;
            altOnState = false;
        }


        public void OnPointerDownDelegate(PointerEventData data)
        {
            handler.ButtonClick(this);

            if (type == ButtonType.ButtonSymbol)
            {
                backgroundImage.color = onColor;
            }
        }

        public void OnPointerUpDelegate(PointerEventData data)
        {
            if (type == ButtonType.ButtonSymbol)
            {
                backgroundImage.color = offColor;
            }
        }


        // Non-visible button is completly hidden and don't respond to clicks
        private bool visible;
        public bool Visible
        {
            get {return visible;}
            set
            {
                visible = value;
                buttonObject.SetActive(visible);
                trigger.enabled = visible;
            }
        }

        // Not active button is grayed and don't respond to clicks
        private bool active;
        public bool Active
        {
            get { return active; }
            set
            {
                active = value;
                trigger.enabled = active;
                if (active)
                {
                    if (type == ButtonType.ButtonSymbol)
                    {
                        backgroundImage.color = offColor;
                    }
                    else
                    {
                        backgroundImage.color = toggleState ? altOnState ? onAltColor : onColor : offColor;
                    }
                    
                }
                else
                {
                    backgroundImage.color = inactiveColor;
                }
                
            }
        }

        // Switch between the on and off background images and update state
        private bool toggleState;
        public bool ToggleState
        {
            get {return toggleState;}
            set
            {
                if (type == ButtonType.ToggleTwoState || type == ButtonType.ToggleThreeState)
                {
                    toggleState = value;
                    if (!active)
                    {
                        backgroundImage.color = inactiveColor;
                    }
                    else
                    {
                        backgroundImage.color = toggleState ? altOnState ? onAltColor : onColor : offColor;
                    }
                }
            }
        }

        private bool altOnState;
        public bool AltOnState
        {
            get { return altOnState; }
            set
            {
                if (type == ButtonType.ToggleThreeState)
                {
                    altOnState = value;
                    backgroundImage.color = toggleState ? altOnState ? onAltColor : onColor : offColor;
                }
            }
        }

        public void ChangeSymbolSprite(Sprite sprite = null)
        {
            if (symbolObject != null)
            {
                symbolImage.sprite = sprite == null ? spriteSymbol : sprite;
            }
        }

        public void ChangeBackgroundSprite(Sprite sprite = null)
        {
            backgroundImage.sprite = sprite == null ? spriteBackground : sprite;
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
