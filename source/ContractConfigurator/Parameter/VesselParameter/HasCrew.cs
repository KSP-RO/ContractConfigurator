using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Parameters;
using Experience;
using KSP.Localization;

namespace ContractConfigurator.Parameters
{
    /// <summary>
    /// Parameter for checking whether a vessel has a crew.
    /// </summary>
    public class HasCrew : VesselParameter, IKerbalNameStorage
    {
        protected string trait { get; set; }
        protected int minCrew { get; set; }
        protected int maxCrew { get; set; }
        protected int minExperience { get; set; }
        protected int maxExperience { get; set; }
        protected bool crewOnly { get; set; }
        protected List<Kerbal> kerbals = new List<Kerbal>();
        protected List<Kerbal> excludeKerbals = new List<Kerbal>();

        public HasCrew()
            : base(null)
        {
        }

        public HasCrew(string title, IEnumerable<Kerbal> kerbals, IEnumerable<Kerbal> excludeKerbals, string trait, int minCrew = 1, int maxCrew = int.MaxValue, int minExperience = 0, int maxExperience = 5, bool crewOnly = false)
            : base(title)
        {
            if (minCrew > maxCrew)
            {
                minCrew = maxCrew;
            }

            this.minCrew = minCrew;
            this.maxCrew = maxCrew;
            this.minExperience = minExperience;
            this.maxExperience = maxExperience;
            this.trait = trait;
            this.kerbals = kerbals == null ? new List<Kerbal>() : kerbals.ToList();
            this.excludeKerbals = excludeKerbals == null ? new List<Kerbal>() : excludeKerbals.ToList();
            this.crewOnly = crewOnly;

            CreateDelegates();
        }

        protected override string GetParameterTitle()
        {
            string output = null;
            if (string.IsNullOrEmpty(title))
            {
                if (kerbals.Count == 0 && (state == ParameterState.Complete || ParameterCount == 1))
                {
                    if (ParameterCount == 1)
                    {
                        hideChildren = true;
                    }

                    string countStr = BuildCrewCountString();
                    string traitStr = BuildTraitString();
                    string experienceStr = BuildExperienceString();
                    output = CombineStrings(countStr, traitStr, experienceStr);
                }
                else
                {
                    if (state == ParameterState.Complete || ParameterCount == 1)
                    {
                        if (ParameterCount == 1)
                        {
                            hideChildren = true;
                        }

                        output = Localizer.Format("#cc.param.HasCrew.1", ParameterDelegate<ProtoCrewMember>.GetDelegateText(this));
                    }
                    else
                    {
                        output = Localizer.GetStringByTag("#cc.param.HasCrew");
                    }
                }
            }
            else
            {
                output = title;
            }
            return output;
        }

        private string BuildCrewCountString()
        {
            if (maxCrew == 0)
            {
                return Localizer.GetStringByTag("#cc.param.HasCrew.unmanned");
            }
            else if (maxCrew == int.MaxValue)
            {
                return Localizer.Format("#cc.param.count.atLeast", minCrew);
            }
            else if (minCrew == 0)
            {
                return Localizer.Format("#cc.param.count.atMost", maxCrew);
            }
            else if (minCrew == maxCrew)
            {
                return Localizer.Format("#cc.param.count.exact", minCrew);
            }
            else
            {
                return Localizer.Format("#cc.param.count.between", minCrew, maxCrew);
            }
        }

        private string BuildTraitString()
        {
            if (!String.IsNullOrEmpty(trait))
            {
                return Localizer.Format("#cc.param.HasAstronaut.trait", LocalizationUtil.TraitTitle(trait));
            }

            return null;
        }

        private string BuildExperienceString()
        {
            if (minExperience != 0 && maxExperience != 5)
            {
                if (minExperience == 0)
                {
                    return Localizer.Format("#cc.param.HasAstronaut.experience.atMost", maxExperience);
                }
                else if (maxExperience == 5)
                {
                    return Localizer.Format("#cc.param.HasAstronaut.experience.atLeast", minExperience);
                }
                else if (minExperience == maxExperience)
                {
                    return Localizer.Format("#cc.param.HasAstronaut.experience.exact", minExperience);
                }
                else
                {
                    return Localizer.Format("#cc.param.HasAstronaut.experience.between", minExperience, maxExperience);
                }
            }

            return null;
        }

