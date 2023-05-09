using System;
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
    /// Parameter for checking that a vessel is recovered.
    /// </summary>
    public class NoStaging : VesselParameter
    {
        protected HashSet<Vessel> staged = new HashSet<Vessel>();
        protected HashSet<Vessel> possibleStages = new HashSet<Vessel>();
        protected float lastPartJointTime;
        protected float lastUndockTime;

        public NoStaging()
            : this(false, null)
        {
        }

        public NoStaging(bool failContract, string title)
            : base(title)
        {
            this.title = title != null ? title : Localizer.GetStringByTag("#cc.param.NoStaging");

            failWhenUnmet = true;
            fakeFailures = !failContract;
            disableOnStateChange = false;

            state = ParameterState.Complete;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            ConfigNode stagedNode = node.AddNode("STAGED_VESSELS");
            foreach (Vessel v in staged)
            {
                stagedNode.AddValue("vessel", v.id);
            }
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            staged = new HashSet<Vessel>(ConfigNodeUtil.ParseValue<List<Vessel>>(node.GetNode("STAGED_VESSELS"), "vessel", new List<Vessel>()));
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            GameEvents.onStageSeparation.Add(OnStageSeparation);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onStageSeparation.Remove(OnStageSeparation);
        }

        protected void OnStageSeparation(EventReport er)
        {
            LoggingUtil.LogVerbose(this, "OnStageSeparation");

            // We have a valid stage seperation
            if (lastPartJointTime == UnityEngine.Time.fixedTime)
            {
                foreach (Vessel v in possibleStages)
                {
                    // Add to staged list
                    staged.Add(v);

                    // Force a vessel check
                    CheckVessel(v);
                }
            }
        }

        protected override void OnPartUndock(Part part)
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR || part?.vessel == null)
            {
                return;
            }

            LoggingUtil.LogVerbose(this, "OnPartUndock");

            staged.Add(part.vessel);
            lastUndockTime = UnityEngine.Time.fixedTime;

            base.OnPartUndock(part);
        }

        protected override void OnPartDeCouple(Part part)
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR || part?.vessel == null)
            {
                return;
            }

            LoggingUtil.LogVerbose(this, "OnPartDeCouple");

            possibleStages.Clear();
            possibleStages.Add(part.vessel);
            lastPartJointTime = UnityEngine.Time.fixedTime;

            // Vessel check happens here
            base.OnPartDeCouple(part);
        }

        protected override void OnVesselCreate(Vessel v)
        {
            LoggingUtil.LogVerbose(this, "OnVesselCreate");
            if (lastPartJointTime == UnityEngine.Time.fixedTime)
            {
                possibleStages.Add(v);
            }
            else if (lastUndockTime == UnityEngine.Time.fixedTime)
            {
                // For undocking, treat as a confirmed staging as this is the last event we'll see
                staged.Add(v);
            }

            base.OnVesselCreate(v);
        }

        /// <summary>
        /// Whether this vessel meets the parameter condition.
        /// </summary>
        /// <param name="vessel">The vessel to check</param>
        /// <returns>Whether the vessel meets the condition</returns>
        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            LoggingUtil.LogVerbose(this, "Checking VesselMeetsCondition: {0}", vessel.id);

            return !staged.Contains(vessel);
        }
    }
}
