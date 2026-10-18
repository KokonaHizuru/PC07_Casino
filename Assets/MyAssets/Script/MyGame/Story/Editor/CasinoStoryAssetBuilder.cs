using System;
using System.IO;
using System.Linq;
using Casino.Common;
using Casino.HighCardDuel;
using Casino.Blackjack;
using Casino.ThreeCardPoker;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Utage;

namespace Casino.Story.Editor
{
    /// <summary>イベント参照と宴のテキストデータを編集時に更新する。</summary>
    [InitializeOnLoad]
    public sealed class CasinoStoryAssetBuilder : IPreprocessBuildWithReport
    {
        public const string Root = "Assets/MyAssets/ScriptableObject/Event/";
        public const string SourceFolder = "Assets/MyAdv/CasinoStory";
        private const string BookPath = "Assets/MyAdv/CasinoStory/CasinoStory.book.asset";
        private const string ChapterPath = "Assets/MyAdv/CasinoStory/CasinoStory.chapter.asset";
        private const string ScenariosPath = "Assets/MyAdv/Scenarios/MyAdv.scenarios.asset";
        private static bool queued;
        private static bool rebuilding;
        public int callbackOrder => -1100;

        /// <summary>コンパイル後と再生開始前の更新を登録する。</summary>
        static CasinoStoryAssetBuilder()
        {
            QueueRebuild();
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
        /// <summary>再生開始前に参照を確定する。</summary>
        /// <param name="state">エディタの再生状態。</param>
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Rebuild();
        }
        /// <summary>インポート完了後へ更新をまとめる。</summary>
        public static void QueueRebuild()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () => { queued = false; if (!EditorApplication.isPlaying) Rebuild(); };
        }
        /// <summary>対象フォルダ直下だけをパス順で収集する。</summary>
        /// <typeparam name="T">ゲームのイベント型。</typeparam>
        /// <param name="folder">対象フォルダ。</param>
        public static T[] Collect<T>(string folder) where T : ScriptableObject
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetDirectoryName(path).Replace('\\', '/') == folder)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<T>).Where(asset => asset != null).ToArray();
        }
        /// <summary>アセットを読み込み、存在しない場合は作成する。</summary>
        /// <typeparam name="T">作成する型。</typeparam>
        /// <param name="path">保存先。</param>
        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        /// <summary>イベント一覧と編集済みXLSから宴アセットを更新する。</summary>
        [MenuItem("Casino/Story/Rebuild Events and Scenarios")]
        public static void Rebuild()
        {
            if (rebuilding) return;
            rebuilding = true;
            try
            {
                var catalog = GetOrCreate<CasinoEventCatalog>(Root + "CasinoEventCatalog.asset");
                var high = Collect<DuelPresentationEvent>(Root + "HightCard");
                var blackjack = Collect<BlackjackPresentationEvent>(Root + "Blackjack");
                var poker = Collect<PokerPresentationEvent>(Root + "Poker");
                if (!catalog.hightCard.SequenceEqual(high) || !catalog.blackjack.SequenceEqual(blackjack) || !catalog.poker.SequenceEqual(poker))
                {
                    catalog.hightCard = high; catalog.blackjack = blackjack; catalog.poker = poker;
                    EditorUtility.SetDirty(catalog);
                }
                var book = GetOrCreate<AdvImportBook>(BookPath);
                var grids = Directory.GetFiles(SourceFolder, "CasinoStory_*.xls", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .SelectMany(path => Utage.ExcelParser.ExcelParser.Read(path.Replace('\\', '/'), '#', false, false, true).List)
                    .Select(item => item.Grid)
                    .Where(grid => !grid.SheetName.StartsWith("#", StringComparison.Ordinal)).ToArray();
                // シナリオはXLSを正本とし、SO追加時に既存の本文を上書き生成しない。
                if (book.GridList.Count != grids.Length || !book.GridList.Zip(grids, SameRows).All(value => value))
                {
                    var sheets = grids.Select(grid => new AdvImportScenarioSheet(
                        grid, new AdvSettingDataManager(), new AdvMacroManager())).ToArray();
                    book.Clear();
                    for (int i = 0; i < grids.Length; i++)
                    {
                        book.GridList.Add(grids[i]);
                        book.ImportGridList.Add(sheets[i]);
                    }
                    EditorUtility.SetDirty(book);
                }
                var chapter = GetOrCreate<AdvChapterData>(ChapterPath);
                if (chapter.ChapterName != "CasinoStory" || chapter.DataList.Count != 1 || chapter.DataList[0] != book)
                {
                    chapter.SetChapterName("CasinoStory"); chapter.DataList.Clear(); chapter.DataList.Add(book);
                    EditorUtility.SetDirty(chapter);
                }
                var scenarios = AssetDatabase.LoadAssetAtPath<AdvImportScenarios>(ScenariosPath);
                if (scenarios == null) throw new InvalidOperationException("宴のMyAdv.scenariosがありません。");
                if (!scenarios.Chapters.Contains(chapter))
                { scenarios.AddChapter(chapter); EditorUtility.SetDirty(scenarios); }
                AssetDatabase.SaveAssets();
            }
            finally { rebuilding = false; }
        }
        /// <summary>グリッドの本文変更を比較する。</summary>
        /// <param name="left">既存の本文。</param>
        /// <param name="right">読み込んだ本文。</param>
        private static bool SameRows(StringGrid left, StringGrid right)
        {
            return left.Name == right.Name && left.SheetName == right.SheetName && left.Rows.Count == right.Rows.Count && left.Rows.Zip(right.Rows, (a, b) => a.Strings.SequenceEqual(b.Strings)).All(value => value);
        }
        /// <summary>ビルド前に最新の参照を保存する。</summary>
        /// <param name="report">ビルド情報。</param>
        public void OnPreprocessBuild(BuildReport report) => Rebuild();
    }

    /// <summary>追加・削除・移動・条件変更後に自動登録を予約する。</summary>
    public sealed class CasinoStoryPostprocessor : AssetPostprocessor
    {
        /// <summary>対象ファイルに変化があった場合だけ更新する。</summary>
        /// <param name="imported">追加・変更。</param>
        /// <param name="deleted">削除。</param>
        /// <param name="moved">移動先。</param>
        /// <param name="movedFrom">移動元。</param>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(Relevant)) CasinoStoryAssetBuilder.QueueRebuild();
        }
        /// <summary>自動登録の対象パスか確認する。</summary>
        /// <param name="path">変更されたパス。</param>
        private static bool Relevant(string path) => (Path.GetDirectoryName(path)?.Replace('\\', '/') == CasinoStoryAssetBuilder.SourceFolder
            && Path.GetFileName(path).StartsWith("CasinoStory_", StringComparison.Ordinal)
            && Path.GetExtension(path).Equals(".xls", StringComparison.OrdinalIgnoreCase))
            || path == "Assets/MyAdv/Scenarios/MyAdv.scenarios.asset"
            || path.StartsWith(CasinoStoryAssetBuilder.Root + "HightCard/", StringComparison.Ordinal)
            || path.StartsWith(CasinoStoryAssetBuilder.Root + "Blackjack/", StringComparison.Ordinal)
            || path.StartsWith(CasinoStoryAssetBuilder.Root + "Poker/", StringComparison.Ordinal);
    }
}
