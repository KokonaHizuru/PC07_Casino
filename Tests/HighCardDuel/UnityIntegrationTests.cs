#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Casino.Cards;
using Casino.HighCardDuel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class UnityIntegrationTests
{
    /// <summary>一時プロジェクトのPlayMode開始を監視する。</summary>
    static UnityIntegrationTests() { EditorApplication.playModeStateChanged += Changed; }
    /// <summary>検証用の空シーンを起動する。</summary>
    public static void Run()
    {
        SessionState.SetBool("DuelTests", true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    /// <summary>検証用ランナーを起動する。</summary>
    /// <param name="state">エディター状態。</param>
    private static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("DuelTests", false))
            new GameObject("DuelTests").AddComponent<DuelIntegrationRunner>();
    }
}

public sealed class DuelIntegrationRunner : MonoBehaviour
{
    private HighCardDuelController controller;
    private bool failOnce;
    private int failures;
    private int checks;

    /// <summary>テスト例外を捕捉しプロセスの終了コードへ反映する。</summary>
    private IEnumerator Start()
    {
        var routine = Tests();
        while (true)
        {
            object value = null; bool more = false;
            try { more = routine.MoveNext(); if (more) value = routine.Current; }
            catch (Exception exception)
            {
                System.IO.File.WriteAllText("../integration-result.txt", exception.ToString());
                EditorApplication.Exit(1); yield break;
            }
            if (!more) break;
            yield return value;
        }
        System.IO.File.WriteAllText("../integration-result.txt", "PASS: " + checks + " integration assertions");
        EditorApplication.Exit(0);
    }

    /// <summary>条件を検証する。</summary>
    /// <param name="condition">期待条件。</param>
    /// <param name="message">検証名。</param>
    private void Check(bool condition, string message)
    {
        checks++; if (!condition) throw new Exception(message);
    }

    /// <summary>演出の待機と例外を模擬する。</summary>
    /// <param name="cue">演出通知。</param>
    private IEnumerator Animation(DuelCue cue)
    {
        if (failOnce) { failOnce = false; throw new Exception("Expected presentation failure"); }
        yield return null;
        yield return new WaitForSecondsRealtime(0.02f);
        Check(controller.Game.PendingCue == cue && controller.Game.State == DuelState.Presenting, "Advanced before animation ended");
    }

