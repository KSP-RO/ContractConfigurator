﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Parameters;
using KSP.Localization;

namespace ContractConfigurator.Parameters
{
    /// <summary>
    /// Parameter for checking that a vessel gets destroyed.
    /// </summary>
    public class VesselDestroyed : VesselParameter
    {
        protected bool mustImpactTerrain = false;
        private Dictionary<Vessel, bool> destroyedVessels = new Dictionary<Vessel, bool>();

        public VesselDestroyed()
            : base(null)
        {
        }

        public VesselDestroyed(string title, bool mustImpactTerrain)
            : base(title)
        {
            disableOnStateChange = true;
            this.mustImpactTerrain = mustImpactTerrain;
        }

        protected override string GetParameterTitle()
        {
            string output = "";
            if (string.IsNullOrEmpty(title))
            {
                output = Localizer.GetStringByTag("#cc.param.VesselDestroyed");
            }
            else
            {
                output = title;
            }
            return output;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("mustImpactTerrain", mustImpactTerrain);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            mustImpactTerrain = ConfigNodeUtil.ParseValue<bool?>(node, "mustImpactTerrain", (bool?)false).Value;
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            GameEvents.onCollision.Add(OnVesselAboutToBeDestroyed);
            GameEvents.onCrash.Add(OnVesselAboutToBeDestroyed);
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);
            GameEvents.onVesselWillDestroy.Add(OnVesselDestroy);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onCollision.Remove(OnVesselAboutToBeDestroyed);
            GameEvents.onCrash.Remove(OnVesselAboutToBeDestroyed);
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
            GameEvents.onVesselWillDestroy.Remove(OnVesselDestroy);
        }

        protected virtual void OnVesselAboutToBeDestroyed(EventReport report)
        {
            LoggingUtil.LogVerbose(this, "OnVesselAboutToBeDestroyed: {0}", report.msg);
            Vessel v = report.origin.vessel;
            if (v == null)
            {
                return;
            }

            // Check if we hit the ground
            if (mustImpactTerrain)
            {
                if (!(
                    report.other.ToLower().Contains(string.Intern("surface")) ||
                    report.other.ToLower().Contains(string.Intern("terrain")) ||
                    report.other.ToLower().Contains(v.mainBody.name.ToLower())))
                {
                    return;
                }
            }

            destroyedVessels[v] = true;
            CheckVessel(v);
        }

        protected virtual void OnVesselDestroy(Vessel v)
        {
            LoggingUtil.LogVerbose(this, "OnVesselDestroy: {0}", (v != null ? v.id.ToString() : "null"));

            if (!mustImpactTerrain)
            {
                destroyedVessels[v] = true;
                CheckVessel(v);
            }
        }

        public override bool IsIgnoredVesselType(VesselType vesselType)
        {
            if (vesselType == VesselType.Debris)
            {
                return false;
            }
            return base.IsIgnoredVesselType(vesselType);
        }

        /// <summary>
        /// Whether this vessel meets the parameter condition.
        /// </summary>
        /// <param name="vessel">The vessel to check</param>
        /// <returns>Always true</returns>
        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return destroyedVessels.ContainsKey(vessel) || vessel.state == Vessel.State.DEAD;
        }
    }
}
