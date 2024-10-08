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
    /// ParameterFactory wrapper for HasResource ContractParameter.
    /// </summary>
    public class ResourceConsumptionFactory : ParameterFactory
    {
        protected double minRate;
        protected double maxRate;
        protected PartResourceDefinition resource;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minRate", x => minRate = x, this, double.MinValue);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "maxRate", x => maxRate = x, this, double.MaxValue);
            valid &= ConfigNodeUtil.ParseValue<PartResourceDefinition>(configNode, "resource", x => resource = x, this);
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, ResourceConsumption.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new ResourceConsumption(minRate, maxRate, resource, updateFrequency, title);
        }
    }
}
