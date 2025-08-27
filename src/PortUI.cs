using System;
using System.Collections.Generic;
using System.Linq;
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
        // attached it the HUD, so it was independent of the inventory
        // I prefer the HUD since that rarely gets hidden on me
        GameObject? go = Object.Instantiate(panel, __instance.transform.parent.Find("HUD"));
        go.name = "PortUI";
        go.AddComponent<PortUI>();
        
        // set all the relevant assets now that we have access to the prefab that we want to target
        // in this case, crafting panel
        // we could technically do this sooner, since the ingame gui is attached the _GameMain
        // but no benefit, since no one is looking at our UI before they enter world
        var panelTexts = go.GetComponentsInChildren<Text>(true);
        var listItemTexts = PortUI.ListItem.GetComponentsInChildren<Text>(true);

        var craftingPanel = __instance.m_crafting.gameObject;
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Selected", "selected_frame/selected (1)");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/bkg", "Bkg");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/TitlePanel/BraidLineHorisontalMedium (1)", "TitlePanel/BraidLineHorisontalMedium (1)");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/TitlePanel/BraidLineHorisontalMedium (2)", "TitlePanel/BraidLineHorisontalMedium (2)");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Port", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Port/Selected", "TabsButtons/Craft/Selected");
        go.CopyButtonState(craftingPanel, "Panel/Tabs/Port", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Shipment", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Shipment/Selected", "TabsButtons/Craft/Selected");
        go.CopyButtonState(craftingPanel, "Panel/Tabs/Shipment", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Delivery", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Delivery/Selected", "TabsButtons/Craft/Selected");
        go.CopyButtonState(craftingPanel, "Panel/Tabs/Delivery", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Manifests", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Tabs/Manifests/Selected", "TabsButtons/Craft/Selected");
        go.CopyButtonState(craftingPanel, "Panel/Tabs/Manifests", "TabsButtons/Craft");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Separator", "TabsButtons/TabBorder");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/LeftPanel/Viewport", "RecipeList/Recipes");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description", "Decription");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Icon", "Decription/Icon");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/SendButton", "Decription/craft_button_panel/CraftButton");
        go.CopyButtonState(craftingPanel, "Panel/Description/SendButton", "Decription/craft_button_panel/CraftButton");
        
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/1", "Decription/requirements/res_bkg");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/2", "Decription/requirements/res_bkg");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/3", "Decription/requirements/res_bkg");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/4", "Decription/requirements/res_bkg");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/1/Icon", "Decription/requirements/res_bkg/res_icon");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/2/Icon", "Decription/requirements/res_bkg/res_icon");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/3/Icon", "Decription/requirements/res_bkg/res_icon");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/4/Icon", "Decription/requirements/res_bkg/res_icon");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/level", "Decription/requirements/level");
        go.CopySpriteAndMaterial(craftingPanel, "Panel/Description/Requirements/level/MinLevel", "Decription/requirements/level/MinLevel");


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

    public static ConfigEntry<Vector3> PanelPositionConfig = null!;

    private Image Selected = null!;
    private Image Background = null!;
    private Text Topic = null!;
    private Tab PortTab = null!;
    private Tab ShipmentTab = null!;
    private Tab DeliveryTab = null!;
    private Tab ManifestTab = null!;
    private RectTransform LeftPanelRoot = null!;
    private Image Icon = null!;
    private Button MainButton = null!;
    private Text MainButtonText = null!;
    private RightPanel Description = null!;
    private Requirement Requirements = null!;
    public static PortUI? instance;
    public Port? m_currentPort;
    public Port.PortInfo? m_selectedDestination;
    public Shipment? m_selectedDelivery;
    public Manifest? m_selectedManifest;
    private readonly List<TempListItem> m_tempListItems = new();
    private readonly List<Tab> m_tabs = new();
    private TabOption m_currentTab = TabOption.Ports;

    private Action<float>? OnUpdate;
    private float m_portPinTimer;
    private static Minimap.PinData? m_tempPin;

    private enum TabOption
    {
        Ports, Shipments, Delivery, Manifest
    }
    public void Awake()
    {
        instance = this;
        PanelPositionConfig.SettingChanged += (_, _) =>
        {
            transform.position = PanelPositionConfig.Value;
        };
        Selected = transform.Find("Panel/Selected").GetComponent<Image>();
        Background = transform.Find("Panel/bkg").GetComponent<Image>();
        Topic = transform.Find("Panel/TitlePanel/topic").GetComponent<Text>();
        Icon  = transform.Find("Panel/Description/Icon").GetComponent<Image>();
        MainButton = transform.Find("Panel/Description/SendButton").GetComponent<Button>();
        MainButtonText =  transform.Find("Panel/Description/SendButton/Text").GetComponent<Text>();
        PortTab = new Tab(transform.Find("Panel/Tabs/Port"));
        ShipmentTab = new Tab(transform.Find("Panel/Tabs/Shipment"));
        DeliveryTab = new Tab(transform.Find("Panel/Tabs/Delivery"));
        ManifestTab = new Tab(transform.Find("Panel/Tabs/Manifests"));
        LeftPanelRoot =  transform.Find("Panel/LeftPanel/Viewport/ListRoot").GetComponent<RectTransform>();
        Description = new RightPanel(
            transform.Find("Panel/Description/Name").GetComponent<Text>(), 
            transform.Find("Panel/Description/Body/Viewport/Text").GetComponent<Text>(), 
            transform.Find("Panel/Description/MapButton").GetComponent<Button>());
        Requirements = new Requirement(transform.Find("Panel/Description/Requirements").gameObject);
        Requirements.Add(transform.Find("Panel/Description/Requirements/1"));
        Requirements.Add(transform.Find("Panel/Description/Requirements/2"));
        Requirements.Add(transform.Find("Panel/Description/Requirements/3"));
        Requirements.Add(transform.Find("Panel/Description/Requirements/4"));
        Requirements.level.Icon = transform.Find("Panel/Description/Requirements/level/MinLevel").GetComponent<Image>();
        Requirements.level.Label = transform.Find("Panel/Description/Requirements/level/MinLevel/Text").GetComponent<Text>();

        m_tabs.Add(PortTab, ShipmentTab, DeliveryTab, ManifestTab);
        PortTab.SetButton(OnPortTab);
        ShipmentTab.SetButton(OnShipmentTab);
        DeliveryTab.SetButton(OnDeliveryTab);
        ManifestTab.SetButton(OnManifestTab);
        MainButton.onClick.AddListener(OnMainButton);
        Description.SetMapButton(OnMapButton);
        
        PortTab.SetLabel(LocalKeys.PortLabel);
        ShipmentTab.SetLabel(LocalKeys.ShipmentLabel);
        DeliveryTab.SetLabel(LocalKeys.DeliveryLabel);
        ManifestTab.SetLabel(LocalKeys.ManifestLabel);
        transform.Find("Panel/Description/MapButton/Text").GetComponent<Text>().text =
            Localization.instance.Localize(LocalKeys.OpenMapLabel);

        Requirements.SetActive(false);
        
        Hide();
    }

    public void Update()
    {
        float dt = Time.deltaTime;
        OnUpdate?.Invoke(dt);
        m_portPinTimer -= dt;
        if (m_portPinTimer <= 0.0f && m_tempPin != null)
        {
            Minimap.instance.RemovePin(m_tempPin);
        }
        if (!ZInput.GetKeyDown(KeyCode.Escape) && !ZInput.GetKeyDown(KeyCode.Tab)) return;
        Hide();
    }
    
    public static bool IsVisible() => instance != null && instance.gameObject.activeInHierarchy;

    public void Hide()
    {
        gameObject.SetActive(false);
        OnUpdate = null;
    }
    public void OnMapButton()
    {
        if (Description.MapInfo == null || !Minimap.instance) return;
        Vector3 pos = Description.MapInfo.position;
        Hide();
        if (m_tempPin != null)
        {
            Minimap.instance.RemovePin(m_tempPin);
        }
        Minimap.PinData? pin = Minimap.instance.AddPin(pos, Minimap.PinType.Icon2, Description.MapInfo.ID.Name, false, false);
        Minimap.instance.ShowPointOnMap(pos);
        m_tempPin = pin;
        m_portPinTimer = 300f;
    }

    public void SetTopic(string topic) => Topic.text = Localization.instance.Localize(topic);
    
    public void SetMainButtonText(string text) => MainButtonText.text = Localization.instance.Localize(text);
    
    public void Show(Port port)
    {
        if (ShipmentManager.instance == null) return;
        m_currentPort = port;
        gameObject.SetActive(true);
        PortTab.SetSelected(true);
        SetTopic(m_currentPort.m_name);
        Description.Reset();
        m_currentTab = TabOption.Ports;
        PortTab.SetSelected(true);
        LoadPorts();
        SetMainButtonText("Exit");
        Requirements.SetActive(false);
        m_selectedDestination = null;
        Description.Reset();
        MainButton.interactable = true;
        OnUpdate = null;
    }

    public void OnManifestTab()
    {
        m_currentTab = TabOption.Manifest;
        if (ManifestTab.IsSelected) return;
        ManifestTab.SetSelected(true);
        LoadManifests();
        SetMainButtonText("Purchase");
        m_selectedDestination = null;
        Description.Reset();
        MainButton.interactable = false;
        Requirements.SetActive(false);
        OnUpdate = null;
    }

    public void OnPortTab()
    {
        m_currentTab = TabOption.Ports;
        if (PortTab.IsSelected) return;
        PortTab.SetSelected(true);
        LoadPorts();
        SetMainButtonText("Exit");
        m_selectedDestination = null;
        m_selectedManifest = null;
        Description.Reset();
        MainButton.interactable = true;
        Requirements.SetActive(false);
        OnUpdate = null;
    }

    public void OnShipmentTab()
    {
        m_currentTab = TabOption.Shipments;
        if (ShipmentTab.IsSelected || m_currentPort == null) return;
        ShipmentTab.SetSelected(true);
        LoadShipments();
        SetMainButtonText("Exit");
        Description.Reset();
        m_selectedDestination = null;
        m_selectedManifest = null;
        MainButton.interactable = true;
        Requirements.SetActive(false);
        OnUpdate = null;
    }

    public void OnDeliveryTab()
    {
        m_currentTab = TabOption.Delivery;
        if (DeliveryTab.IsSelected) return;
        DeliveryTab.SetSelected(true);
        LoadDeliveries();
        SetMainButtonText("Open Delivery");
        MainButton.interactable = false;
        Description.Reset();
        m_selectedDestination = null;
        m_selectedManifest = null;
        Requirements.SetActive(false);
        OnUpdate = null;
    }

    public void OnMainButton()
    {
        if (m_currentPort == null) return;
        switch (m_currentTab)
        {
            case TabOption.Ports:
                if (m_selectedDestination != null)
                {
                    if (!m_currentPort.SendShipment(m_selectedDestination))
                    {
                        Debug.LogWarning("Failed to send shipment, are containers empty ??");
                    }
                    else m_selectedDestination.Reload();
                    m_selectedDestination = null;
                }
                else Hide();
                break;
            case TabOption.Shipments:
                Hide();
                break;
            case TabOption.Delivery:
                if (m_selectedDelivery == null) return;
                m_currentPort.LoadDelivery(m_selectedDelivery.ShipmentID);
                MainButton.interactable = false;
                Description.Reset();
                m_selectedDelivery = null;
                OnUpdate = null;
                break;
            case TabOption.Manifest:
                if (m_selectedManifest == null || !Player.m_localPlayer.HasRequirements(m_selectedManifest)) return;
                if (m_currentPort.SpawnContainer(m_selectedManifest))
                {
                    Player.m_localPlayer.Purchase(m_selectedManifest);
                    m_selectedManifest.IsPurchased = true;
                    LoadManifests();
                    Description.Reset();
                    MainButton.interactable = false;
                    Requirements.SetActive(false);
                    OnUpdate = null;
                }
                m_selectedManifest = null;
                break;
        }
    }

    public void LoadPorts()
    {
        ClearLeftPanel();
        foreach (ZDO port in ShipmentManager.GetPorts()) AddPort(port);
    }

    public void LoadManifests()
    {
        ClearLeftPanel();
        foreach (Manifest manifest in Manifest.Manifests.Values) AddManifest(manifest);
    }

    public void LoadShipments()
    {
        ClearLeftPanel();
        if (m_currentPort == null) return;
        var shipments = ShipmentManager.GetShipments(m_currentPort.m_portID.Guid);
        foreach (Shipment shipment in shipments)
        {
            AddShipment(shipment);
        }
    }

    public void LoadDeliveries()
    {
        ClearLeftPanel();
        if (m_currentPort == null) return;
        List<Shipment> deliveries = ShipmentManager.GetDeliveries(m_currentPort.m_portID.Guid);
        foreach(Shipment? delivery in deliveries) AddDelivery(delivery);
    }

    public void ClearLeftPanel()
    {
        foreach(TempListItem? item in m_tempListItems) item.Destroy();
        m_tempListItems.Clear();
    }

    public void AddManifest(Manifest manifest)
    {
        if (manifest.IsPurchased) return;
        TempListItem item = new TempListItem(Instantiate(ListItem, LeftPanelRoot));
        item.SetIcon("VikingShip");
        item.SetLabel(manifest.Name);
        item.SetButton(() =>
        {
            m_selectedManifest = manifest;
            Description.SetName(manifest.Name);
            Description.SetBodyText(manifest.GetTooltip());
            MainButton.interactable = Player.m_localPlayer.HasRequirements(manifest);
            Requirements.SetActive(true);
            Requirements.LoadRequirements(manifest);
            OnUpdate = dt => Requirements.Update(dt, Player.m_localPlayer);
        });
        m_tempListItems.Add(item);
    }

    public void AddDelivery(Shipment shipment)
    {
        if (shipment.State != ShipmentState.Delivered) return;
        TempListItem item = new TempListItem(Instantiate(ListItem, LeftPanelRoot));
        item.SetIcon(Minimap.PinType.Icon2);
        item.SetLabel(shipment.OriginPortName);
        item.SetButton(() =>
        {
            item.SetSelected(true);
            m_selectedDelivery = shipment;
            Description.SetName(shipment.OriginPortName);
            Description.SetBodyText(shipment.GetTooltip());
            MainButton.interactable = shipment.State == ShipmentState.Delivered;
            OnUpdate = null;
        });
        m_tempListItems.Add(item);
    }

    public void AddShipment(Shipment shipment)
    {
        TempListItem item = new TempListItem(Instantiate(ListItem, LeftPanelRoot));
        item.SetIcon(Minimap.PinType.Icon2);
        item.SetLabel(shipment.DestinationPortName);
        item.SetButton(() =>
        {
            item.SetSelected(true);
            Description.SetName(shipment.DestinationPortName);
            Description.SetBodyText(shipment.GetTooltip());
            float timer = 0f;
            OnUpdate = dt =>
            {
                bool shouldUpdate = shipment.State == ShipmentState.InTransit;
                if (!shouldUpdate) return;
                timer += dt;
                if (timer <= 1f) return;
                timer = 0.0f;
                Description.SetBodyText(shipment.GetTooltip());
            };
        });
        m_tempListItems.Add(item);
    }

    public void AddPort(ZDO port)
    {
        if (m_currentPort == null) return;
        if (port == m_currentPort.m_view.GetZDO()) return;
        Port.PortInfo info = new Port.PortInfo(port);
        if (string.IsNullOrEmpty(info.ID.Name)) return;
        TempListItem item = new TempListItem(Instantiate(ListItem, LeftPanelRoot));
        item.SetIcon(Minimap.instance.GetSprite(Minimap.PinType.Icon2), Color.white);
        item.SetLabel($"{info.ID.Name}");
        item.SetButton(() =>
        {
            item.SetSelected(true);
            m_selectedDestination = info;
            Description.SetName($"<color=orange>{info.ID.Name}</color> ({(int)info.GetDistance(Player.m_localPlayer)}m)");
            Description.SetBodyText(info.GetTooltip());
            Description.ShowMapButton(info);
            SetMainButtonText("Send Shipment");
            float timer = 0f;
            OnUpdate = dt =>
            {
                bool shouldUpdate =
                    info.shipments.Any(s => s.State == ShipmentState.InTransit) ||
                    info.deliveries.Any(d => d.State == ShipmentState.InTransit);
                if (!shouldUpdate) return;
                timer += dt;
                if (timer <= 1f) return;
                timer = 0.0f;
                Description.SetBodyText(info.GetTooltip());
            };
        });
        m_tempListItems.Add(item);
    }

    private class TempListItem
    {
        private readonly GameObject? Prefab;
        private readonly Button? Button;
        private readonly Image? Icon;
        private readonly Text? Label;
        private readonly GameObject? Selected;
        private bool IsSelected => Selected != null && Selected.activeInHierarchy;

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

        public void SetIcon(Sprite? sprite, Color color)
        {
            if (Icon == null) return;
            Icon.sprite = sprite;
            Icon.color = color;
        }
        
        public void SetIcon(Minimap.PinType pinType) => SetIcon(Minimap.instance.GetSprite(pinType), Color.white);

        public void SetIcon(string prefabName)
        {
            if (ZNetScene.instance.GetPrefab(prefabName) is not {} prefab) return;
            if (prefab.TryGetComponent(out ItemDrop itemDrop))
            {
                SetIcon(itemDrop.m_itemData);
            }
            else if (prefab.TryGetComponent(out Piece piece))
            {
                SetIcon(piece.m_icon, Color.white);
            }
        }
        
        public void SetIcon(ItemDrop.ItemData item)
        {
            if (!item.IsValid()) return;
            SetIcon(item.GetIcon(), Color.white);
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
        private readonly Button Button;
        private readonly Text Label;
        private readonly GameObject Selected;
        private readonly Text SelectedLabel;
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

    private class RightPanel
    {
        private readonly Text Name;
        private readonly Text BodyText;
        private readonly RectTransform BodyRect;
        private readonly Button MapButton;
        private readonly Image MapIcon;
        public Port.PortInfo? MapInfo;
        
        private readonly float BodyMinHeight;
        
        public RightPanel(Text name, Text body, Button mapButton)
        {
            Name = name;
            BodyText = body;
            BodyRect = BodyText.rectTransform;
            MapButton = mapButton;
            MapIcon = MapButton.GetComponent<Image>();
            BodyMinHeight = body.rectTransform.sizeDelta.y;
        }
        public void SetMapButton(UnityAction action) => MapButton.onClick.AddListener(action);
        public void SetName(string name)
        {
            Name.text = Localization.instance.Localize(name);
        }
        public void SetBodyText(string body)
        {
            BodyText.text = Localization.instance.Localize(body);
            ResizePanel();
        }
        public void Reset()
        {
            SetName("");
            SetBodyText("");
            ShowMapButton(null);
        }

        public void ShowMapButton(Port.PortInfo? info)
        {
            MapButton.gameObject.SetActive(info != null);
            MapInfo = info;
            if (Minimap.instance && MapIcon.sprite == null)
            {
                MapIcon.sprite = Minimap.instance.GetSprite(Minimap.PinType.Icon2);
            }
        }

        private void ResizePanel()
        {
            float height = GetTextPreferredHeight(BodyText, BodyRect);
            float finalHeight = Mathf.Max(height, BodyMinHeight);
            BodyRect.sizeDelta = new Vector2(BodyRect.sizeDelta.x, finalHeight);
        }
    
        private static float GetTextPreferredHeight(Text text, RectTransform rect)
        {
            if (string.IsNullOrEmpty(text.text)) return 0f;
        
            TextGenerator textGen = text.cachedTextGenerator;
        
            TextGenerationSettings settings = text.GetGenerationSettings(rect.rect.size);
            float preferredHeight = textGen.GetPreferredHeight(text.text, settings);
        
            return preferredHeight;
        }
    }

    public class Requirement
    {
        private readonly GameObject Prefab;
        private readonly List<RequirementItem> items = new List<RequirementItem>();
        public readonly Level level = new();
        public Requirement(GameObject prefab)
        {
            Prefab = prefab;
        }
        public void Add(Transform parent, string icon = "Icon", string name = "Name", string amount = "Amount")
        {
            Image Icon = parent.Find(icon).GetComponent<Image>();
            Text Label = parent.Find(name).GetComponent<Text>();
            Text Amount = parent.Find(amount).GetComponent<Text>();
            items.Add(new RequirementItem(Icon, Label, Amount));
        }

        public void LoadRequirements(Manifest manifest)
        {
            for (int i = 0; i < items.Count; i++)
            {
                RequirementItem item = items[i];
                if (manifest.GetRequirement(i) is not { } requirement)
                {
                    // if requirements are less than 4 (max amount of requirements currently available)
                    // then make the requirement item invisible
                    item.Hide();
                }
                else
                {
                    item.Set(requirement.Item.GetIcon(), requirement.Item.m_shared.m_name, requirement.Amount);
                }
            }
        }

        public void Update(float dt, Player? player)
        {
            if (player is null) return;
            foreach (var item in items) item.Update(dt, player);
        }
        
        public void SetActive(bool active) => Prefab.SetActive(active);

        private class RequirementItem
        {
            private readonly Image Icon;
            private readonly Text Name;
            private readonly Text Amount;

            private string? SharedName;
            private int Count;

            public RequirementItem(Image icon, Text name, Text amount)
            {
                Icon = icon;
                Name = name;
                Amount = amount;
            }

            public void Set(Sprite icon, string sharedName, int amount)
            {
                SharedName = sharedName;
                Count = amount;
                Icon.sprite = icon;
                Icon.color = Color.white;
                Name.text = Localization.instance.Localize(sharedName);
                Amount.text = amount.ToString();
            }

            public void Update(float dt, Player player)
            {
                if (SharedName == null) return;
                var inventory = player.GetInventory();
                var count = inventory.CountItems(SharedName);
                bool hasRequirement = Count <= count;

                if (!hasRequirement)
                {
                    Amount.color = Mathf.Sin(Time.time * 10f) > 0.0 ? Color.red : Color.white;
                }
                else
                {
                    Amount.color = Color.white;
                }
            }

            public void Hide()
            {
                Icon.color = Color.clear;
                Name.text = "";
                Amount.text = "";
                Count = 0;
                SharedName = null;
            }
        }

        public class Level
        {
            public Image? Icon;
            public Text? Label;
        }
    }
}