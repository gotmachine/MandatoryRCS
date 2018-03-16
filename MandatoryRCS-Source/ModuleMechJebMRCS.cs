using MuMech;
using Smooth.Slinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/* MECHJEB INTEGRATION GOALS :
 * - SAS Menu
 *   - "Force roll"
 *   - "Maximum relative angular velocity" -> slider in the SAS menu
 *   - "autoexecute maneuver" -> to be made compliant with control levels
 * - RCS Menu
 *   - "RCS auto mode" -> needs to be tweaked ?
 *   - "RCS balancer" + overdrive level -> test overdrive 
 *   - global RCS throttle : currently not implemented in mechjeb.
 * - SAS markers revamp :
 * 

GENERAL MARKERS
Always available

- Hold : 
  Used to hold an orientation. The orientation is defined in two possible ways:
  - When hold mode activated, hold the current vessel orientation at time of activation
  - On pilot input key release, register the current orientation and then hold it
- HoldSmooth (Default mode):
  Same as Hold, but on input key release angular velocity is killed and once the vessel has stabilized, the reached orientation is registered.
- KillRot : 
  Don't hold any orientation, just keep angular velocity to zero.
- ForceRoll (3 horizontal markers) :
  - center marker : toggle that activate/deactivate force roll. Icon change to reflect the current selected roll angle
  - right / left markers : buttons with arrows icons, add / remove roll angle by 45° increments
  Roll angle is relative to the current reference (ORBIT, SURFACE OR TARGET)
- PitchOffset (3 horizontal markers + value) :
  Add a pitch offset to the current selected orientation. Disabled if Hold, HoldSmooth, KillRot or Maneuver mode is active.
  Usefull for managing attitude in atmosphere, expecially for reentry 
  - center marker : reset pitch offset and set mode to HoldSmooth
  - right / left markers : increase / decrease pitch offset (2.5° increments)
- Maneuver (if node exists)
  
ORBIT / SURFACE
- Prograde
- Retrograde
- Normal
- Antinormal
- Radial
- Antiradial

TARGET
- Target
- AntiTarget
- Prograde
- Retrograde
- ProgradeCorrected
  To target, correcting lateral velocity
  This is the orientation you need to burn to so Prograde become aligned with Target
  See TCA code for how to get the vector
- RetrogradeCorrected
  Against target, correcting lateral velocity
  This is the orientation you need to burn to so Retrograde become aligned with AntiTarget
  See TCA code for how to get the vector
- Parallel +
- Parallel -
 */

namespace MandatoryRCS
{

    public class MRCSMechJebCore : MechJebCore
    {
        // Keep a reference to MJ module in case we want to ractivate the UI ?
        private List<BaseAction> toggleableBaseActions = new List<BaseAction>();
        private List<DisplayModuleVisibility> toggleableDisplayModules = new List<DisplayModuleVisibility>();

        // Need this to do running=false after OnStart in case a mechjeb module is running (saved ships, existing saves, etc)
        //private bool firstUpdate = false;

        private class DisplayModuleVisibility
        {
            public DisplayModule module;
            public bool showInEditor;
            public bool showInFlight;

            public DisplayModuleVisibility(DisplayModule module, bool showInEditor, bool showInFlight)
            {
                this.module = module;
                this.showInEditor = showInEditor;
                this.showInFlight = showInFlight;
            }

        }

        public new void FixedUpdate()
        {
            // disable if base MechJeb is running
            if(running && vessel.IsBaseMechJebRunning())
            {
                running = false;
                Events["ToggleRunning"].guiName = GetRunningInfo();
            }
            base.FixedUpdate();
        }

        // VAB/SPH description
        public override string GetModuleDisplayName()
        {
            return "MRCS SAS";
        }

        public override string GetInfo()
        {
            return "Better stabilization, optimized RCS systems & advanced control options.\nTechnology by MechJeb™";
        }

        private string GetRunningInfo()
        {
            return "MRCS SAS by MechJeb™ : " + (running ? "<b><color=#b4d455>Enabled</color></b>" : "<b><color=#f3a413>Disabled</color></b>");
        }
        

