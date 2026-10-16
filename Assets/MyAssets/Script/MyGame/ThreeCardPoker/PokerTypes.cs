using System;
using Casino.Cards;

namespace Casino.ThreeCardPoker
{
    public enum PokerState { Idle, Presenting, ChoosingAction, WaitingForFunds, Completed }
    public enum PokerActor { None, Player, Dealer }
    public enum PokerAction { Play, Fold }
    public enum PokerResult { None, PlayerWin, DealerWin, Push, DealerNotQualified, PlayerFold }
    public enum PokerTiming
    {
        RoundStarted, AntePaid, CardDealt, CardsDealt, BeforePlayerDecision,
        BeforeAction, PlayBetPaid, ActionTaken, BeforeShowdown, BeforeCardReveal,
        CardRevealed, QualificationChecked, FundsRequired, ResultDecided, Settled, RoundEnded
    }

    /// <summary>演出に渡す変更不能な通知。非公開カードは信頼された表示コードだけで扱う。</summary>
    public sealed class PokerCue
    {
        public long Id { get; }
        public PokerTiming Timing { get; }
        public PokerActor Actor { get; }
        public PlayingCard? Card { get; }
        public int CardIndex { get; }
        public bool FaceUp { get; }
        public PokerAction? Action { get; }
        public PokerResult Result { get; }
        public long Amount { get; }
        public long Balance { get; }
        public long Bet { get; }
        public long Ante { get; }
        public long PlayBet { get; }
        public bool? DealerQualified { get; }
        public long Shortage => Math.Max(0, Amount - Balance);

        /// <summary>現在の通知情報を保持する。</summary>
        /// <param name="id">完了通知ID。</param>
        /// <param name="timing">タイミング。</param>
        /// <param name="game">現在のゲーム。</param>
        /// <param name="actor">対象者。</param>
        /// <param name="card">対象カード。</param>
        /// <param name="index">配布順。</param>
        /// <param name="faceUp">表向きか。</param>
        /// <param name="action">行動。</param>
        /// <param name="amount">関連金額。</param>
        internal PokerCue(long id, PokerTiming timing, PokerGame game, PokerActor actor,
            PlayingCard? card, int index, bool faceUp, PokerAction? action, long amount)
        {
            Id = id; Timing = timing; Actor = actor; Card = card; CardIndex = index; FaceUp = faceUp;
            Action = action; Amount = amount; Result = game.Result; Balance = game.Balance; Bet = game.PlayerBet;
            Ante = game.Ante; PlayBet = game.PlayBet; DealerQualified = game.DealerQualified;
        }
    }
}
