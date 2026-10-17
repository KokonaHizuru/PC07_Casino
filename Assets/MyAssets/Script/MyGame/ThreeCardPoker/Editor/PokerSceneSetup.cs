using System;
using System.IO;
using Casino.Common;
using Casino.HighCardDuel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.ThreeCardPoker.Editor
{
    /// <summary>コピーしたハイカードシーンの配置を保持してスリーカードポーカーへ接続する。</summary>
    public static class PokerSceneSetup
    {
        /// <summary>既存の視点、机、手札、ボタンを利用してゲーム固有の接続を置き換える。</summary>
        [MenuItem("Casino/Poker/Set Up Copied Scene")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を停止してください。");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Scene_Poker") throw new InvalidOperationException("Scene_Pokerを開いてください。");
            var existing = UnityEngine.Object.FindFirstObjectByType<PokerController>();
            if (existing != null) { AddExchangeUI(existing); return; }
            var old = UnityEngine.Object.FindFirstObjectByType<HighCardDuelController>();
            if (old == null) throw new InvalidOperationException("コピー元のHighCardDuelControllerが必要です。");
            var host = old.gameObject;
            var oldRig = host.GetComponent<DuelCameraRig>();
            var oldDirector = host.GetComponent<DuelPresentationDirector>();
            var oldUI = host.GetComponent<DuelSceneUI>();
            var oldCards = host.GetComponentInChildren<DuelTableCards>();
            if (oldRig == null || oldDirector == null || oldUI == null || oldCards == null)
                throw new InvalidOperationException("コピー元の演出接続が不足しています。");
            var controller = Undo.AddComponent<PokerController>(host);
            var rig = Undo.AddComponent<CardTableCameraRig>(host);
            var director = Undo.AddComponent<PokerPresentationDirector>(host);
            var ui = Undo.AddComponent<PokerSceneUI>(host);
            var cards = Undo.AddComponent<TableCardPresenter>(oldCards.gameObject);
            Copy(old, controller, "playerHand", "dealerHand");
            // コピー元の低いプレイヤー手札は維持し、上部は状態表示の余白を確保する。
            var dealerHand = (Casino.Cards.CardHandUI)new SerializedObject(old).FindProperty("dealerHand").objectReferenceValue;
            var dealerRect = (RectTransform)dealerHand.transform;
            Undo.RecordObject(dealerRect, "Poker status clearance");
            dealerRect.anchoredPosition += new Vector2(0, -40);
            Copy(oldRig, rig, "brain", "normalCamera", "tableCamera", "playerHand", "dealerHand", "handSlideDuration", "hiddenPadding", "transitionTimeout");
            Copy(oldCards, cards, "catalog", "playerOrigin", "dealerOrigin", "playerSlots", "dealerSlots", "modelScale", "moveDuration", "flipDuration", "liftHeight",
                "deckAnchor", "discardAnchor", "transferDuration", "transferArc", "discardThickness");
            var cardSettings = new SerializedObject(cards);
            cardSettings.FindProperty("spreadToFit").boolValue = false;
            cardSettings.ApplyModifiedProperties();
            Copy(oldDirector, director, "comparisonText", "adv");
            Set(director, "controller", controller); Set(director, "cameras", rig); Set(director, "tableCards", cards);
            var comparison = (Text)new SerializedObject(oldDirector).FindProperty("comparisonText").objectReferenceValue;
            Undo.RecordObject(comparison, "Poker result text"); comparison.fontSize = 28;
            Undo.RecordObject(comparison.rectTransform, "Poker result layout");
            comparison.rectTransform.anchorMin = comparison.rectTransform.anchorMax = new Vector2(.5f, .5f);
            comparison.rectTransform.anchoredPosition = Vector2.zero;
            comparison.rectTransform.sizeDelta = new Vector2(800, 100);
            Copy(oldUI, ui, "previewMode", "previewMoney", "initialBet", "status", "nextRound", "funding", "retry");
            var status = (Text)new SerializedObject(oldUI).FindProperty("status").objectReferenceValue;
            Undo.RecordObject(status, "Poker status text"); status.fontSize = 24;
            Undo.RecordObject(status.rectTransform, "Poker status layout");
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(.5f, 1);
            status.rectTransform.anchoredPosition = new Vector2(0, -46);
            status.rectTransform.sizeDelta = new Vector2(1700, 72);
            Set(ui, "controller", controller);
            var oldFields = new SerializedObject(oldUI);
            string[] from = { "check", "fold", "nextRound", "funding", "retry" };
            string[] to = { "play", "fold", "nextRound", "funding", "retry" };
            string[] labels = { "Play", "Fold", "Next Round", "Resume Funds", "Retry" };
            for (int i = 0; i < from.Length; i++)
            {
                var button = (Button)oldFields.FindProperty(from[i]).objectReferenceValue;
                Set(ui, to[i], button);
                Undo.RecordObject(button.gameObject, "Poker button"); button.name = labels[i];
                var label = button.GetComponentInChildren<Text>(true);
                Undo.RecordObject(label, "Poker label"); label.text = labels[i];
                var rect = (RectTransform)button.transform;
                Undo.RecordObject(rect, "Poker layout");
                rect.anchoredPosition = i == 0 ? new Vector2(-430, 42) : i == 1 ? new Vector2(430, 42)
                    : i == 2 ? new Vector2(0, 42) : new Vector2(i == 3 ? -430 : 430, 110);
            }
            foreach (string name in new[] { "raise", "call" })
            {
                var unused = (Button)oldFields.FindProperty(name).objectReferenceValue;
                Undo.DestroyObjectImmediate(unused.gameObject);
            }
            string folder = "Assets/MyAssets/Poker";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var events = new[] {
                Template(folder, "BeforePlayerDecision", PokerTiming.BeforePlayerDecision, PokerActionFilter.Any),
                Template(folder, "BeforePlay", PokerTiming.BeforeAction, PokerActionFilter.Play)
            };
            var settings = new SerializedObject(director); var list = settings.FindProperty("events"); list.arraySize = events.Length;
            for (int i = 0; i < events.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = events[i];
            settings.ApplyModifiedProperties();
            Undo.DestroyObjectImmediate(oldUI); Undo.DestroyObjectImmediate(oldDirector);
            Undo.DestroyObjectImmediate(old); Undo.DestroyObjectImmediate(oldRig); Undo.DestroyObjectImmediate(oldCards);
            Undo.RecordObject(host, "Poker host"); host.name = "PokerPresentation";
            AddExchangeUI(controller);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        }

        /// <summary>設定済みの配置を保ちながら、未接続の交換UIとイベント例だけを追加する。</summary>
        /// <param name="controller">ポーカー管理。</param>
        private static void AddExchangeUI(PokerController controller)
        {
            var ui = controller.GetComponent<PokerSceneUI>();
            var director = controller.GetComponent<PokerPresentationDirector>();
            if (ui == null || director == null) throw new InvalidOperationException("ポーカーのUIと演出管理が必要です。");
            var fields = new SerializedObject(ui);
            var area = (RectTransform)fields.FindProperty("exchangeArea").objectReferenceValue;
            if (area == null)
            {
                var player = (Casino.Cards.CardHandUI)new SerializedObject(controller).FindProperty("playerHand").objectReferenceValue;
                var canvas = player.GetComponentInParent<Canvas>().rootCanvas;
                var node = new GameObject("PokerExchangeArea", typeof(RectTransform), typeof(Image));
                node.layer = canvas.gameObject.layer;
                Undo.RegisterCreatedObjectUndo(node, "Poker exchange area"); node.transform.SetParent(canvas.transform, false);
                node.transform.SetAsFirstSibling();
                area = (RectTransform)node.transform; area.anchorMin = area.anchorMax = new Vector2(.5f, .5f);
                area.anchoredPosition = Vector2.zero; area.sizeDelta = new Vector2(680, 170);
                var panel = node.GetComponent<Image>(); panel.color = new Color(.1f, .025f, .025f, .75f); panel.raycastTarget = false;
                var labelNode = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelNode.layer = node.layer;
                Undo.RegisterCreatedObjectUndo(labelNode, "Poker exchange label"); labelNode.transform.SetParent(area, false);
                var label = labelNode.GetComponent<Text>();
                var status = (Text)fields.FindProperty("status").objectReferenceValue;
                label.font = status.font; label.fontSize = 28; label.alignment = TextAnchor.MiddleCenter;
                label.text = "DROP ONE CARD TO EXCHANGE"; label.raycastTarget = false; label.color = Color.white;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(20, 10); label.rectTransform.offsetMax = new Vector2(-20, -10);
                node.SetActive(false); Set(ui, "exchangeArea", area);
            }
            Set(controller, "exchangeArea", area);
            if (fields.FindProperty("keepHand").objectReferenceValue == null)
            {
                var source = (Button)fields.FindProperty("fold").objectReferenceValue;
                var node = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
                Undo.RegisterCreatedObjectUndo(node, "Poker keep hand"); node.name = "Keep Hand";
                node.GetComponentInChildren<Text>(true).text = "Keep Hand";
                var button = node.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent();
                node.SetActive(false); Set(ui, "keepHand", button);
            }
            string folder = "Assets/MyAssets/Poker"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var entries = new[] {
                ExchangeTemplate(folder, "BeforeDealerExchange", PokerTiming.BeforeDealerExchange, PokerActor.Dealer),
                ExchangeTemplate(folder, "BeforePlayerExchange", PokerTiming.BeforePlayerExchange, PokerActor.Player),
                ExchangeTemplate(folder, "AfterExchange", PokerTiming.CardExchanged, PokerActor.None)
            };
            var settings = new SerializedObject(director); var list = settings.FindProperty("events");
            foreach (var entry in entries)
            {
                bool found = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == entry) found = true;
                if (found) continue;
                int index = list.arraySize; list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = entry;
            }
            settings.ApplyModifiedProperties(); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        /// <summary>交換用SOの見本を作る。既存のユーザー設定は変更しない。</summary>
        /// <param name="folder">保存先。</param>
        /// <param name="name">名前。</param>
        /// <param name="timing">タイミング。</param>
        /// <param name="actor">対象者。</param>
        private static PokerPresentationEvent ExchangeTemplate(string folder, string name, PokerTiming timing, PokerActor actor)
        {
            var existing = AssetDatabase.LoadAssetAtPath<PokerPresentationEvent>(folder + "/" + name + ".asset");
            if (existing != null) return existing;
            var entry = Template(folder, name, timing, PokerActionFilter.Any);
            var settings = new SerializedObject(entry); settings.FindProperty("actor").enumValueIndex = (int)actor;
            if (timing == PokerTiming.CardExchanged) settings.FindProperty("stage").enumValueIndex = (int)PokerEventStage.AfterVisual;
            settings.ApplyModifiedProperties(); return entry;
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
        private static PokerPresentationEvent Template(string folder, string name, PokerTiming timing, PokerActionFilter action)
        {
            string path = folder + "/" + name + ".asset";
            var entry = AssetDatabase.LoadAssetAtPath<PokerPresentationEvent>(path);
            if (entry != null) return entry;
            entry = ScriptableObject.CreateInstance<PokerPresentationEvent>();
            var settings = new SerializedObject(entry);
            settings.FindProperty("eventEnabled").boolValue = false;
            settings.FindProperty("timing").enumValueIndex = (int)timing;
            settings.FindProperty("actor").enumValueIndex = (int)PokerActor.Player;
            settings.FindProperty("action").enumValueIndex = (int)action;
            settings.ApplyModifiedProperties(); AssetDatabase.CreateAsset(entry, path); return entry;
        }
    }
}
