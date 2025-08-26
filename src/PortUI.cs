using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using Managers;
using MWL_Ports.Managers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MWL_Ports;

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
public static class LoadPortUI
{
    [UsedImplicitly]
    private static void Postfix(InventoryGui __instance)
    {
        GameObject? panel = AssetBundleManager.LoadAsset<GameObject>("portbundle", "PortUI");
        if (panel == null)
        {
            Debug.LogWarning("PortUI is null");
            return;
        }
        
        GameObject? go = Object.Instantiate(panel, __instance.transform.parent.Find("HUD"));
        go.name = "PortUI";
        go.AddComponent<PortUI>();
        
        var panelTexts = go.GetComponentsInChildren<Text>(true);
        var listItemTexts = PortUI.ListItem.GetComponentsInChildren<Text>(true);

        var craftingPanel = __instance.m_crafting.gameObject;
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Selected", "selected_frame/selected (1)");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/bkg", "Bkg");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Port", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Port/Selected", "TabsButtons/Craft/Selected");
        go.CopyButtonState(craftingPanel, "Panel/Tabs/Port", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Shipment", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Shipment/Selected", "TabsButtons/Craft/Selected");
        go.CopyButtonState(craftingPanel, "Panel/Tabs/Shipment", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Delivery", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Delivery/Selected", "TabsButtons/Craft/Selected");
        go.CopyButtonState(craftingPanel, "Panel/Tabs/Delivery", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Seperator", "TabsButtons/TabBorder");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/LeftPanel/Viewport", "RecipeList/Recipes");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description", "Decription");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Icon", "Decription/Icon");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/SendButton", "Decription/craft_button_panel/CraftButton");
        go.CopyButtonState(craftingPanel, "Panel/Description/SendButton", "Decription/craft_button_panel/CraftButton");

        go.transform.position = new Vector3(1760f, 850f, 0f);
        PortUI.ListItem.CopySpriteAndMaterial(craftingPanel, "Icon", "RecipeList/Recipes/RecipeElement/icon");
        
        var sfx = craftingPanel.GetComponentInChildren<ButtonSfx>().m_sfxPrefab;
        foreach (var component in go.GetComponentsInChildren<ButtonSfx>(true)) component.m_sfxPrefab = sfx;
        FontManager.SetFont(panelTexts);
        FontManager.SetFont(listItemTexts);
    }
}


[HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
public static class PlayerTakeInput_Patch
{
    [UsedImplicitly]
    private static void Postfix(ref bool __result)
    {
        __result |= !PortUI.IsVisible();
    } 
}

[HarmonyPatch(typeof(PlayerController), nameof(PlayerController.InInventoryEtc))]
public static class PlayerController_InInventoryEtc_Patch
{
    [UsedImplicitly]
    private static void Postfix(ref bool __result)
    {
        __result |= PortUI.IsVisible();
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsVisible))]
public static class InventoryGui_IsVisible_Patch
{
    [UsedImplicitly]
    private static void Postfix(ref bool __result)
    {
        __result |= PortUI.IsVisible();
    }
}

public class PortUI : MonoBehaviour
{
    internal static readonly GameObject ListItem = AssetBundleManager.LoadAsset<GameObject>("portbundle", "ListItem")!;

    public static ConfigEntry<Vector3> posConfig = null!;

    private Image Selected = null!;
    private Image Background = null!;
    private Text Topic = null!;
    private Tab PortTab = null!;
    private Tab ShipmentTab = null!;
    private Tab DeliveryTab = null!;
    private RectTransform LeftPanelRoot = null!;
    private Image Icon = null!;
    private Button MainButton = null!;
    private Text MainButtonText = null!;
    private RightPanel Description = null!;
    
    public static PortUI? instance;
    public Port? m_currentPort;
    public ShipmentManager.PortID? m_selectedDestination;
    public Shipment? m_selectedShipment;
    private readonly List<TempListItem> m_tempListItems = new();
    private readonly List<Tab> m_tabs = new();
    private TabOption m_currentTab = TabOption.Ports;