        private static string CombineStrings(string countStr, string traitStr, string experienceStr)
        {
            if (String.IsNullOrEmpty(traitStr))
            {
                if (String.IsNullOrEmpty(experienceStr))
                {
                    return Localizer.Format("#cc.param.HasCrew.1", countStr);
                }
                else
                {
                    return Localizer.Format("#cc.param.HasCrew.2", countStr, experienceStr);
                }
            }
            else if (String.IsNullOrEmpty(experienceStr))
            {
                return Localizer.Format("#cc.param.HasCrew.2", countStr, traitStr);
            }
            else
            {
                return Localizer.Format("#cc.param.HasCrew.3", countStr, traitStr, experienceStr);
            }
        }

        protected override string GetParameterTitlePreview(out bool hideChildren)
        {
            if (!string.IsNullOrEmpty(title))
            {
                hideChildren = true;
                return title;
            }

            hideChildren = true;
            string countStr = BuildCrewCountString();
            string traitStr = BuildTraitString();
            string experienceStr = BuildExperienceString();
            return CombineStrings(countStr, traitStr, experienceStr);
        }

        protected void CreateDelegates()
        {
            // Experience trait
            if (!string.IsNullOrEmpty(trait))
            {
                AddParameter(new ParameterDelegate<ProtoCrewMember>(Localizer.Format("#cc.param.HasCrew.trait", LocalizationUtil.TraitTitle(trait)),
                    cm => cm.experienceTrait.Config.Name == trait));
            }

            // Filter for experience
            if (minExperience != 0 || maxExperience != 5)
            {
                string countText;
                if (minExperience == 0)
                {
                    countText = Localizer.Format("#cc.param.count.atMost.num", maxExperience);
                }
                else if (maxExperience == 5)
                {
                    countText = Localizer.Format("#cc.param.count.atLeast.num", minExperience);
                }
                else if (minExperience == maxExperience)
                {
                    countText = Localizer.Format("#cc.param.count.exact.num", minExperience);
                }
                else
                {
                    countText = Localizer.Format("#cc.param.count.between.num", minExperience, maxExperience);
                }

                AddParameter(new ParameterDelegate<ProtoCrewMember>(Localizer.Format("#cc.param.HasCrew.experience", countText),
                    cm => cm.experienceLevel >= minExperience && cm.experienceLevel <= maxExperience));
            }

            // Validate count
            if (kerbals.Count == 0)
            {
                // Special handling for unmanned
                if (minCrew == 0 && maxCrew == 0)
                {
                    AddParameter(new ParameterDelegate<ProtoCrewMember>(Localizer.GetStringByTag("#cc.param.HasCrew.unmanned"), pcm => true, ParameterDelegateMatchType.NONE));
                }
                else
                {
                    AddParameter(new CountParameterDelegate<ProtoCrewMember>(minCrew, maxCrew));
                }
            }

            // Validate specific kerbals
            foreach (Kerbal kerbal in kerbals)
            {
                AddParameter(new ParameterDelegate<ProtoCrewMember>(Localizer.Format("#cc.param.HasCrew.specific", kerbal.name),
                    pcm => pcm == kerbal.pcm, ParameterDelegateMatchType.VALIDATE));
            }
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            if (trait != null)
            {
                node.AddValue("trait", trait);
            }
            node.AddValue("minCrew", minCrew);
            node.AddValue("maxCrew", maxCrew);
            node.AddValue("minExperience", minExperience);
            node.AddValue("maxExperience", maxExperience);
            node.AddValue("crewOnly", crewOnly);
            foreach (Kerbal kerbal in kerbals)
            {
                ConfigNode kerbalNode = new ConfigNode("KERBAL");
                node.AddNode(kerbalNode);

                kerbal.Save(kerbalNode);
            }
            foreach (Kerbal kerbal in excludeKerbals)
            {
                ConfigNode kerbalNode = new ConfigNode("KERBAL_EXCLUDE");
                node.AddNode(kerbalNode);

                kerbal.Save(kerbalNode);
            }
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            try
            {
                base.OnParameterLoad(node);
                trait = ConfigNodeUtil.ParseValue<string>(node, "trait", (string)null);
                minExperience = Convert.ToInt32(node.GetValue("minExperience"));
                maxExperience = Convert.ToInt32(node.GetValue("maxExperience"));
                minCrew = Convert.ToInt32(node.GetValue("minCrew"));
                maxCrew = Convert.ToInt32(node.GetValue("maxCrew"));
                string cO = node.GetValue("crewOnly");
                crewOnly = cO != null && cO.ToLowerInvariant() == "true";

                foreach (ConfigNode kerbalNode in node.GetNodes("KERBAL"))
                {
                    kerbals.Add(Kerbal.Load(kerbalNode));
                }
                foreach (ConfigNode kerbalNode in node.GetNodes("KERBAL_EXCLUDE"))
                {
                    excludeKerbals.Add(Kerbal.Load(kerbalNode));
                }

                CreateDelegates();
            }
            finally
            {
                ParameterDelegate<Vessel>.OnDelegateContainerLoad(node);
            }
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            GameEvents.onCrewTransferred.Add(OnCrewTransferred);
            GameEvents.onVesselWasModified.Add(OnVesselWasModified);
            GameEvents.Contract.onAccepted.Add(OnContractAccepted);
            ContractConfigurator.OnVesselCrewDie.Add(OnVesselCrewDie);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onCrewTransferred.Remove(OnCrewTransferred);
            GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            GameEvents.Contract.onAccepted.Remove(OnContractAccepted);
            ContractConfigurator.OnVesselCrewDie.Remove(OnVesselCrewDie);
        }

