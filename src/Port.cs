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
    public readonly List<TempContainer> m_tempContainers = new();
    public readonly Dictionary<string, Shipment> m_deliveries = new ();
    public readonly Dictionary<string, Container> m_containers = new();
    public bool m_containersActive;
    public bool m_containersAreEmpty = true;
    public string? m_selectedDelivery;
    public Humanoid? m_currentHumanoid;
    public static readonly List<ShipmentItem> m_tempItems = new();
    public void Awake()
    {
        m_view = GetComponent<ZNetView>();
        // make sure znetview is valid before we access it
        if (!m_view.IsValid()) return;
        if (m_tempContainers.Count <= 0)
        {
            // find all the relevant Transform that has the position and rotation of where
            // we want to place our containers
            foreach (Transform child in transform.FindAll("containerPosition"))
            {
                // make sure the container we want to spawn actually exists
                GameObject? original = Helpers.GetPrefab("port_chest_wood");
                if (original == null || !original.GetComponent<Container>()) continue;
                // register as its own class to keep code easy to read
                TempContainer temp = new TempContainer(child, original);
                m_tempContainers.Add(temp);
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
        CreateContainers();
        SetContainersVisible(false);
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
        // tempItems is a static List, so make sure its empty before adding new items
        m_tempItems.Clear();
        // make sure znetview is valid before trying to save data to it
        if (!m_view.IsValid())
        {
            Debug.LogWarning("ZNETVIEW not valid when trying to save items");
            return;
        }
        if (!m_containersAreEmpty)
        {
            // iterate through our containers and add them to the temp list
            m_tempItems.Add(m_containers.Values.ToArray());
            // I use ZPackage since it's optimized for networking
            // instead of Json, but we could use Json if you wanted to
            ZPackage pkg = new ZPackage();
            // save the amount of items, so we know how to parse it later
            pkg.Write(m_tempItems.Count);
            foreach (ShipmentItem? item in m_tempItems)
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

    public void LoadSavedItems()
    {
        // get the serialized items from ZDO
        string? data = m_view.GetZDO().GetString(PortVars.Items);
        // make sure it's not null
        if (string.IsNullOrWhiteSpace(data)) return;
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
        LoadItems(m_tempItems);
    }

    public void SetContainersVisible(bool visible)
    {
        foreach (Container container in m_containers.Values)
        {
            container.gameObject.SetActive(visible);
        }
        // save setting for reference
        m_containersActive = visible;
    }

    public void CreateContainers()
    {
        DestroyContainers(); // make sure to clean up if for some reason containers are already spawned
        int count = 0; // keep a count so we can rename them to give each a unique name for ChestID
        foreach (TempContainer? temp in m_tempContainers)
        {
            Container container = temp.Spawn();
            ++count;
            string newName = container.name.Replace("Clone", count.ToString());
            container.name = newName;
            m_containers.Add(newName, container);
            // when a container inventory changes, it will trigger Port to check all the containers
            // I do this, since it should be more performant that having a custom Update, to check containers
            container.GetInventory().m_onChanged = CheckContainers;
        }
    }

    public void DestroyContainers()
    {
        foreach (Container container in m_containers.Values)
        {
            // make sure container reference is valid
            // some admin used tools to remove them ??
            // I cloned a chest, and removed Piece/WearNTear components, so they should be immune to damage
            if (container == null || container.m_nview == null || !container.m_nview.IsValid()) continue;
            // make sure to claim ownership before destroying
            // TODO: test multiplayer if only the owner should destroy
            container.m_nview.ClaimOwnership();
            container.m_nview.Destroy();
        }
        // clean up our references
        m_containers.Clear();
    }

    public void GetDeliveries()
    {
        m_deliveries.Clear();
        foreach (Shipment? delivery in ShipmentManager.GetDeliveries(m_portID.Guid))
        {
            m_deliveries[delivery.ShipmentID] = delivery;
        }
    }

    public void CheckContainers()
    {
        if (ShipmentManager.instance == null) return;
        // set to true, then check all containers
        m_containersAreEmpty = true;
        foreach (Container container in m_containers.Values)
        {
            if (!container.GetInventory().HasItems()) continue;
            m_containersAreEmpty = false;
            // if any has items, then break and set to false
            break;
        }
        // since this is triggered anytime someone changes container inventory
        // perfect time to update Port ZDO with container items
        SaveItems();
        // only runs if PortUI has a selected delivery that the player opened
        // and if our referenced shipments has the ID
        // TODO: check multiplayer if one player collected delivery, do we need to make sure to update our references ??
        if (!m_containersAreEmpty || m_selectedDelivery == null || !m_deliveries.TryGetValue(m_selectedDelivery, out Shipment currentDelivery)) return;
        // if containers are empty, then mark shipment as collected
        m_deliveries.Remove(m_selectedDelivery);
        m_selectedDelivery = null;
        currentDelivery.OnCollected();
        // message player that, to give visual feedback on their actions
        if (m_currentHumanoid != null) m_currentHumanoid.Message(MessageHud.MessageType.Center, "Selected delivery marked as collected!");
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

    public void LoadItems(List<ShipmentItem> items)
    {
        // remove all and make sure not to trigger CheckContainers()
        foreach (Container container in m_containers.Values)
        {
            container.GetInventory().m_onChanged = null;
            container.GetInventory().RemoveAll();
        }
        foreach (ShipmentItem item in items)
        {
            if (item.GetItemData() is not { } itemData) continue;
            if (!m_containers.TryGetValue(item.ChestID, out Container container))
            {
                Debug.LogWarning("Failed to find container: " + item.ChestID);
            }
            else
            {
                container.GetInventory().AddItem(itemData);
            }
        }
        // reset CheckContainers()
        foreach (Container container in m_containers.Values)
        {
            container.GetInventory().m_onChanged = CheckContainers;
        }
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
            LoadItems(shipment.Items);
            m_selectedDelivery = selectedShipment;
        }
        CheckContainers(); // checks if containers are loaded and saves to ZDO
        return !m_containersAreEmpty; // if true, can use this statement to make containers visible ??
    }

    public bool SendShipment(ShipmentManager.PortID selectedPort)
    {
        CheckContainers(); // check if there are items to send
        if (m_containersAreEmpty) return false;
        // construct a new shipment
        Shipment shipment = new Shipment(m_portID, selectedPort);
        Container[] containers = m_containers.Values.ToArray();
        // add items from containers
        shipment.Items.Add(containers);
        // send shipment to server to manage
        shipment.SendToServer();
        // empty containers
        containers.EmptyAll();
        m_view.GetZDO().Set(PortVars.Items, ""); // make sure to tell ZDO that there are no items
        if (m_currentHumanoid != null) m_currentHumanoid.Message(MessageHud.MessageType.Center, "Successfully sent shipment!");
        return true;
    }

    public string GetHoverName() => Localization.instance.Localize(m_name);
    
    public class TempContainer
    {
        // class to manage spawning new containers
        // to keep relevant information organized
        // and keep the Spawn function within its own scope
        private readonly Transform transform;
        private readonly GameObject prefab;

        public TempContainer(Transform transform, GameObject prefab)
        {
            this.transform = transform;
            this.prefab = prefab;
        }

        public Container Spawn()
        {
            GameObject? chest = Instantiate(prefab, transform.position, transform.rotation);
            Container? container = chest.GetComponent<Container>();
            return container;
        }
    }

    public class PortInfo
    {
        // class to parse ZDO into relevant information
        // and keep relevant functions within their own scope
        public readonly string name;
        public readonly string guid;
        public Vector3 position;
        public readonly List<Shipment> deliveries;
        public readonly List<Shipment> shipments;

        private static readonly StringBuilder sb = new StringBuilder();

        public PortInfo(ZDO zdo)
        {
            name = zdo.GetString(PortVars.Name);
            guid = zdo.GetString(PortVars.Guid);
            position = zdo.GetPosition();
            deliveries = ShipmentManager.GetDeliveries(guid);
            shipments = ShipmentManager.GetShipments(guid);
        }
        
        public float GetDistance(Player player) => Vector3.Distance(player.transform.position, position);

        public string GetTooltip()
        {
            sb.Clear();
            sb.Append($"Deliveries (<color=yellow>{deliveries.Count}</color>): ");
            foreach (Shipment? delivery in deliveries)
            {
                var time = delivery.FormatTimeToArrival();
                sb.AppendFormat("\nOrigin: <color=orange>{0}</color> (<color=yellow>{1}</color>{2})", delivery.OriginPortName, delivery.State, string.IsNullOrEmpty(time) ? "" : $", {time}");
            }
            sb.Append($"\n\nShipments (<color=yellow>{shipments.Count}</color>): ");
            foreach (Shipment? shipment in shipments)
            {
                var time = shipment.FormatTimeToArrival();
                sb.AppendFormat("\nDestination: <color=orange>{0}</color> (<color=yellow>{1}</color>{2})", shipment.DestinationPortName, shipment.State, string.IsNullOrEmpty(time) ? "" : $", {time}");
            }
            return sb.ToString();
        }
    }

    public static class PortVars
    {
        // organization sake to manage Port ZDO variables
        public static readonly int Name = "PortName".GetStableHashCode();
        public static readonly int Guid = "PortGUID".GetStableHashCode();
        public static readonly int Items = "PortItems".GetStableHashCode();
    }
}