using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utage;
using Casino.HighCardDuel;

namespace Casino.Story
{
    /// <summary>宴から基本BET、今回のレイズ、プレイヤーフラグを変更する。</summary>
    public sealed class CasinoAdvCommands : AdvCustomCommandManager
    {
        private HighCardDuelController duel;
        /// <summary>宴の起動時にコマンドとシーン通知を接続する。</summary>
        public override void OnBootInit()
        {
            AdvCommandParser.OnCreateCustomCommandFromID -= CreateCommand;
            AdvCommandParser.OnCreateCustomCommandFromID += CreateCommand;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            CacheController();
        }
        /// <summary>シーンが変わった際に参照を更新する。</summary>
        /// <param name="scene">読み込まれたシーン。</param>
        /// <param name="mode">読み込み方法。</param>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CacheController();
        /// <summary>シーン内のゲーム参照を一度取得して保持する。</summary>
        private void CacheController() => duel = FindFirstObjectByType<HighCardDuelController>();
        /// <summary>購読を解除する。</summary>
        private void OnDestroy()
        {
            AdvCommandParser.OnCreateCustomCommandFromID -= CreateCommand;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        /// <summary>宴のカスタムコマンドを生成する。</summary>
        /// <param name="id">コマンド名。</param>
        /// <param name="row">シナリオの行。</param>
        /// <param name="dataManager">宴の設定。</param>
        /// <param name="command">生成するコマンド。</param>
        private void CreateCommand(string id, StringGridRow row, AdvSettingDataManager dataManager, ref AdvCommand command)
        {
            if (id == "CasinoSetBaseBet" || id == "CasinoSetDealerRaise" || id == "CasinoSetFlag")
                command = new CasinoCommand(row, id, this);
        }
        /// <summary>今回のディーラーRaiseに追加額を指定する。</summary>
        /// <param name="amount">今回追加する金額。</param>
        public void SetDealerRaise(int amount)
        {
            if (duel == null || duel.Game == null || !duel.Game.SetCurrentDealerRaise(amount))
                throw new InvalidOperationException("CasinoSetDealerRaiseはHightCardのDealer/Raise/BeforeActionイベント内で正の額を指定してください。");
        }

        private sealed class CasinoCommand : AdvCommand
        {
            private readonly string id;
            private readonly CasinoAdvCommands owner;
            /// <summary>コマンドの行と実行先を保持する。</summary>
            /// <param name="row">シナリオの行。</param>
            /// <param name="id">コマンド名。</param>
            /// <param name="owner">ゲームへの接続先。</param>
            public CasinoCommand(StringGridRow row, string id, CasinoAdvCommands owner) : base(row)
            { this.id = id; this.owner = owner; }
            /// <summary>シナリオの引数を読み取りゲームへ反映する。</summary>
            /// <param name="engine">実行中の宴。</param>
            public override void DoCommand(AdvEngine engine)
            {
                var instance = GameInstance.Instance;
                if (instance == null) throw new InvalidOperationException("GameInstanceがありません。");
                if (id == "CasinoSetDealerRaise") owner.SetDealerRaise(ParseCell<int>(AdvColumnName.Arg1));
                else if (id == "CasinoSetFlag")
                {
                    string key = ParseCell<string>(AdvColumnName.Arg1);
                    if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("フラグIDは空欄にできません。");
                    instance.playerData.flag.SetFlag(key, ParseCell<bool>(AdvColumnName.Arg2));
                }
                else
                {
                    string target = ParseCell<string>(AdvColumnName.Arg1);
                    if (target == "HighCard") target = "HightCard";
                    if (!Enum.TryParse(target, out CasinoGameKind game) || !Enum.IsDefined(typeof(CasinoGameKind), game))
                        throw new ArgumentException("ゲーム名はHightCard / Poker / Blackjackです。");
                    instance.casinoStory.SetBaseBet(game, ParseCell<int>(AdvColumnName.Arg2));
                }
            }
        }
    }
}
