using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>任意の ScriptableObject をランタイム用レジストリへ自動収録します。</summary>
public sealed class ScriptableObjectRegistryBuilder : IPreprocessBuildWithReport
{
    private const string RegistryFolder = "Assets/Resources";
    private const string RegistryPath = RegistryFolder + "/ScriptableObjectRegistry.asset";

    public int callbackOrder => -1000;

    [InitializeOnLoadMethod]
    private static void RebuildMissingRegistryAfterCompilation()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObjectRegistryAsset>(RegistryPath) == null)
                Rebuild();
        };
    }

    [MenuItem("Tools/ScriptableObject Registry/Rebuild")]
    public static void Rebuild()
    {
        EnsureFolderExists();

        ScriptableObjectRegistryAsset registry =
            AssetDatabase.LoadAssetAtPath<ScriptableObjectRegistryAsset>(RegistryPath);
        if (registry == null)
        {
            // クラス名とファイル名の不一致などで壊れた旧レジストリも復旧する。
            if (AssetDatabase.AssetPathExists(RegistryPath))
                AssetDatabase.DeleteAsset(RegistryPath);

            registry = ScriptableObject.CreateInstance<ScriptableObjectRegistryAsset>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
        var entries = new List<ScriptableObjectRegistryEntry>(guids.Length);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == RegistryPath || IsEditorOnlyPath(path))
                continue;

            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset != null)
                entries.Add(new ScriptableObjectRegistryEntry(guid, asset));
        }

        entries.Sort((left, right) => string.CompareOrdinal(
            AssetDatabase.GetAssetPath(left.Asset),
            AssetDatabase.GetAssetPath(right.Asset)));

        registry.SetEntries(entries);
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        ScriptableObjectRegistry.Reload();

        Debug.Log($"ScriptableObject Registry: {entries.Count} assets registered.", registry);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        Rebuild();
    }

    private static bool IsEditorOnlyPath(string path)
    {
        return path.Contains("/Editor/") || path.StartsWith("Assets/Editor/");
    }

    private static void EnsureFolderExists()
    {
        if (!AssetDatabase.IsValidFolder(RegistryFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");
    }
}

/// <summary>アセットの追加・削除・移動後にレジストリを追従させます。</summary>
public sealed class ScriptableObjectRegistryPostprocessor : AssetPostprocessor
{
    private const string RegistryPath = "Assets/Resources/ScriptableObjectRegistry.asset";
    private static bool rebuildQueued;

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (ContainsRelevantAsset(importedAssets) ||
            ContainsRelevantAsset(deletedAssets) ||
            ContainsRelevantAsset(movedAssets) ||
            ContainsRelevantAsset(movedFromAssetPaths))
        {
            QueueRebuild();
        }
    }

    private static bool ContainsRelevantAsset(string[] paths)
    {
        foreach (string path in paths)
        {
            if (path != RegistryPath && path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void QueueRebuild()
    {
        if (rebuildQueued)
            return;

        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            rebuildQueued = false;
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                ScriptableObjectRegistryBuilder.Rebuild();
        };
    }
}
