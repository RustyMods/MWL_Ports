using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MWL_Ports.Managers;

public class Clone
{
    private GameObject? Prefab;
    private readonly string PrefabName;
    private readonly string NewName;

    public event Action<GameObject>? OnCreated;

    public Clone(string prefabName, string newName)
    {
        PrefabName = prefabName;
        NewName = newName;
        PrefabManager.Clones.Add(this);
    }

    public void Create()
    {
        if (Helpers.GetPrefab(PrefabName) is not { } prefab) return;
        Prefab = Object.Instantiate(prefab, MWL_PortsPlugin.root.transform, false);
        Prefab.name = NewName;
        PrefabManager.RegisterPrefab(Prefab);
        OnCreated?.Invoke(Prefab);
    }
}