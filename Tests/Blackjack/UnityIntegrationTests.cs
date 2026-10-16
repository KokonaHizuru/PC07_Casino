#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Casino.Blackjack;
using Casino.Cards;
using Casino.Common;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>検証コピーでシーンを構築し、実際の表示と操作を確認する。</summary>
[InitializeOnLoad]
public static class BlackjackIntegrationTests
{
    /// <summary>PlayMode開始を監視する。</summary>
    static BlackjackIntegrationTests() { EditorApplication.playModeStateChanged += Changed; }
    /// <summary>シーンを移行・保存してから検証用の設定で再生する。</summary>
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Scene_Blackjack.unity");
        Type.GetType("Casino.Blackjack.Editor.BlackjackSceneSetup, Assembly-CSharp-Editor").GetMethod("Setup").Invoke(null, null);
        EditorSceneManager.SaveOpenScenes();
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/TestPipeline.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        UnityEngine.Object.FindFirstObjectByType<BlackjackSceneUI>().enabled = false;
        SessionState.SetBool("BlackjackTests", true); EditorApplication.isPlaying = true;
    }
    /// <summary>検証ランナーを生成する。</summary>
    /// <param name="state">状態。</param>
    private static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("BlackjackTests", false))
            new GameObject("BlackjackTests").AddComponent<BlackjackTestRunner>();
    }
}

