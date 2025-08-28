using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MWL_Ports.Managers;
using UnityEngine;

namespace MWL_Ports;

public class Port : MonoBehaviour, Interactable, Hoverable
{
    public ZNetView m_view = null!;
    public ShipmentManager.PortID m_portID;
    public string m_name = "";
    public readonly TempContainers m_containers = new();
    public readonly Dictionary<string, Shipment> m_deliveries = new ();
    public string? m_selectedDelivery;
    public Humanoid? m_currentHumanoid;
    public readonly TempItems m_tempItems = new();
    public void Awake()
    {
        m_view = GetComponent<ZNetView>();
        // make sure znetview is valid before we access it
        if (!m_view.IsValid()) return;
        if (m_containers.m_list.Count <= 0)
        {
            // find all the relevant Transform that has the position and rotation of where
            // we want to place our containers
            foreach (Transform child in transform.FindAll("containerPosition"))
            {
                TempContainer temp = new TempContainer(child);
                m_containers.m_list.Add(temp);
            }
        }
        // get the name from ZDO, if it does not exist, use random name
        m_name = m_view.GetZDO().GetString(PortVars.Name, PortNames.GetRandomName());
        // get the Guid from ZDO, if it does not exist, generate a new one
        m_portID.Guid = m_view.GetZDO().GetString(PortVars.Guid, Guid.NewGuid().ToString());
        // save it within our PortID struct
        m_portID.Name = m_name;
        // save them to ZDO, to make sure our new random name, or new Guid is saved
        m_view.GetZDO().Set(PortVars.Guid, m_portID.Guid);
        m_view.GetZDO().Set(PortVars.Name, m_name);
        // get relevant deliveries from shipment manager for ease of access
        GetDeliveries();
    }

    public void Start()
    {
        // defensive programming, znetview should be valid at this point, but who knows ??
        if (!m_view.IsValid()) return;
        // spawn our containers and load them with saved data
        LoadSavedItems();
    }

    public void OnDestroy()
    {
        // make sure to delete spawned containers
        // I set them as non-persistant, when I cloned original, so they should get deleted anyway
        DestroyContainers();
    }

    public void SaveItems()
    {
        // temp items changed to handle more dynamic changes
        // it now is a real-time reference to the contents
        // anytime contents change, items are saved as ShipmentItem
        // and cached in temp items
        // ready to be loaded into containers, sent as a shipment, and get tooltip
        m_tempItems.Clear();
        // make sure znetview is valid before trying to save data to it
        if (!m_view.IsValid())
        {
            Debug.LogWarning("ZNETVIEW not valid when trying to save items");
            return;
        }
        // has item checks spawned containers, if no spawned containers, returns false
        // else checks contents
        if (m_containers.HasItems())
        {
            // iterate through our containers and add them to the temp list
            m_tempItems.Add(m_containers.GetSpawnedContainers());
            // I use ZPackage since it's optimized for networking
            // instead of Json, but we could use Json if you wanted to
            ZPackage pkg = new ZPackage();
            // save the amount of items, so we know how to parse it later
            pkg.Write(m_tempItems.Items.Count);
            foreach (ShipmentItem? item in m_tempItems.Items)
            {
                // created a function within shipment item class
                // to easily write each item data to package
                item.Write(pkg);
                // to keep the packaging, and parsing next to each other
            }
            // save it as a string
            m_view.GetZDO().Set(PortVars.Items, pkg.GetBase64());
        }
        else
        {
            // if containers are empty, make sure to set ZDO to that
            m_view.GetZDO().Set(PortVars.Items, "");
        }
    }

