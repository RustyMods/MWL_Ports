using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace MWL_Ports;

[Serializable][PublicAPI][JsonObject(MemberSerialization.Fields)]
public class Shipment
{
    public string OriginPortName = string.Empty;
    public string DestinationPortName = string.Empty;
    public string OriginPortID = string.Empty; // Guid.ToString()
    public string DestinationPortID = string.Empty;
    public string ShipmentID = string.Empty;
    public ShipmentState State = ShipmentState.Pending;
    public double ArrivalTime;
    public List<ShipmentItem> Items = new List<ShipmentItem>();

    [NonSerialized] public bool IsValid = true;
    public Shipment(ShipmentManager.PortID originPort, ShipmentManager.PortID destinationPort)
    {
        OriginPortName = originPort.Name;
        OriginPortID = originPort.Guid;
        DestinationPortID = destinationPort.Guid;
        DestinationPortName = destinationPort.Name;
        ShipmentID = Guid.NewGuid().ToString();
        ArrivalTime = ZNet.instance.GetTimeSeconds() + ShipmentManager.TransitDurationConfig.Value;
    }

    public Shipment(string serializedShipment)
    {
        if (ShipmentManager.instance == null) return;
        var data = JsonConvert.DeserializeObject<Shipment>(serializedShipment);
        if (data == null)
        {
            Debug.LogWarning("[SERVER] Failed to parse shipment JSON");
            IsValid = false;
            return;
        }
        OriginPortName = data.OriginPortName;
        OriginPortID = data.OriginPortID;
        DestinationPortName = data.DestinationPortName;
        DestinationPortID = data.DestinationPortID;
        ShipmentID = data.ShipmentID;
        State = data.State;
        ArrivalTime = ZNet.instance.GetTimeSeconds() + ShipmentManager.TransitDurationConfig.Value;
        Items = data.Items;
        ShipmentManager.Shipments[ShipmentID] = this;
        ShipmentManager.UpdateShipments();
    }
    
    public void SendToServer()
    {
        if (ShipmentManager.instance == null) return;
        if (ZNet.instance && ZNet.instance.IsServer())
        {
            // if local client is server, then simply register to shipments, and update
            ShipmentManager.Shipments[ShipmentID]  = this;
            ShipmentManager.UpdateShipments();
        }
        else
        {
            // else send data to server to manage
            ZRoutedRpc.instance.InvokeRoutedRPC(nameof(ShipmentManager.RPC_ServerReceiveShipment), Player.m_localPlayer.GetPlayerName(), ToJson());
        }
    }

    public void OnCollected()
    {
        if (ShipmentManager.instance == null) return;
        if (ZNet.instance && ZNet.instance.IsServer())
        {
            if (!ShipmentManager.Shipments.Remove(ShipmentID))
            {
                Debug.LogWarning($"{Player.m_localPlayer.GetPlayerName()} said that they collected shipment {ShipmentID}, but not found in dictionary");
            }
            else
            {
                Debug.LogWarning($"{Player.m_localPlayer.GetPlayerName()} said collected shipment {ShipmentID}, removing from dictionary");
                ShipmentManager.UpdateShipments();
            }
        }
        else
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(nameof(ShipmentManager.RPC_ServerShipmentCollected), Player.m_localPlayer.GetPlayerName(), ShipmentID);
        }
    }

    public void CheckTransit()
    {
        State = ZNet.instance.GetTimeSeconds() < ArrivalTime ? ShipmentState.InTransit : ShipmentState.Delivered;
    }

    public double GetTimeToArrivalSeconds()
    {
        return Math.Max(ArrivalTime - ZNet.instance.GetTimeSeconds(), 0);
    }
    
    public string FormatTimeToArrival()
    {
        double totalSeconds = GetTimeToArrivalSeconds();
        if (totalSeconds < 0) totalSeconds = 0;

        int hours   = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);

        var parts = new List<string>();

        if (hours > 0)   parts.Add($"{hours}h");
        if (minutes > 0) parts.Add($"{minutes}m");
        if (seconds > 0) parts.Add($"{seconds}s");

        return string.Join(" ", parts);
    }
    
    public string ToJson() => JsonConvert.SerializeObject(this);

    public Sprite? GetIcon() => Minimap.instance.GetLocationIcon("MWL_Port_Location");
    
    public string GetTooltip()
    {
        string time = FormatTimeToArrival();
        StringBuilder stringBuilder = new();
        stringBuilder.Append($"Origin Port: <color=orange>{OriginPortName}</color>");
        stringBuilder.Append($"\nDestination Port:  <color=orange>{DestinationPortName}</color>");
        stringBuilder.AppendFormat("\nState: <color=yellow>{0}</color>{1}\n", State, string.IsNullOrEmpty(time) ? "" : $" ({time})");
        stringBuilder.Append("\nItems: ");
        foreach (ShipmentItem? shipmentItem in Items)
        {
            if (ObjectDB.instance.GetItemPrefab(shipmentItem.ItemName) is not { } itemPrefab || !itemPrefab.TryGetComponent(out ItemDrop component)) continue;
            stringBuilder.Append($"\n<color=orange>{component.m_itemData.m_shared.m_name}</color>");
            if (shipmentItem.Stack > 1) stringBuilder.Append($" x{shipmentItem.Stack}");
        }
        return stringBuilder.ToString();
    }

    public List<string> LogPrint()
    {
        List<string> log = new List<string>
        {
            "Origin Port Name:      " + OriginPortName,
            "Destination Port Name: " + DestinationPortName,
            "Origin Port ID:        " + OriginPortID,
            "Destination Port ID:   " + DestinationPortID,
            "Shipment ID:           " + ShipmentID,
            "State:                 " + State,
            "Arrival Time:          " + ArrivalTime,
            "Items Count:           " + Items.Count,
            ""
        };
        return log;
    }
}

