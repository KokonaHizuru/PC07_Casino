using System;
using Casino.Cards;

namespace Casino.HighCardDuel
{
    public enum DuelState { Idle, Presenting, SelectingCard, ChoosingBet, WaitingForFunds, Completed }
    public enum DuelActor { None, Player, Dealer }
    public enum DuelAction { Check, Raise, Call, Fold }
    public enum DuelResult { None, PlayerWin, DealerWin, Push }
    public enum DuelTiming
    {
        RoundStarted, InitialBetPaid, CardsDealt, CardSelected, CardRevealed,
        BetStarted, ActionTaken, BetEnded, FundsRequired, ResultDecided, Settled, RoundEnded,
        BeforeCardSelection, BeforeCardReveal, BeforeBetDecision, BeforeAction, CardsCompared
    }

    /// <summary>演出へ渡す、その時点で確定した情報。Idを使って完了を通知する。</summary>
    public sealed class DuelCue
    {
        public long Id { get; }
        public DuelTiming Timing { get; }
        public DuelActor Actor { get; }
        public PlayingCard? Card { get; }
        public DuelAction? Action { get; }
        public int Phase { get; }
        public long Amount { get; }
        public long PlayerBet { get; }
        public long DealerBet { get; }
        public long Balance { get; }
        public long Shortage => Math.Max(0, Amount - Balance);
        public DuelResult Result { get; }

        /// <summary>進行情報を変更されない演出通知へコピーする。</summary>
        /// <param name="id">完了通知用ID。</param>
        /// <param name="timing">演出タイミング。</param>
        /// <param name="game">現在のゲーム。</param>
        /// <param name="actor">対象者。</param>
        /// <param name="card">対象カード。</param>
        /// <param name="action">対象行動。</param>
        /// <param name="amount">支払額・不足判定の必要額・受取額。</param>
        internal DuelCue(long id, DuelTiming timing, HighCardDuelGame game, DuelActor actor,
            PlayingCard? card, DuelAction? action, long amount)
        {
            Id = id; Timing = timing; Actor = actor; Card = card; Action = action;
            Phase = game.Phase; Amount = amount; PlayerBet = game.PlayerBet;
            DealerBet = game.DealerBet; Balance = game.Balance; Result = game.Result;
        }
    }

    /// <summary>保存先から独立した資金処理の接続口。</summary>
    public interface IDuelWallet
    {
        long Balance { get; }
        /// <summary>所持金が足りる場合だけ支払う。</summary>
        /// <param name="amount">支払額。</param>
        bool TryDebit(long amount);
        /// <summary>精算額を所持金へ加算する。</summary>
        /// <param name="amount">受取額。</param>
        void Credit(long amount);
    }

    /// <summary>未公開カードを含まないディーラーの判断材料。</summary>
    public readonly struct DealerDecisionContext
    {
        public int Phase { get; }
        public int PlayerScore { get; }
        public int DealerScore { get; }
        public long PlayerBet { get; }
        public long DealerBet { get; }

        /// <summary>公開情報だけを判断用にコピーする。</summary>
        /// <param name="phase">現在の公開枚数。</param>
        /// <param name="playerScore">プレイヤーの公開合計。</param>
        /// <param name="dealerScore">ディーラーの公開合計。</param>
        /// <param name="playerBet">プレイヤー投入額。</param>
        /// <param name="dealerBet">ディーラー投入額。</param>
        public DealerDecisionContext(int phase, int playerScore, int dealerScore, long playerBet, long dealerBet)
        {
            Phase = phase; PlayerScore = playerScore; DealerScore = dealerScore;
            PlayerBet = playerBet; DealerBet = dealerBet;
        }
    }

    public interface IHighCardDealer
    {
        /// <summary>先手としてCheckまたはRaiseを選ぶ。</summary>
        /// <param name="context">公開情報。</param>
        DuelAction ChooseOpening(DealerDecisionContext context);
        /// <summary>プレイヤーのRaiseにCallまたはFoldで応じる。</summary>
        /// <param name="context">公開情報。</param>
        DuelAction RespondToRaise(DealerDecisionContext context);
    }
}
