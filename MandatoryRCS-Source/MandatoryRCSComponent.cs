using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MandatoryRCS
{
    public class MandatoryRCSComponent
    {
        public Vessel vessel;
        public VesselModuleMandatoryRCS vesselModule;

        public virtual void OnStart()
        {
        }
        public virtual void OnLoad(ConfigNode c)
        {
        }
        public virtual void OnSave(ConfigNode c)
        {
        }
        public virtual void Update()
        {
        }
        public virtual void FixedUpdate()
        {
        }
    }
}