    private enum TabOption
    {
        Ports, Shipments, Delivery
    }
    public void Awake()
    {
        instance = this;
        posConfig.SettingChanged += (sender, args) =>
        {
            transform.position = posConfig.Value;
        };
        Selected = transform.Find("Panel/Selected").GetComponent<Image>();
        Background = transform.Find("Panel/bkg").GetComponent<Image>();
        Topic = transform.Find("Panel/topic").GetComponent<Text>();
        Icon  = transform.Find("Panel/Description/Icon").GetComponent<Image>();
        MainButton = transform.Find("Panel/Description/SendButton").GetComponent<Button>();
        MainButtonText =  transform.Find("Panel/Description/SendButton/Text").GetComponent<Text>();
        PortTab = new Tab(transform.Find("Panel/Tabs/Port"));
        ShipmentTab = new Tab(transform.Find("Panel/Tabs/Shipment"));
        DeliveryTab = new Tab(transform.Find("Panel/Tabs/Delivery"));
        LeftPanelRoot =  transform.Find("Panel/LeftPanel/Viewport/ListRoot").GetComponent<RectTransform>();
        Description = new RightPanel(transform.Find("Panel/Description/Name").GetComponent<Text>(), transform.Find("Panel/Description/Body/Viewport/Text").GetComponent<Text>());
        m_tabs.Add(PortTab, ShipmentTab, DeliveryTab);
        PortTab.SetButton(() => OnPortTab());
        ShipmentTab.SetButton(OnShipmentTab);
        DeliveryTab.SetButton(OnDeliveryTab);
        MainButton.onClick.AddListener(OnMainButton);
        OnPortTab(false);
        Hide();
    }

    public void Update()
    {
        if (!ZInput.GetKeyDown(KeyCode.Escape) && !ZInput.GetKeyDown(KeyCode.Tab)) return;
        Hide();
    }
    
    public static bool IsVisible() => instance != null && instance.gameObject.activeInHierarchy;

    public void Hide() => gameObject.SetActive(false);

    public void SetTopic(string topic) => Topic.text = Localization.instance.Localize(topic);
    
    public void SetMainButtonText(string text) => MainButtonText.text = Localization.instance.Localize(text);
    
    public void Show(Port port)
    {
        if (ShipmentManager.instance == null) return;
        m_currentPort = port;
        gameObject.SetActive(true);
        PortTab.SetSelected(true);
        LoadPorts();
        SetTopic(m_currentPort.m_name);
        Description.SetBodyText("");
    }

    public void OnPortTab(bool loadPorts = true)
    {
        m_currentTab = TabOption.Ports;
        if (PortTab.IsSelected) return;
        PortTab.SetSelected(true);
        if (loadPorts) LoadPorts();
        SetMainButtonText("Exit");
        m_selectedDestination = null;
        m_selectedShipment = null;
        Description.SetBodyText("");
        MainButton.interactable = true;
    }

    public void OnShipmentTab()
    {
        m_currentTab = TabOption.Shipments;
        if (ShipmentTab.IsSelected || m_currentPort == null) return;
        ShipmentTab.SetSelected(true);
        LoadShipments();
        SetMainButtonText(m_currentPort.m_containersActive ? "Hide Containers" : "Show Containers");
        Description.SetBodyText("");
        m_selectedDestination = null;
        m_selectedShipment = null;
        MainButton.interactable = true;
    }

    public void OnDeliveryTab()
    {
        m_currentTab = TabOption.Delivery;
        if (DeliveryTab.IsSelected) return;
        DeliveryTab.SetSelected(true);
        ClearLeftPanel();
        SetMainButtonText("Open Delivery");
        MainButton.interactable = false;
        Description.SetBodyText("");
        m_selectedDestination = null;
        m_selectedShipment = null;
    }

