using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Casino.Common;
using Casino.Story;
using Casino.Story.Editor;
using Casino.HighCardDuel;
using Casino.Blackjack;
using Casino.ThreeCardPoker;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>コピーした検証専用プロジェクトで実際の宴と全シーンを検証する。</summary>
[InitializeOnLoad]
public static class CasinoStoryValidation
{
    private const string Key = "CasinoStoryValidation.Running";
    private static double started;
    private static bool clicked;
    private static PlayerData player;
    private static string lastScene;
    private static readonly List<string> visited = new List<string>();
    private static readonly Dictionary<string, int> rounds = new Dictionary<string, int>();
    private static HighCardDuelController duel;
    private static PokerController poker;
    private static BlackjackController blackjack;
    private static MonoBehaviour ui;
    private static Button next;
    private static int assertions;
    private static bool commandMode;
    private static bool sawCustomRaise;
    private static bool sawNewBaseBet;
    /// <summary>再生用ドメインへ移った後も検証を継続する。</summary>
    static CasinoStoryValidation()
    {
        commandMode = SessionState.GetBool("CasinoStoryValidation.Commands", false);
        if (SessionState.GetBool(Key, false)) { started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
    }
    /// <summary>条件を検証する。</summary>
    /// <param name="condition">成立すべき条件。</param>
    /// <param name="message">検証内容。</param>
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }
    /// <summary>隔離プロジェクトだけに検証用ADVを追加してコマンドを実再生する。</summary>
    public static void RunCommands()
    {
        SessionState.SetBool("CasinoStoryValidation.Commands", true);
        RunInternal();
    }
    /// <summary>検証専用イベントと台詞中のBET変更を準備する。</summary>
    private static void PrepareCommandScenarios()
    {
        string source = CasinoStoryAssetBuilder.SourceFolder + "/CasinoStory_HightCard.xls";
        var rows = Utage.ExcelParser.ExcelParser.Read(source, '#', false, false, true).List.SelectMany((item, index) => item.Grid.Rows.Skip(index == 0 ? 0 : 1)).ToArray();
        string text = string.Join("\n", rows.Select(row => string.Join("\t", row.Strings).TrimEnd('\t'))) + "\n";
        text = text.Replace("*Event_HightCard_Opening_Default\n", "*Event_HightCard_Opening_Default\nCasinoSetBaseBet\tHightCard\t20\nCasinoSetFlag\tValidationRaise\tTRUE\n");
        text = text.Replace("*Event_HightCard_RoundStarted_Default\n", "*Event_HightCard_RoundStarted_Default\nCasinoSetBaseBet\tHightCard\t30\n");
        text += "*ValidationDealerRaise\nCasinoSetDealerRaise\t100\n\t\t\t\t\t\t\t\tValidationDealerRaise\nEndScenario\n";
        var workbook = new NPOI.HSSF.UserModel.HSSFWorkbook();
        var sheet = workbook.CreateSheet("CasinoStory");
        var lines = text.TrimEnd('\n').Split('\n');
        for (int r = 0; r < lines.Length; r++)
        {
            var row = sheet.CreateRow(r);
            var cells = lines[r].Split('\t');
            for (int c = 0; c < cells.Length; c++) row.CreateCell(c).SetCellValue(cells[c]);
        }
        using (var stream = File.Create(source)) workbook.Write(stream);
        var entry = ScriptableObject.CreateInstance<DuelPresentationEvent>();
        var so = new SerializedObject(entry);
        so.FindProperty("flagId").stringValue = "ValidationRaise";
        so.FindProperty("timing").enumValueIndex = (int)DuelTiming.BeforeAction;
        so.FindProperty("stage").enumValueIndex = (int)DuelEventStage.BeforeVisual;
        so.FindProperty("actor").enumValueIndex = (int)DuelActor.Dealer;
        so.FindProperty("action").enumValueIndex = (int)DuelActionFilter.Raise;
        so.FindProperty("scenarioLabel").stringValue = "ValidationDealerRaise";
        var flags = so.FindProperty("completionFlags"); flags.arraySize = 2;
        flags.GetArrayElementAtIndex(0).FindPropertyRelative("flagId").stringValue = "ValidationRaise";
        flags.GetArrayElementAtIndex(0).FindPropertyRelative("value").boolValue = false;
        flags.GetArrayElementAtIndex(1).FindPropertyRelative("flagId").stringValue = "ValidationRaiseFinished";
        flags.GetArrayElementAtIndex(1).FindPropertyRelative("value").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(entry, CasinoStoryAssetBuilder.Root + "HightCard/Event_HightCard_BeforeAction_Validation.asset");
        CasinoStoryAssetBuilder.Rebuild();
    }
    /// <summary>フラグ条件と自動登録を検証しタイトルから再生を開始する。</summary>
    public static void Run()
    {
        SessionState.SetBool("CasinoStoryValidation.Commands", false);
        RunInternal();
    }
    /// <summary>選択した検証モードで一連のシーンを検証する。</summary>
    private static void RunInternal()
    {
        try
        {
            CasinoStoryAssetBuilder.Rebuild();
            var catalog = AssetDatabase.LoadAssetAtPath<CasinoEventCatalog>(CasinoStoryAssetBuilder.Root + "CasinoEventCatalog.asset");
            Check(catalog.hightCard.Length == 17 && catalog.blackjack.Length == 17 && catalog.poker.Length == 22, "All timing assets registered");
            var a = ScriptableObject.CreateInstance<DuelPresentationEvent>();
            var b = ScriptableObject.CreateInstance<DuelPresentationEvent>();
            var fallback = ScriptableObject.CreateInstance<DuelPresentationEvent>();
            SetString(a, "flagId", "branch"); SetString(b, "flagId", "branch");
            var flags = new FlagData();
            var selection = new PresentationEventSelection<DuelPresentationEvent>();
            Check(selection.Select("off", new[] { fallback, a, b }, flags, e => true).SequenceEqual(new[] { fallback }), "Fallback when flags are absent");
            flags.SetFlag("branch", true);
            Check(selection.Select("on", new[] { fallback, a, b }, flags, e => true).SequenceEqual(new[] { a, b }), "All duplicate matching flags play; no fallback");
            flags.SetFlag("branch", false);
            Check(selection.Select("on", new[] { fallback, a, b }, flags, e => true).Count == 2, "Retry keeps original selection");
            var so = new SerializedObject(a); var changes = so.FindProperty("completionFlags"); changes.arraySize = 2;
            changes.GetArrayElementAtIndex(0).FindPropertyRelative("flagId").stringValue = "branch";
            changes.GetArrayElementAtIndex(0).FindPropertyRelative("value").boolValue = false;
            changes.GetArrayElementAtIndex(1).FindPropertyRelative("flagId").stringValue = "next";
            changes.GetArrayElementAtIndex(1).FindPropertyRelative("value").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo(); a.ApplyCompletionFlags(flags);
            Check(flags.Is("branch", false) && flags.Is("next", true), "Completion changes both flags");
            var session = new CasinoStorySession(); session.SetBaseBet(CasinoGameKind.Poker, 100);
            Check(session.GetBaseBet(CasinoGameKind.Poker, 10) == 100, "Next base bet override"); session.Reset();
            Check(session.GetBaseBet(CasinoGameKind.Poker, 10) == 10, "New Game clears overrides");
            var data = new PlayerData(); data.Reset(); Check(data.casinoMoney == 1000, "Default money 1000");
            UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(fallback);
            // 一時イベントを実際に追加して直下だけの自動収集を確認する。
            string direct = CasinoStoryAssetBuilder.Root + "HightCard/ValidationTemporary.asset";
            string nested = CasinoStoryAssetBuilder.Root + "HightCard/ValidationNested";
            AssetDatabase.CreateFolder(CasinoStoryAssetBuilder.Root + "HightCard", "ValidationNested");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DuelPresentationEvent>(), direct);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DuelPresentationEvent>(), nested + "/Ignored.asset");
            CasinoStoryAssetBuilder.Rebuild(); Check(catalog.hightCard.Length == 18, "Direct child only, nested excluded");
            AssetDatabase.DeleteAsset(direct); AssetDatabase.DeleteAsset(nested); CasinoStoryAssetBuilder.Rebuild();
            Check(catalog.hightCard.Length == 17, "Removed assets unregister");
            Debug.Log("STORY_EDIT_TESTS_PASS " + assertions);
            if (SessionState.GetBool("CasinoStoryValidation.Commands", false)) PrepareCommandScenarios();
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
            EditorApplication.isPlaying = true;
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    /// <summary>テスト用のフラグIDを設定する。</summary>
    /// <param name="target">対象アセット。</param>
    /// <param name="property">設定名。</param>
    /// <param name="value">設定値。</param>
    private static void SetString(UnityEngine.Object target, string property, string value)
    { var so = new SerializedObject(target); so.FindProperty(property).stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    /// <summary>実際のシーン・UI・宴を順番に通して最終到達を検証する。</summary>
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 240) throw new Exception("Story playback timeout in " + lastScene);
            string scene = SceneManager.GetActiveScene().name;
            if (scene != lastScene)
            {
                lastScene = scene; visited.Add(scene); Debug.Log("STORY_VISIT " + scene);
                duel = UnityEngine.Object.FindFirstObjectByType<HighCardDuelController>();
                poker = UnityEngine.Object.FindFirstObjectByType<PokerController>();
                blackjack = UnityEngine.Object.FindFirstObjectByType<BlackjackController>();
                ui = duel != null ? duel.GetComponent<DuelSceneUI>() : poker != null ? poker.GetComponent<PokerSceneUI>() : blackjack != null ? blackjack.GetComponent<BlackjackSceneUI>() : null;
                if (ui != null)
                {
                    next = (Button)new SerializedObject(ui).FindProperty("nextRound").objectReferenceValue;
                    rounds[scene] = 0;
                    if (duel != null && commandMode)
                    {
                        var settings = (HighCardDealerSettings)typeof(HighCardDuelController).GetField("dealerSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(duel);
                        settings.raiseWhenAhead = settings.raiseWhenEven = settings.raiseWhenBehind = 1;
                    }
                    if (duel != null) duel.StateChanged += state => { if (state == DuelState.Completed) rounds[scene]++; };
                    if (poker != null) poker.StateChanged += state => { if (state == PokerState.Completed) rounds[scene]++; };
                    if (blackjack != null) blackjack.StateChanged += state => { if (state == BlackjackState.Completed) rounds[scene]++; };
                }
                if (player != null) Check(ReferenceEquals(player, GameInstance.Instance.playerData), "Same PlayerData across scenes");
            }
            if (!clicked && scene == "TitleScene" && GameInstance.Instance != null)
            {
                var title = UnityEngine.Object.FindFirstObjectByType<TitleManager>();
                if (title == null) throw new Exception("TitleManager missing");
                player = GameInstance.Instance.playerData;
                player.casinoMoney = 1; player.flag.SetFlag("old", true);
                GameInstance.Instance.casinoStory.SetBaseBet(CasinoGameKind.HightCard, 200);
                var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(b => b.name == "NewGameButton");
                clicked = true; button.onClick.Invoke();
                Check(player.casinoMoney == 1000 && player.flag.Is("old", false), "New Game button resets data");
                return;
            }
            var adv = AdvEngineController.Instance;
            if (adv != null && !adv.AdvEngine.IsWaitBootLoading && adv.IsPlaying) adv.AdvEngine.Page.InputSendMessage();
            if (duel != null && duel.Game != null)
            {
                var game = duel.Game;
                if (game.State == DuelState.SelectingCard) game.SelectCard(game.PlayerHand[0]);
                else if (game.CanAct(DuelAction.Check)) duel.Act(DuelAction.Check);
                else if (game.CanAct(DuelAction.Call))
                {
                    if (commandMode)
                    {
                        if (!sawCustomRaise)
                        { Check(game.CallAmount == 100 && game.InitialBet == 20, $"Actual ADV changes one Raise, preserves current base (call={game.CallAmount}, base={game.InitialBet})"); sawCustomRaise = true; }
                        if (rounds[scene] > 0)
                        { Check(game.InitialBet == 30 && game.CallAmount == 30, "ADV base change applies on following round"); sawNewBaseBet = true; }
                    }
                    duel.Act(DuelAction.Call);
                }
            }
            if (poker != null && poker.Game != null)
            {
                if (poker.Game.State == PokerState.ChoosingExchange) poker.SkipExchange();
                else if (poker.Game.CanAct(PokerAction.Play)) poker.Act(PokerAction.Play);
            }
            if (blackjack != null && blackjack.Game != null && blackjack.Game.CanAct(BlackjackAction.Stand)) blackjack.Act(BlackjackAction.Stand);
            if (next != null && next.gameObject.activeInHierarchy && next.interactable) next.onClick.Invoke();
            if (scene == "EndingScene")
            {
                string[] expected = { "TitleScene", "OpeningScene", "Scene_HightAndLow", "HighCardEndingScene", "PokerOpeningScene", "Scene_Poker", "PokerEndingScene", "BlackjackOpeningScene", "Scene_Blackjack", "BlackjackEndingScene", "EndingScene" };
                Check(visited.SequenceEqual(expected), "Exact requested scene order");
                Check(rounds.Count == 3 && rounds.Values.All(n => n == 5), "Five completed rounds per game");
                if (commandMode) Check(sawCustomRaise && sawNewBaseBet && player.flag.Is("ValidationRaiseFinished", true) && player.flag.Is("ValidationRaise", false), "Actual ADV commands and completion flags applied");
                Debug.Log("STORY_PLAY_TESTS_PASS " + assertions + " money=" + player.casinoMoney);
                SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception ex) { Debug.LogException(ex); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
