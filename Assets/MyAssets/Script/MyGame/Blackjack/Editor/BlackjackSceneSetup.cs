using System;
using System.IO;
using Casino.Common;
using Casino.HighCardDuel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Blackjack.Editor
{
    /// <summary>コピーしたハイカードシーンの配置を保持してブラックジャックへ接続する。</summary>
    public static class BlackjackSceneSetup
    {
        /// <summary>既存の視点、机、手札、ボタンを利用してゲーム固有の接続を置き換える。</summary>
        [MenuItem("Casino/Blackjack/Set Up Copied Scene")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を停止してください。");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Scene_Blackjack") throw new InvalidOperationException("Scene_Blackjackを開いてください。");
            if (UnityEngine.Object.FindFirstObjectByType<BlackjackController>() != null) return;
            var old = UnityEngine.Object.FindFirstObjectByType<HighCardDuelController>();
            if (old == null) throw new InvalidOperationException("コピー元のHighCardDuelControllerが必要です。");
            var host = old.gameObject;
            var oldRig = host.GetComponent<DuelCameraRig>();
            var oldDirector = host.GetComponent<DuelPresentationDirector>();
            var oldUI = host.GetComponent<DuelSceneUI>();
            var oldCards = host.GetComponentInChildren<DuelTableCards>();
            if (oldRig == null || oldDirector == null || oldUI == null || oldCards == null)
                throw new InvalidOperationException("コピー元の演出接続が不足しています。");
            var controller = Undo.AddComponent<BlackjackController>(host);
            var rig = Undo.AddComponent<CardTableCameraRig>(host);
            var director = Undo.AddComponent<BlackjackPresentationDirector>(host);
            var ui = Undo.AddComponent<BlackjackSceneUI>(host);
            var cards = Undo.AddComponent<TableCardPresenter>(oldCards.gameObject);
            Copy(old, controller, "playerHand", "dealerHand");
            Copy(oldRig, rig, "brain", "normalCamera", "tableCamera", "playerHand", "dealerHand", "handSlideDuration", "hiddenPadding", "transitionTimeout");
            Copy(oldCards, cards, "catalog", "playerOrigin", "dealerOrigin", "playerSlots", "dealerSlots", "modelScale", "moveDuration", "flipDuration", "liftHeight");
            var cardSettings = new SerializedObject(cards);
            cardSettings.FindProperty("spreadToFit").boolValue = true;
            cardSettings.ApplyModifiedProperties();
            Copy(oldDirector, director, "comparisonText", "adv");
            Set(director, "controller", controller); Set(director, "cameras", rig); Set(director, "tableCards", cards);
            var comparison = (Text)new SerializedObject(oldDirector).FindProperty("comparisonText").objectReferenceValue;
            Undo.RecordObject(comparison, "Blackjack result text"); comparison.fontSize = 28;
            Undo.RecordObject(comparison.rectTransform, "Blackjack result layout");
            comparison.rectTransform.anchorMin = comparison.rectTransform.anchorMax = new Vector2(.5f, .5f);
            comparison.rectTransform.anchoredPosition = Vector2.zero;
            comparison.rectTransform.sizeDelta = new Vector2(800, 100);
            Copy(oldUI, ui, "previewMode", "previewMoney", "initialBet", "status", "nextRound", "funding", "retry");
            Set(ui, "controller", controller);
            var oldFields = new SerializedObject(oldUI);
            string[] from = { "check", "raise", "call", "nextRound", "funding", "retry" };
            string[] to = { "hit", "stand", "doubleDown", "nextRound", "funding", "retry" };
            string[] labels = { "Hit", "Stand", "Double Down", "Next Round", "Resume Funds", "Retry" };
            for (int i = 0; i < from.Length; i++)
            {
                var button = (Button)oldFields.FindProperty(from[i]).objectReferenceValue;
                Set(ui, to[i], button);
                Undo.RecordObject(button.gameObject, "Blackjack button"); button.name = labels[i];
                var label = button.GetComponentInChildren<Text>(true);
                Undo.RecordObject(label, "Blackjack label"); label.text = labels[i];
                var rect = (RectTransform)button.transform;
                Undo.RecordObject(rect, "Blackjack layout");
                rect.anchoredPosition = new Vector2((i - 2.5f) * 215, rect.anchoredPosition.y);
            }
            var fold = (Button)oldFields.FindProperty("fold").objectReferenceValue;
            Undo.RecordObject(fold.gameObject, "Hide Fold"); fold.gameObject.SetActive(false);
            string folder = "Assets/MyAssets/Blackjack";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var events = new[] {
                Template(folder, "BeforePlayerDecision", BlackjackTiming.BeforePlayerDecision, BlackjackActionFilter.Any),
                Template(folder, "BeforeDoubleDown", BlackjackTiming.BeforeAction, BlackjackActionFilter.DoubleDown)
            };
            var settings = new SerializedObject(director); var list = settings.FindProperty("events"); list.arraySize = events.Length;
            for (int i = 0; i < events.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = events[i];
            settings.ApplyModifiedProperties();
            Undo.DestroyObjectImmediate(oldUI); Undo.DestroyObjectImmediate(oldDirector);
            Undo.DestroyObjectImmediate(old); Undo.DestroyObjectImmediate(oldRig); Undo.DestroyObjectImmediate(oldCards);
            Undo.RecordObject(host, "Blackjack host"); host.name = "BlackjackPresentation";
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        }

        /// <summary>同名のシリアライズ項目を共有コンポーネントへ引き継ぐ。</summary>
        /// <param name="source">移行元。</param>
        /// <param name="destination">移行先。</param>
        /// <param name="names">フィールド名。</param>
        private static void Copy(UnityEngine.Object source, UnityEngine.Object destination, params string[] names)
        {
            var from = new SerializedObject(source); var to = new SerializedObject(destination);
            foreach (string name in names)
            {
                var property = from.FindProperty(name);
                if (property != null && to.FindProperty(name) != null) to.CopyFromSerializedProperty(property);
            }
            to.ApplyModifiedProperties();
        }

        /// <summary>参照をInspectorへ保存する。</summary>
        /// <param name="target">対象。</param>
        /// <param name="name">フィールド名。</param>
        /// <param name="value">参照。</param>
        private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var settings = new SerializedObject(target); settings.FindProperty(name).objectReferenceValue = value; settings.ApplyModifiedProperties();
        }

        /// <summary>未設定の宴イベント見本を一度だけ作る。</summary>
        /// <param name="folder">保存先。</param>
        /// <param name="name">アセット名。</param>
        /// <param name="timing">発生タイミング。</param>
        /// <param name="action">行動条件。</param>
        private static BlackjackPresentationEvent Template(string folder, string name, BlackjackTiming timing, BlackjackActionFilter action)
        {
            string path = folder + "/" + name + ".asset";
            var entry = AssetDatabase.LoadAssetAtPath<BlackjackPresentationEvent>(path);
            if (entry != null) return entry;
            entry = ScriptableObject.CreateInstance<BlackjackPresentationEvent>();
            var settings = new SerializedObject(entry);
            settings.FindProperty("eventEnabled").boolValue = false;
            settings.FindProperty("timing").enumValueIndex = (int)timing;
            settings.FindProperty("actor").enumValueIndex = (int)BlackjackActor.Player;
            settings.FindProperty("action").enumValueIndex = (int)action;
            settings.ApplyModifiedProperties(); AssetDatabase.CreateAsset(entry, path); return entry;
        }
    }
}
