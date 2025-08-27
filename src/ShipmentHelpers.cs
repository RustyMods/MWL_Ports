using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

namespace MWL_Ports;

[PublicAPI]
public static class ShipmentHelpers
{
    public static void Add(this List<ShipmentItem> list, string chestID, ItemDrop.ItemData item)
    {
        list.Add(new  ShipmentItem(chestID, item));
    }

    public static void Add(this List<ShipmentItem> list, params Container[] containers)
    {
        foreach (Container container in containers) list.Add(container);
    }

    public static void Add(this List<ShipmentItem> list, Container container)
    {
        string chestName = container.name.Replace("(Clone)", string.Empty);
        List<ItemDrop.ItemData> items = container.GetInventory().GetAllItems();
        if (items.Count <= 0) return;
        foreach (ItemDrop.ItemData item in items)
        {
            list.Add(chestName, item);
        }
    }

    public static void EmptyAll(this Container[] containers)
    {
        foreach (var container in containers)
        {
            container.GetInventory().RemoveAll();
        }
    }

    public static void Add<T>(this List<T> list, params T[] values) => list.AddRange(values);

    public static bool HasItems(this Inventory inventory) => inventory.GetAllItems().Count > 0;
    
    public static void CopySpriteAndMaterial(this GameObject prefab, GameObject source, string childName, string sourceChildName = "")
    {
        try
        {
            Image? toImage = prefab.transform.Find(childName).GetComponent<Image>();
            Image? fromImage = source.transform
                .Find(string.IsNullOrWhiteSpace(sourceChildName) ? childName : sourceChildName)
                .GetComponent<Image>();
            toImage.sprite = fromImage.sprite;
            toImage.material = fromImage.material;
            toImage.color = fromImage.color;
            toImage.type = fromImage.type;
        }
        catch
        {
            Debug.LogWarning("Failed to find " + childName + " or " + sourceChildName) ;
        }
    }
    
    public static void CopyButtonState(this GameObject prefab, GameObject source, string childName, string sourceChildName = "")
    {
        try
        {
            prefab.transform.Find(childName).GetComponent<Button>().spriteState =
                source.transform.Find(string.IsNullOrWhiteSpace(sourceChildName) ? childName : sourceChildName)
                    .GetComponent<Button>().spriteState;
        }
        catch
        {
            Debug.LogWarning("Failed to find " + childName + " or " + sourceChildName) ;
        }
    }

    public static bool IsValid(this ItemDrop.ItemData item)
    {
        return item.m_shared.m_icons.Length > 0;
    }
}