        private void OnContractAccepted(Contract c)
        {
            if (c != Root)
            {
                return;
            }

            foreach (Kerbal kerbal in kerbals.Union(excludeKerbals))
            {
                // Instantiate the kerbals if necessary
                if (kerbal.pcm == null)
                {
                    kerbal.GenerateKerbal();
                }
            }
        }

        private void OnVesselCrewDie(Vessel vessel, ProtoCrewMember pcm)
        {
            CheckVessel(vessel);
        }

        protected override void OnPartAttach(GameEvents.HostTargetAction<Part, Part> e)
        {
            base.OnPartAttach(e);
            CheckVessel(FlightGlobals.ActiveVessel);
        }

        protected void OnVesselWasModified(Vessel v)
        {
            LoggingUtil.LogVerbose(this, "OnVesselWasModified: {0}", v);
            CheckVessel(v);
        }

        protected override void OnCrewTransferred(GameEvents.HostedFromToAction<ProtoCrewMember, Part> a)
        {
            base.OnCrewTransferred(a);

            // Check both, as the Kerbal/ship swap spots depending on whether the vessel is
            // incoming or outgoing
            CheckVessel(a.from.vessel);
            CheckVessel(a.to.vessel);
        }

        /// <summary>
        /// Whether this vessel meets the parameter condition.
        /// </summary>
        /// <param name="vessel">The vessel to check</param>
        /// <returns>Whether the vessel meets the condition</returns>
        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            LoggingUtil.LogVerbose(this, "Checking VesselMeetsCondition: {0}", vessel.id);

            // If we're a VesselParameterGroup child, only do actual state change if we're the tracked vessel
            bool checkOnly = false;
            if (Parent is VesselParameterGroup)
            {
                checkOnly = ((VesselParameterGroup)Parent).TrackedVessel != vessel;
            }

            return ParameterDelegate<ProtoCrewMember>.CheckChildConditions(this, GetVesselCrew(vessel, !crewOnly && maxCrew == int.MaxValue), checkOnly);
        }

        /// <summary>
        /// Gets the vessel crew and works for EVAs as well
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        protected IEnumerable<ProtoCrewMember> GetVesselCrew(Vessel v, bool includeTourists)
        {
            if (v == null)
            {
                yield break;
            }

            // EVA vessel
            if (v.vesselType == VesselType.EVA)
            {
                if (v.parts == null)
                {
                    yield break;
                }

                foreach (Part p in v.parts)
                {
                    foreach (ProtoCrewMember pcm in p.protoModuleCrew)
                    {
                        if (!excludeKerbals.Any(k => k.pcm == pcm))
                        {
                            yield return pcm;
                        }
                    }
                }
            }
            else
            {
                // Vessel with crew
                foreach (ProtoCrewMember pcm in v.GetVesselCrew())
                {
                    if (!excludeKerbals.Any(k => k.pcm == pcm) && (includeTourists || pcm.type == ProtoCrewMember.KerbalType.Crew))
                    {
                        yield return pcm;
                    }
                }
            }
        }

        public IEnumerable<string> KerbalNames()
        {
            foreach (Kerbal kerbal in kerbals)
            {
                yield return kerbal.name;
            }
        }
    }
}
