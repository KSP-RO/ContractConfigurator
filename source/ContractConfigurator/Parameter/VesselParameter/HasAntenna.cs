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
    /// Parameter for checking the relay or transmit antenna power of a vessel
    /// </summary>
    public class HasAntenna : VesselParameter
    {
        public enum AntennaType
		{
			RELAY,
			TRANSMIT,
			EITHER
		};

		protected double minAntennaPower { get; set; }
        protected double maxAntennaPower { get; set; }
		protected AntennaType antennaType { get; set; }
        protected float updateFrequency { get; set; }

        private float lastUpdate = 0.0f;
        internal const float DEFAULT_UPDATE_FREQUENCY = 0.25f;

        public HasAntenna()
            : this(DEFAULT_UPDATE_FREQUENCY, 0.0)
        {
        }

		public HasAntenna(float updateFrequency, double minAntennaPower = 0.0, double maxAntennaPower = double.MaxValue, AntennaType antennaType = AntennaType.TRANSMIT, string title = null)
            : base(title)
        {
            this.minAntennaPower = minAntennaPower;
            this.maxAntennaPower = maxAntennaPower;
			this.antennaType = antennaType;
            this.updateFrequency = updateFrequency;

            if (title == null)
            {
                string countStr;
                if (maxAntennaPower == double.MaxValue)
                {
                    countStr = Localizer.Format("#cc.param.count.atLeast.num", KSPUtil.PrintSI(minAntennaPower,""));
                }
                else if (minAntennaPower == 0.0)
                {
                    countStr = Localizer.Format("#cc.param.count.atMost.num", KSPUtil.PrintSI(maxAntennaPower,""));
                }
                else
                {
                    countStr = Localizer.Format("#cc.param.count.between.num", KSPUtil.PrintSI(minAntennaPower, ""), KSPUtil.PrintSI(maxAntennaPower, ""));
                }

                switch (antennaType)
                {
                    case AntennaType.RELAY: this.title = Localizer.Format("#cc.param.HasAntenna.relay", countStr); break;
                    case AntennaType.TRANSMIT: this.title = Localizer.Format("#cc.param.HasAntenna.transmit", countStr); break;
                    case AntennaType.EITHER: this.title = Localizer.Format("#cc.param.HasAntenna.either", countStr); break;
                }
            }
            else
            {
                this.title = title;
            }
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("updateFrequency", updateFrequency);
            node.AddValue("minAntennaPower", minAntennaPower);
            if (maxAntennaPower != double.MaxValue)
            {
                node.AddValue("maxAntennaPower", maxAntennaPower);
            }
			node.AddValue("antennaType", antennaType);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
            minAntennaPower = Convert.ToDouble(node.GetValue("minAntennaPower"));
            maxAntennaPower = node.HasValue("maxAntennaPower") ? Convert.ToDouble(node.GetValue("maxAntennaPower")) : double.MaxValue;
			antennaType = ConfigNodeUtil.ParseValue<AntennaType>(node, "antennaType", AntennaType.TRANSMIT);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (UnityEngine.Time.fixedTime - lastUpdate > updateFrequency)
            {
                lastUpdate = UnityEngine.Time.fixedTime;
                CheckVessel(FlightGlobals.ActiveVessel);
            }
        }

        /// <summary>
        /// Whether this vessel meets the parameter condition.
        /// </summary>
        /// <param name="vessel">The vessel to check</param>
        /// <returns>Whether the vessel meets the condition</returns>
        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            LoggingUtil.LogVerbose(this, "Checking VesselMeetsCondition: {0}", vessel.id);
            double antennaPower = 0.0f;
            if (vessel.connection != null)
            {
                double relayAntennaPower = vessel.connection.Comm.antennaRelay.power;
                double transmitAntennaPower = vessel.connection.Comm.antennaTransmit.power;
                switch (antennaType)
                {
                    case AntennaType.RELAY: antennaPower = relayAntennaPower; break;
                    case AntennaType.TRANSMIT: antennaPower = transmitAntennaPower; break;
                    case AntennaType.EITHER: antennaPower = Math.Max(relayAntennaPower, transmitAntennaPower); break;
                }
            }
            return antennaPower >= minAntennaPower && antennaPower <= maxAntennaPower;
        }
    }
}
