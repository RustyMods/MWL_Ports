using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace MWL_Ports;

[Serializable][PublicAPI]
public class Shipment
{
    public string OriginPortName;
    public string DestinationPortName;
    public string OriginPortID = null!; // Guid.ToString()
    public string DestinationPortID = null!;
    public string ShipmentID = null!;
    public ShipmentState State = ShipmentState.Pending;
    public double ArrivalTime;
    public List<ShipmentItem> Items = new List<ShipmentItem>();

    public bool IsValid = true;

    public Shipment(ShipmentManager.PortID originPort, ShipmentManager.PortID destinationPort)
    {
        OriginPortName = originPort.Name;
        OriginPortID = originPort.Guid;
        DestinationPortID = destinationPort.Guid;
        DestinationPortName = destinationPort.Name;
        ShipmentID = Guid.NewGuid().ToString();
        ArrivalTime = ZNet.instance.GetTimeSeconds() + ShipmentManager.TransitDuration;
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
        ArrivalTime = ZNet.instance.GetTimeSeconds() + ShipmentManager.TransitDuration;
        Items = data.Items;
        ShipmentManager.Shipments[ShipmentID] = this;
        ShipmentManager.instance.UpdateShipments();
    }
    
    public void SendToServer()
    {
        if (ShipmentManager.instance == null) return;
        if (ZNet.instance && ZNet.instance.IsServer())
        {
            // if local client is server, then simply register to shipments, and update
            ShipmentManager.Shipments[ShipmentID]  = this;
            ShipmentManager.instance.UpdateShipments();
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
                ShipmentManager.instance.UpdateShipments();
            }
        }
        else
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(nameof(ShipmentManager.RPC_ServerShipmentCollected), Player.m_localPlayer.GetPlayerName(), ShipmentID);
        }
    }

    public void CheckTransit()
    {
        State = ZNet.instance.GetTimeSeconds() < ArrivalTime ? ShipmentState.InTransit : ShipmentState.Pending;
    }
    
    public string ToJson() => JsonConvert.SerializeObject(this);
}

[PublicAPI]
public enum ShipmentState
{
    Pending,
    InTransit,
    Delivered
}

[Serializable][PublicAPI]
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
}