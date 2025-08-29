using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ServerSync;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace MWL_Ports;

[PublicAPI]
public class ShipmentManager : MonoBehaviour
{
    public static ConfigEntry<float> TransitByDistance = null!; 
    public static ConfigEntry<string> CurrencyConfig = null!;
    public static ConfigEntry<float> TransitTime = null!;
    public static ConfigEntry<MWL_PortsPlugin.Toggle> OverrideTransitTime = null!;
    public static ConfigEntry<float> ExpirationTime = null!;
    public static ConfigEntry<MWL_PortsPlugin.Toggle> ExpirationEnabled = null!;
    private static CustomSyncedValue<string> ServerSyncedShipments = new (MWL_PortsPlugin.ConfigSync, "MWL_SyncedShipments", "");
    
    public static ShipmentManager? instance;
    
    private static string ShipmentFileName = "shipments.dat";
    private static string MWL_FolderName = "MWL_Ports";
    private static string MWL_FolderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + MWL_FolderName;
    private static string GetFilePath(string worldName) => MWL_FolderPath + Path.DirectorySeparatorChar + worldName + "_" + ShipmentFileName;
    private const bool COMPRESS_DATA = true;
    internal static Dictionary<string, Shipment> Shipments = new();
    private static readonly List<ZDO> TempZDO = new(); 
    private static HashSet<ZDO> TempZDOHashSet = new();
    public static readonly List<string> PrefabsToSearch = new();
    public static event Action? OnShipmentsUpdated;
    
    public static ItemDrop.ItemData? _currencyItem;
    public static ItemDrop.ItemData? CurrencyItem
    {
        get
        {
            if (_currencyItem != null) return _currencyItem;
            if (!ObjectDB.instance) return null;
            if (ObjectDB.instance.GetItemPrefab(CurrencyConfig.Value) is { } itemPrefab && itemPrefab.TryGetComponent(out ItemDrop component))
            {
                _currencyItem = component.m_itemData;
            }
            return _currencyItem;
        }
    }
    
    private static WaitForSeconds _wait = new (10f);
    private static Coroutine? _sendZDOCoroutine;
    
    private float m_checkTransitTimer;
    private float m_checkTransitInterval = 1f;

    public void Awake()
    {
        instance = this;
        ServerSyncedShipments.ValueChanged += OnClientUpdateShipments;
    }

    public void Update()
    {
        float dt = Time.deltaTime;
        CheckTransit(dt);
    }

    public void OnDestroy()
    {
        instance = null;
        if (_sendZDOCoroutine != null) StopCoroutine(_sendZDOCoroutine);
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNet __instance)
        {
            if (!__instance.IsServer()) return;
            ReadLocalFile();
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        [UsedImplicitly]
        private static void Postfix()
        {
            if (instance == null) return;
            if (!ZNet.instance || !ZNet.instance.IsServer()) return;
            instance.InitCoroutine();
        }
    }
    public void InitCoroutine()
    {
        _sendZDOCoroutine = StartCoroutine(SendPortsToClients());
    }

