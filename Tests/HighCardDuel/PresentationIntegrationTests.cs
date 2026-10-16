#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Casino.HighCardDuel;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>隔離したUnity検証プロジェクトでシーン生成と演出を検証する。</summary>
[InitializeOnLoad]
public static class PresentationIntegrationTests
{
    /// <summary>PlayModeの開始を監視する。</summary>
    static PresentationIntegrationTests() { EditorApplication.playModeStateChanged += Changed; }
    /// <summary>コピーしたシーンへ演出を接続し保存して再生する。</summary>
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
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        EditorSceneManager.OpenScene("Assets/Scenes/Scene_HightAndLow.unity");
        if (UnityEngine.Object.FindFirstObjectByType<DuelPresentationDirector>() == null)
            Type.GetType("Casino.HighCardDuel.Editor.DuelSceneSetup, Assembly-CSharp-Editor").GetMethod("Setup").Invoke(null, null);
        var comparison = GameObject.Find("Comparison").GetComponent<RectTransform>();
        comparison.anchorMin = comparison.anchorMax = new Vector2(.5f, .5f);
        EditorSceneManager.SaveOpenScenes();
        SessionState.SetBool("DuelPresentationTests", true);
        EditorApplication.isPlaying = true;
    }
    /// <summary>保存済みシーンに検証ランナーを追加する。</summary>
    /// <param name="state">現在のPlayMode状態。</param>
    private static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("DuelPresentationTests", false))
            new GameObject("PresentationTests").AddComponent<DuelPresentationTestRunner>();
    }
}

