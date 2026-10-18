using System;
using Casino.Common;
using Casino.Blackjack;
using Casino.HighCardDuel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Editor
{
    /// <summary>共通の円形選択肢プレハブを作り、中央のボタンだけに適用する。</summary>
    public static class CircularChoiceSetup
    {
        public const string PrefabPath = "Assets/MyAssets/Prefabs/Cards/CircularChoiceButton.prefab";

        /// <summary>現在のカードゲームシーンを円形の中央選択肢へ更新する。</summary>
        [MenuItem("Casino/UI/Apply Circular Choices")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を停止してください。");
            var poker = UnityEngine.Object.FindFirstObjectByType<ThreeCardPoker.PokerSceneUI>();
            var blackjack = UnityEngine.Object.FindFirstObjectByType<BlackjackSceneUI>();
            var duel = UnityEngine.Object.FindFirstObjectByType<DuelSceneUI>();
            if (poker != null) Replace(poker, new[] { "play", "fold", "funding", "retry" });
            else if (blackjack != null) Replace(blackjack, new[] { "hit", "stand", "doubleDown", "funding", "retry" });
            else if (duel != null) Replace(duel, new[] { "check", "raise", "call", "fold", "funding", "retry" });
            else throw new InvalidOperationException("カードゲームのSceneUIが見つかりません。");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        /// <summary>未作成の場合だけ、円形背景・波形・文字・Buttonを持つプレハブを保存する。</summary>
        /// <param name="font">既存UIと同じフォント。</param>
        private static GameObject GetPrefab(Font font)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null) return prefab;
            var node = new GameObject("CircularChoiceButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(CircularChoiceGraphic), typeof(Button));
            try
            {
                node.layer = 5;
                var rect = (RectTransform)node.transform;
                rect.sizeDelta = new Vector2(220, 220);
                var graphic = node.GetComponent<CircularChoiceGraphic>();
                graphic.color = new Color(.12f, .18f, .22f, .95f);
                var button = node.GetComponent<Button>();
                button.targetGraphic = graphic;
                var colors = button.colors;
                colors.highlightedColor = new Color(.84f, .96f, 1, 1);
                colors.pressedColor = new Color(.65f, .82f, .9f, 1);
                button.colors = colors;
                var labelNode = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelNode.layer = 5; labelNode.transform.SetParent(rect, false);
                var labelRect = (RectTransform)labelNode.transform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(.5f, .5f);
                labelRect.sizeDelta = new Vector2(144, 90);
                var label = labelNode.GetComponent<Text>();
                label.font = font; label.fontSize = 23; label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white; label.raycastTarget = false; label.text = "Choice";
                prefab = PrefabUtility.SaveAsPrefabAsset(node, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
            return prefab;
        }

        /// <summary>参照とラベルを引き継ぎ、既存の中央選択肢をプレハブのインスタンスへ置換する。</summary>
        /// <param name="ui">対象のSceneUI。</param>
        /// <param name="fields">中央に表示するボタンの参照名。</param>
        private static void Replace(MonoBehaviour ui, string[] fields)
        {
            var settings = new SerializedObject(ui);
            foreach (string field in fields)
            {
                var property = settings.FindProperty(field);
                var old = property.objectReferenceValue as Button;
                if (old == null) throw new InvalidOperationException(field + "の参照がありません。");
                if (old.GetComponent<CircularChoiceGraphic>() != null) continue;
                var oldLabel = old.GetComponentInChildren<Text>(true);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(GetPrefab(oldLabel.font), old.transform.parent);
                Undo.RegisterCreatedObjectUndo(instance, "Create circular choice");
                instance.name = old.name; instance.layer = old.gameObject.layer;
                var rect = (RectTransform)instance.transform;
                rect.SetSiblingIndex(old.transform.GetSiblingIndex());
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                var label = instance.GetComponentInChildren<Text>(true);
                label.text = oldLabel.text;
                if (label.font != oldLabel.font) label.font = oldLabel.font;
                var button = instance.GetComponent<Button>();
                var targetSettings = new SerializedObject(button);
                targetSettings.CopyFromSerializedProperty(new SerializedObject(old).FindProperty("m_OnClick"));
                targetSettings.ApplyModifiedProperties();
                // 起動前の中央への重なりを避け、各ゲームの状態通知で必要なものだけ表示する。
                instance.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                property.objectReferenceValue = button;
                settings.ApplyModifiedProperties();
                Undo.DestroyObjectImmediate(old.gameObject);
                settings.Update();
            }
        }
    }

    /// <summary>円の色と波形パラメーターをInspectorへ表示する。</summary>
    [CustomEditor(typeof(CircularChoiceGraphic))]
    public sealed class CircularChoiceGraphicEditor : UnityEditor.Editor
    {
        /// <summary>背景色・波の色・振幅・速度などの編集欄を描く。</summary>
        public override void OnInspectorGUI() => DrawDefaultInspector();
    }
}
