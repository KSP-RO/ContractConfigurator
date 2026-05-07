using Contracts;
using HarmonyLib;
using System;
using System.Collections;
using static ContractConfigurator.LoggingUtil;

namespace ContractConfigurator.Harmony
{
    [HarmonyPatch(typeof(ContractSystem))]
    internal class PatchContractSystem
    {
        /// <summary>
        /// Workaround for stock ContractSystem "feature" where it doesn't call onContractsLoaded event
        /// and set ContractSystem.loaded to true on fresh saves.
        /// </summary>
        /// <param name="__result"></param>
        /// <param name="gameNode"></param>
        [HarmonyPostfix]
        [HarmonyPatch("OnLoadRoutine")]
        internal static void Postfix_OnLoadRoutine(ref IEnumerator __result, ConfigNode gameNode)
        {
            var enumerator = new PostfixEnumerator
            {
                enumerator = __result,
                postfixAction = () => PostfixAction(gameNode),
            };
            __result = enumerator.GetEnumerator();
        }

        internal static void PostfixAction(ConfigNode gameNode)
        {
            if (!ContractSystem.loaded)
            {
                ConfigNode node = gameNode.GetNode("CONTRACTS");
                if (node == null)
                {
                    Log(LogLevel.INFO, typeof(PatchContractSystem), "Fresh save detected, calling onContractsLoaded");
                    GameEvents.Contract.onContractsLoaded.Fire();
                    ContractSystem.loaded = true;
                }
            }
        }

        /// <summary>
        /// Make KSP count pre-loaded CC contracts toward the offered-contract quota
        /// </summary>
        /// <param name="__result"></param>
        /// <param name="difficulty"></param>
        [HarmonyPostfix]
        [HarmonyPatch("CountContracts")]
        internal static void Postfix_CountContracts(ref int __result, Contract.ContractPrestige difficulty)
        {
            if (ContractPreLoader.Instance == null) return;
            foreach (ConfiguredContract c in ContractPreLoader.Instance.PendingContracts())
            {
                if (c.Prestige == difficulty)
                    __result++;
            }
        }

        private class PostfixEnumerator : IEnumerable
        {
            public IEnumerator enumerator;
            public Action postfixAction;

            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

            public IEnumerator GetEnumerator()
            {
                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current;
                }
                postfixAction();
            }
        }
    }
}
