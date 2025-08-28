using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using MWL_Ports.Managers;
using UnityEngine;

namespace MWL_Ports;

[PublicAPI]
public class Manifest
{
    public static Dictionary<string, Manifest> Manifests = new();
    public string Name;
    public GameObject Prefab;
    public RequiredItems Requirements = new();
    public string RequiredDefeatKey = "";
    public int CostToShip = 50;

    public bool IsPurchased;
    private static StringBuilder sb = new StringBuilder();

    private string _creatureName = string.Empty;
    private string CreatureName
    {
        get
        {
            if (string.IsNullOrEmpty(_creatureName) && DefeatKeyToCreatureMap.TryGetValue(RequiredDefeatKey, out var sharedName))
                _creatureName = sharedName ?? string.Empty;
            return _creatureName;
        }
    }

    private static Dictionary<string, string> _defeatKeyToCreatureMap = new();

    private static Dictionary<string, string> DefeatKeyToCreatureMap
    {
        get
        {
            if (_defeatKeyToCreatureMap.Count > 0 || !ZNetScene.instance) return _defeatKeyToCreatureMap;
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (!prefab.TryGetComponent(out Character component)) continue;
                if (string.IsNullOrEmpty(component.m_defeatSetGlobalKey)) continue;
                string sharedName = component.m_name;
                _defeatKeyToCreatureMap[component.m_defeatSetGlobalKey] = sharedName;
            }
            return _defeatKeyToCreatureMap;
        }
    }

    public Manifest(string name, GameObject chestPrefab)
    {
        Name = name;
        Prefab = chestPrefab;
        Manifests[name] = this;
    }
    
    public string GetTooltip()
    {
        if (!Prefab.TryGetComponent(out Container component)) return "";
        int size = component.m_width * component.m_height;
        sb.Clear();
        if (!string.IsNullOrEmpty(CreatureName)) sb.Append($"\nRequired To Defeat: <color=yellow>{CreatureName}</color>");
        sb.Append($"\nCapacity: <color=yellow>{size}</color>");
        return sb.ToString();
    }
    
    public class RequiredItems
    {
        public readonly List<Requirement> Requirements = new();

        public void Add(string itemName, int amount)
        {
            if (Requirements.Count >= 4) return;
            if (Helpers.GetPrefab(itemName) is not { } itemPrefab) return;
            if (!itemPrefab.TryGetComponent(out ItemDrop component)) return;
            Requirements.Add(new Requirement()
            {
                Item = component.m_itemData,
                Amount = amount
            });
        }
    }
    
    public record struct Requirement
    {
        public ItemDrop.ItemData Item;
        public int Amount;
    }
}

public static class ManifestHelpers
{
    public static Manifest.Requirement? GetRequirement(this Manifest manifest, int index)
    {
        if (index < 0 || index >= manifest.Requirements.Requirements.Count) return null;
        return manifest.Requirements.Requirements[index];
    }
    
    
    public static void Purchase(this Player player, Manifest manifest)
    {
        if (!player.NoCostCheat())
        {
            var inventory =  player.GetInventory();
            foreach (var requirement in manifest.Requirements.Requirements)
            {
                inventory.RemoveItem(requirement.Item.m_shared.m_name, requirement.Amount);
            }
        }
        manifest.IsPurchased = true;
    }

    public static bool HasRequirements(this Player player, Manifest manifest)
    {
        if (player.NoCostCheat()) return true;
        if (!string.IsNullOrEmpty(manifest.RequiredDefeatKey))
        {
            if (!ZoneSystem.instance.GetGlobalKey(manifest.RequiredDefeatKey) &&
                !player.GetUniqueKeys().Contains(manifest.RequiredDefeatKey))
                return false;
        }
        Inventory inventory = player.GetInventory();
        foreach (var requirement in manifest.Requirements.Requirements)
        {
            var count = inventory.CountItems(requirement.Item.m_shared.m_name);
            if (count >= requirement.Amount) continue;
            return false;
        }
        return true;
    }
}