/// <summary>実際のCinemachineとカードモデルで完了待機・ADV接続を確認する。</summary>
public sealed class DuelPresentationTestRunner : MonoBehaviour
{
    private int checks;
    private HighCardDuelController controller;
    private Exception failure;
    /// <summary>アサーション失敗をファイルと終了コードへ記録する。</summary>
    private IEnumerator Start()
    {
        IEnumerator tests = Tests();
        while (true)
        {
            object value = null; bool more = false;
            try { more = tests.MoveNext(); if (more) value = tests.Current; }
            catch (Exception exception) { failure = exception; }
            if (failure != null)
            { System.IO.File.WriteAllText("../presentation-result.txt", failure.ToString()); EditorApplication.Exit(1); yield break; }
            if (!more) break;
            yield return value;
        }
        System.IO.File.WriteAllText("../presentation-result.txt", "PASS: " + checks + " presentation assertions");
        EditorApplication.Exit(0);
    }
    /// <summary>期待する状態を検証する。</summary>
    /// <param name="condition">期待条件。</param>
    /// <param name="message">失敗メッセージ。</param>
    private void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    /// <summary>検証用の非公開フィールドを設定する。</summary>
    /// <param name="target">対象。</param>
    /// <param name="name">フィールド名。</param>
    /// <param name="value">値。</param>
    private static void Set(object target, string name, object value)
    { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value); }
    /// <summary>選択前ADVを待機し、全公開とカメラ切替を検証する。</summary>
    private IEnumerator Tests()
    {
        controller = FindFirstObjectByType<HighCardDuelController>();
        controller.PresentationFailed += exception => failure = exception;
        var director = FindFirstObjectByType<DuelPresentationDirector>();
        var adv = new GameObject("MockADV").AddComponent<AdvEngineController>();
        var entry = ScriptableObject.CreateInstance<DuelPresentationEvent>();
        Set(entry, "timing", DuelTiming.BeforeCardSelection); Set(entry, "actor", DuelActor.Player);
        Set(entry, "scenarioLabel", "TestBeforeSelect"); Set(entry, "oncePerRound", true);
        Set(director, "events", new[] { entry });
        var brain = Camera.main.GetComponent<CinemachineBrain>();
        var playerHand = GameObject.Find("PlayerHand").GetComponent<RectTransform>();
        var dealerHand = GameObject.Find("DealerHand").GetComponent<RectTransform>();
        Vector2 playerHome = playerHand.anchoredPosition;
        Vector2 dealerHome = dealerHand.anchoredPosition;
        var canvas = (RectTransform)playerHand.GetComponentInParent<Canvas>().rootCanvas.transform;
        Check(GameObject.Find("DealerAnchor").transform.childCount == 0, "Dealer placeholder must be empty");
        float deadline = Time.realtimeSinceStartup + 60;
        while (!adv.IsPlaying)
        { Check(Time.realtimeSinceStartup < deadline, "ADV did not begin"); yield return null; }
        Check(controller.Game.State == DuelState.Presenting, "ADV did not hold game");
        Check(controller.Game.PlayerHand.Count == 3, "ADV changed hand");
        Check(!controller.Game.SelectCard(controller.Game.PlayerHand[0]), "Card selectable during ADV");
        Check(entry.Matches(controller.Game.PendingCue, DuelEventStage.BeforeVisual), "Event match failed");
        Check(!entry.Matches(controller.Game.PendingCue, DuelEventStage.AfterVisual), "Event stage filter failed");
        Set(entry, "actor", DuelActor.Dealer);
        Check(!entry.Matches(controller.Game.PendingCue, DuelEventStage.BeforeVisual), "Event actor filter failed");
        Set(entry, "actor", DuelActor.Player); Set(entry, "phase", 2);
        Check(!entry.Matches(controller.Game.PendingCue, DuelEventStage.BeforeVisual), "Event phase filter failed");
        Set(entry, "phase", 0); Set(entry, "action", DuelActionFilter.Raise);
        Check(!entry.Matches(controller.Game.PendingCue, DuelEventStage.BeforeVisual), "Event action filter failed");
        Set(entry, "action", DuelActionFilter.Any);
        long cueId = controller.Game.PendingCue.Id;
        controller.gameObject.SetActive(false);
        yield return null;
        controller.gameObject.SetActive(true);
        yield return null;
        yield return null;
        Check(adv.Starts == 1 && controller.Game.PendingCue.Id == cueId, "ADV repeated on retry");
        adv.Finish();
        int selections = 0;
        while (controller.Game.State != DuelState.Completed)
        {
            Check(Time.realtimeSinceStartup < deadline, "Round timed out");
            if (controller.Game.State == DuelState.SelectingCard)
            {
                Check(brain.ActiveVirtualCamera.Name == "NormalCamera" && !brain.IsBlending, "Selection before normal view ready");
                Check(Vector2.Distance(playerHand.anchoredPosition, playerHome) < .01f && Vector2.Distance(dealerHand.anchoredPosition, dealerHome) < .01f, "Hands not restored before selection");
                Check(controller.Game.SelectCard(controller.Game.PlayerHand[0]), "Select rejected"); selections++;
            }
            else if (controller.Game.State == DuelState.ChoosingBet)
            {
                Check(Vector2.Distance(playerHand.anchoredPosition, playerHome) < .01f && Vector2.Distance(dealerHand.anchoredPosition, dealerHome) < .01f, "Hands not restored before betting");
                controller.Act(controller.Game.CanAct(DuelAction.Check) ? DuelAction.Check : DuelAction.Call);
            }
            else if (controller.Game.PendingCue != null && controller.Game.PendingCue.Timing == DuelTiming.CardRevealed)
            {
                Check(RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, playerHand).max.y < canvas.rect.yMin, "Player hand not below screen during reveal");
                Check(RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, dealerHand).min.y > canvas.rect.yMax, "Dealer hand not above screen during reveal");
            }
            yield return null;
        }
        Check(selections == 3, "Did not reveal all cards");
        Check(adv.Starts == 1, "Once-per-round event repeated");
        Check(brain.ActiveVirtualCamera.Name == "TableCamera" && !brain.IsBlending, "Result before table view ready");
        var cards = FindFirstObjectByType<DuelTableCards>();
        Check(cards.transform.childCount == 6, "Missing or duplicated 3D card");
        foreach (Transform card in cards.transform)
        {
            Check(Vector3.Dot(card.up, Vector3.up) > .99f, "Card not face up"); Check(card.GetComponentInChildren<MeshRenderer>() != null, "No card mesh");
        }
        // シーンの3D部分を描画し、机上の配置とカードの表向きを目視確認できる画像を残す。
        var texture = new RenderTexture(1280, 720, 24);
        Camera camera = Camera.main; camera.targetTexture = texture; camera.Render();
        RenderTexture.active = texture;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
        System.IO.File.WriteAllBytes("../presentation-table.png", pixels.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = null;
        Destroy(texture); Destroy(pixels);
        Check(controller.StartRound(10), "Next round rejected");
        while (!adv.IsPlaying)
        { Check(Time.realtimeSinceStartup < deadline, "Next-round ADV missing"); yield return null; }
        Check(adv.Starts == 2, "Once-per-round event did not reset");
        Check(cards.transform.childCount == 0, "Old cards not cleared");
        Check(Vector2.Distance(playerHand.anchoredPosition, playerHome) < .01f && Vector2.Distance(dealerHand.anchoredPosition, dealerHome) < .01f, "Hands not restored on next round");
    }
}
#endif
