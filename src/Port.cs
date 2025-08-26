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

    public static readonly List<ShipmentItem> m_tempItems = new();
    public void Awake()
    {
        m_view = GetComponent<ZNetView>();
        if (!m_view.IsValid()) return;
        if (m_tempContainers.Count <= 0)
        {
            foreach (Transform child in transform.FindAll("containerPosition"))
            {
                GameObject? original = Helpers.GetPrefab("port_chest_wood");
                if (original == null || !original.GetComponent<Container>()) continue;
                TempContainer temp = new TempContainer(child, original);
                m_tempContainers.Add(temp);
            }
        }
        m_name = m_view.GetZDO().GetString(PortVars.Name, PortNames.GetRandomName());
        m_portID.Guid = m_view.GetZDO().GetString(PortVars.Guid, Guid.NewGuid().ToString());
        m_portID.Name = m_name;
        m_view.GetZDO().Set(PortVars.Guid, m_portID.Guid);
        m_view.GetZDO().Set(PortVars.Name, m_name);
        GetDeliveries();
    }

    public void Start()
    {
        if (!m_view.IsValid()) return;
        CreateContainers();
        SetContainersVisible(false);
        LoadSavedItems();
    }

    public void OnDestroy()
    {
        DestroyContainers();
    }

    public void SaveItems()
    {
        m_tempItems.Clear();
        if (!m_view.IsValid())
        {
            Debug.LogWarning("ZNETVIEW not valid when trying to save items");
            return;
        }
        if (!m_containersAreEmpty)
        {
            m_tempItems.Add(m_containers.Values.ToArray());
            ZPackage pkg = new ZPackage();
            pkg.Write(m_tempItems.Count);
            foreach (ShipmentItem? item in m_tempItems)
            {
                item.Write(pkg);
            }

            m_view.GetZDO().Set(PortVars.Items, pkg.GetBase64());
        }
    }

    public void LoadSavedItems()
    {
        string? data = m_view.GetZDO().GetString(PortVars.Items);
        if (string.IsNullOrWhiteSpace(data)) return;
        ZPackage pkg = new ZPackage(data);
        int itemCount = pkg.ReadInt();
        for (int i = 0; i < itemCount; i++)
        {
            ShipmentItem temp = new ShipmentItem(pkg);
            m_tempItems.Add(temp);
        }

        LoadItems(m_tempItems);
    }

    public void SetContainersVisible(bool visible)
    {
        foreach (Container container in m_containers.Values)
        {
            container.gameObject.SetActive(visible);
        }

        m_containersActive = visible;
    }

    public void CreateContainers()
    {
        DestroyContainers(); // make sure to clean up if for some reason, containers are already spawned
        int count = 0;
        foreach (TempContainer? temp in m_tempContainers)
        {
            Container container = temp.Spawn();
            ++count;
            var newName = container.name.Replace("Clone", count.ToString());
            container.name = newName;
            m_containers.Add(newName, container);
            container.GetInventory().m_onChanged = CheckContainers;
        }
    }

    public void DestroyContainers()
    {
        foreach (Container container in m_containers.Values)
        {
            if (container == null || container.m_nview == null || !container.m_nview.IsValid()) continue;
            container.m_nview.ClaimOwnership();
            container.m_nview.Destroy();
        }
        m_containers.Clear();
    }

    public void GetDeliveries()
    {
        foreach (Shipment? delivery in ShipmentManager.GetDeliveries(m_portID.Guid))
        {
            m_deliveries[delivery.ShipmentID] = delivery;
        }
    }

    public void CheckContainers()
    {
        if (ShipmentManager.instance == null) return;
        m_containersAreEmpty = true;
        foreach (Container container in m_containers.Values)
        {
            if (!container.GetInventory().HasItems()) continue;
            m_containersAreEmpty = false;
            break;
        }
        SaveItems();
        if (!m_containersAreEmpty || m_selectedDelivery == null || !m_deliveries.TryGetValue(m_selectedDelivery, out Shipment currentDelivery)) return;
        // if containers are empty, then mark shipment as collected
        m_deliveries.Remove(m_selectedDelivery);
        m_selectedDelivery = null;
        currentDelivery.OnCollected();
    }
    
    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (PortUI.instance == null) return false;
        GetDeliveries();
        PortUI.instance.Show(this);
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

    public bool LoadContainers(string selectedShipment)
    {
        if (!m_deliveries.TryGetValue(selectedShipment, out Shipment shipment))
        {
            Debug.LogWarning("Failed to find shipment: " + selectedShipment);
        }
        else
        {
            LoadItems(shipment.Items);
            m_selectedDelivery = selectedShipment;
        }
        CheckContainers();
        return !m_containersAreEmpty; // if true, make containers visible
    }

    public bool SendShipment(ShipmentManager.PortID selectedPort)
    {
        CheckContainers();
        if (m_containersAreEmpty) return false;
        Shipment shipment = new Shipment(m_portID, selectedPort);
        Container[] containers = m_containers.Values.ToArray();
        shipment.Items.Add(containers);
        shipment.SendToServer();
        containers.EmptyAll();
        m_view.GetZDO().Set(PortVars.Items, "");
        return true;
    }

    public string GetHoverName() => Localization.instance.Localize(m_name);
    
    public class TempContainer
    {
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
        public readonly string name;
        public readonly string guid;
        public Vector3 position;

        public PortInfo(ZDO zdo)
        {
            name = zdo.GetString(PortVars.Name);
            guid = zdo.GetString(PortVars.Guid);
            position = zdo.GetPosition();
        }
        
        public float GetDistance(Player player) => Vector3.Distance(player.transform.position, position);
    }

    public static class PortVars
    {
        public static readonly int Name = "PortName".GetStableHashCode();
        public static readonly int Guid = "PortGUID".GetStableHashCode();
        public static readonly int Items = "PortItems".GetStableHashCode();
    }
}