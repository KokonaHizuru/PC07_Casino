using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲーム中に任意の ScriptableObject を名前または GUID から取得する汎用レジストリ。
/// </summary>
public static class ScriptableObjectRegistry
{
    private const string ResourceName = "ScriptableObjectRegistry";

    private static ScriptableObjectRegistryAsset registryAsset;
    private static bool loadAttempted;

    /// <summary>指定型のアセットを名前から取得します。見つからない場合は null を返します。</summary>
    public static T Get<T>(string assetName) where T : ScriptableObject
    {
        TryGet(assetName, out T asset);
        return asset;
    }

    /// <summary>指定型のアセットを名前から取得します。</summary>
    public static bool TryGet<T>(string assetName, out T asset) where T : ScriptableObject
    {
        asset = null;
        if (string.IsNullOrWhiteSpace(assetName))
            return false;

        ScriptableObjectRegistryAsset database = LoadRegistry();
        return database != null && database.TryGetByName(assetName, out asset);
    }

    /// <summary>UnityのAsset GUIDを使って取得します。名前変更の影響を受けません。</summary>
    public static T GetByGuid<T>(string guid) where T : ScriptableObject
    {
        ScriptableObjectRegistryAsset database = LoadRegistry();
        return database == null ? null : database.GetByGuid<T>(guid);
    }

    /// <summary>指定型（派生型を含む）の全アセットを取得します。</summary>
    public static IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
    {
        ScriptableObjectRegistryAsset database = LoadRegistry();
        return database == null ? Array.Empty<T>() : database.GetAll<T>();
    }

    /// <summary>次回アクセス時に Resources からレジストリを読み直します。</summary>
    public static void Reload()
    {
        registryAsset = null;
        loadAttempted = false;
    }

    private static ScriptableObjectRegistryAsset LoadRegistry()
    {
        if (!loadAttempted)
        {
            loadAttempted = true;
            registryAsset = Resources.Load<ScriptableObjectRegistryAsset>(ResourceName);
            if (registryAsset == null)
            {
                Debug.LogError(
                    "ScriptableObjectRegistry がありません。" +
                    "Unityメニューの Tools/ScriptableObject Registry/Rebuild を実行してください。");
            }
        }

        return registryAsset;
    }
}
