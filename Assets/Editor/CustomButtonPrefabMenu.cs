using UnityEditor;
using UnityEngine;

public static class CustomButtonPrefabMenu
{
	private const string PrefabPath = "Assets/MyAssets/Script/Utility/CustomButton.prefab";

	[MenuItem("GameObject/UI/CustomButton", false, 10)]
	private static void CreateCustomButton(MenuCommand menuCommand)
	{
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

		if (prefab == null)
		{
			Debug.LogError($"Prefabが見つかりません: {PrefabPath}");
			return;
		}

		GameObject obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

		GameObject parent = menuCommand.context as GameObject;
		if (parent != null)
		{
			obj.transform.SetParent(parent.transform, false);
		}

		Undo.RegisterCreatedObjectUndo(obj, "Create Custom Button");
		Selection.activeGameObject = obj;
	}
}