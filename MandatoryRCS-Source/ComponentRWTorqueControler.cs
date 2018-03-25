using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MandatoryRCS
{
    class ComponentRWTorqueControler : MandatoryRCSComponent
    {

                    // Determine the velocity saturation factor for reaction wheels (used by ModuleTorqueController)
    //        if (MandatoryRCSSettings.velocitySaturation)
    //        {
    //            velSaturationTorqueFactor = Math.Max(1.0f - Math.Min((Math.Max(angularVelocity.magnitude - MandatoryRCSSettings.saturationMinAngVel, 0.0f) * MandatoryRCSSettings.saturationMaxAngVel), 1.0f), MandatoryRCSSettings.saturationMinTorqueFactor);
    //        }
    //        else
    //        {
    //            velSaturationTorqueFactor = 1.0f;
    //        }
    }
}