    public IEnumerator SendPortsToClients()
    {
        while (true)
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

            yield return _wait;
        }
    }

    public void CheckTransit(float dt)
    {
        if (!ZNet.instance) return;
        m_checkTransitTimer += dt;
        if (m_checkTransitTimer < m_checkTransitInterval) return;
        List<Shipment> expiredShipments = new();
        foreach (Shipment shipment in Shipments.Values)
        {
            shipment.CheckTransit();
            if (shipment.State is ShipmentState.Expired && ExpirationEnabled.Value is MWL_PortsPlugin.Toggle.On) expiredShipments.Add(shipment);
        }
        foreach (Shipment? shipment in expiredShipments)
        {
            Shipments.Remove(shipment.ShipmentID);
        }
    }

    public void OnClientUpdateShipments()
    {
        if (ZNet.instance && ZNet.instance.IsServer()) return;
        if (string.IsNullOrEmpty(ServerSyncedShipments.Value)) return;
        Dictionary<string, Shipment>? data = JsonConvert.DeserializeObject<Dictionary<string, Shipment>>(ServerSyncedShipments.Value);
        if (data == null) return;
        Shipments.Clear();
        Shipments.AddRange(data);
        OnShipmentsUpdated?.Invoke();
        MWL_PortsPlugin.MWL_PortsLogger.LogDebug($"Received {Shipments.Count} shipments from server");
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
        foreach (Shipment shipment in Shipments.Values)
        {
            if (shipment.OriginPortID != portID) continue;
            shipments.Add(shipment);
        }
        return shipments;
    }

    public static List<Shipment> GetDeliveries(string portID)
    {
        List<Shipment> shipments = new List<Shipment>();
        foreach (Shipment? shipment in Shipments.Values)
        {
            if (shipment.DestinationPortID != portID) continue;
            shipments.Add(shipment);
        }
        return shipments;
    }
    
    public static void ReadLocalFile()
    {
        if (!ZNet.instance) return;
        if (!Directory.Exists(MWL_FolderPath)) Directory.CreateDirectory(MWL_FolderPath);
        string path = GetFilePath(ZNet.m_world.m_name);
        if (!File.Exists(path)) return;
        string json;
        if (COMPRESS_DATA)
        {
            byte[] compressed = File.ReadAllBytes(path);
            using MemoryStream memory = new MemoryStream(compressed);
            using GZipStream zip = new GZipStream(memory, CompressionMode.Decompress);
            using StreamReader reader = new StreamReader(zip, Encoding.UTF8);
            json = reader.ReadToEnd();
        }
        else
#pragma warning disable CS0162 // Unreachable code detected
        {
            json = File.ReadAllText(path);
        }
#pragma warning restore CS0162 // Unreachable code detected
        Dictionary<string, Shipment>? data = JsonConvert.DeserializeObject<Dictionary<string, Shipment>>(json);
        if (data == null) return;
        Shipments = data;
        ServerSyncedShipments.Value = json;
    }

    public static void UpdateShipments()
    {
        if (!ZNet.instance || !ZNet.instance.IsServer()) return;
        string json = JsonConvert.SerializeObject(Shipments, Formatting.Indented);
        ServerSyncedShipments.Value = json;
        if (!Directory.Exists(MWL_FolderPath)) Directory.CreateDirectory(MWL_FolderPath);
        string path = GetFilePath(ZNet.m_world.m_name);
        if (COMPRESS_DATA)
        {
            byte[] rawBytes = Encoding.UTF8.GetBytes(json);
            using MemoryStream memory = new MemoryStream();
            using (GZipStream zip = new GZipStream(memory, CompressionLevel.Fastest))
            {
                zip.Write(rawBytes, 0, rawBytes.Length);
            }
            File.WriteAllBytes(path, memory.ToArray());
        }
        else
#pragma warning disable CS0162 // Unreachable code detected
        {
            File.WriteAllText(path, json);
        }
#pragma warning restore CS0162 // Unreachable code detected
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
        Shipment newShipment = new Shipment(serializedShipment);
        MWL_PortsPlugin.MWL_PortsLogger.LogDebug(newShipment.IsValid
            ? $"Shipment from {senderName} registered!"
            : $"Shipment from {senderName} is invalid");
        if (newShipment.IsValid) UpdateShipments();
    }

    public static void RPC_ServerShipmentCollected(long sender, string senderName, string shipmentID)
    {
        if (!Shipments.Remove(shipmentID))
        {
            MWL_PortsPlugin.MWL_PortsLogger.LogDebug($"{senderName} said that they collected shipment {shipmentID}, but not found in dictionary");
        }
        else
        {
            UpdateShipments();
        }
    }

    public struct PortID
    {
        public string Name;
        public string GUID;

        public PortID(string guid, string name)
        {
            Name = name;
            GUID = guid;
        }
    }
}