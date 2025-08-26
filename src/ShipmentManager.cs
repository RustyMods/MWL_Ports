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
    public static ShipmentManager? instance;
    public static ConfigEntry<float> TransitDurationConfig = null!; 
    private static string ShipmentFileName = "shipments.dat";
    private static string MWL_FolderName = "MWL_Ports";
    private static string MWL_FolderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + MWL_FolderName;
    private static string GetFilePath(string worldName) => MWL_FolderPath + Path.DirectorySeparatorChar + worldName + "_" + ShipmentFileName;
    private const bool COMPRESS_DATA = true;
    private static readonly CustomSyncedValue<string> ServerPortPositions = new(MWL_PortsPlugin.ConfigSync, "MWL_ServerPortPositions", "");
    private static CustomSyncedValue<string>? ServerSyncedShipments;
    internal static Dictionary<string, Shipment> Shipments = new();
    private static readonly List<ZDO> TempZDO = new(); // server side
    private static HashSet<ZDO> TempZDOHashSet = new(); // client side
    public static readonly List<string> PrefabsToSearch = new();
    
    private float m_checkTransitTimer;
    private float m_checkTransitInterval = 1f;
    private float m_sendPortsInterval = 10f;

    public void Awake()
    {
        instance = this;
        // can move this like above
        // was not sure what kind of config sync you like to use
        // so you have options when to create your custom sync
        ServerSyncedShipments = new CustomSyncedValue<string>(MWL_PortsPlugin.ConfigSync, "MWL_SyncedShipments", "");
        ServerSyncedShipments.ValueChanged += OnClientUpdateShipments;
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

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        [UsedImplicitly]
        private static void Postfix(ZNet __instance)
        {
            if (!__instance.IsServer()) return;
            // read file once world is set, to get the world name
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
            // only the server should run this
            instance.InitCoroutine();
        }
    }
    
    public void InitCoroutine() => StartCoroutine(SendPortsToClients());

    public IEnumerator SendPortsToClients()
    {
        // only the server runs this operation
        // runs forever while game is active
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
        // everyone runs this
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
        // make sure that the server does not do this, since it is principle manager
        // no need to update twice
        if (!ZNet.instance || ZNet.instance.IsServer()) return;
        // when the client first connects to the server, and the server has no data
        // make sure that the value passed is not null
        if (string.IsNullOrEmpty(ServerSyncedShipments.Value)) return;
        Dictionary<string, Shipment>? data = JsonConvert.DeserializeObject<Dictionary<string, Shipment>>(ServerSyncedShipments.Value);
        if (data == null) return;
        Shipments = data;
    }

    public static HashSet<ZDO> GetPorts()
    {
        // client side
        // since we have the server (who is the principle manager of ZDOs) force send the ZDO of our ports
        // we can now search through our own ZDOMan for the relevant ZDOs
        // we use this system because we want access to the prefab ZDO
        // which contains the name, guid, etc... that we saved on it
        List<ZDO> ports = new List<ZDO>();
        foreach (string prefab in PrefabsToSearch)
        {
            int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, ports, ref index))
            {
            }
        }
        // cache the list, so we can use it elsewhere, without needing to iterate
        // data can be invalid, since prefab might be destroyed
        // so we only use it in special cases
        // for our case, to update minimap pins
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
        if (!ZNet.instance) return;
        if (!Directory.Exists(MWL_FolderPath)) Directory.CreateDirectory(MWL_FolderPath);
        // use world name as a prefix to the file name
        // to keep each unique between worlds
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
        {
            json = File.ReadAllText(path);
        }
        Dictionary<string, Shipment>? data = JsonConvert.DeserializeObject<Dictionary<string, Shipment>>(json);
        if (data == null) return;
        Shipments = data;
    }

    public static void UpdateShipments()
    {
        if (ServerSyncedShipments == null) return;
        if (!ZNet.instance || !ZNet.instance.IsServer()) return;
        // serialize entire dictionary of shipments
        string json = JsonConvert.SerializeObject(Shipments, Formatting.Indented);
        // share it with all the clients
        ServerSyncedShipments.Value = json;
        if (!Directory.Exists(MWL_FolderPath)) Directory.CreateDirectory(MWL_FolderPath);
        string path = GetFilePath(ZNet.m_world.m_name);
        // save it on disk
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
        {
            File.WriteAllText(path, json);
        }
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
        // client sends a JSON serialized shipment
        // parse it, add it
        // share updated dictionary with all clients
        // save to disk
        Shipment newShipment = new Shipment(serializedShipment);
        Debug.Log(newShipment.IsValid // make sure that the shipment is deserialized correctly
            ? $"Shipment from {senderName} registered!"
            : $"Shipment from {senderName} is invalid");
        if (newShipment.IsValid) UpdateShipments();
    }

    public static void RPC_ServerShipmentCollected(long sender, string senderName, string shipmentID)
    {
        // when client fully collects delivery
        // it will automatically send the shipment ID to the server
        // to remove from dictionary
        // and update all clients
        if (!Shipments.Remove(shipmentID))
        {
            Debug.LogWarning($"{senderName} said that they collected shipment {shipmentID}, but not found in dictionary");
        }
        else
        {
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