    public void OnMainButton()
    {
        if (m_currentPort == null) return;
        switch (m_currentTab)
        {
            case TabOption.Ports:
                if (m_selectedDestination.HasValue)
                {
                    var result = m_currentPort.SendShipment(m_selectedDestination.Value);
                    m_selectedDestination = null;
                }
                else Hide();
                break;
            case TabOption.Shipments:
                m_currentPort.SetContainersVisible(!m_currentPort.m_containersActive);
                MainButtonText.text = m_currentPort.m_containersActive ? "Hide Containers" : "Show Containers";
                break;
            case TabOption.Delivery:
                m_currentPort.SetContainersVisible(true);
                break;
        }
    }

    public void LoadPorts()
    {
        ClearLeftPanel();
        if (ShipmentManager.instance == null) return;
        foreach (ZDO port in ShipmentManager.instance.GetPorts()) AddPort(port);
    }

    public void LoadShipments()
    {
        ClearLeftPanel();
        if (ShipmentManager.instance == null || m_currentPort == null) return;
        var shipments = ShipmentManager.instance.GetShipments(m_currentPort.m_portID.Guid);
        foreach (Shipment shipment in shipments)
        {
            AddShipment(shipment);
        }
    }

    public void ClearLeftPanel()
    {
        foreach(TempListItem? item in m_tempListItems) item.Destroy();
        m_tempListItems.Clear();
    }

    public void AddShipment(Shipment shipment)
    {
        TempListItem item = new TempListItem(Instantiate(ListItem, LeftPanelRoot));
        item.SetIcon(false);
        item.SetLabel(shipment.DestinationPortName);
        item.SetButton(() =>
        {
            item.SetSelected(true);
            m_selectedShipment = shipment;
            Description.SetName(shipment.DestinationPortName);
            StringBuilder stringBuilder = new();
            stringBuilder.Append($"Origin Port: {shipment.OriginPortName}");
            stringBuilder.Append($"\nState: {shipment.State}");
            foreach (var shipmentItem in shipment.Items)
            {
                if (ObjectDB.instance.GetItemPrefab(shipmentItem.ItemName) is not { } itemPrefab ||
                    !itemPrefab.TryGetComponent(out ItemDrop component)) continue;
                stringBuilder.Append($"\n{component.m_itemData.m_shared.m_name}");
                if (shipmentItem.Stack > 1) stringBuilder.Append($" x{shipmentItem.Stack}");
            }
            Description.SetBodyText(stringBuilder.ToString());
        });
    }

    public void AddPort(ZDO port)
    {
        if (m_currentPort == null) return;
        if (port == m_currentPort.m_view.GetZDO()) return;
        Port.PortInfo info = new Port.PortInfo(port);
        if (string.IsNullOrEmpty(info.name)) return;
        TempListItem item = new TempListItem(Instantiate(ListItem, LeftPanelRoot));
        item.SetIcon(false);
        item.SetLabel($"{info.name}");
        item.SetButton(() =>
        {
            item.SetSelected(true);
            m_selectedDestination = new ShipmentManager.PortID(info.name, info.guid);
            Description.SetName($"{info.name} ({info.GetDistance(Player.m_localPlayer):0})");
            m_currentPort.CheckContainers();
            if (!m_currentPort.m_containersAreEmpty)
            {
                MainButtonText.text = "Send Shipment";
            }
        });
        m_tempListItems.Add(item);
    }

    public void OnSelectDelivery(string shipmentID)
    {
        if (m_currentPort == null) return;
        bool hasItems = m_currentPort.LoadContainers(shipmentID);
        m_currentPort.SetContainersVisible(hasItems);
    }

    public void OnSend()
    {
        if (m_currentPort == null || m_selectedDestination == null) return;
        if (m_currentPort.m_containers.Count <= 0 || ShipmentManager.instance == null) return;
        Shipment shipment = new Shipment(m_currentPort.m_portID,  m_selectedDestination.Value);
        shipment.Items.Add(m_currentPort.m_containers.Values.ToArray());
        shipment.SendToServer();
    }

