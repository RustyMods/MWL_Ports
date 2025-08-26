using System;
using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MWL_Ports.Managers;

[PublicAPI]
public class Blueprint
{
    internal static Dictionary<string, GameObject> registeredPrefabs = new();
    public GameObject Prefab;
    public event Action<Blueprint>? OnCreated;
    internal bool Loaded;
    
    public Blueprint(string assetBundleName, string prefabName):this(AssetBundleManager.GetAssetBundle(assetBundleName), prefabName){}
    
    public Blueprint(AssetBundle bundle, string prefabName):this(bundle.LoadAsset<GameObject>(prefabName)){}

    public Blueprint(GameObject prefab)
    {
        Prefab = Object.Instantiate(prefab, MWL_PortsPlugin.root.transform, false);
        Prefab.name = prefab.name;
        PrefabManager.Blueprints[Prefab.name] = this;
    }

    internal void Create()
    {
        if (Loaded) return;
        List<GameObject> objectsToDestroy = new();
        List<BlueprintObject> objectsToAdd = new List<BlueprintObject>();
        foreach (Transform child in Prefab.transform)
        {
            if (!child.name.StartsWith("MOCK_")) continue;
            if (Helpers.GetPrefab(child.name.Replace("MOCK_", string.Empty)) is not { } original)
            {
                Debug.LogError($"Prefab {child.name} not found");
            }
            else
            {
                objectsToAdd.Add(new BlueprintObject(original, child));
                objectsToDestroy.Add(child.gameObject);
            }
        }

        foreach (BlueprintObject? obj in objectsToAdd)
        {
            GameObject clone = Object.Instantiate(obj.Original, Prefab.transform);
            clone.name = obj.Mock.name.Replace("MOCK_", string.Empty);
            clone.layer = obj.Mock.gameObject.layer;
            clone.transform.SetLocalPositionAndRotation(obj.Mock.localPosition, obj.Mock.localRotation);
        }

        foreach (GameObject? obj in objectsToDestroy)
        { 
            Object.DestroyImmediate(obj);
        }
        OnCreated?.Invoke(this);
        registeredPrefabs[Prefab.name] = Prefab;
        Loaded = true;
    }
    
    public class BlueprintObject
    {
        public readonly GameObject Original;
        public readonly Transform Mock;

        public BlueprintObject(GameObject original, Transform mock)
        {
            Original = original;
            Mock = mock;
        }
    }
    
    [HarmonyPatch(typeof(ZNetView), nameof(ZNetView.Awake))]
    private static class ZNetView_Awake_Patch
    {
        private static bool Prefix(ZNetView __instance)
        {
            
            return true;
        }
    }
}