        [KSPEvent(guiName = "MRCS SAS by MechJeb™", guiActive = true, guiActiveEditor = true)]
        public void ToggleRunning()
        {
            running = !running;

            foreach (MRCSMechJebCore m in vessel.GetModules<MRCSMechJebCore>())
            {
                m.running = running;
                m.Events["ToggleRunning"].guiName = GetRunningInfo();
            }

            if (running)
            {
                foreach (MechJebCore m in vessel.GetModules<MechJebCore>())
                {
                    if (m as MRCSMechJebCore != null) continue;
                    m.running = false;
                }
            }

            MandatoryRCSNavBall.instance.EnableCustomSASUI(running);
        }


        public override void OnStart(PartModule.StartState state)
        {
            //firstUpdate = true;

            base.OnStart(state);


            // remove enable/disable mechjeb toggle
            foreach (BaseField f in Fields)
            {
                if (f.name.Equals("running"))
                {
                    f.guiActive = false;
                    f.guiActiveEditor = false;
                }
            }

            // get all MechJeb action groups
            if (toggleableBaseActions.Count == 0)
            {
                foreach (BaseAction a in Actions)
                {
                    toggleableBaseActions.Add(a);
                }
            }

            // get all mechjeb DisplayModules
            if (toggleableDisplayModules.Count == 0)
            {
                foreach (DisplayModule dm in GetDisplayModules(MechJebModuleMenu.DisplayOrder.instance))
                {
                    toggleableDisplayModules.Add(new DisplayModuleVisibility(dm, dm.ShowInEditor, dm.ShowInFlight));
                }
            }

            MechJebVisibility(false);

            Events["ToggleRunning"].guiName = GetRunningInfo();

        }

        private void MechJebVisibility(bool visibility)
        {
            foreach(BaseAction ba in toggleableBaseActions)
            {
                ba.active = visibility;
            }
            foreach (DisplayModuleVisibility bm in toggleableDisplayModules)
            {
                bm.module.ShowInEditor = visibility ? bm.showInEditor : false;
                bm.module.ShowInFlight = visibility ? bm.showInFlight : false;
            }
        }
    }

    public class MRCSAttitudeController : MechJebModuleAttitudeController
    {
        public MRCSAttitudeController(MechJebCore core) : base(core)
        {
            priority = 800; // necessary ?
        }

        // Prevent Mechjeb from disabling the stock SAS when using its own attitude control
        public override void Drive(FlightCtrlState s)
        {
            if (core is MRCSMechJebCore)
            {
                bool SASisEnabled = part.vessel.ActionGroups[KSPActionGroup.SAS];
                base.Drive(s);
                part.vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, SASisEnabled);
            }
            else
            {
                base.Drive(s);
            }
        }
    }


    public static class MRCSVesselExtensions
    {
        // This return the first running MRCSMechJebCore, or null if none present
        // Note : MRCSMasterMechJeb modules are always running since the RMB menu button is hidden
        public static MRCSMechJebCore GetMRCSMasterMechJeb(this Vessel vessel)
        {
            return vessel.GetMasterMechJeb() as MRCSMechJebCore;
        }

        private static float lastFixedTime = 0;
        private static readonly Dictionary<Guid, bool> isBaseMechJebRunning = new Dictionary<Guid, bool>();

        public static bool IsBaseMechJebRunning(this Vessel vessel)
        {
            if (lastFixedTime != Time.fixedTime)
            {
                isBaseMechJebRunning.Clear();
                lastFixedTime = Time.fixedTime;
            }
            Guid vesselKey = vessel == null ? Guid.Empty : vessel.id;

            bool running;
            if (!isBaseMechJebRunning.TryGetValue(vesselKey, out running))
            {
                running = vessel.GetModules<MechJebCore>().Exists(p => p.running && p.GetType() != typeof(MRCSMechJebCore));
                isBaseMechJebRunning.Add(vesselKey, running);
            }
            return running;
        }



    }
}