    public bool LoadSavedItems()
    {
        // get the serialized items from ZDO
        string? data = m_view.GetZDO().GetString(PortVars.Items);
        // make sure it's not null
        if (string.IsNullOrWhiteSpace(data)) return false;
        ZPackage pkg = new ZPackage(data);
        // we designed the first line to be the amount of items
        int itemCount = pkg.ReadInt();
        // so we know how long our for loop should be
        for (int i = 0; i < itemCount; i++)
        {
            // shipment item class has a built-in ZPackage input
            // to parse the package
            ShipmentItem temp = new ShipmentItem(pkg);
            m_tempItems.Add(temp);
        }
        // load the items into the containers
        return LoadItems(m_tempItems.Items);
    }
    public bool SpawnContainer(Manifest manifest)
    {
        // modified to handle manifests
        // containers are dynamic instead of static objects
        // that way they can be formatted to reference back to the manifest that triggered them
        foreach (TempContainer? temp in m_containers.m_list)
        {
            if (temp.IsSpawned) continue;
            // set temp container to manifest
            temp.manifest = manifest;
            // spawn container and rename container to manifest name
            // that way when they are saved to shipment, they can take container name (manifest name) ID
            var container = temp.Spawn();
            
            if (container == null) return false;
            container.GetInventory().m_onChanged = OnContainersChanged;
            return true;
        }
        return false;
    }
    public void DestroyContainers()
    {
        foreach (TempContainer? temp in m_containers.m_list)
        {
            temp.Destroy();
        }
    }

    public void GetDeliveries()
    {
        m_deliveries.Clear();
        foreach (Shipment? delivery in ShipmentManager.GetDeliveries(m_portID.Guid))
        {
            m_deliveries[delivery.ShipmentID] = delivery;
        }
    }

