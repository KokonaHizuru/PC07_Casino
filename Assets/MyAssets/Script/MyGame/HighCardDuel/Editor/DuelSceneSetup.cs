using System;
using System.IO;
using Casino.Cards;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.HighCardDuel.Editor
{
    /// <summary>既存の手札と机を使って演出確認用の接続を作成する。</summary>
    public static class DuelSceneSetup
    {
        private const string AssetFolder = "Assets/MyAssets/HighCardDuel";

        /// <summary>開いているハイカードシーンに未作成の演出一式を追加する。</summary>
        [MenuItem("Casino/High Card Duel/Set Up Presentation Scene")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を停止してください。");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Scene_HightAndLow") throw new InvalidOperationException("Scene_HightAndLowを開いてください。");
            if (UnityEngine.Object.FindFirstObjectByType<DuelPresentationDirector>() != null)
                throw new InvalidOperationException("演出は配置済みです。既存オブジェクトを調整してください。");
            Camera main = Camera.main;
            CardHandUI player = null, dealer = null;
            foreach (var hand in UnityEngine.Object.FindObjectsByType<CardHandUI>(FindObjectsSortMode.None))
            {
                if (hand.name == "PlayerHand") player = hand;
                if (hand.name == "DealerHand") dealer = hand;
            }
            if (main == null || player == null || dealer == null) throw new InvalidOperationException("MainCameraと両者の手札が必要です。");
            Transform canvas = player.GetComponentInParent<Canvas>().transform;
            Directory.CreateDirectory(AssetFolder);
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<CasinoCardModelCatalog>(AssetFolder + "/CasinoCardModels.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CasinoCardModelCatalog>();
                var models = new UnityEngine.Object[52];
                string[] suits = { "Club/PCc", "Diamond/PCd", "Heart/PCh", "Spade/PCs" };
                for (int suit = 0; suit < 4; suit++)
                    for (int rank = 1; rank <= 13; rank++)
                    {
                        string path = "Assets/Casino/LWRP/Prefabs/PlayingCards/" + suits[suit] + rank + ".prefab";
                        models[suit * 13 + rank - 1] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (models[suit * 13 + rank - 1] == null) throw new InvalidOperationException(path);
                    }
                SetArray(catalog, "models", models);
                AssetDatabase.CreateAsset(catalog, AssetFolder + "/CasinoCardModels.asset");
            }
            var beforeSelect = Template("BeforeSelection", DuelTiming.BeforeCardSelection, DuelActor.Player, DuelActionFilter.Any);
            var beforeRaise = Template("BeforeDealerRaise", DuelTiming.BeforeAction, DuelActor.Dealer, DuelActionFilter.Raise);
            var host = new GameObject("HighCardDuelPresentation");
            Undo.RegisterCreatedObjectUndo(host, "Set up High Card Duel");
            var controller = host.AddComponent<HighCardDuelController>();
            var rig = host.AddComponent<DuelCameraRig>();
            var cards = Node("TableCards", host.transform).gameObject.AddComponent<DuelTableCards>();
            var director = host.AddComponent<DuelPresentationDirector>();
            var ui = host.AddComponent<DuelSceneUI>();
            var brain = main.GetComponent<CinemachineBrain>();
            if (brain == null) brain = Undo.AddComponent<CinemachineBrain>(main.gameObject);
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .7f);
            brain.IgnoreTimeScale = true;
            var normal = Node("NormalCamera", host.transform).gameObject.AddComponent<CinemachineCamera>();
            normal.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            normal.Lens = LensSettings.FromCamera(main);
            normal.Priority = 100;

            // 机のメッシュへ真上からレイを飛ばし、縁ではなく中央のフェルト面を基準にする。
            float surface = .75f;
            GameObject table = GameObject.Find("CRTableOval");
            Vector3 center = table != null ? table.transform.position : Vector3.zero;
            if (table != null)
            {
                float highestHit = float.NegativeInfinity;
                foreach (var filter in table.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    var probe = new GameObject("SurfaceProbe");
                    probe.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    probe.transform.localScale = filter.transform.lossyScale;
                    var collider = probe.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    Physics.SyncTransforms();
                    if (collider.Raycast(new Ray(center + Vector3.up * 5, Vector3.down), out RaycastHit hit, 10)) highestHit = Mathf.Max(highestHit, hit.point.y);
                    UnityEngine.Object.DestroyImmediate(probe);
                }
                if (!float.IsNegativeInfinity(highestHit)) surface = highestHit;
            }
            center.y = surface + .004f;
            Transform layout = Node("CardPositions", host.transform);
            Transform playerOrigin = Node("PlayerOrigin", layout); playerOrigin.position = center + new Vector3(0, .14f, -.4f);
            Transform dealerOrigin = Node("DealerOrigin", layout); dealerOrigin.position = center + new Vector3(0, .14f, .4f);
            var playerSlots = new UnityEngine.Object[3]; var dealerSlots = new UnityEngine.Object[3];
            for (int i = 0; i < 3; i++)
            {
                Transform p = Node("PlayerSlot" + (i + 1), layout), d = Node("DealerSlot" + (i + 1), layout);
                p.position = center + new Vector3((i - 1) * .14f, 0, -.12f);
                d.position = center + new Vector3((i - 1) * .14f, 0, .12f);
                playerSlots[i] = p; dealerSlots[i] = d;
            }
            Transform anchor = Node("DealerAnchor", host.transform);
            anchor.position = new Vector3(center.x, 0, center.z + .8f); anchor.rotation = Quaternion.Euler(0, 180, 0);
            var top = Node("TableCamera", host.transform).gameObject.AddComponent<CinemachineCamera>();
            top.transform.SetPositionAndRotation(center + Vector3.up * 1.1f, Quaternion.Euler(90, 0, 0));
            top.Lens = normal.Lens; top.Lens.FieldOfView = 45; top.Lens.NearClipPlane = .01f; top.Priority = 0;
            Set(rig, "brain", brain); Set(rig, "normalCamera", normal); Set(rig, "tableCamera", top);
            Set(rig, "playerHand", player); Set(rig, "dealerHand", dealer);
            Set(cards, "catalog", catalog); Set(cards, "playerOrigin", playerOrigin); Set(cards, "dealerOrigin", dealerOrigin);
            SetArray(cards, "playerSlots", playerSlots); SetArray(cards, "dealerSlots", dealerSlots);
            var sample = catalog.GetModel(new PlayingCard(CardSuit.Club, 1)).GetComponentInChildren<MeshFilter>();
            float width = sample.sharedMesh.bounds.size.x * sample.transform.lossyScale.x;
            var cardSettings = new SerializedObject(cards); cardSettings.FindProperty("modelScale").floatValue = .095f / width; cardSettings.ApplyModifiedPropertiesWithoutUndo();
            RectTransform area = Rect("PlayArea", canvas, new Vector2(.5f, .5f), new Vector2(900, 380), Vector2.zero);
            Set(controller, "playerHand", player); Set(controller, "dealerHand", dealer); Set(controller, "playArea", area);
            Set(director, "controller", controller); Set(director, "cameras", rig); Set(director, "tableCards", cards);
            SetArray(director, "events", new UnityEngine.Object[] { beforeSelect, beforeRaise });
            Text comparison = Label("Comparison", canvas, new Vector2(.5f, .5f), new Vector2(800, 60), Vector2.zero, 32);
            Set(director, "comparisonText", comparison);
            Set(ui, "controller", controller);
            Text status = Label("DuelStatus", canvas, new Vector2(.5f, .95f), new Vector2(1000, 100), Vector2.zero, 25);
            Set(ui, "status", status);
            string[] fields = { "check", "raise", "call", "fold", "nextRound", "funding", "retry" };
            string[] labels = { "Check", "Raise", "Call", "Fold", "Next Round", "Resume Funds", "Retry" };
            for (int i = 0; i < fields.Length; i++)
            {
                RectTransform rect = Rect(labels[i], canvas, new Vector2(.5f, 0), new Vector2(200, 52), new Vector2((i - 3) * 215, 42));
                var background = rect.gameObject.AddComponent<Image>(); background.color = new Color(.12f, .18f, .22f, .95f);
                var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = background;
                Label("Label", rect, new Vector2(.5f, .5f), new Vector2(198, 50), Vector2.zero, 23).text = labels[i];
                Set(ui, fields[i], button);
            }
            foreach (var debug in UnityEngine.Object.FindObjectsByType<CardHandDebug>(FindObjectsSortMode.None))
            { Undo.RecordObject(debug, "Disable hand preview"); debug.enabled = false; }
            EditorUtility.SetDirty(brain);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = host;
        }

        /// <summary>会話ラベル未指定の無効な設定例を作る。</summary>
        /// <param name="name">アセット名。</param>
        /// <param name="timing">タイミング。</param>
        /// <param name="actor">対象者。</param>
        /// <param name="action">行動条件。</param>
        private static DuelPresentationEvent Template(string name, DuelTiming timing, DuelActor actor, DuelActionFilter action)
        {
            string path = AssetFolder + "/" + name + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<DuelPresentationEvent>(path);
            if (item != null) return item;
            item = ScriptableObject.CreateInstance<DuelPresentationEvent>();
            var so = new SerializedObject(item);
            so.FindProperty("eventEnabled").boolValue = false;
            so.FindProperty("timing").enumValueIndex = (int)timing;
            so.FindProperty("actor").enumValueIndex = (int)actor;
            so.FindProperty("action").enumValueIndex = (int)action;
            so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(item, path); return item;
        }

        /// <summary>調整用の空オブジェクトを作る。</summary>
        /// <param name="name">表示名。</param>
        /// <param name="parent">親。</param>
        private static Transform Node(string name, Transform parent)
        { var node = new GameObject(name).transform; node.SetParent(parent, false); return node; }

        /// <summary>空UIの矩形を作る。</summary>
        /// <param name="name">表示名。</param>
        /// <param name="parent">親。</param>
        /// <param name="anchor">固定アンカー。</param>
        /// <param name="size">サイズ。</param>
        /// <param name="position">相対位置。</param>
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = 5; rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }

        /// <summary>確認用のテキストを作る。</summary>
        /// <param name="name">表示名。</param>
        /// <param name="parent">親。</param>
        /// <param name="anchor">固定アンカー。</param>
        /// <param name="size">サイズ。</param>
        /// <param name="position">相対位置。</param>
        /// <param name="fontSize">文字サイズ。</param>
        private static Text Label(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position, int fontSize)
        {
            var label = Rect(name, parent, anchor, size, position).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; label.color = Color.white; return label;
        }

        /// <summary>シリアライズされた参照を設定する。</summary>
        /// <param name="target">対象。</param>
        /// <param name="field">フィールド名。</param>
        /// <param name="value">参照。</param>
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }

        /// <summary>シリアライズされた参照配列を設定する。</summary>
        /// <param name="target">対象。</param>
        /// <param name="field">フィールド名。</param>
        /// <param name="values">参照一覧。</param>
        private static void SetArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target); var array = so.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
