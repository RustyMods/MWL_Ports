using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ServerSync;
using UnityEngine;

namespace MWL_Ports;

[PublicAPI]
public class ShipmentManager : MonoBehaviour
{
    public static ShipmentManager? instance;
    internal static double TransitDuration = 10.0;
    private static string ShipmentFileName = "shipments.json";
    private static string MWL_FolderName = "MWL_Ports";
    private static string MWL_FolderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + MWL_FolderName;
    private static string ShipmentFilePath = MWL_FolderPath + Path.DirectorySeparatorChar + ShipmentFileName;
    
    private static CustomSyncedValue<string>? ServerSyncedShipments;
    internal static Dictionary<string, Shipment> Shipments = new Dictionary<string, Shipment>();
    
    private float m_checkTransitTimer;
    private float m_checkTransitInterval = 1f;
    
    private float m_sendPortsInterval = 10f;
    private static readonly List<ZDO> TempZDO = new(); // server side
    private static HashSet<ZDO> TempZDOHashSet = new();
    private static readonly List<string> PrefabsToSearch = new()
    {
        "MWL_Port"
    };

    public void Awake()
    {
        instance = this;
        ServerSyncedShipments = new CustomSyncedValue<string>(MWL_PortsPlugin.ConfigSync, "MWL_SyncedShipments", "");
        ServerSyncedShipments.ValueChanged += OnClientUpdateShipments;
        ReadLocalFile();
    }

    public void Update()
    {
        float dt = Time.deltaTime;
        CheckTransit(dt);
    }

    public void OnDestroy()
    {
        // clean up if destroyed for some reason
        instance = null;
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        [UsedImplicitly]
        private static void Postfix()
        {
            if (instance == null) return;
            instance.InitCoroutine();
        }
    }
    
    public void InitCoroutine() => StartCoroutine(SendPortsToClients());

    public IEnumerator SendPortsToClients()
    {
        for (;;)
        {
            if (Game.instance && ZDOMan.instance != null && ZNet.instance && ZNet.instance.IsServer())
            {
                TempZDO.Clear();
                foreach (string prefab in PrefabsToSearch)
                {
                    int index = 0;
                    while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, TempZDO, ref index)) yield return null;
                }

                foreach (ZDO zdo in TempZDO)
                {
                    ZDOMan.instance.ForceSendZDO(zdo.m_uid);
                }
            }
            yield return new WaitForSeconds(m_sendPortsInterval);
        }
    }

    public void CheckTransit(float dt)
    {
        if (!ZNet.instance) return;
        m_checkTransitTimer += dt;
        if (m_checkTransitTimer < m_checkTransitInterval) return;
        foreach (Shipment shipment in Shipments.Values)
        {
            shipment.CheckTransit();
        }
    }

    public void OnClientUpdateShipments()
    {
        if (ServerSyncedShipments == null) return;
        if (!ZNet.instance || ZNet.instance.IsServer()) return;
        if (string.IsNullOrEmpty(ServerSyncedShipments.Value)) return;
        Dictionary<string, Shipment>? data = JsonConvert.DeserializeObject<Dictionary<string, Shipment>>(ServerSyncedShipments.Value);
        if (data == null) return;
        Shipments = data;
    }

    public static HashSet<ZDO> GetPorts()
    {
        List<ZDO> ports = new List<ZDO>();
        foreach (string prefab in PrefabsToSearch)
        {
            int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, ports, ref index))
            {
            }
        }
        TempZDOHashSet = new HashSet<ZDO>(ports);
        return TempZDOHashSet;
    }
    
    public static HashSet<ZDO> GetTempPorts() => TempZDOHashSet;

    public static List<Shipment> GetShipments(string portID)
    {
        List<Shipment> shipments = new List<Shipment>();
        foreach (var shipment in Shipments.Values)
        {
            if (shipment.OriginPortID != portID) continue;
            shipments.Add(shipment);
        }
        return shipments;
    }

    public static List<Shipment> GetDeliveries(string portID)
    {
        List<Shipment> shipments = new List<Shipment>();
        foreach (var shipment in Shipments.Values)
        {
            if (shipment.DestinationPortID != portID) continue;
            shipments.Add(shipment);
        }
        return shipments;
    }
    
    public static void ReadLocalFile()
    {
        if (!Directory.Exists(MWL_FolderPath)) Directory.CreateDirectory(MWL_FolderPath);
        if (!File.Exists(ShipmentFilePath)) return;
        Dictionary<string, Shipment>? data = JsonConvert.DeserializeObject<Dictionary<string, Shipment>>(File.ReadAllText(ShipmentFilePath));
        if (data == null) return;
        Shipments = data;
    }

    public static void UpdateShipments()
    {
        if (ServerSyncedShipments == null) return;
        if (!ZNet.instance || !ZNet.instance.IsServer()) return;
        string data = JsonConvert.SerializeObject(Shipments, Formatting.Indented);
        ServerSyncedShipments.Value = data;
        if (!Directory.Exists(MWL_FolderPath)) Directory.CreateDirectory(MWL_FolderPath);
        File.WriteAllText(ShipmentFilePath, data);
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class RegisterCustomRPC
    {
        [UsedImplicitly]
        private static void Postfix()
        {
            ZRoutedRpc.instance.Register<string, string>(nameof(RPC_ServerReceiveShipment), RPC_ServerReceiveShipment);
            ZRoutedRpc.instance.Register<string, string>(nameof(RPC_ServerShipmentCollected), RPC_ServerShipmentCollected);
        }
    }

    public static void RPC_ServerReceiveShipment(long sender, string senderName, string serializedShipment)
    {
        if (instance == null) return;
        Shipment newShipment = new Shipment(serializedShipment);
        Debug.LogWarning(newShipment.IsValid
            ? $"Shipment from {senderName} registered!"
            : $"Shipment from {senderName} is invalid");
        if (newShipment.IsValid) UpdateShipments();
    }

    public static void RPC_ServerShipmentCollected(long sender, string senderName, string shipmentID)
    {
        if (instance == null) return;
        if (!Shipments.Remove(shipmentID))
        {
            Debug.LogWarning($"{senderName} said that they collected shipment {shipmentID}, but not found in dictionary");
        }
        else
        {
            Debug.LogWarning($"{senderName} said collected shipment {shipmentID}, removing from dictionary");
            UpdateShipments();
        }
    }

    public struct PortID
    {
        public string Name;
        public string Guid;

        public PortID(string guid, string name)
        {
            Name = name;
            Guid = guid;
        }
    }
}