    public void OnContainersChanged()
    {
        if (ShipmentManager.instance == null) return;
        SaveItems();
        if (m_containers.HasItems() || m_selectedDelivery == null || !m_deliveries.TryGetValue(m_selectedDelivery, out Shipment currentDelivery)) return;
        m_deliveries.Remove(m_selectedDelivery);
        m_selectedDelivery = null;
        currentDelivery.OnCollected();
        if (m_currentHumanoid != null) m_currentHumanoid.Message(MessageHud.MessageType.Center, "Selected delivery marked as collected!");
        DestroyContainers();
    }
    
    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        // make sure PortUI is there if some weirdo used tools to destroy our UI ???
        if (PortUI.instance == null) return false;
        // anytime a player opens UI, let's update our delivery references
        // to make sure they stay up to date
        GetDeliveries();
        PortUI.instance.Show(this);
        // save user so we can spam them with messages
        m_currentHumanoid = user;
        return false;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    public string GetHoverText()
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append("Port Manager");
        stringBuilder.Append("\n[<color=yellow><b>$KEY_Use</b></color>] Open");
        return Localization.instance.Localize(stringBuilder.ToString());
    }

    public bool LoadItems(List<ShipmentItem> items)
    {
        // remove callback
        foreach (Container container in m_containers.GetSpawnedContainers())
        {
            container.GetInventory().m_onChanged = null;
        }
        // load items
        foreach (ShipmentItem item in items)
        {
            Container? container = m_containers.GetOrCreate(item.ManifestName);
            if (container == null)
            {
                Debug.LogWarning($"Failed to create container: {item.ManifestName}");
                continue;
            }
            if (!item.AddItem(container))
            {
                Debug.LogWarning("Failed to add item: " + item.ItemName);
            }
        }
        // reset callback
        foreach (Container container in m_containers.GetSpawnedContainers())
        {
            container.GetInventory().m_onChanged = OnContainersChanged;
        }
        // save to ZDO and temp items
        OnContainersChanged();
        return true;
    }

    public bool LoadDelivery(string selectedShipment)
    {
        // this is used when a player selects a delivery from PortUI
        if (!m_deliveries.TryGetValue(selectedShipment, out Shipment shipment))
        {
            Debug.LogWarning("Failed to find shipment: " + selectedShipment);
        }
        else
        {
            if (m_containers.HasItems())
            {
                // since containers reference to specific manifests
                // we do not want random containers that exist without being formatted to manifests
                // so stop loading delivery if current containers are active
                // that is, are spawned
                if (m_currentHumanoid != null) m_currentHumanoid.Message(MessageHud.MessageType.Center, "Containers have items! cannot load delivery");
                return false;
            }
            LoadItems(shipment.Items);
            m_selectedDelivery = selectedShipment;
        }
        
        OnContainersChanged(); // checks if containers are loaded and saves to ZDO
        return m_containers.HasItems(); // if true, can use this statement to make containers visible ??
    }

    public bool SendShipment(PortInfo selectedPort)
    {
        if (!m_containers.HasItems())
        {
            if (m_currentHumanoid != null) m_currentHumanoid.Message(MessageHud.MessageType.Center, "Tried to send shipment, but containers are empty !");
            return false;
        }
        // construct a new shipment
        float distance = Utils.DistanceXZ(transform.position, selectedPort.position);
        Shipment shipment = new Shipment(m_portID, selectedPort.ID, distance);
        Container[] containers = m_containers.GetSpawnedContainers();
        // add items from containers
        shipment.Items.Add(containers);
        // send shipment to server to manage
        shipment.SendToServer();
        DestroyContainers();
        m_tempItems.Clear();
        m_view.GetZDO().Set(PortVars.Items, ""); // make sure to tell ZDO that there are no items
        if (m_currentHumanoid != null) m_currentHumanoid.Message(MessageHud.MessageType.Center, "Successfully sent shipment!");
        return true;
    }

    public string GetHoverName() => Localization.instance.Localize(m_name);

    public string GetTooltip()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Current Shipment:");
        sb.Append($"\nCost: <color=orange>{ShipmentManager.CurrencyItem?.m_shared.m_name ?? "$item_coins"}</color> <color=yellow>x{m_containers.GetCost()}</color>");
        sb.Append($"\n{m_tempItems.GetTooltip()}");
        return sb.ToString();
    }

    public class TempItems
    {
        public readonly List<ShipmentItem> Items = new();
        private float GetTotalWeight() => Items.Sum(i => i.Weight);
        private int GetTotalStack() => Items.Sum(i => i.Stack);
        
        public string GetTooltip()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"\nNumber of items: <color=yellow>{GetTotalStack()}</color>");
            sb.Append($"\nTotal Weight: <color=yellow>{GetTotalWeight():0.0}</color>");
            sb.Append("\nContents:");
            foreach (var item in Items)
            {
                sb.Append($"\n<color=orange>{item.SharedName}</color> x{item.Stack}");
            }
            return sb.ToString();
        }
        public void Clear() => Items.Clear();
        public void Add(ShipmentItem shipmentItem) => Items.Add(shipmentItem);
        public void Add(params Container[] containers) => Items.Add(containers);
        
    }

    public class TempContainers
    {
        public readonly List<TempContainer> m_list = new List<TempContainer>();
        public Container? GetOrCreate(string chestID)
        {
            foreach (var temp in m_list)
            {
                if (!temp.IsSpawned || temp.manifest == null || temp.manifest.Name != chestID) continue;
                return temp.SpawnedContainer;
            }
            
            foreach (var temp in m_list)
            {
                if (temp.IsSpawned) continue;
                if (!Manifest.Manifests.TryGetValue(chestID, out Manifest manifest))
                {
                    Debug.LogWarning("Failed to find manifest: " + chestID);
                    return null;
                }
                temp.manifest = manifest;
                temp.manifest.IsPurchased = true;
                return temp.Spawn();
            }
            // if null, then all temp containers are spawned
            return null;
        }

        public Container[] GetSpawnedContainers()
        {
            List<Container> containers = new List<Container>();
            foreach (var temp in m_list)
            {
                if (temp.SpawnedContainer != null) containers.Add(temp.SpawnedContainer);
            }

            return containers.ToArray();
        }

        public bool HasItems()
        {
            foreach (var container in GetSpawnedContainers())
            {
                if (container.GetInventory().HasItems()) return true;
            }

            return false;
        }

        public int GetCost()
        {
            List<Manifest> manifests = new();
            foreach (var temp in m_list)
            {
                if (temp.manifest == null) continue;
                manifests.Add(temp.manifest);
            }
            return manifests.Sum(i => i.CostToShip);
        }
    }

    public class TempContainer
    {
        // class to manage spawning new containers
        // to keep relevant information organized
        // and keep the Spawn function within its own scope
        private readonly Transform transform;
        public Manifest? manifest;
        public bool IsSpawned => SpawnedContainer != null;
        public Container? SpawnedContainer;

        public TempContainer(Transform transform)
        {
            this.transform = transform;
        }

        public Container? Spawn()
        {
            if (manifest == null) return null;
            GameObject? chest = Instantiate(manifest.Prefab, transform.position, transform.rotation);
            // set chest name to manifest name to know which manifest spawned which container
            // could save to ZDO, but these are "cloned" objects, so changing name is fine
            // so easier access
            chest.name = manifest.Name;
            Container? container = chest.GetComponent<Container>();
            // set temp container as spawned and hold reference
            SpawnedContainer = container;
            // set manifest to purchased to remove from UI list
            manifest.IsPurchased = true;
            return container;
        }

        public void Destroy()
        {
            if (SpawnedContainer == null || !SpawnedContainer.m_nview.IsValid()) return;
            if (manifest != null)
            {
                // reset manifest to make it available to purchase again
                manifest.IsPurchased = false;
                // remove reference
                manifest = null;
            }
            SpawnedContainer.m_nview.ClaimOwnership();
            SpawnedContainer.m_nview.Destroy();
            // remove reference
            SpawnedContainer = null;
        }
    }

    public class PortInfo
    {
        // class to parse ZDO into relevant information
        // and keep relevant functions within their own scope
        public readonly ShipmentManager.PortID ID;
        public readonly Vector3 position;
        public readonly List<Shipment> deliveries;
        public readonly List<Shipment> shipments;
        
        private double _estimatedDuration;
        private double EstimatedDuration
        {
            get
            {
                if (_estimatedDuration != 0 || PortUI.instance is null || PortUI.instance.m_currentPort is null) return _estimatedDuration;
                float distance = Utils.DistanceXZ(PortUI.instance.m_currentPort.transform.position, position);
                // calculate time by distance
                // cache result
                _estimatedDuration = Shipment.CalculateDistanceTime(distance);
                // since our ports are static (not moving)
                // no need to recalculate this
                return _estimatedDuration;
            }
        }

        private static readonly StringBuilder sb = new StringBuilder();

        public PortInfo(ZDO zdo)
        {
            // cache information
            // to use to manage selected destination
            // and details about port
            ID = new ShipmentManager.PortID(zdo.GetString(PortVars.Guid), zdo.GetString(PortVars.Name));
            position = zdo.GetPosition();
            deliveries = ShipmentManager.GetDeliveries(ID.Guid);
            shipments = ShipmentManager.GetShipments(ID.Guid);
        }

        /// <summary>
        /// <see cref="ShipmentManager.OnShipmentsUpdated"/>
        /// </summary>
        public void Reload()
        {
            // when shipments are sent, they are sent to server, then local shipment manager receives new shipments
            // re-cache the updated shipments
            // TODO: check multiplayer if re-caching is too soon, what is the lag time between client sends and server callback ???
            // TODO: if it is too soon, we can use event handler in the shipment manager, so when they are updated, we reload
            deliveries.Clear();
            shipments.Clear();
            deliveries.AddRange(ShipmentManager.GetDeliveries(ID.Guid));
            shipments.AddRange(ShipmentManager.GetShipments(ID.Guid));
        }
        
        public float GetDistance(Player player) => Vector3.Distance(player.transform.position, position);

        public string GetTooltip()
        {
            sb.Clear();

            sb.Append($"Estimated Shipment Time: <color=yellow>{Shipment.FormatTime(EstimatedDuration)}</color>\n");
            sb.Append($"\nDeliveries (<color=yellow>{deliveries.Count}</color>): ");
            foreach (Shipment? delivery in deliveries)
            {
                string time = Shipment.FormatTime(delivery.GetTimeToArrivalSeconds());
                sb.AppendFormat("\nOrigin: <color=orange>{0}</color> (<color=yellow>{1}</color>{2})", delivery.OriginPortName, delivery.State, string.IsNullOrEmpty(time) ? "" : $", {time}");
            }
            sb.Append($"\n\nShipments (<color=yellow>{shipments.Count}</color>): ");
            foreach (Shipment? shipment in shipments)
            {
                string time = Shipment.FormatTime(shipment.GetTimeToArrivalSeconds());
                sb.AppendFormat("\nDestination: <color=orange>{0}</color> (<color=yellow>{1}</color>{2})", shipment.DestinationPortName, shipment.State, string.IsNullOrEmpty(time) ? "" : $", {time}");
            }
            return sb.ToString();
        }
    }

    private static class PortVars
    {
        // organization sake to manage Port ZDO variables
        public static readonly int Name = "PortName".GetStableHashCode();
        public static readonly int Guid = "PortGUID".GetStableHashCode();
        public static readonly int Items = "PortItems".GetStableHashCode();
    }
}