using System;
using System.Collections.Generic;
using Casino.Cards;
using Casino.Common;
using Casino.ThreeCardPoker;

/// <summary>全3枚組の役判定と、資金・進行の境界を検証する。</summary>
public static class PokerLogicTests
{
    private static int checks;
    private sealed class Wallet : ICasinoWallet
    {
        public long Balance { get; set; }
        public int Credits;
        /// <summary>不足時は徴収しない。</summary>
        /// <param name="amount">必要額。</param>
        public bool TryDebit(long amount) { if (Balance < amount) return false; Balance -= amount; return true; }
        /// <summary>精算額と回数を記録する。</summary>
        /// <param name="amount">受取額。</param>
        public void Credit(long amount) { Balance += amount; Credits++; }
    }
    /// <summary>期待条件を確認する。</summary>
    /// <param name="condition">条件。</param>
    /// <param name="message">失敗理由。</param>
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    /// <summary>異なるスートまたはフラッシュの3枚を評価する。</summary>
    /// <param name="a">1枚目。</param>
    /// <param name="b">2枚目。</param>
    /// <param name="c">3枚目。</param>
    /// <param name="flush">同じスートか。</param>
    private static PokerHandValue Hand(int a, int b, int c, bool flush = false) => PokerHandEvaluator.Evaluate(new[] {
        new PlayingCard(CardSuit.Club, a), new PlayingCard(flush ? CardSuit.Club : CardSuit.Diamond, b), new PlayingCard(flush ? CardSuit.Club : CardSuit.Heart, c) });
    /// <summary>演出を一つずつ完了し、不正な進行を拒否することを確認する。</summary>
    /// <param name="game">ゲーム。</param>
    private static void Drain(PokerGame game)
    {
        int limit = 100;
        while (game.PendingCue != null && --limit > 0)
        {
            var cue = game.PendingCue;
            Check(!game.Act(PokerAction.Play) && !game.Act(PokerAction.Fold) && !game.StartRound(5), "Input during presentation");
            Check(!game.CompletePresentation(cue.Id - 1), "Stale completion");
            Check(game.CompletePresentation(cue.Id), "Completion rejected");
            Check(!game.CompletePresentation(cue.Id), "Duplicate completion");
        }
        Check(limit > 0, "Loop");
    }
    /// <summary>全22100通り、同役比較、Qualification境界を確認する。</summary>
    private static void EvaluateHands()
    {
        int[] counts = new int[6]; var deck = new PlayingCard[52];
        for (int i = 0; i < 52; i++) deck[i] = new PlayingCard((CardSuit)(i / 13), i % 13 + 1);
        for (int a = 0; a < 50; a++) for (int b = a + 1; b < 51; b++) for (int c = b + 1; c < 52; c++)
        {
            var hand = PokerHandEvaluator.Evaluate(new[] { deck[a], deck[b], deck[c] }); counts[(int)hand.Rank]++;
            Check(hand.CompareTo(PokerHandEvaluator.Evaluate(new[] { deck[c], deck[a], deck[b] })) == 0, "Order dependency");
        }
        int[] expected = { 16440, 3744, 1096, 720, 52, 48 };
        for (int i = 0; i < 6; i++) Check(counts[i] == expected[i], "Wrong frequency " + (PokerHandRank)i + ": " + counts[i]);
        Check(Hand(1, 13, 12).CompareTo(Hand(13, 12, 11)) > 0, "Highest straight");
        Check(Hand(1, 2, 3).CompareTo(Hand(2, 3, 4)) < 0 && Hand(1, 2, 3).First == 3, "Wheel straight");
        Check(Hand(13, 1, 2).Rank == PokerHandRank.HighCard, "Invalid wrap straight");
        Check(Hand(2, 3, 4).CompareTo(Hand(1, 10, 5, true)) > 0, "Straight outranks flush");
        Check(Hand(2, 2, 2).CompareTo(Hand(1, 13, 12)) > 0, "Trips outranks straight");
        Check(Hand(2, 3, 4, true).CompareTo(Hand(1, 1, 1)) > 0, "Straight flush outranks trips");
        Check(Hand(1, 1, 2).CompareTo(Hand(13, 13, 12)) > 0, "Pair aces");
        Check(Hand(4, 4, 1).CompareTo(Hand(4, 4, 13)) > 0, "Pair kicker");
        Check(Hand(1, 10, 9).CompareTo(Hand(1, 10, 8)) > 0, "Third high card");
        Check(Hand(1, 10, 9, true).CompareTo(Hand(1, 10, 8, true)) > 0, "Flush third card");
        Check(Hand(13, 11, 9).CompareTo(Hand(13, 10, 9)) > 0, "Second high card");
        var dealer = new PokerDealer();
        Check(!dealer.Qualifies(Hand(11, 8, 2)) && dealer.Qualifies(Hand(12, 8, 2)), "Queen qualification");
        Check(dealer.Qualifies(Hand(2, 2, 3)) && dealer.Qualifies(Hand(2, 4, 7, true)), "Low made hands qualify");
        Check(Hand(12, 8, 4).CompareTo(PokerHandEvaluator.Evaluate(new[] { new PlayingCard(CardSuit.Spade, 12), new PlayingCard(CardSuit.Heart, 8), new PlayingCard(CardSuit.Diamond, 4) })) == 0, "Suit breaks tie");
        bool rejected = false;
        try { PokerHandEvaluator.Evaluate(new[] { deck[0], deck[0], deck[1] }); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Duplicate accepted by evaluator");
    }
    /// <summary>多数の山札順で全結果、奇数Ante、補充、精算待機を検証する。</summary>
    private static void Rounds()
    {
        var results = new int[6];
        for (int seed = 0; seed < 10000; seed++)
        {
            var wallet = new Wallet(); var game = new PokerGame(wallet, seed); int reveals = 0;
            game.CueRaised += cue => { if (cue.Timing == PokerTiming.CardRevealed) reveals++; };
            Check(!game.StartRound(0) && !game.StartRound(-1), "Invalid ante");
            Check(game.StartRound(5), "Odd ante rejected"); Drain(game);
            Check(game.State == PokerState.WaitingForFunds && game.PlayerCards.Count == 0 && game.Shortage == 5, "Initial funds wait");
            wallet.Balance = 4; Check(!game.ResumeAfterFunding() && wallet.Balance == 4, "Partial funds charged");
            wallet.Balance = 5; Check(game.ResumeAfterFunding(), "Resume ante"); Drain(game);
            Check(game.State == PokerState.ChoosingAction && game.Ante == 5 && game.PlayBet == 0, "Initial choice");
            Check(game.PlayerCards.Count == 3 && game.DealerCards.Count == 3 && game.DealerRevealedCount == 0 && game.DealerQualified == null, "Deal / hidden status");
            Check(!game.Act((PokerAction)99), "Invalid action");
            var unique = new HashSet<PlayingCard>(game.PlayerCards); foreach (var card in game.DealerCards) Check(unique.Add(card), "Duplicate in deck");
            if (seed % 7 == 0)
            {
                Check(game.Act(PokerAction.Fold), "Fold rejected"); Drain(game);
                Check(game.Result == PokerResult.PlayerFold && game.Payout == 0 && game.PlayBet == 0 && reveals == 0 && game.DealerQualified == null, "Fold reveals or pays");
            }
            else
            {
                Check(game.Act(PokerAction.Play), "Play rejected");
                Check(game.PlayBet == 0 && game.Balance == 0, "Paid before BeforeAction finished"); Drain(game);
                Check(game.State == PokerState.WaitingForFunds && game.PlayBet == 0 && reveals == 0, "Play funds wait");
                wallet.Balance = 5; Check(game.ResumeAfterFunding(), "Resume play");
                while (game.PendingCue != null && game.PendingCue.Timing != PokerTiming.ResultDecided) game.CompletePresentation(game.PendingCue.Id);
                Check(game.PendingCue != null && wallet.Credits == 0, "Settled before result presentation");
                Check(game.PlayBet == 5 && game.DealerCardsRevealed && reveals == 3, "Reveal count");
                bool qualifies = game.DealerScore.Rank != PokerHandRank.HighCard || game.DealerScore.First >= 12;
                int comparison = game.PlayerScore.CompareTo(game.DealerScore);
                var result = !qualifies ? PokerResult.DealerNotQualified : comparison > 0 ? PokerResult.PlayerWin : comparison < 0 ? PokerResult.DealerWin : PokerResult.Push;
                Check(game.Result == result && game.DealerQualified == qualifies, "Result mismatch"); Drain(game);
            }
            long expected = game.Result == PokerResult.PlayerWin ? 20 : game.Result == PokerResult.DealerNotQualified ? 15 : game.Result == PokerResult.Push ? 10 : 0;
            Check(game.State == PokerState.Completed && wallet.Balance == expected && game.Payout == expected && wallet.Credits == 1, "Payout or duplicate settlement");
            results[(int)game.Result]++;
            long oldId = game.PendingCue?.Id ?? -1;
            Check(game.StartRound(3), "Second round");
            Check(!game.CompletePresentation(oldId) && game.PlayBet == 0 && !game.DealerQualified.HasValue, "Round reset");
        }
        for (int i = 1; i < results.Length; i++) Check(results[i] > 0, "Missing outcome " + (PokerResult)i);
        Console.WriteLine("Outcomes win/loss/push/not-qualified/fold: " + string.Join(",", results));
    }
    /// <summary>検証を実行して結果を出力する。</summary>
    public static void Main() { EvaluateHands(); Rounds(); Console.WriteLine("PASS: " + checks + " assertions; 22100 hands / 10000 rounds"); }
}
