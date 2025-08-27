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

    public bool IsPurchased;
    // private StringBuilder sb = new StringBuilder();

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
        return $"Capacity: <color=yellow>{size}</color>";
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
    
    public class Requirement
    {
        public ItemDrop.ItemData Item = null!;
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
        var inventory =  player.GetInventory();
        foreach (var requirement in manifest.Requirements.Requirements)
        {
            inventory.RemoveItem(requirement.Item.m_shared.m_name, requirement.Amount);
        }
        manifest.IsPurchased = true;
    }

    public static bool HasRequirements(this Player player, Manifest manifest)
    {
        var inventory = player.GetInventory();
        foreach (var requirement in manifest.Requirements.Requirements)
        {
            var count = inventory.CountItems(requirement.Item.m_shared.m_name);
            if (count >= requirement.Amount) continue;
            return false;
        }
        return true;
    }
}