    /// <summary>UIドラッグと実際のUnityコルーチンを通じて進行を検証する。</summary>
    private IEnumerator Tests()
    {
        new GameObject("EventSystem", typeof(EventSystem));
        var table = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MyAssets/Prefabs/Cards/CardTableUI.prefab"));
        CardHandUI player = table.transform.Find("PlayerHand").GetComponent<CardHandUI>();
        CardHandUI dealer = table.transform.Find("DealerHand").GetComponent<CardHandUI>();
        typeof(CardHandUI).GetField("curveDepth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 18f);
        typeof(CardHandUI).GetField("fanAngle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 8f);
        typeof(CardHandUI).GetField("spacing", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 140f);
        var area = (RectTransform)new GameObject("PlayArea", typeof(RectTransform)).transform;
        area.SetParent(table.transform, false); area.sizeDelta = new Vector2(400, 200);
        var host = new GameObject("Controller"); host.SetActive(false);
        controller = host.AddComponent<HighCardDuelController>();
        typeof(HighCardDuelController).GetField("playerHand", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, player);
        typeof(HighCardDuelController).GetField("dealerHand", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, dealer);
        host.SetActive(true);
        controller.SetPlayArea(area);
        var data = new PlayerData { casinoMoney = 10 };
        var settings = new HighCardDealerSettings { raiseWhenAhead = 1, raiseWhenEven = 1, raiseWhenBehind = 1 };
        controller.Initialize(data, new HighCardDealer(settings, 4), 4);
        controller.Presentation += Animation;
        controller.PresentationFailed += exception => failures++;
        failOnce = true;
        Check(controller.StartRound(10), "Start round");
        yield return null;
        Check(failures == 1 && controller.Game.PendingCue.Timing == DuelTiming.RoundStarted && data.casinoMoney == 10, "Failure skipped cue or charged money");
        Check(controller.RetryPresentation(), "Retry failed presentation");
        float deadline = Time.realtimeSinceStartup + 10;
        while (controller.Game.State != DuelState.SelectingCard) { Check(Time.realtimeSinceStartup < deadline, "Deal timeout"); yield return null; }
        Check(player.Count == 3 && dealer.Count == 3 && data.casinoMoney == 0, "Deal integration");
        for (int i = 0; i < 3; i++) Check(!player.GetCard(i).FaceUp && !dealer.GetCard(i).FaceUp, "Hidden hands");
        Check(player.GetCard(0).Rect.anchoredPosition.y == -18 && player.GetCard(1).Rect.anchoredPosition.y == 0, "Hand curve missing");
        Check(Quaternion.Angle(player.GetCard(0).Rect.localRotation, Quaternion.Euler(0, 0, 8)) < .01f, "Left fan angle");
        Check(Quaternion.Angle(player.GetCard(2).Rect.localRotation, Quaternion.Euler(0, 0, -8)) < .01f, "Right fan angle");
        var returningCard = player.GetCard(0);
        Vector2 originalPosition = returningCard.Rect.anchoredPosition;
        var outside = new PointerEventData(EventSystem.current) { pointerId = -1, position = new Vector2(-500, -500), button = PointerEventData.InputButton.Left };
        returningCard.OnPointerEnter(outside); returningCard.OnBeginDrag(outside);
        Check(Quaternion.Angle(returningCard.Rect.localRotation, Quaternion.identity) < .01f, "Dragging card must be upright");
        returningCard.OnEndDrag(outside);
        yield return new WaitForSecondsRealtime(.35f);
        Check(player.Count == 3 && Vector2.Distance(returningCard.Rect.anchoredPosition, originalPosition) < .01f, "Return position changed");
        Check(Quaternion.Angle(returningCard.Rect.localRotation, Quaternion.Euler(0, 0, 8)) < .01f, "Return angle changed");
        for (int phase = 1; phase <= 3; phase++)
        {
            Canvas.ForceUpdateCanvases();
            var view = player.GetCard(player.Count - 1);
            var card = view.Card;
            var pointer = new PointerEventData(EventSystem.current) { pointerId = -1, position = area.position, button = PointerEventData.InputButton.Left };
            view.OnPointerEnter(pointer); view.OnBeginDrag(pointer); view.OnEndDrag(pointer);
            Check(controller.Game.PendingCue.Timing == DuelTiming.CardSelected && !player.Interactable, "Drop did not reach game");
            if (phase == 1)
            {
                long cueId = controller.Game.PendingCue.Id;
                controller.enabled = false;
                yield return new WaitForSecondsRealtime(.08f);
                Check(controller.Game.PendingCue.Id == cueId, "Disabled controller advanced");
                controller.enabled = true;
            }
            deadline = Time.realtimeSinceStartup + 10;
            while (controller.Game.State == DuelState.Presenting) { Check(Time.realtimeSinceStartup < deadline, "Reveal timeout"); yield return null; }
            Check(controller.Game.PlayerRevealed[phase - 1].Equals(card), "Drag selection changed");
            Check(player.Count == 3 - phase && dealer.Count == 3 - phase, "Hand sync");
            if (player.Count == 1)
                Check(player.GetCard(0).Rect.anchoredPosition == Vector2.zero && Quaternion.Angle(player.GetCard(0).Rect.localRotation, Quaternion.identity) < .01f, "Single card must be centered and upright");
            if (phase == 3) break;
            Check(controller.Game.CanAct(DuelAction.Call), "Dealer did not act first");
            Check(controller.Act(DuelAction.Call), "Call input");
            deadline = Time.realtimeSinceStartup + 10;
            while (controller.Game.State != DuelState.WaitingForFunds) { Check(Time.realtimeSinceStartup < deadline, "Funding timeout"); yield return null; }
            Check(controller.Game.RequiredFunds == 10 && controller.Game.Shortage == 10, "Funding information");
            data.casinoMoney += 10;
            Check(controller.ResumeAfterFunding(), "Resume call");
            deadline = Time.realtimeSinceStartup + 10;
            while (controller.Game.State != DuelState.SelectingCard) { Check(Time.realtimeSinceStartup < deadline, "Next phase timeout"); yield return null; }
        }
        Check(controller.Game.State == DuelState.Completed && failures == 1, "Completion");
        Check(data.casinoMoney == controller.Game.Payout, "Wallet settlement");
    }
}
#endif
