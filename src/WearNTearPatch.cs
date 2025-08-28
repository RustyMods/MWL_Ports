using HarmonyLib;
using JetBrains.Annotations;
using MWL_Ports.Managers;

namespace MWL_Ports;

[HarmonyPatch(typeof(WearNTear),nameof(WearNTear.RPC_Damage))]
public class WearNTearPatch
{
    [UsedImplicitly]
    private static bool Prefix(WearNTear __instance, HitData hit)
    {
        foreach (Location? location in Location.s_allLocations)
        {
            if (!location.IsInside(__instance.transform.position, 50f, true)) continue;
            if (Helpers.GetNormalizedName(location.name) != "MWL_Port_Location_Large") continue;
            return false;
        }
        return true;
    }
}