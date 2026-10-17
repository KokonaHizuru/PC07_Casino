#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Casino.Cards;
using Casino.Common;
using Casino.ThreeCardPoker;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>隔離コピーでシーンを構築して実際のモデルと操作を検証する。</summary>
[InitializeOnLoad]
public static class PokerIntegrationTests
{
    /// <summary>検証用のPlayMode開始を監視する。</summary>
    static PokerIntegrationTests() { EditorApplication.playModeStateChanged += Changed; }
    /// <summary>コピーしたシーンを設定・保存してから検証を開始する。</summary>
    public static void Run()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/TestPipeline.asset");
        if (pipeline == null)
        {
            var renderer = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/TestRenderer.asset");
            pipeline = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, "Assets/TestPipeline.asset");
        }
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        EditorSceneManager.OpenScene("Assets/Scenes/Scene_Poker.unity");
        Type.GetType("Casino.ThreeCardPoker.Editor.PokerSceneSetup, Assembly-CSharp-Editor").GetMethod("Setup").Invoke(null, null);
        // 再実行しても交換UIやイベントが増えないことを確認する。
        Type.GetType("Casino.ThreeCardPoker.Editor.PokerSceneSetup, Assembly-CSharp-Editor").GetMethod("Setup").Invoke(null, null);
        EditorSceneManager.SaveOpenScenes();
        UnityEngine.Object.FindFirstObjectByType<PokerSceneUI>().enabled = false;
        SessionState.SetBool("PokerTests", true); EditorApplication.isPlaying = true;
    }
    /// <summary>PlayModeで検証ランナーを生成する。</summary>
    /// <param name="state">現在の状態。</param>
    private static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PokerTests", false))
            new GameObject("PokerTests").AddComponent<PokerTestRunner>();
    }
}

