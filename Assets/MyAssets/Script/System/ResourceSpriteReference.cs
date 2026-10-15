using System;
using UnityEngine;

/// <summary>
/// Spriteを直接参照せず、Resources相対パスだけを保持するソフト参照。
/// Inspectorでのアセット選択とパス変換は専用PropertyDrawerが行います。
/// </summary>
[Serializable]
public sealed class ResourceSpriteReference
{
	[SerializeField, HideInInspector] string resourcePath;
	[SerializeField, HideInInspector] string editorAssetGuid;

	public string Path => resourcePath;
	public bool IsEmpty => string.IsNullOrEmpty(resourcePath);

	/// <summary>参照先のSpriteを同期ロードします。</summary>
	public AssetLoadHandle<Sprite> Load()
	{
		return ResourceAssetLoader.Load<Sprite>(resourcePath);
	}

	/// <summary>参照先のSpriteを非同期ロードします。</summary>
	public AssetLoadHandle<Sprite> LoadAsync()
	{
		return ResourceAssetLoader.LoadAsync<Sprite>(resourcePath);
	}

	public override string ToString()
	{
		return resourcePath ?? string.Empty;
	}
}
