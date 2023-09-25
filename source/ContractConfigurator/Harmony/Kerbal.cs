using HarmonyLib;

namespace ContractConfigurator.Harmony
{
    [HarmonyPatch(typeof(global::Kerbal))]
    internal class PatchKerbal
    {
        [HarmonyPostfix]
        [HarmonyPatch("die")]
        internal static void Postfix_Die(global::Kerbal __instance)
        {
            if (__instance.InVessel == null) return;

            ContractConfigurator.OnVesselCrewDie.Fire(__instance.InVessel, __instance.protoCrewMember);
        }
    }
}
