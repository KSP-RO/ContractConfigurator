using Contracts;
using HarmonyLib;
using System;
using System.Collections;
using System.Threading.Tasks;

namespace ContractConfigurator.Harmony
{
    [HarmonyPatch(typeof(Contract))]
    internal class PatchContract
    {
        /// <summary>
        /// KSP's ContractSystem calls Contract.Generate() with State.Generated when filling the offered contracts.
        /// ConfiguredContract.Generate() always returns false for these calls (contractType is unset on fresh instances) so each attempt is a wasted allocation.
        /// Short-circuit here before Activator.CreateInstance is reached. The pre-loader uses State.Withdrawn, which is allowed through.
        /// </summary>
        /// <param name="__result"></param>
        /// <param name="contractType"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch("Generate", new Type[] { typeof(Type), typeof(Contract.ContractPrestige), typeof(int), typeof(Contract.State) })]
        internal static bool Prefix_Generate(ref Contract __result, Type contractType, Contract.State state)
        {
            if (contractType == typeof(ConfiguredContract) && state == Contract.State.Generated)
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
}
