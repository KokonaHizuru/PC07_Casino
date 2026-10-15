using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[FilePath("ProjectSettings/HierarchyColors.asset", FilePathAttribute.Location.ProjectFolder)]
sealed class HierarchyColorSettings : ScriptableSingleton<HierarchyColorSettings>
{
	[Serializable]
	class Entry
	{
		public string globalObjectId;
		public Color color;
	}

	[SerializeField] List<Entry> entries = new();

	public bool TryGetColor(GameObject gameObject, out Color color)
	{
		string id = GetId(gameObject);
		if (string.IsNullOrEmpty(id))
		{
			color = default;
			return false;
		}

		Entry entry = entries.Find(item => item.globalObjectId == id);
		if (entry == null)
		{
			color = default;
			return false;
		}

		color = entry.color;
		return true;
	}

	public void SetColor(GameObject gameObject, Color color)
	{
		string id = GetId(gameObject);
		if (string.IsNullOrEmpty(id))
			return;

		Entry entry = entries.Find(item => item.globalObjectId == id);
		if (entry == null)
		{
			entry = new Entry { globalObjectId = id };
			entries.Add(entry);
		}

		entry.color = color;
		Save(true);
	}

	public void RemoveColor(GameObject gameObject)
	{
		string id = GetId(gameObject);
		if (string.IsNullOrEmpty(id))
			return;

		if (entries.RemoveAll(item => item.globalObjectId == id) > 0)
			Save(true);
	}

	static string GetId(GameObject gameObject)
	{
		// 再生中はCamera Stackなどに実行時限定のクロスシーン参照が入る。
		// GlobalObjectIdの取得がその参照の保存検証を発生させるため、編集時だけ取得する。
		if (EditorApplication.isPlayingOrWillChangePlaymode)
			return null;

		GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
		return id.identifierType == 0 ? null : id.ToString();
	}
}

[InitializeOnLoad]
static class HierarchyColorEditor
{
	static readonly Color DefaultColor = new(0.2f, 0.5f, 0.8f, 1f);

	static HierarchyColorEditor()
	{
		EditorApplication.hierarchyWindowItemOnGUI += DrawHierarchyItem;
		Editor.finishedDefaultHeaderGUI += DrawInspectorHeader;
	}

	static void DrawHierarchyItem(int instanceId, Rect selectionRect)
	{
		if (EditorApplication.isPlayingOrWillChangePlaymode)
			return;

		GameObject gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
		if (gameObject == null)
			return;

		// アイコンはHierarchy文字色の設定とは独立してUnity標準設定を反映する
		Texture icon = EditorGUIUtility.GetIconForObject(gameObject);
		if (icon != null)
		{
			Rect iconRect = selectionRect;
			iconRect.width = 16f;
			iconRect.height = 16f;
			iconRect.y += (selectionRect.height - iconRect.height) * 0.5f;
			EditorGUI.DrawRect(iconRect, GetHierarchyBackgroundColor(instanceId));
			GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
		}

		if (!HierarchyColorSettings.instance.TryGetColor(gameObject, out Color color))
			return;

		color.a = 1f;
		GUIStyle labelStyle = new(EditorStyles.label);
		labelStyle.normal.textColor = color;

		Rect labelRect = selectionRect;
		labelRect.xMin += 18f;
		labelRect.width = labelStyle.CalcSize(new GUIContent(gameObject.name)).x + 4f;

		// Unity標準のラベルを背景色で消してから、色付きラベルを一度だけ描画する
		EditorGUI.DrawRect(labelRect, GetHierarchyBackgroundColor(instanceId));
		EditorGUI.LabelField(labelRect, gameObject.name, labelStyle);
	}

	static Color GetHierarchyBackgroundColor(int instanceId)
	{
		bool isSelected = Array.IndexOf(Selection.instanceIDs, instanceId) >= 0;
		if (isSelected)
		{
			bool hierarchyHasFocus =
				EditorWindow.focusedWindow != null &&
				EditorWindow.focusedWindow.GetType().Name == "SceneHierarchyWindow";

			return hierarchyHasFocus
				? new Color(0.17f, 0.36f, 0.53f, 1f)
				: new Color(0.30f, 0.30f, 0.30f, 1f);
		}

		return EditorGUIUtility.isProSkin
			? new Color(0.22f, 0.22f, 0.22f, 1f)
			: new Color(0.76f, 0.76f, 0.76f, 1f);
	}

	static void DrawInspectorHeader(Editor editor)
	{
		if (EditorApplication.isPlayingOrWillChangePlaymode)
			return;

		if (editor.targets.Length != 1 || editor.target is not GameObject gameObject)
			return;

		bool hasColor = HierarchyColorSettings.instance.TryGetColor(gameObject, out Color color);
		if (!hasColor)
			color = DefaultColor;

		using (new EditorGUILayout.HorizontalScope())
		{
			GUILayout.Label("Hierarchy Color", GUILayout.Width(100f));

			EditorGUI.BeginChangeCheck();
			Color newColor = EditorGUILayout.ColorField(color);
			if (EditorGUI.EndChangeCheck())
			{
				HierarchyColorSettings.instance.SetColor(gameObject, newColor);
				EditorApplication.RepaintHierarchyWindow();
			}

			using (new EditorGUI.DisabledScope(!hasColor))
			{
				if (GUILayout.Button("Clear", GUILayout.Width(50f)))
				{
					HierarchyColorSettings.instance.RemoveColor(gameObject);
					EditorApplication.RepaintHierarchyWindow();
				}
			}
		}
	}
}