    private class TempListItem
    {
        public readonly GameObject? Prefab;
        public readonly Button? Button;
        public readonly Image? Icon;
        public readonly Text? Label;
        public readonly GameObject? Selected;
        
        public bool IsSelected => Selected != null && Selected.activeInHierarchy;

        public TempListItem(GameObject prefab)
        {
            Prefab = prefab;
            Button = Prefab.GetComponent<Button>();
            Icon = Prefab.transform.Find("Icon").GetComponent<Image>();
            Label = Prefab.transform.Find("Label").GetComponent<Text>();
            Selected = Prefab.transform.Find("selected").gameObject;
        }

        public void SetLabel(string label)
        {
            if (Label == null) return;
            Label.text = Localization.instance.Localize(label);
        }

        public void SetIcon(Sprite sprite, Color color)
        {
            if (Icon == null) return;
            Icon.sprite = sprite;
            Icon.color = color;
        }

        public void SetIcon(bool enable)
        {
            if (Icon == null) return;
            Icon.gameObject.SetActive(enable);
        }

        public void SetSelected(bool selected)
        {
            if (IsSelected) return;
            if (Selected == null || instance == null) return;
            foreach (TempListItem? temp in instance.m_tempListItems)
            {
                if (temp.Selected == null) continue;
                temp.Selected.SetActive(false);
            }
            Selected.SetActive(selected);
        }

        public void SetButton(UnityAction action)
        {
            if (Button == null) return;
            Button.onClick.AddListener(action);
        }

        public void Destroy()
        {
            Object.Destroy(Prefab);
        }
        
    }
    
    private class Tab
    {
        public readonly Button Button;
        public readonly Text Label;
        public readonly GameObject Selected;
        public readonly Text SelectedLabel;
        public bool IsSelected => Selected.activeInHierarchy;

        public Tab(Transform transform)
        {
            Button = transform.GetComponent<Button>();
            Label = transform.Find("Text").GetComponent<Text>();
            Selected = transform.Find("Selected").gameObject;
            SelectedLabel = transform.Find("Selected/SelectedText").GetComponent<Text>();
        }
        
        public void SetButton(UnityAction action) => Button.onClick.AddListener(action);

        public void SetLabel(string label)
        {
            Label.text = Localization.instance.Localize(label);
            SelectedLabel.text = Localization.instance.Localize(label);
        }

        public void SetSelected(bool selected)
        {
            if (instance == null) return;
            foreach(Tab? tab in instance.m_tabs) tab.Selected.SetActive(false);
            Selected.SetActive(selected);
        }
    }

    public class RightPanel
    {
        private Text Name;
        private Text BodyText;
        private RectTransform BodyRect;

        public RightPanel(Text name, Text body)
        {
            Name = name;
            BodyText = body;
            BodyRect = BodyText.rectTransform;
        }

        public void SetName(string name)
        {
            Name.text = Localization.instance.Localize(name);
        }

        public void SetBodyText(string body)
        {
            BodyText.text = Localization.instance.Localize(body);
            ResizePanel();
        }
        
        internal void ResizePanel()
        {
            float height = GetTextPreferredHeight(BodyText, BodyRect);
            float minHeight = 400f;
            float finalHeight = Mathf.Max(height, minHeight);
            BodyRect.sizeDelta = new Vector2(BodyRect.sizeDelta.x, finalHeight);
        }
    
        private static float GetTextPreferredHeight(Text text, RectTransform rect)
        {
            if (string.IsNullOrEmpty(text.text)) return 0f;
        
            TextGenerator textGen = text.cachedTextGenerator;
        
            var settings = text.GetGenerationSettings(rect.rect.size);
            float preferredHeight = textGen.GetPreferredHeight(text.text, settings);
        
            return preferredHeight;
        }
    }
}