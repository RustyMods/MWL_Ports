using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace MWL_Ports;

public static class KnownPorts
{
    private static SerializedGuid? localKnownPorts;
    
    [HarmonyPatch(typeof(Player), nameof(Player.Load))]
    private static class Player_Load_Patch
    {
        [UsedImplicitly]
        private static void Postfix(Player __instance)
        {
            localKnownPorts = new SerializedGuid(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Save))]
    private static class Player_Save_Patch
    {
        [UsedImplicitly]
        private static void Prefix(Player __instance)
        {
            localKnownPorts?.Save(__instance);
        }
    }

    private class SerializedGuid
    {
        private const string CustomDataKey = "MWL_KnownPorts";
        private readonly List<string> GUIDs = new();
        public SerializedGuid(Player player)
        {
            if (!player.m_customData.TryGetValue(CustomDataKey, out string json)) return;
            if (string.IsNullOrEmpty(json)) return;
            List<string>? list = JsonConvert.DeserializeObject<List<string>>(json);
            if (list is null)
            {
                player.m_customData.Remove(CustomDataKey);
            }
            else
            {
                GUIDs = list;
            }
        }

        public void Save(Player player)
        {
            player.m_customData[CustomDataKey] = ToJson();
        }

        private string ToJson() => JsonConvert.SerializeObject(this);
        
        public bool IsKnownPort(ShipmentManager.PortID portID) => GUIDs.Contains(portID.GUID);

        public void Add(ShipmentManager.PortID portID)
        {
            if (IsKnownPort(portID)) return;
            GUIDs.Add(portID.GUID);
        }
    }

    public static bool IsKnownPort(this Player player, ShipmentManager.PortID portID)
    {
        localKnownPorts ??= new SerializedGuid(player);
        return localKnownPorts.IsKnownPort(portID);
    }

    public static void AddKnownPort(this Player player, ShipmentManager.PortID portID)
    {
        localKnownPorts ??= new SerializedGuid(player);
        localKnownPorts.Add(portID);
    }
}