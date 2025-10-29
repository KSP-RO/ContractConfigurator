using ContractConfigurator;
using ContractConfigurator.ExpressionParser;
using KSP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ContractConfigurator.Behaviour
{
    /// <summary>
    /// BehaviourFactory wrapper for SpawnVessel ContractBehaviour.
    /// </summary>
    public class SpawnVesselFactory : BehaviourFactory
    {
        protected List<SpawnVessel.ConditionDetail> conditions = new List<SpawnVessel.ConditionDetail>();
        protected SpawnVessel spawnVessel;

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            int index = 0;
            foreach (ConfigNode child in ConfigNodeUtil.GetChildNodes(configNode, "CONDITION"))
            {
                DataNode childDataNode = new DataNode("CONDITION_" + index++, dataNode, this);
                try
                {
                    ConfigNodeUtil.SetCurrentDataNode(childDataNode);
                    SpawnVessel.ConditionDetail cd = new SpawnVessel.ConditionDetail();
                    valid &= ConfigNodeUtil.ParseValue<SpawnVessel.ConditionDetail.Condition>(child, "condition", x => cd.condition = x, this);
                    valid &= ConfigNodeUtil.ParseValue<string>(child, "parameter", x => cd.parameter = x, this, "", x => ValidateMandatoryParameter(x, cd.condition));
                    conditions.Add(cd);
                }
                finally
                {
                    ConfigNodeUtil.SetCurrentDataNode(dataNode);
                }
            }

            // Call SpawnKerbal for load behaviour
            spawnVessel = SpawnVessel.Create(configNode, this);

            return valid && spawnVessel != null;
        }

        protected bool ValidateMandatoryParameter(string parameter, SpawnVessel.ConditionDetail.Condition condition)
        {
            if (parameter == null && (condition == SpawnVessel.ConditionDetail.Condition.PARAMETER_COMPLETED ||
                condition == SpawnVessel.ConditionDetail.Condition.PARAMETER_FAILED))
            {
                throw new ArgumentException("Required if condition is PARAMETER_COMPLETED or PARAMETER_FAILED.");
            }
            return true;
        }

        public override ContractBehaviour Generate(ConfiguredContract contract)
        {
            return new SpawnVessel(conditions, spawnVessel);
        }
    }
}
