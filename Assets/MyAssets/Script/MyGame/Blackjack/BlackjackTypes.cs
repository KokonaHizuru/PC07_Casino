using System;
using Casino.Cards;

namespace Casino.Blackjack
{
    public enum BlackjackState { Idle, Presenting, ChoosingAction, WaitingForFunds, Completed }
    public enum BlackjackActor { None, Player, Dealer }
    public enum BlackjackAction { Hit, Stand, DoubleDown }
    public enum BlackjackResult { None, PlayerWin, DealerWin, Push }
    public enum BlackjackTiming
    {
        RoundStarted, BetPaid, CardDealt, CardsDealt, BeforePlayerDecision,
        BeforeAction, ActionTaken, DealerTurnStarted, BeforeDealerDecision,
        BeforeCardReveal, CardRevealed, BlackjackDetected, Busted,
        FundsRequired, ResultDecided, Settled, RoundEnded
    }

    /// <summary>演出に渡す変更不能な通知。非公開カードは信頼された表示コードだけで扱う。</summary>
    public sealed class BlackjackCue
    {
        public long Id { get; }
        public BlackjackTiming Timing { get; }
        public BlackjackActor Actor { get; }
        public PlayingCard? Card { get; }
        public int CardIndex { get; }
        public bool FaceUp { get; }
        public BlackjackAction? Action { get; }
        public BlackjackResult Result { get; }
        public long Amount { get; }
        public long Balance { get; }
        public long Bet { get; }
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
        internal BlackjackCue(long id, BlackjackTiming timing, BlackjackGame game, BlackjackActor actor,
            PlayingCard? card, int index, bool faceUp, BlackjackAction? action, long amount)
        {
            Id = id; Timing = timing; Actor = actor; Card = card; CardIndex = index; FaceUp = faceUp;
            Action = action; Amount = amount; Result = game.Result; Balance = game.Balance; Bet = game.PlayerBet;
        }
    }
}
