using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>レジストリへ収録される1アセット分の情報。</summary>
[Serializable]
public sealed class ScriptableObjectRegistryEntry
{
    [SerializeField] private string guid;
    [SerializeField] private ScriptableObject asset;

    public string Guid => guid;
    public ScriptableObject Asset => asset;

    public ScriptableObjectRegistryEntry(string guid, ScriptableObject asset)
    {
        this.guid = guid;
        this.asset = asset;
    }
}

/// <summary>ビルドへ ScriptableObject の参照を収録するための内部アセット。</summary>
public sealed class ScriptableObjectRegistryAsset : ScriptableObject
{
    private sealed class TypeCache<T> where T : ScriptableObject
    {
        public readonly List<T> Assets = new List<T>();
        public readonly Dictionary<string, T> Names =
            new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
    }

    [SerializeField] private List<ScriptableObjectRegistryEntry> entries = new List<ScriptableObjectRegistryEntry>();

    private Dictionary<string, ScriptableObject> guidLookup;
    private Dictionary<Type, object> typeCaches;

    public bool TryGetByName<T>(string assetName, out T result) where T : ScriptableObject
    {
        return GetTypeCache<T>().Names.TryGetValue(assetName, out result);
    }

    public T GetByGuid<T>(string guid) where T : ScriptableObject
    {
        if (string.IsNullOrEmpty(guid))
            return null;

        EnsureGuidLookup();
        return guidLookup.TryGetValue(guid, out ScriptableObject asset) ? asset as T : null;
    }

    public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
    {
        return GetTypeCache<T>().Assets;
    }

#if UNITY_EDITOR
    public void SetEntries(List<ScriptableObjectRegistryEntry> newEntries)
    {
        entries = newEntries ?? new List<ScriptableObjectRegistryEntry>();
        guidLookup = null;
        typeCaches = null;
    }
#endif

    private void EnsureGuidLookup()
    {
        if (guidLookup != null)
            return;

        guidLookup = new Dictionary<string, ScriptableObject>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            ScriptableObjectRegistryEntry entry = entries[i];
            if (entry.Asset != null && !string.IsNullOrEmpty(entry.Guid))
                guidLookup[entry.Guid] = entry.Asset;
        }
    }

    private TypeCache<T> GetTypeCache<T>() where T : ScriptableObject
    {
        if (typeCaches == null)
            typeCaches = new Dictionary<Type, object>();

        Type requestedType = typeof(T);
        if (typeCaches.TryGetValue(requestedType, out object cached))
            return (TypeCache<T>)cached;

        var typeCache = new TypeCache<T>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (!(entries[i].Asset is T asset))
                continue;

            typeCache.Assets.Add(asset);
            if (!typeCache.Names.ContainsKey(asset.name))
                typeCache.Names.Add(asset.name, asset);
        }

        typeCaches.Add(requestedType, typeCache);
        return typeCache;
    }
}
