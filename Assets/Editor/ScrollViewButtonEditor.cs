using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(ScrollViewButton))]
[CanEditMultipleObjects]
public class ScrollViewButtonEditor : ButtonEditor
{
	private SerializedProperty clickCancelDistance;
	private SerializedProperty wheelScrollMultiplier;

	private SerializedProperty hoverSE;
	private SerializedProperty clickSE;
	private SerializedProperty hoverSEInterval;
	private SerializedProperty clickSEInterval;

	protected override void OnEnable()
	{
		base.OnEnable();

		clickCancelDistance = serializedObject.FindProperty("clickCancelDistance");
		wheelScrollMultiplier = serializedObject.FindProperty("wheelScrollMultiplier");

		hoverSE = serializedObject.FindProperty("hoverSE");
		clickSE = serializedObject.FindProperty("clickSE");
		hoverSEInterval = serializedObject.FindProperty("hoverSEInterval");
		clickSEInterval = serializedObject.FindProperty("clickSEInterval");
	}

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		serializedObject.Update();

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Scroll View Button", EditorStyles.boldLabel);

		EditorGUILayout.PropertyField(clickCancelDistance);
		EditorGUILayout.PropertyField(wheelScrollMultiplier);

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Sound", EditorStyles.boldLabel);

		EditorGUILayout.PropertyField(hoverSE);
		EditorGUILayout.PropertyField(clickSE);
		EditorGUILayout.PropertyField(hoverSEInterval);
		EditorGUILayout.PropertyField(clickSEInterval);

		serializedObject.ApplyModifiedProperties();
	}
}