/// <summary>UI、資金、カード公開、視点、宴、例外再試行を結合検証する。</summary>
public sealed class PokerTestRunner : MonoBehaviour
{
    private PokerController controller;
    private Exception failure;
    private int checks;
    private bool expectedFailure;
    private int failures;
    private PokerTiming failureTiming = PokerTiming.CardDealt;
    /// <summary>全検証を実行して例外もファイルに記録する。</summary>
    private IEnumerator Start()
    {
        yield return PresentationRoutine.Run(Tests(), e => failure = e);
        System.IO.File.WriteAllText("../scene-result.txt", failure == null ? "PASS: " + checks + " scene assertions" : failure.ToString());
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    /// <summary>条件を確認する。</summary>
    /// <param name="condition">期待条件。</param>
    /// <param name="message">失敗理由。</param>
    private void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    /// <summary>テスト用にInspector値を取得する。</summary>
    /// <param name="target">対象。</param>
    /// <param name="name">フィールド名。</param>
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(target);
    /// <summary>テスト用にInspector値を指定する。</summary>
    /// <param name="target">対象。</param>
    /// <param name="name">フィールド名。</param>
    /// <param name="value">値。</param>
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    /// <summary>ボタンの中心がカードに遮られていないことを確認する。</summary>
    /// <param name="button">操作ボタン。</param>
    private void CheckPointer(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
        }, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "Button blocked: " + button.name + "; first hit: " + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
    }
    /// <summary>演出完了まで上限付きで待つ。</summary>
    private IEnumerator Ready()
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (controller.Game.State == PokerState.Presenting)
        { if (failure != null) throw failure; Check(Time.realtimeSinceStartup < deadline, "Presentation timeout"); yield return null; }
    }
    /// <summary>宴の開始まで上限付きで待つ。</summary>
    /// <param name="adv">検証用の宴接続。</param>
    private IEnumerator WaitAdv(AdvEngineController adv)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!adv.IsPlaying)
        { if (failure != null) throw failure; Check(Time.realtimeSinceStartup < deadline, "ADV timeout"); yield return null; }
    }
    /// <summary>画面上のUIも含めて目視確認用画像を保存する。</summary>
    /// <param name="canvas">UI。</param>
    /// <param name="filename">保存先。</param>
    private IEnumerator Capture(Canvas canvas, string filename)
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main;
        canvas.planeDistance = Mathf.Max(.2f, Camera.main.nearClipPlane + .05f);
        Canvas.ForceUpdateCanvases(); yield return null;
        var target = new RenderTexture(1280, 720, 24); Camera.main.targetTexture = target; Camera.main.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); System.IO.File.WriteAllBytes(filename, pixels.EncodeToPNG());
        Camera.main.targetTexture = null; RenderTexture.active = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Destroy(target); Destroy(pixels);
    }
    /// <summary>カード配置後に一度だけ失敗させる。</summary>
    /// <param name="cue">演出通知。</param>
    private IEnumerator FailOnce(PokerCue cue)
    {
        if (expectedFailure && cue.Timing == failureTiming && (failureTiming != PokerTiming.CardExchanged || cue.Actor == PokerActor.Player))
        { yield return new WaitForSecondsRealtime(.15f); expectedFailure = false; throw new InvalidOperationException("Expected Poker presentation test failure"); }
        yield break;
    }
    /// <summary>選択前、Play、Fold、補充、次ラウンドを操作する。</summary>
    private IEnumerator Tests()
    {
        controller = FindFirstObjectByType<PokerController>();
        var ui = FindFirstObjectByType<PokerSceneUI>(FindObjectsInactive.Include);
        var director = FindFirstObjectByType<PokerPresentationDirector>();
        var table = FindFirstObjectByType<TableCardPresenter>();
        var player = (CardHandUI)Get(controller, "playerHand"); var dealer = (CardHandUI)Get(controller, "dealerHand");
        var playerRect = (RectTransform)player.transform; var dealerRect = (RectTransform)dealer.transform;
        var canvas = player.GetComponentInParent<Canvas>().rootCanvas; var canvasRect = (RectTransform)canvas.transform;
        Vector2 playerHome = playerRect.anchoredPosition, dealerHome = dealerRect.anchoredPosition;
        var brain = Camera.main.GetComponent<CinemachineBrain>();
        var play = (Button)Get(ui, "play"); var fold = (Button)Get(ui, "fold"); var next = (Button)Get(ui, "nextRound");
        var funding = (Button)Get(ui, "funding"); var retry = (Button)Get(ui, "retry"); var status = (Text)Get(ui, "status");
        var keep = (Button)Get(ui, "keepHand"); var exchangeArea = (RectTransform)Get(ui, "exchangeArea");
        Check(FindFirstObjectByType<Casino.HighCardDuel.HighCardDuelController>() == null, "Old controller remains");
        Check(GameObject.Find("DealerAnchor").transform.childCount == 0, "Dealer anchor changed");
        Check(Camera.main.GetComponent<Skybox>()?.material != null, "Background lost");
        Set(table, "moveDuration", .03f); Set(table, "flipDuration", .03f); Set(table, "transferDuration", .08f); Set(director, "resultSeconds", .03f);
        controller.PresentationFailed += e => { failures++; if (!e.Message.Contains("Expected Poker")) failure = e; };
        var adv = new GameObject("TestADV").AddComponent<AdvEngineController>();
        var choice = ScriptableObject.CreateInstance<PokerPresentationEvent>();
        Set(choice, "timing", PokerTiming.BeforePlayerExchange);
        Set(choice, "scenarioLabel", "TestChoice"); Set(choice, "oncePerRound", true);
        var qualify = ScriptableObject.CreateInstance<PokerPresentationEvent>();
        Set(qualify, "timing", PokerTiming.QualificationChecked); Set(qualify, "scenarioLabel", "TestQualification");
        Set(director, "events", new[] { choice, qualify });
        var data = new PlayerData { casinoMoney = 10 }; Set(ui, "data", data);
        controller.Initialize(data, 7); Check(controller.StartRound(10), "Start rejected");
        float drawDeadline = Time.realtimeSinceStartup + 30;
        while (controller.Game.PendingCue?.Timing != PokerTiming.CardDealt || table.transform.childCount == 0)
        { Check(Time.realtimeSinceStartup < drawDeadline, "3D draw not reached"); yield return null; }
        Check(player.Count == 0 && !player.Interactable, "UI appeared before 3D draw completed");
        controller.enabled = false; yield return null;
        yield return Capture(canvas, "../poker-draw.png");
        controller.enabled = true;
        yield return WaitAdv(adv);
        Check(!play.interactable && !fold.interactable && !controller.Act(PokerAction.Play), "Input during ADV");
        long cueId = controller.Game.PendingCue.Id;
        controller.gameObject.SetActive(false); yield return null; controller.gameObject.SetActive(true); yield return null; yield return null;
        Check(adv.Starts == 1 && controller.Game.PendingCue.Id == cueId, "ADV restarted");
        adv.UiModal.IsBlocking = true; adv.Finish(); yield return null; yield return null;
        Check(controller.Game.State == PokerState.Presenting, "Modal not awaited"); adv.UiModal.IsBlocking = false; yield return Ready();
        Check(brain.ActiveVirtualCamera.Name == "NormalCamera" && !brain.IsBlending, "Normal view not ready");
        Check(Vector2.Distance(playerRect.anchoredPosition, playerHome) < .01f && Vector2.Distance(dealerRect.anchoredPosition, dealerHome) < .01f, "Hand not restored");
        Check(player.Count == 3 && dealer.Count == 3 && table.TableCardCount == 0, "Initial card count");
        for (int i = 0; i < 3; i++) Check(player.GetCard(i).FaceUp && !dealer.GetCard(i).FaceUp, "Initial face visibility");
        Check(player.Interactable && !dealer.Interactable && keep.gameObject.activeSelf && exchangeArea.gameObject.activeSelf, "Exchange input mode");
        Check(controller.Game.State == PokerState.ChoosingExchange && controller.Game.DealerExchangeCompleted && !controller.Game.PlayerExchangeCompleted, "Exchange order");
        yield return null; CheckPointer(keep);
        var keepRect = (RectTransform)keep.transform;
        Check(keepRect.anchorMin == new Vector2(1, 0) && keepRect.anchoredPosition.x < 0 && keepRect.anchoredPosition.y > 0, "Keep Hand not bottom right");
        Check(exchangeArea.sizeDelta == new Vector2(900, 300) && exchangeArea.anchoredPosition == Vector2.zero, "Exchange area not expanded");
        Check(status.text.Contains("DEALER ???"), "Hidden role leaked");
        Check(status.preferredHeight <= status.rectTransform.rect.height + 1, "Status text clipped");
        int backs = 0; foreach (Transform card in table.transform) if (card.gameObject.activeSelf && Vector3.Dot(card.up, Vector3.up) < -.99f) backs++;
        Check(backs == table.DiscardCount && table.DiscardCount == controller.Game.DiscardedCards.Count, "Discard pile missing or face up"); yield return Capture(canvas, "../poker-exchange.png");
        var view = player.GetCard(2); PlayingCard previous = view.Card;
        int discarded = controller.Game.DiscardedCards.Count;
        Vector2 home = view.Rect.anchoredPosition;
        var pointer = new PointerEventData(EventSystem.current) { pointerId = 0, button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, view.Rect.position) };
        view.OnPointerEnter(pointer); view.OnBeginDrag(pointer);
        pointer.position = new Vector2(-100, -100); view.OnDrag(pointer); view.OnEndDrag(pointer);
        yield return new WaitForSecondsRealtime(.5f);
        Check(controller.Game.State == PokerState.ChoosingExchange && player.Count == 3 && controller.Game.DiscardedCards.Count == discarded, "Outside drop exchanged");
        Check(Vector2.Distance(view.Rect.anchoredPosition, home) < .1f, "Outside drop did not return home");
        // 交換演出を途中で中断し、その後の故意の例外でも二重交換しないことを確認する。
        expectedFailure = true; failureTiming = PokerTiming.CardExchanged; controller.Presentation += FailOnce;
        Set(table, "moveDuration", .25f);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, view.Rect.position);
        view.OnPointerEnter(pointer); view.OnBeginDrag(pointer);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, exchangeArea.position); view.OnDrag(pointer);
        Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, view.Rect.position), pointer.position) < .1f, "Drag did not follow mouse");
        view.OnEndDrag(pointer);
        float swapDeadline = Time.realtimeSinceStartup + 30;
        while (table.DiscardCount != discarded + 1)
        { Check(Time.realtimeSinceStartup < swapDeadline, "3D discard not reached"); yield return null; }
        Check(player.Count == 2 && controller.Game.PendingCue.Timing == PokerTiming.BeforeExchange, "Discard did not remove UI immediately");
        controller.enabled = false; yield return null; controller.enabled = true;
        while (controller.Game.PendingCue?.Timing != PokerTiming.CardExchanged)
        { Check(Time.realtimeSinceStartup < swapDeadline, "Exchange animation not reached"); yield return null; }
        long swapId = controller.Game.PendingCue.Id; yield return null;
        controller.enabled = false; yield return null; controller.enabled = true;
        while (failures == 0) { Check(Time.realtimeSinceStartup < swapDeadline, "Exchange retry failure not reached"); yield return null; }
        Check(controller.Game.PendingCue.Id == swapId && controller.Game.DiscardedCards.Count == discarded + 1, "Interrupted exchange drew twice");
        retry.onClick.Invoke(); yield return Ready();
        controller.Presentation -= FailOnce; failureTiming = PokerTiming.CardDealt;
        Set(table, "moveDuration", .03f);
        Check(controller.Game.State == PokerState.ChoosingAction && controller.Game.PlayerExchangeCompleted && !player.Interactable, "Exchange did not finish");
        Check(player.Count == 3 && table.TableCardCount == 0 && controller.Game.DiscardedCards.Count == discarded + 1, "Exchange duplicated cards");
        Check(!player.GetCard(2).Card.Equals(previous) && player.GetCard(2).Card.Equals(controller.Game.PlayerCards[2]), "Replacement position mismatch");
        Check(controller.Game.Balance == 0 && controller.Game.PlayBet == 0 && !controller.SkipExchange(), "Exchange charged or allowed twice");
        Check(!exchangeArea.gameObject.activeSelf && !keep.gameObject.activeSelf && play.interactable && fold.interactable, "Post exchange controls");
        // 非表示から戻したボタンのCanvas更新を待って当たり判定を確認する。
        yield return null; CheckPointer(play); CheckPointer(fold);
        yield return Capture(canvas, "../poker-choice.png");
        play.onClick.Invoke(); yield return Ready();
        Check(controller.Game.State == PokerState.WaitingForFunds && controller.Game.PlayBet == 0 && !controller.Game.DealerCardsRevealed, "Play funding wait");
        Check(funding.gameObject.activeSelf && !play.interactable && !fold.interactable, "Funding controls");
        funding.onClick.Invoke();
        float showdownDeadline = Time.realtimeSinceStartup + 30;
        while (controller.Game.PendingCue?.Timing != PokerTiming.BeforeShowdown || table.TableCardCount == 0)
        { Check(Time.realtimeSinceStartup < showdownDeadline, "Showdown placement not reached"); yield return null; }
        controller.enabled = false; yield return null; controller.enabled = true;
        yield return WaitAdv(adv);
        Check(adv.Starts == 2 && controller.Game.PendingCue.Timing == PokerTiming.QualificationChecked, "Qualification event");
        bool qualified = controller.Game.DealerQualified.Value;
        Set(qualify, "qualification", qualified ? PokerQualificationFilter.NotQualified : PokerQualificationFilter.Qualified);
        Check(!qualify.Matches(controller.Game.PendingCue, PokerEventStage.BeforeVisual), "Qualification filter ignored");
        Set(qualify, "qualification", qualified ? PokerQualificationFilter.Qualified : PokerQualificationFilter.NotQualified);
        Check(qualify.Matches(controller.Game.PendingCue, PokerEventStage.BeforeVisual), "Qualification filter mismatch");
        Check(table.TableCardCount == 6, "Showdown must place both final hands");
        var placedCards = (IDictionary)Get(table, "cards");
        foreach (PlayingCard card in controller.Game.PlayerCards) Check(placedCards.Contains(card), "Player final card missing");
        foreach (PlayingCard card in controller.Game.DealerCards) Check(placedCards.Contains(card), "Dealer final card missing");
        foreach (PlayingCard card in controller.Game.DiscardedCards) Check(!placedCards.Contains(card), "Discarded card placed");
        for (int i = 0; i < 3; i++) Check(dealer.GetCard(i).FaceUp, "UI flip missing");
        foreach (object placement in placedCards.Values) Check(Vector3.Dot(((Transform)Get(placement, "Transform")).up, Vector3.up) > .99f, "3D flip missing");
        Check(table.DiscardCount == controller.Game.DiscardedCards.Count, "Discard pile count mismatch");
        Check(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, playerRect).max.y < canvasRect.rect.yMin, "Player hand not hidden");
        Check(RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, dealerRect).min.y > canvasRect.rect.yMax, "Dealer hand not hidden");
        adv.Finish(); yield return Ready();
        Check(controller.Game.State == PokerState.Completed && data.casinoMoney == controller.Game.Payout && controller.Game.PlayBet == 10, "Settlement");
        var comparison = (Text)Get(director, "comparisonText");
        Check(comparison.preferredHeight <= comparison.rectTransform.rect.height + 1, "Result text clipped");
        yield return Capture(canvas, "../poker-result.png");
        // 次ラウンドの配布途中に例外を挟み、Retryでもカードを重複させない。
        Set(director, "events", Array.Empty<PokerPresentationEvent>());
        data.casinoMoney = 1000; expectedFailure = true; controller.Presentation += FailOnce;
        int previousFailures = failures;
        next.onClick.Invoke(); float deadline = Time.realtimeSinceStartup + 30;
        while (failures == previousFailures) { Check(Time.realtimeSinceStartup < deadline, "Failure not reached"); yield return null; }
        Check(retry.gameObject.activeSelf && controller.Game.State == PokerState.Presenting, "Failed presentation not paused");
        Check(player.Count == 1 && table.TableCardCount == 0 && table.DiscardCount == 0, "Unexpected first deal count / old discards remain");
        retry.onClick.Invoke(); yield return Ready();
        Check(player.Count == 3 && dealer.Count == 3 && table.TableCardCount == 0 && failures == previousFailures + 1, "Retry duplicated cards");
        controller.Presentation -= FailOnce;
        var kept = new[] { player.GetCard(0).Card, player.GetCard(1).Card, player.GetCard(2).Card };
        Check(controller.Game.State == PokerState.ChoosingExchange, "Second round missing exchange");
        keep.onClick.Invoke(); yield return Ready();
        for (int i = 0; i < 3; i++) Check(player.GetCard(i).Card.Equals(kept[i]), "Keep hand changed cards");
        fold.onClick.Invoke(); yield return Ready();
        Check(controller.Game.Result == PokerResult.PlayerFold && data.casinoMoney == 990 && controller.Game.PlayBet == 0, "Fold payout");
        Check(!controller.Game.DealerCardsRevealed && controller.Game.DealerQualified == null, "Fold revealed dealer");
        for (int i = 0; i < 3; i++) Check(!dealer.GetCard(i).FaceUp, "Fold leaked UI");
        Check(status.text.Contains("DEALER ???"), "Fold leaked role");
        Check(table.TableCardCount == 0 && brain.ActiveVirtualCamera.Name == "NormalCamera", "Fold placed cards or changed to table camera");
    }
}
#endif
