using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace MWL_Ports;

// remove this if you do not want minimap icons, or do not want minimap icons to show port name
[HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateLocationPins))]
public static class Minimap_UpdateLocationPins_Patch
{
    [UsedImplicitly]
    private static bool Prefix(Minimap __instance, float dt)
    {
        __instance.m_updateLocationsTimer -= dt;
        if (__instance.m_updateLocationsTimer > 0.0) return false;
        __instance.m_updateLocationsTimer = 5f;
        Dictionary<Vector3, string> icons = new Dictionary<Vector3, string>();
        ZoneSystem.instance.GetLocationIcons(icons);
        bool flag = false;
        while (!flag)
        {
            flag = true;
            foreach (KeyValuePair<Vector3, Minimap.PinData> locationPin in __instance.m_locationPins)
            {
                if (icons.ContainsKey(locationPin.Key)) continue;
                ZLog.DevLog("Minimap: Removing location " + locationPin.Value.m_name);
                __instance.RemovePin(locationPin.Value);
                __instance.m_locationPins.Remove(locationPin.Key);
                flag = false;
                break;
            }
        }

        HashSet<ZDO> ports = ShipmentManager.GetTempPorts(); // I do this, to make it more performant, instead of iterating every time
        if (ports.Count <= 0) ports = ShipmentManager.GetPorts(); // If empty, then iterate, cache in TempPorts
        // TempPorts get updated as soon as player interacts with any port
        var portNames = new Dictionary<Vector3, string>();
        foreach (ZDO? port in ports)
        {
            var name = port.GetString(Port.PortVars.Name);
            var pos = port.GetPosition();
            if (string.IsNullOrEmpty(name)) continue;
            portNames[Quantize(pos)] = name;
        }
        
        foreach (KeyValuePair<Vector3, string> keyValuePair in icons)
        {
            if (__instance.m_locationPins.ContainsKey(keyValuePair.Key)) continue;
            string locationName = keyValuePair.Value;
            string? portName = "Port";
            if (portNames.TryGetValue(Quantize(keyValuePair.Key), out var name))
            {
                portName = name;
            }
            Sprite? locationIcon = __instance.GetLocationIcon(locationName);
            if (locationIcon != null)
            {
                bool IsPort = locationName == "MWL_Port_Location";
                Minimap.PinData? pinData = __instance.AddPin(keyValuePair.Key, Minimap.PinType.None, IsPort ? portName : "", false, false);
                pinData.m_icon = locationIcon;
                if (IsPort)
                {
                    ZLog.DevLog("Minimap: Adding port location " + keyValuePair.Key);
                }
                else
                {
                    pinData.m_doubleSize = true;
                    ZLog.Log("Minimap: Adding unique location " + keyValuePair.Key);
                }
                __instance.m_locationPins.Add(keyValuePair.Key, pinData);
            }
        }
        return false;
    }
    
    private static Vector3 Quantize(Vector3 v, float precision = 1f)
    {
        return new Vector3(
            Mathf.Round(v.x / precision) * precision,
            Mathf.Round(v.y / precision) * precision,
            Mathf.Round(v.z / precision) * precision
        );
    }
}