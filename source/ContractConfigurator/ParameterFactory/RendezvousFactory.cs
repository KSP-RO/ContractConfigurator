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
    /// ParameterFactory wrapper for Rendezvous ContractParameter. 
    /// </summary>
    public class RendezvousFactory : ParameterFactory
    {
        protected List<VesselIdentifier> vessels;
        protected double distance;
        protected float updateFrequency;

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            if (parent is VesselParameterGroupFactory)
            {
                valid &= ConfigNodeUtil.ParseValue<List<VesselIdentifier>>(configNode, "vessel", x => vessels = x, this, new List<VesselIdentifier>(), l => ValidateVesselList(l));
            }
            else
            {
                valid &= ConfigNodeUtil.ParseValue<List<VesselIdentifier>>(configNode, "vessel", x => vessels = x, this, l => ValidateVesselList(l));
            }

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "distance", x => distance = x, this, 2000.0);
            valid &= ConfigNodeUtil.ParseValue<float>(configNode, "updateFrequency", x => updateFrequency = x, this, Rendezvous.DEFAULT_UPDATE_FREQUENCY, x => Validation.GT(x, 0.0f));

            return valid;
        }

        private bool ValidateVesselList(List<VesselIdentifier> vessels)
        {
            bool valid = true;

            if (parent is VesselParameterGroupFactory)
            {
                if (vessels.Count > 1)
                {
                    LoggingUtil.LogError(this, "{0}: When used under a VesselParameterGroup, no more than one vessel may be specified for the Rendezvous parameter.", ErrorPrefix());
                    valid = false;
                }
            }
            else
            {
                if (vessels.Count == 0)
                {
                    LoggingUtil.LogError(this, "{0}: Need at least one vessel specified for the Rendezvous parameter.", ErrorPrefix());
                    valid = false;
                }
                if (vessels.Count > 2)
                {
                    LoggingUtil.LogError(this, "{0}: Cannot specify more than two vessels for the Rendezvous parameter.", ErrorPrefix());
                    valid = false;
                }
            }

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new Rendezvous(vessels.Select<VesselIdentifier, string>(vi => vi.identifier), distance, title, updateFrequency);
        }
    }
}
