using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 任意の ScriptableObject 型にある int フィールドへ、一覧の順番に従って連番を書き込む。
/// </summary>
public sealed class ScriptableObjectIntOrderEditor : EditorWindow
{
    private readonly List<Type> assetTypes = new List<Type>();
    private readonly List<FieldInfo> intFields = new List<FieldInfo>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();

    private string[] assetTypeLabels = Array.Empty<string>();
    private string[] fieldLabels = Array.Empty<string>();
    private int selectedTypeIndex = -1;
    private int selectedFieldIndex = -1;
    private int startValue = 10;
    private int stepValue = 10;
    private Vector2 scrollPosition;
    private ReorderableList reorderableList;

    [MenuItem("Tools/ScriptableObject Registry/Int Order Editor")]
    private static void Open()
    {
        var window = GetWindow<ScriptableObjectIntOrderEditor>();
        window.titleContent = new GUIContent("SO Int Order");
        window.minSize = new Vector2(520f, 360f);
        window.Show();
    }

    private void OnEnable()
    {
        LoadTypes();
    }

    private void OnGUI()
    {
        DrawSettings();

        if (selectedTypeIndex < 0)
        {
            EditorGUILayout.HelpBox("ScriptableObject の型を選択してください。", MessageType.Info);
            return;
        }

        if (intFields.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "選択した型に、シリアライズ可能な int フィールドがありません。\n" +
                "public または [SerializeField] の int フィールドが対象です。",
                MessageType.Warning);
            return;
        }

        if (assets.Count == 0)
        {
            EditorGUILayout.HelpBox("選択した型のアセットが見つかりません。", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "行をドラッグして並べ替え、「現在の順番を書き込む」を押してください。",
            MessageType.None);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        reorderableList.DoLayoutList();
        EditorGUILayout.EndScrollView();
    }

    private void DrawSettings()
    {
        EditorGUI.BeginChangeCheck();
        int newTypeIndex = EditorGUILayout.Popup("ScriptableObject 型", selectedTypeIndex, assetTypeLabels);
        if (EditorGUI.EndChangeCheck())
            SelectType(newTypeIndex);

        using (new EditorGUI.DisabledScope(intFields.Count == 0))
        {
            EditorGUI.BeginChangeCheck();
            int newFieldIndex = EditorGUILayout.Popup("int フィールド", selectedFieldIndex, fieldLabels);
            if (EditorGUI.EndChangeCheck())
            {
                selectedFieldIndex = newFieldIndex;
                SortByCurrentValue();
            }
        }

        EditorGUILayout.BeginHorizontal();
        startValue = EditorGUILayout.IntField("開始値", startValue);
        stepValue = EditorGUILayout.IntField("増分", stepValue);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("再読み込み"))
            RefreshAssets();

        using (new EditorGUI.DisabledScope(selectedFieldIndex < 0 || assets.Count == 0))
        {
            if (GUILayout.Button("現在値で並べ直す"))
                SortByCurrentValue();
            if (GUILayout.Button("現在の順番を書き込む"))
                ApplyOrder();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void LoadTypes()
    {
        assetTypes.Clear();
        assetTypes.AddRange(TypeCache.GetTypesDerivedFrom<ScriptableObject>()
            .Where(type => !type.IsAbstract && !type.IsGenericType && !IsEditorType(type))
            .OrderBy(type => type.FullName, StringComparer.OrdinalIgnoreCase));

        assetTypeLabels = assetTypes
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        if (selectedTypeIndex >= assetTypes.Count)
            selectedTypeIndex = -1;

        if (selectedTypeIndex >= 0)
            SelectType(selectedTypeIndex);
    }

    private void SelectType(int typeIndex)
    {
        selectedTypeIndex = typeIndex;
        selectedFieldIndex = -1;
        intFields.Clear();

        if (selectedTypeIndex < 0 || selectedTypeIndex >= assetTypes.Count)
        {
            fieldLabels = Array.Empty<string>();
            assets.Clear();
            BuildList();
            return;
        }

        Type type = assetTypes[selectedTypeIndex];
        for (Type current = type; current != null && current != typeof(ScriptableObject); current = current.BaseType)
        {
            FieldInfo[] declaredFields = current.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in declaredFields)
            {
                if (field.FieldType == typeof(int) && IsSerializedField(field))
                    intFields.Add(field);
            }
        }

        intFields.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        fieldLabels = intFields.Select(field => field.Name).ToArray();
        selectedFieldIndex = intFields.Count > 0 ? 0 : -1;
        RefreshAssets();
    }