/// <summary>ゲームロジックと手札、3D、カメラ、宴接続の結合テスト。</summary>
public sealed class BlackjackTestRunner : MonoBehaviour
{
    private int checks;
    private Exception failure;
    private BlackjackController controller;
    /// <summary>例外も記録して検証プロセスを終了する。</summary>
    private IEnumerator Start()
    {
        yield return PresentationRoutine.Run(Tests(), e => failure = e);
        System.IO.File.WriteAllText("../blackjack-result.txt", failure == null ? "PASS: " + checks + " scene assertions" : failure.ToString());
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    /// <summary>条件を検証する。</summary>
    /// <param name="condition">条件。</param>
    /// <param name="message">失敗理由。</param>
    private void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    /// <summary>テスト専用にInspector項目を設定する。</summary>
    /// <param name="target">対象。</param>
    /// <param name="name">項目。</param>
    /// <param name="value">値。</param>
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    /// <summary>演出の完了まで上限付きで待つ。</summary>
    private IEnumerator Ready()
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (controller.Game.State == BlackjackState.Presenting)
        { if (failure != null) throw failure; Check(Time.realtimeSinceStartup < deadline, "Presentation timeout"); yield return null; }
    }
    /// <summary>二度Hitできる山札を探す。</summary>
    private static int Seed()
    {
        for (int seed = 0; seed < 10000; seed++)
        {
            var deck = new CardDeck(seed); deck.Reset(); deck.Shuffle();
            deck.TryDraw(out var a); deck.TryDraw(out var b); deck.TryDraw(out var c); deck.TryDraw(out var d); deck.TryDraw(out var e);
            if (BlackjackScore.Evaluate(new[] { a, c }).Total <= 11 && !BlackjackScore.Evaluate(new[] { b, d }).IsBlackjack
                && BlackjackScore.Evaluate(new[] { a, c, e }).Total <= 16) return seed;
        }
        throw new Exception("Seed not found");
    }
    /// <summary>ボタン、伏せ札、イベント再開と配布枚数を検証する。</summary>
    private IEnumerator Tests()
    {
        controller = FindFirstObjectByType<BlackjackController>();
        var ui = FindFirstObjectByType<BlackjackSceneUI>(FindObjectsInactive.Include);
        var director = FindFirstObjectByType<BlackjackPresentationDirector>();
        var table = FindFirstObjectByType<TableCardPresenter>();
        var player = GameObject.Find("PlayerHand").GetComponent<CardHandUI>();
        var dealer = GameObject.Find("DealerHand").GetComponent<CardHandUI>();
        var playerRect = (RectTransform)player.transform; var dealerRect = (RectTransform)dealer.transform;
        Vector2 playerHome = playerRect.anchoredPosition, dealerHome = dealerRect.anchoredPosition;
        var brain = Camera.main.GetComponent<CinemachineBrain>();
        var hit = GameObject.Find("Hit").GetComponent<Button>();
        var stand = GameObject.Find("Stand").GetComponent<Button>();
        var doubleDown = GameObject.Find("Double Down").GetComponent<Button>();
        Check(FindFirstObjectByType<Casino.HighCardDuel.HighCardDuelController>() == null, "Old controller remains");
        Check(GameObject.Find("DealerAnchor").transform.childCount == 0, "Dealer placeholder changed");
        Check(Camera.main.GetComponent<Skybox>()?.material != null, "Background lost");
        Set(table, "moveDuration", .03f); Set(table, "flipDuration", .03f); Set(director, "resultSeconds", .03f);
        controller.PresentationFailed += e => failure = e;
        var adv = new GameObject("TestADV").AddComponent<AdvEngineController>();
        var entry = ScriptableObject.CreateInstance<BlackjackPresentationEvent>();
        Set(entry, "scenarioLabel", "TestChoice"); Set(entry, "oncePerRound", true);
        Set(director, "events", new[] { entry });
        var data = new PlayerData { casinoMoney = 1000 }; Set(ui, "data", data);
        controller.Initialize(data, Seed()); Check(controller.StartRound(10), "Start rejected");
        float deadline = Time.realtimeSinceStartup + 30;
        while (!adv.IsPlaying) { if (failure != null) throw failure; Check(Time.realtimeSinceStartup < deadline, "ADV missing"); yield return null; }
        Check(controller.Game.State == BlackjackState.Presenting && !hit.interactable, "ADV did not block");
        long cue = controller.Game.PendingCue.Id;
        controller.gameObject.SetActive(false); yield return null; controller.gameObject.SetActive(true); yield return null; yield return null;
        Check(adv.Starts == 1 && controller.Game.PendingCue.Id == cue, "ADV restarted after resume");
        adv.Finish(); yield return Ready();
        Check(brain.ActiveVirtualCamera.Name == "NormalCamera" && !brain.IsBlending, "Input before normal camera");
        Check(Vector2.Distance(playerRect.anchoredPosition, playerHome) < .01f && Vector2.Distance(dealerRect.anchoredPosition, dealerHome) < .01f, "Hands not restored");
        Check(player.Count == 2 && dealer.Count == 2 && table.transform.childCount == 4, "Initial card count");
        Check(player.GetCard(0).FaceUp && player.GetCard(1).FaceUp && dealer.GetCard(0).FaceUp && !dealer.GetCard(1).FaceUp, "Initial faces");
        Check(!player.Interactable && !dealer.Interactable, "Blackjack hand draggable");
        int backCount = 0; foreach (Transform card in table.transform) if (Vector3.Dot(card.up, Vector3.up) < -.99f) backCount++;
        Check(backCount == 1, "3D hidden card count");
        hit.onClick.Invoke(); yield return Ready();
        Check(player.Count == 3 && !doubleDown.interactable, "Hit or Double restriction");
        hit.onClick.Invoke(); yield return Ready();
        Check(player.Count == 4, "Second Hit missing");
        if (controller.Game.State == BlackjackState.ChoosingAction) { stand.onClick.Invoke(); yield return Ready(); }
        Check(controller.Game.State == BlackjackState.Completed, "Round incomplete");
        Check(table.transform.childCount == player.Count + dealer.Count, "3D duplicate or missing card");
        Check(adv.Starts == 1, "Once-per-round repeated");
        var canvas = (RectTransform)player.GetComponentInParent<Canvas>().rootCanvas.transform;
        Check(RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, playerRect).max.y < canvas.rect.yMin, "Player not hidden");
        Check(RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, dealerRect).min.y > canvas.rect.yMax, "Dealer not hidden");
        var comparison = GameObject.Find("Comparison").GetComponent<Text>();
        Check(comparison.cachedTextGenerator.lineCount >= 2 || comparison.preferredHeight <= comparison.rectTransform.rect.height, "Result text clipped");
        var overlay = player.GetComponentInParent<Canvas>().rootCanvas;
        overlay.renderMode = RenderMode.ScreenSpaceCamera; overlay.worldCamera = Camera.main; overlay.planeDistance = .2f;
        Canvas.ForceUpdateCanvases(); yield return null;
        var target = new RenderTexture(1280, 720, 24); Camera.main.targetTexture = target; Camera.main.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
        System.IO.File.WriteAllBytes("../blackjack-table.png", pixels.EncodeToPNG()); Camera.main.targetTexture = null; RenderTexture.active = null;
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        Destroy(target); Destroy(pixels);
        Set(director, "events", Array.Empty<BlackjackPresentationEvent>());
        data.casinoMoney = 10; controller.Initialize(data, Seed()); controller.StartRound(10); yield return Ready();
        Check(table.transform.childCount == 4, "Old table cards not cleared");
        doubleDown.onClick.Invoke(); yield return Ready();
        Check(controller.Game.State == BlackjackState.WaitingForFunds && player.Count == 2, "Double funding wait");
        GameObject.Find("Resume Funds").GetComponent<Button>().onClick.Invoke(); yield return Ready();
        Check(controller.Game.State == BlackjackState.Completed && controller.Game.PlayerBet == 20 && player.Count == 3, "Double funded resume");
        Check(dealer.GetCard(1).FaceUp == controller.Game.DealerHoleRevealed, "Hole reveal UI mismatch");
        // 共通表示は多数枚でも最大幅の中に収める。
        table.Clear(); yield return null;
        for (int i = 0; i < 11; i++) yield return table.Place(new PlayingCard(CardSuit.Club, i + 1), true, i, true);
        Check(table.transform.childCount == 11, "Dynamic row limited to three cards");
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        foreach (Transform card in table.transform) { min = Mathf.Min(min, card.position.x); max = Mathf.Max(max, card.position.x); }
        Check(max - min <= .601f, "Dynamic row exceeded width");
    }
}
#endif

