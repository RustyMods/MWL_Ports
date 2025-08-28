using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ServerSync;
using UnityEngine;

namespace MWL_Ports;

[HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.GenerateLocationsIfNeeded))]
public static class ZoneSystem_GenerateLocationsIfNeeded_Patch
{
    [UsedImplicitly]
    private static void Postfix()
    {
        if (PortManager.instance == null) return;
        PortManager.instance.Invoke(nameof(PortManager.UpdatePortLocations), 10f);
    }
}
public class PortManager : MonoBehaviour
{
    private static readonly CustomSyncedValue<string> ServerSyncedPortLocations = new(MWL_PortsPlugin.ConfigSync, "ServerPortLocations", "");

    private static PortLocations? locations;
    public static PortManager? instance;

    public void Awake()
    {
        instance = this;
        ServerSyncedPortLocations.ValueChanged += () =>
        {
            if (!ZNet.instance || ZNet.instance.IsServer()) return;
            if (string.IsNullOrEmpty(ServerSyncedPortLocations.Value)) return;
            locations = new PortLocations(ServerSyncedPortLocations.Value);
        };
    }

    public static List<Vector3> GetPortLocations()
    {
        if (instance == null || locations == null) return new List<Vector3>();
        return locations.ToVector();
    }

    public void UpdatePortLocations()
    {
        if (!ZNet.instance || !ZNet.instance.IsServer() || !ZoneSystem.instance) return;
        var allLocations = ZoneSystem.instance.GetLocationList();
        List<ZoneSystem.LocationInstance> ports = allLocations.Where(location => location.m_location.m_group == "MWL_Ports").ToList();
        if (ports.Count == 0) return;
        locations = new PortLocations(ports);
        ServerSyncedPortLocations.Value = locations.ToJson();
    }

    [Serializable]
    public class PortLocations
    {
        public List<SerializedVector> positions = new();

        public PortLocations(List<ZoneSystem.LocationInstance> ports)
        {
            foreach(var port in ports) positions.Add(new SerializedVector(port.m_position));
        }

        public PortLocations(string json)
        {
            var data = JsonConvert.DeserializeObject<PortLocations>(json);
            if (data == null) return;
            positions = data.positions;
        }

        public string ToJson() => JsonConvert.SerializeObject(this);

        public List<Vector3> ToVector()
        {
            List<Vector3> output = new();
            foreach (var position in positions) output.Add(position.ToVector3());
            return output;
        }
    }

    [Serializable]
    public struct SerializedVector
    {
        public float x;
        public float y;
        public float z;

        public SerializedVector(Vector3 vector)
        {
            x = vector.x;
            y = vector.y;
            z = vector.z;
        }

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }
}