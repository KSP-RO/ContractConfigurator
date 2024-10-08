﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Parameters;
using ContractConfigurator.Parameters;

namespace ContractConfigurator
{
    /// <summary>
    /// ParameterFactory wrapper for VesselMass ContractParameter.
    /// </summary>
    public class VesselMassFactory : ParameterFactory
    {
        protected float minMass;
        protected float maxMass;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "minMass", x => minMass = x, this, 0.0f, x => Validation.GE(x, 0.0f));
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "maxMass", x => maxMass = x, this, float.MaxValue, x => Validation.GE(x, 0.0f));
            valid &= ConfigNodeUtil.AtLeastOne(configNode, new string[] { "minMass", "maxMass" }, this);
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, VesselMass.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new VesselMass(updateFrequency, minMass, maxMass, title);
        }
    }
}
