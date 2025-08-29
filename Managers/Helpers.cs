using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MWL_Ports.Managers;

[PublicAPI]
public static class Helpers
{
    internal static ZNetScene? _ZNetScene;
    internal static ObjectDB? _ObjectDB;

    internal static GameObject? GetPrefab(string prefabName)
    {
        if (ZNetScene.instance != null) return ZNetScene.instance.GetPrefab(prefabName);
        if (_ZNetScene == null) return null;
        GameObject? result = _ZNetScene.m_prefabs.Find(prefab => prefab.name == prefabName);
        if (result != null) return result;
        if (Blueprint.registeredPrefabs.TryGetValue(prefabName, out GameObject blueprint)) return blueprint;
        return Clone.registeredPrefabs.TryGetValue(prefabName, out GameObject clone) ? clone : result;
    }
    
    public static string GetNormalizedName(string name) => Regex.Replace(name, @"\s*\(.*?\)", "").Trim();

    public static bool HasComponent<T>(this GameObject go) where T : Component => go.GetComponent<T>();

    public static void RemoveComponent<T>(this GameObject go) where T : Component
    {
        if (!go.TryGetComponent(out T component)) return;
        Object.DestroyImmediate(component);
    }

    public static void RemoveAllComponents<T>(this GameObject go, bool includeChildren = false, params Type[] ignoreComponents) where T : MonoBehaviour
    {
        List<T> components = go.GetComponents<T>().ToList();
        if (includeChildren)
        {
            components.AddRange(go.GetComponentsInChildren<T>(true));
        }
        foreach (T component in components)
        {
            if (ignoreComponents.Contains(component.GetType())) continue;
            Object.DestroyImmediate(component);
        }
    }

    public static List<Transform> FindAll(this Transform parent, string name)
    {
        List<Transform> result = new List<Transform>();
        foreach (Transform transform in parent)
        {
            if (transform.name == name) result.Add(transform);
        }

        return result;
    }
    
    internal static string GetInternalName(this LocationManager.IconSettings.LocationIcon table)
    {
        Type type = typeof(LocationManager.IconSettings.LocationIcon);
        MemberInfo[] memInfo = type.GetMember(table.ToString());
        if (memInfo.Length <= 0) return table.ToString();
        LocationManager.IconSettings.InternalName? attr = (LocationManager.IconSettings.InternalName)Attribute.GetCustomAttribute(memInfo[0], typeof(LocationManager.IconSettings.InternalName));
        return attr != null ? attr.internalName : table.ToString();
    }
}