    private void RefreshAssets()
    {
        assets.Clear();
        if (selectedTypeIndex < 0 || selectedTypeIndex >= assetTypes.Count)
        {
            BuildList();
            return;
        }

        Type selectedType = assetTypes[selectedTypeIndex];
        string[] guids = AssetDatabase.FindAssets("t:" + selectedType.Name, new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath(path, selectedType) as ScriptableObject;
            if (asset != null && selectedType.IsInstanceOfType(asset))
                assets.Add(asset);
        }

        SortByCurrentValue();
    }

    private void SortByCurrentValue()
    {
        FieldInfo field = GetSelectedField();
        if (field == null)
            assets.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        else
            assets.Sort((left, right) =>
            {
                int comparison = ((int)field.GetValue(left)).CompareTo((int)field.GetValue(right));
                return comparison != 0
                    ? comparison
                    : string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
            });

        BuildList();
        Repaint();
    }

    private void BuildList()
    {
        reorderableList = new ReorderableList(assets, typeof(ScriptableObject), true, true, false, false);
        reorderableList.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, $"現在値の昇順（{assets.Count}件）    現在値 → 設定値");
        reorderableList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
        reorderableList.drawElementCallback = DrawElement;
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (index < 0 || index >= assets.Count)
            return;

        ScriptableObject asset = assets[index];
        rect.y += 2f;

        const float valueWidth = 110f;
        Rect valueRect = new Rect(rect.x, rect.y, valueWidth, EditorGUIUtility.singleLineHeight);
        Rect objectRect = new Rect(
            rect.x + valueWidth + 4f,
            rect.y,
            rect.width - valueWidth - 4f,
            EditorGUIUtility.singleLineHeight);

        FieldInfo field = GetSelectedField();
        string currentValue = field == null ? "-" : ((int)field.GetValue(asset)).ToString();
        int nextValue = startValue + index * stepValue;
        EditorGUI.LabelField(valueRect, $"{currentValue}  →  {nextValue}");
        EditorGUI.ObjectField(objectRect, asset, asset.GetType(), false);
    }

    private void ApplyOrder()
    {
        FieldInfo field = GetSelectedField();
        if (field == null)
            return;

        if (stepValue == 0 && !EditorUtility.DisplayDialog(
                "増分が0です",
                "すべてのアセットへ同じ値が設定されます。続行しますか？",
                "続行",
                "キャンセル"))
        {
            return;
        }

        Undo.RecordObjects(assets.Cast<UnityEngine.Object>().ToArray(), "Apply ScriptableObject Order");
        for (int i = 0; i < assets.Count; i++)
        {
            var serializedObject = new SerializedObject(assets[i]);
            SerializedProperty property = serializedObject.FindProperty(field.Name);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
            {
                Debug.LogError($"{assets[i].name}: intフィールド '{field.Name}' を保存できませんでした。", assets[i]);
                continue;
            }

            property.intValue = startValue + i * stepValue;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(assets[i]);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{assets.Count}件の {field.Name} を更新しました。");
    }

    private FieldInfo GetSelectedField()
    {
        return selectedFieldIndex >= 0 && selectedFieldIndex < intFields.Count
            ? intFields[selectedFieldIndex]
            : null;
    }

    private static bool IsSerializedField(FieldInfo field)
    {
        return !field.IsStatic &&
               !field.IsInitOnly &&
               !field.IsDefined(typeof(NonSerializedAttribute), true) &&
               (field.IsPublic || field.IsDefined(typeof(SerializeField), true));
    }

    private static bool IsEditorType(Type type)
    {
        return type.Assembly.GetName().Name.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
