using System;
using System.Collections.Generic;
using Casino.Cards;
using Casino.Common;
using Casino.Blackjack;

/// <summary>ブラックジャックの境界条件、配当、演出待機を描画なしで検証する。</summary>
public static class BlackjackLogicTests
{
    private static int checks;
    private sealed class Wallet : ICasinoWallet
    {
        public long Balance { get; set; }
        public int Credits;
        /// <summary>不足時には資金を変更しない。</summary>
        /// <param name="amount">支払額。</param>
        public bool TryDebit(long amount) { if (Balance < amount) return false; Balance -= amount; return true; }
        /// <summary>受取金額と回数を記録する。</summary>
        /// <param name="amount">受取額。</param>
        public void Credit(long amount) { Balance += amount; Credits++; }
    }
    /// <summary>期待条件を検証する。</summary>
    /// <param name="condition">期待条件。</param>
    /// <param name="message">失敗理由。</param>
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    /// <summary>指定ランクの合計を評価する。</summary>
    /// <param name="ranks">カードランク。</param>
    private static BlackjackScore Score(params int[] ranks)
    {
        var cards = new List<PlayingCard>(); foreach (int rank in ranks) cards.Add(new PlayingCard(CardSuit.Club, rank));
        return BlackjackScore.Evaluate(cards);
    }
    /// <summary>演出通知を一つずつ完了し、重複と古い完了通知を検証する。</summary>
    /// <param name="game">ゲーム。</param>
    private static void Drain(BlackjackGame game)
    {
        int limit = 200;
        while (game.PendingCue != null && --limit > 0)
        {
            long id = game.PendingCue.Id;
            Check(!game.Act(BlackjackAction.Hit), "Input allowed during presentation");
            Check(!game.CompletePresentation(id - 1), "Stale cue accepted");
            Check(game.CompletePresentation(id), "Current cue rejected");
            Check(!game.CompletePresentation(id), "Cue completed twice");
        }
        Check(limit > 0, "Presentation loop");
    }
    /// <summary>点数と多数の山札順で通常・Double・自然21・資金不足を検証する。</summary>
    public static void Main()
    {
        Check(Score(1, 8).Total == 19 && Score(1, 8).IsSoft, "Soft ace");
        Check(Score(1, 8, 5).Total == 14 && !Score(1, 8, 5).IsSoft, "Hard ace");
        Check(Score(1, 1, 9).Total == 21 && !Score(1, 1, 9).IsBlackjack, "Multiple ace / ordinary 21");
        Check(Score(1, 13).IsBlackjack && Score(10, 12, 2).IsBust, "Natural / bust");
        var dealer = new BlackjackDealer();
        Check(dealer.Decide(Score(1, 6)) == BlackjackAction.Stand, "Soft 17 must stand");
        Check(dealer.Decide(Score(10, 6)) == BlackjackAction.Hit, "16 must hit");
        Check(dealer.Decide(Score(10, 7)) == BlackjackAction.Stand, "17 must stand");
        int naturalPlayer = 0, naturalDealer = 0, both = 0, doubleCount = 0, longHands = 0, pushes = 0;
        for (int seed = 0; seed < 6000; seed++)
        {
            var wallet = new Wallet { Balance = 0 }; var game = new BlackjackGame(wallet, seed);
            Check(!game.StartRound(3) && !game.StartRound(0) && !game.StartRound(-2), "Invalid bet");
            Check(game.StartRound(10), "Start"); Drain(game);
            Check(game.State == BlackjackState.WaitingForFunds && game.PlayerCards.Count == 0, "Initial funding wait");
            Check(!game.ResumeAfterFunding(), "Unfunded resume");
            wallet.Balance = 10; Check(game.ResumeAfterFunding(), "Funded resume"); Drain(game);
            bool playerNatural = game.PlayerScore.IsBlackjack, dealerNatural = game.DealerScore.IsBlackjack;
            if (playerNatural || dealerNatural)
            {
                Check(game.State == BlackjackState.Completed && game.PlayerBet == 10, "Natural must settle immediately");
                Check(game.PlayerCards.Count == 2 && game.DealerCards.Count == 2 && game.DealerHoleRevealed, "Natural reveal");
                Check(!game.Act(BlackjackAction.DoubleDown), "Natural accepted double");
                Check(game.Result == (playerNatural && dealerNatural ? BlackjackResult.Push : playerNatural ? BlackjackResult.PlayerWin : BlackjackResult.DealerWin), "Natural result");
                if (playerNatural && dealerNatural) both++; else if (playerNatural) naturalPlayer++; else naturalDealer++;
            }
            else
            {
                Check(game.State == BlackjackState.ChoosingAction && !game.DealerHoleRevealed, "Initial choice");
                int rank = game.DealerCards[0].Rank;
                Check(game.DealerVisibleScore.Total == (rank == 1 ? 11 : Math.Min(10, rank)), "Hidden card leaked");
                if (seed % 3 == 0)
                {
                    Check(game.Act(BlackjackAction.DoubleDown), "Double available");
                    Check(game.Balance == 0 && game.PlayerBet == 10, "Charged before action presentation");
                    Drain(game); Check(game.State == BlackjackState.WaitingForFunds && game.PlayerCards.Count == 2, "Double funding wait");
                    wallet.Balance += 10; Check(game.ResumeAfterFunding(), "Double funded"); Drain(game);
                    Check(game.PlayerCards.Count == 3 && game.PlayerBet == 20, "Double exactly one card and twice bet"); doubleCount++;
                }
                else
                {
                    int turns = 0;
                    while (game.State == BlackjackState.ChoosingAction && turns++ < 12)
                    {
                        if (game.PlayerCards.Count > 2) Check(!game.CanAct(BlackjackAction.DoubleDown), "Late double");
                        Check(game.Act(seed % 3 == 1 || game.PlayerScore.Total < 17 ? BlackjackAction.Hit : BlackjackAction.Stand), "Action"); Drain(game);
                    }
                    if (game.PlayerCards.Count > 3) longHands++;
                }
            }
            Check(game.State == BlackjackState.Completed, "Round incomplete");
            Check(wallet.Credits == 1, "Duplicate credit");
            long expected = game.Result == BlackjackResult.Push ? game.PlayerBet : game.Result == BlackjackResult.PlayerWin ? playerNatural ? 25 : game.PlayerBet * 2 : 0;
            Check(game.Payout == expected && wallet.Balance == expected, "Payout / balance");
            if (game.Result == BlackjackResult.Push) pushes++;
            var unique = new HashSet<PlayingCard>(game.PlayerCards);
            foreach (var card in game.DealerCards) Check(unique.Add(card), "Duplicate card");
            if (game.PlayerScore.IsBust) Check(game.Result == BlackjackResult.DealerWin && game.DealerCards.Count == 2, "Player bust continued dealer");
            if (!playerNatural && !dealerNatural && !game.PlayerScore.IsBust)
            {
                Check(game.DealerScore.Total >= 17, "Dealer stood below 17");
                var result = game.DealerScore.IsBust || game.PlayerScore.Total > game.DealerScore.Total ? BlackjackResult.PlayerWin
                    : game.PlayerScore.Total < game.DealerScore.Total ? BlackjackResult.DealerWin : BlackjackResult.Push;
                Check(game.Result == result, "Comparison result");
            }
        }
        Check(naturalPlayer > 0 && naturalDealer > 0 && both > 0 && doubleCount > 0 && longHands > 0 && pushes > 0, "Coverage missing");
        Console.WriteLine($"PASS: {checks} assertions / 6000 rounds; naturals {naturalPlayer}/{naturalDealer}/{both}, doubles {doubleCount}, long hands {longHands}, pushes {pushes}");
    }
}