[PublicAPI]
public enum ShipmentState
{
    Pending,
    InTransit,
    Delivered
}

[Serializable][PublicAPI][JsonObject(MemberSerialization.Fields)]
public class ShipmentItem
{
    public string ChestID;
    public string ItemName;
    public int Stack;
    public float Durability;
    public int Quality;
    public int Variant;
    public long CrafterID;
    public string CrafterName;
    public Dictionary<string, string> CustomData;

    public ShipmentItem(string chestID, ItemDrop.ItemData item)
    {
        ChestID = chestID;
        ItemName = item.m_dropPrefab.name;
        Stack = item.m_stack;
        Durability = item.m_durability;
        Quality = item.m_quality;
        Variant = item.m_variant;
        CrafterID = item.m_crafterID;
        CrafterName = item.m_crafterName;
        CustomData = item.m_customData;
    }

    public ItemDrop.ItemData? GetItemData()
    {
        if (ObjectDB.instance.GetItemPrefab(ItemName) is not { } itemPrefab)
        {
            Debug.LogWarning("Failed to find item: " + ItemName);
        }
        else if (!itemPrefab.TryGetComponent(out ItemDrop itemDrop))
        {
            Debug.LogWarning(itemPrefab.name + " missing ItemDrop !!");
        }
        else
        {
            ItemDrop.ItemData itemData = itemDrop.m_itemData.Clone();
            itemData.m_dropPrefab = itemPrefab;
            itemData.m_stack = Stack;
            itemData.m_quality = Quality;
            itemData.m_durability = Durability;
            itemData.m_variant = Variant;
            itemData.m_crafterID = CrafterID;
            itemData.m_crafterName = CrafterName;
            itemData.m_customData = CustomData;

            return itemData;
        }

        return null;
    }
    
    public ShipmentItem(ZPackage pkg)
    {
        ChestID = pkg.ReadString();
        ItemName = pkg.ReadString();
        Stack = pkg.ReadInt();
        Durability = (float)pkg.ReadDouble();
        Quality = pkg.ReadInt();
        Variant = pkg.ReadInt();
        CrafterID = pkg.ReadLong();
        CrafterName = pkg.ReadString();
        CustomData = new Dictionary<string, string>();
        int customDataCount = pkg.ReadInt();
        if (customDataCount <= 0) return;
        for (int i = 0; i < customDataCount; i++)
        {
            CustomData[pkg.ReadString()] = pkg.ReadString();
        }
    }

    public void Write(ZPackage pkg)
    {
        pkg.Write(ChestID);
        pkg.Write(ItemName);
        pkg.Write(Stack);
        pkg.Write((double)Durability);
        pkg.Write(Quality);
        pkg.Write(Variant);
        pkg.Write(CrafterID);
        pkg.Write(CrafterName);
        pkg.Write(CustomData.Count);
        foreach (var kvp in CustomData)
        {
            pkg.Write(kvp.Key);
            pkg.Write(kvp.Value);
        }
    }
}