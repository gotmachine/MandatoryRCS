using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MandatoryRCS
{
    public class ComponentBase
    {
        public Vessel vessel;
        public VesselModuleMandatoryRCS vesselModule;

        public virtual void Start()
        {
        }
        public virtual void ComponentUpdate()
        {
        }
    }
}
