/* 
 * This file and all code it contains is released in the public domain
 */

using UnityEngine;

namespace MandatoryRCS.UI
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, true)]
    public class UISprites : MonoBehaviour
    {
        // Overlays
        public static Sprite spriteOnLocked;
        public static Sprite spriteOnNotLocked;
        public static Sprite spriteOff;
        // Markers
        public static Sprite spriteHold;
        public static Sprite spriteFlyByWire;
        public static Sprite spriteManeuver;
        public static Sprite spriteKillRot;
        public static Sprite spriteTarget;
        public static Sprite spriteAntiTarget;
        public static Sprite spritePrograde;
        public static Sprite spriteRetrograde;
        public static Sprite spriteNormal;
        public static Sprite spriteAntiNormal;
        public static Sprite spriteRadialIn;
        public static Sprite spriteRadialOut;
        public static Sprite spriteProgradeCorrected;
        public static Sprite spriteRetrogradeCorrected;
        public static Sprite spriteParallel;
        public static Sprite spriteAntiParallel;
        // Roll markers
        public static Sprite spriteFreeRoll;
        public static Sprite spriteRollRight;
        public static Sprite spriteRollLeft;
        public static Sprite spriteRoll0;
        public static Sprite spriteRoll45N;
        public static Sprite spriteRoll90N;
        public static Sprite spriteRoll135N;
        public static Sprite spriteRoll45;
        public static Sprite spriteRoll90;
        public static Sprite spriteRoll135;
        public static Sprite spriteRoll180;
        // Other markers
        public static Sprite spriteRCSAuto;
        public static Sprite spriteSun;
        public static Sprite spriteVel0;
        public static Sprite spriteVel1;
        public static Sprite spriteVel2;
        public static Sprite spriteVel3;
        public static Sprite spriteVel4;
        public static Sprite spriteVel5;
        public static Sprite spriteVel6;
        // NavBall markers
        public static Sprite spriteFlyByWireNavBall;

        void Start()
        {
            DontDestroyOnLoad(this);

            spriteOnLocked = UILib.GetSprite("OVERLAY_GREEN");
            spriteOnNotLocked = UILib.GetSprite("OVERLAY_YELLOW");
            spriteOff = UILib.GetSprite("OVERLAY_RED");
            // Markers
            spriteHold = UILib.GetSprite("HOLDSMOOTH");
            spriteFlyByWire = UILib.GetSprite("FLYBYWIRE");
            spriteManeuver = UILib.GetSprite("MANEUVER");
            spriteKillRot = UILib.GetSprite("KILLROT");
            spriteTarget = UILib.GetSprite("TARGET");
            spriteAntiTarget = UILib.GetSprite("ANTITARGET");
            spritePrograde = UILib.GetSprite("PROGRADE");
            spriteRetrograde = UILib.GetSprite("RETROGRADE");
            spriteNormal = UILib.GetSprite("NORMAL");
            spriteAntiNormal = UILib.GetSprite("ANTINORMAL");
            spriteRadialIn = UILib.GetSprite("RADIAL_IN");
            spriteRadialOut = UILib.GetSprite("RADIAL_OUT");
            spriteProgradeCorrected = UILib.GetSprite("PROGRADE_CORRECTED");
            spriteRetrogradeCorrected = UILib.GetSprite("RETROGRADE_CORRECTED");
            spriteParallel = UILib.GetSprite("PARALLEL");
            spriteAntiParallel = UILib.GetSprite("ANTIPARALLEL");
            // Roll markers
            spriteFreeRoll = UILib.GetSprite("FREE_ROLL");
            spriteRollRight = UILib.GetSprite("ROLL_RIGHT");
            spriteRollLeft = UILib.GetSprite("ROLL_LEFT");
            spriteRoll0 = UILib.GetSprite("ROT0");
            spriteRoll45N = UILib.GetSprite("ROT-45");
            spriteRoll90N = UILib.GetSprite("ROT-90");
            spriteRoll135N = UILib.GetSprite("ROT-135");
            spriteRoll45 = UILib.GetSprite("ROT45");
            spriteRoll90 = UILib.GetSprite("ROT90");
            spriteRoll135 = UILib.GetSprite("ROT135");
            spriteRoll180 = UILib.GetSprite("ROT180");
            // Other markers
            spriteRCSAuto = UILib.GetSprite("RCSAUTO");
            spriteSun = UILib.GetSprite("SUN");
            spriteVel0 = UILib.GetSprite("VEL0");
            spriteVel1 = UILib.GetSprite("VEL1");
            spriteVel2 = UILib.GetSprite("VEL2");
            spriteVel3 = UILib.GetSprite("VEL3");
            spriteVel4 = UILib.GetSprite("VEL4");
            spriteVel5 = UILib.GetSprite("VEL5");
            spriteVel6 = UILib.GetSprite("VEL6");
            // NavBall markers
            spriteFlyByWireNavBall = UILib.GetSprite("FLYBYWIRE_NAV");
        }
    }
}
