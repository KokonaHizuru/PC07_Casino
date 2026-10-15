using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>ResourceSpriteReferenceを通常のSprite参照のように編集可能にします。</summary>
[CustomPropertyDrawer(typeof(ResourceSpriteReference))]
public sealed class ResourceSpriteReferenceDrawer : PropertyDrawer
{
	const float PathLineHeight = 16f;
	const float LineSpacing = 2f;
	const string ResourcesMarker = "/Resources/";
	string cachedGuid;
	Sprite cachedSprite;

	/// <summary>参照欄とパス欄の高さを取得します。</summary>
	/// <param name="property"></param>
	/// <param name="label"></param>
	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return EditorGUIUtility.singleLineHeight + LineSpacing + PathLineHeight;
	}

	/// <summary>Resources専用の画像選択欄を描画します。</summary>
	/// <param name="position"></param>
	/// <param name="property"></param>
	/// <param name="label"></param>
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		SerializedProperty pathProperty = property.FindPropertyRelative("resourcePath");
		SerializedProperty guidProperty = property.FindPropertyRelative("editorAssetGuid");

		EditorGUI.BeginProperty(position, label, property);
		RefreshPathFromGuid(pathProperty, guidProperty);

		Rect objectRect = new Rect(
			position.x,
			position.y,
			position.width,
			EditorGUIUtility.singleLineHeight);
		Rect pathRect = new Rect(
			position.x,
			objectRect.yMax + LineSpacing,
			position.width,
			PathLineHeight);

		if (cachedGuid != guidProperty.stringValue ||
			(!string.IsNullOrEmpty(cachedGuid) && cachedSprite == null))
		{
			cachedGuid = guidProperty.stringValue;
			cachedSprite = LoadSpriteFromGuid(cachedGuid);
		}
		Rect fieldRect = EditorGUI.PrefixLabel(objectRect, label);
		GUIContent content = guidProperty.hasMultipleDifferentValues
			? new GUIContent("—")
			: cachedSprite == null ? new GUIContent("None (Sprite)")
			: new GUIContent(cachedSprite.name, AssetPreview.GetMiniThumbnail(cachedSprite));
		if (GUI.Button(fieldRect, content, EditorStyles.objectField))
		{
			UnityEngine.Object[] targets = property.serializedObject.targetObjects;
			string propertyPath = property.propertyPath;
			PopupWindow.Show(fieldRect, new SpritePicker(sprite =>
			{
				// ポップアップを開いた後のInspector更新に備え、プロパティを取得し直します。
				if (Array.Exists(targets, target => target == null))
					return;
				using (var serialized = new SerializedObject(targets))
				{
					serialized.Update();
					SerializedProperty reference = serialized.FindProperty(propertyPath);
					if (reference == null)
						return;
					SetSprite(sprite, reference.FindPropertyRelative("resourcePath"),
						reference.FindPropertyRelative("editorAssetGuid"));
					serialized.ApplyModifiedProperties();
				}
			}));
		}
		HandleDragAndDrop(fieldRect, pathProperty, guidProperty);

		using (new EditorGUI.DisabledScope(true))
			EditorGUI.TextField(pathRect, "自動設定パス", pathProperty.stringValue);

		EditorGUI.EndProperty();
	}

	/// <summary>Resources内のSpriteのドロップを受け付けます。</summary>
	/// <param name="rect"></param>
	/// <param name="pathProperty"></param>
	/// <param name="guidProperty"></param>
	static void HandleDragAndDrop(Rect rect, SerializedProperty pathProperty, SerializedProperty guidProperty)
	{
		Event current = Event.current;
		if (!GUI.enabled || !rect.Contains(current.mousePosition) ||
			(current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
			return;
		Sprite sprite = null;
		if (DragAndDrop.objectReferences.Length == 1)
		{
			UnityEngine.Object dragged = DragAndDrop.objectReferences[0];
			string assetPath = AssetDatabase.GetAssetPath(dragged);
			if (TryGetResourcesPath(assetPath, out _))
				sprite = dragged as Sprite ?? AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
		}
		DragAndDrop.visualMode = sprite == null ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
		if (current.type == EventType.DragPerform && sprite != null)
		{
			DragAndDrop.AcceptDrag();
			SetSprite(sprite, pathProperty, guidProperty);
		}
		current.Use();
	}

	/// <summary>SpriteのResources相対パスとGUIDを保存します。</summary>
	/// <param name="sprite"></param>
	/// <param name="pathProperty"></param>
	/// <param name="guidProperty"></param>
	static void SetSprite(
		Sprite sprite,
		SerializedProperty pathProperty,
		SerializedProperty guidProperty)
	{
		if (sprite == null)
		{
			pathProperty.stringValue = string.Empty;
			guidProperty.stringValue = string.Empty;
			return;
		}

		string assetPath = AssetDatabase.GetAssetPath(sprite).Replace('\\', '/');
		if (!TryGetResourcesPath(assetPath, out string resourcesPath))
		{
			Debug.LogError(
				$"'{sprite.name}' はResourcesフォルダー内にありません。" +
				"ResourceSpriteReferenceへ設定する画像は、いずれかのResourcesフォルダー内へ配置してください。",
				sprite);
			return;
		}

		pathProperty.stringValue = resourcesPath;
		guidProperty.stringValue = AssetDatabase.AssetPathToGUID(assetPath);
	}

	/// <summary>アセットの移動に合わせて保存パスを更新します。</summary>
	/// <param name="pathProperty"></param>
	/// <param name="guidProperty"></param>
	static void RefreshPathFromGuid(
		SerializedProperty pathProperty,
		SerializedProperty guidProperty)
	{
		if (guidProperty.hasMultipleDifferentValues || string.IsNullOrEmpty(guidProperty.stringValue))
			return;

		string assetPath = AssetDatabase.GUIDToAssetPath(guidProperty.stringValue);
		if (TryGetResourcesPath(assetPath, out string currentPath) &&
			pathProperty.stringValue != currentPath)
		{
			pathProperty.stringValue = currentPath;
		}
	}

	/// <summary>GUIDに対応するSpriteを取得します。</summary>
	/// <param name="guid"></param>
	static Sprite LoadSpriteFromGuid(string guid)
	{
		if (string.IsNullOrEmpty(guid))
			return null;

		string assetPath = AssetDatabase.GUIDToAssetPath(guid);
		return string.IsNullOrEmpty(assetPath)
			? null
			: AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
	}

	/// <summary>アセットパスをResources相対パスへ変換します。</summary>
	/// <param name="assetPath"></param>
	/// <param name="resourcesPath"></param>
	static bool TryGetResourcesPath(string assetPath, out string resourcesPath)
	{
		resourcesPath = string.Empty;
		if (string.IsNullOrEmpty(assetPath))
			return false;

		string normalizedPath = assetPath.Replace('\\', '/');
		int markerIndex = normalizedPath.LastIndexOf(
			ResourcesMarker,
			StringComparison.OrdinalIgnoreCase);
		if (markerIndex < 0)
			return false;

		int relativeStart = markerIndex + ResourcesMarker.Length;
		string relativePath = normalizedPath.Substring(relativeStart);
		resourcesPath = Path.ChangeExtension(relativePath, null).Replace('\\', '/');
		return !string.IsNullOrEmpty(resourcesPath);
	}

	/// <summary>Resources内の画像を検索して選択するポップアップ。</summary>
	sealed class SpritePicker : PopupWindowContent
	{
		readonly Action<Sprite> onSelected;
		readonly List<Sprite> sprites = new List<Sprite>();
		readonly List<GUIContent> contents = new List<GUIContent>();
		string search = string.Empty;
		Vector2 scroll;
		bool focusSearch = true;

		/// <summary>選択結果の通知先を保持します。</summary>
		/// <param name="onSelected"></param>
		public SpritePicker(Action<Sprite> onSelected)
		{
			this.onSelected = onSelected;
		}

		/// <summary>ポップアップを開いたときに候補を一度収集します。</summary>
		public override void OnOpen()
		{
			var paths = new List<string>();
			foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets" }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (TryGetResourcesPath(path, out _))
					paths.Add(path);
			}
			paths.Sort(StringComparer.OrdinalIgnoreCase);
			foreach (string path in paths)
			{
				// 保存形式がパス単位なので、実行時と同じ代表Spriteを候補にします。
				Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
				if (sprite == null)
					continue;
				sprites.Add(sprite);
				contents.Add(new GUIContent(sprite.name + "\n" + path,
					AssetPreview.GetMiniThumbnail(sprite), path));
			}
		}

		/// <summary>選択ウィンドウのサイズを取得します。</summary>
		public override Vector2 GetWindowSize()
		{
			return new Vector2(520f, 420f);
		}

		/// <summary>検索欄とサムネイル付き候補一覧を描画します。</summary>
		/// <param name="rect"></param>
		public override void OnGUI(Rect rect)
		{
			GUILayout.Label("Resources内のSprite", EditorStyles.boldLabel);
			GUI.SetNextControlName("ResourceSpriteSearch");
			search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
			if (focusSearch)
			{
				EditorGUI.FocusTextInControl("ResourceSpriteSearch");
				focusSearch = false;
			}
			if (GUILayout.Button("None（参照を解除）"))
			{
				Select(null);
				return;
			}
			scroll = EditorGUILayout.BeginScrollView(scroll);
			int count = 0;
			for (int i = 0; i < sprites.Count; i++)
			{
				if (sprites[i] == null || contents[i].text.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
					continue;
				count++;
				if (GUILayout.Button(contents[i], EditorStyles.miniButton, GUILayout.Height(44f)))
				{
					Select(sprites[i]);
					break;
				}
			}
			if (count == 0)
				GUILayout.Label("該当するSpriteがありません。");
			EditorGUILayout.EndScrollView();
		}

		/// <summary>選択したSpriteを通知してウィンドウを閉じます。</summary>
		/// <param name="sprite"></param>
		void Select(Sprite sprite)
		{
			onSelected(sprite);
			editorWindow.Close();
		}
	}
}
