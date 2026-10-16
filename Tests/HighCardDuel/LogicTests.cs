using System;
using System.Collections.Generic;
using Casino.Cards;
using Casino.HighCardDuel;

/// <summary>Unity描画に依存しない進行・資金・ディーラー判断の回帰テスト。</summary>
public static class LogicTests
{
    private static int checks;
    private sealed class Wallet : IDuelWallet
    {
        public long Balance { get; set; }
        public int Credits;
        /// <summary>残高が十分な場合だけ支払う。</summary>
        /// <param name="amount">支払額。</param>
        public bool TryDebit(long amount) { if (Balance < amount) return false; Balance -= amount; return true; }
        /// <summary>受取額を加算する。</summary>
        /// <param name="amount">受取額。</param>
        public void Credit(long amount) { Balance += amount; Credits++; }
    }
    private sealed class Dealer : IHighCardDealer
    {
        public DuelAction Opening = DuelAction.Check;
        public DuelAction Response = DuelAction.Call;
        public readonly List<DealerDecisionContext> Contexts = new List<DealerDecisionContext>();
        /// <summary>指定した先手行動を返す。</summary>
        /// <param name="context">公開情報。</param>
        public DuelAction ChooseOpening(DealerDecisionContext context) { Contexts.Add(context); return Opening; }
        /// <summary>指定した応答を返す。</summary>
        /// <param name="context">公開情報。</param>
        public DuelAction RespondToRaise(DealerDecisionContext context) { Contexts.Add(context); return Response; }
    }

    /// <summary>条件を確認する。</summary>
    /// <param name="value">期待する真偽。</param>
    /// <param name="message">検証内容。</param>
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }

    /// <summary>演出を順番に完了し、入力受付まで進める。</summary>
    /// <param name="game">対象ゲーム。</param>
    /// <param name="cues">記録先。</param>
    private static void Drain(HighCardDuelGame game, List<DuelCue> cues = null)
    {
        int guard = 0;
        while (game.PendingCue != null)
        {
            Check(++guard < 50, "Progress loop");
            var cue = game.PendingCue;
            cues?.Add(cue);
            Check(!game.Act(DuelAction.Check) && !game.SelectCard(default) && !game.StartRound(10), "Input during presentation");
            Check(!game.CompletePresentation(cue.Id + 1000), "Foreign completion ID");
            Check(game.CompletePresentation(cue.Id), "Completion rejected");
            Check(!game.CompletePresentation(cue.Id), "Duplicate completion accepted");
        }
    }

    /// <summary>カードを指定順に選択し、双方の公開まで進める。</summary>
    /// <param name="game">対象ゲーム。</param>
    /// <param name="cues">記録先。</param>
    private static void Reveal(HighCardDuelGame game, List<DuelCue> cues = null)
    {
        Check(game.State == DuelState.SelectingCard, "Card input state");
        PlayingCard selected = game.PlayerHand[game.PlayerHand.Count - 1];
        int count = game.PlayerRevealed.Count;
        Check(game.SelectCard(selected), "Select hidden card");
        Check(game.PlayerRevealed.Count == count, "Revealed before selected animation completed");
        Check(!game.SelectCard(selected), "Same card selected twice");
        Drain(game, cues);
        Check(game.PlayerRevealed[count].Equals(selected), "Selection order ignored");
    }

    /// <summary>行動前の演出が終わるまで掛け金と所持金を変更しないことを確認する。</summary>
    private static void CheckBeforeAction()
    {
        foreach (bool dealerRaises in new[] { false, true })
        {
            var wallet = new Wallet { Balance = 100 };
            var dealer = new Dealer { Opening = dealerRaises ? DuelAction.Raise : DuelAction.Check };
            var game = new HighCardDuelGame(wallet, dealer, 1);
            game.StartRound(10); Drain(game);
            game.SelectCard(game.PlayerHand[0]);
            while (game.PendingCue.Timing != DuelTiming.BeforeAction) game.CompletePresentation(game.PendingCue.Id);
            Check(game.PendingCue.Actor == DuelActor.Dealer && game.DealerBet == 10, "Dealer bet applied before BeforeAction");
            Drain(game);
            Check(game.DealerBet == (dealerRaises ? 20 : 10), "Dealer opening not committed");
            game.Act(dealerRaises ? DuelAction.Call : DuelAction.Raise);
            Check(game.PendingCue.Timing == DuelTiming.BeforeAction && game.PendingCue.Actor == DuelActor.Player, "Player action hook missing");
            Check(wallet.Balance == 90 && game.PlayerBet == 10, "Payment before action presentation");
            Check(!game.Act(DuelAction.Fold), "Action accepted during before-action presentation");
            Drain(game);
            Check(wallet.Balance == 80 && game.PlayerBet == 20, "Action payment not committed exactly once");
        }
    }

    /// <summary>一連の正常終了・Fold・資金不足・AI確率を検証する。</summary>
    public static void Main()
    {
        CheckBeforeAction();
        var outcomes = new HashSet<DuelResult>();
        for (int seed = 0; seed < 300; seed++)
        {
            var wallet = new Wallet { Balance = 1000 };
            var dealer = new Dealer();
            var game = new HighCardDuelGame(wallet, dealer, seed);
            var cues = new List<DuelCue>();
            Check(!game.StartRound(0) && !game.StartRound(-10), "Invalid bet");
            Check(game.StartRound(10), "Start");
            Check(wallet.Balance == 1000, "Debited before start animation completed");
            Drain(game, cues);
            Check(wallet.Balance == 990 && game.PlayerHand.Count == 3 && game.DealerHand.Count == 3, "Initial deal");
            var unique = new HashSet<PlayingCard>(game.PlayerHand);
            foreach (var card in game.DealerHand) Check(unique.Add(card), "Duplicate dealt card");
            for (int phase = 1; phase <= 3; phase++)
            {
                Reveal(game, cues);
                if (phase == 3) break;
                Check(dealer.Contexts[phase - 1].PlayerScore == game.PlayerScore && dealer.Contexts[phase - 1].DealerScore == game.DealerScore, "AI public scores");
                Check(game.CanAct(DuelAction.Check) && game.CanAct(DuelAction.Raise) && !game.CanAct(DuelAction.Call), "After dealer check");
                Check(game.Act(DuelAction.Check), "Check");
                Drain(game, cues);
            }
            int expectedPlayer = 0, expectedDealer = 0;
            foreach (var card in game.PlayerRevealed) expectedPlayer += card.Rank == 1 ? 14 : card.Rank;
            foreach (var card in game.DealerRevealed) expectedDealer += card.Rank == 1 ? 14 : card.Rank;
            Check(game.PlayerScore == expectedPlayer && game.DealerScore == expectedDealer, "Ace score");
            DuelResult result = expectedPlayer > expectedDealer ? DuelResult.PlayerWin : expectedPlayer < expectedDealer ? DuelResult.DealerWin : DuelResult.Push;
            Check(game.Result == result && game.State == DuelState.Completed, "Showdown result");
            Check(wallet.Balance == (result == DuelResult.PlayerWin ? 1010 : result == DuelResult.Push ? 1000 : 990), "Showdown payout");
            Check(wallet.Credits == 1 && dealer.Contexts.Count == 2, "Settlement once and no third betting phase");
            Check(cues.FindAll(c => c.Timing == DuelTiming.BetStarted).Count == 2, "Two betting phases");
            outcomes.Add(result);
            Check(game.StartRound(20), "Next round"); Drain(game);
            Check(game.Phase == 1 && game.Result == DuelResult.None && game.PlayerRevealed.Count == 0, "Next round reset");
        }
        Check(outcomes.Count == 3, "Did not cover win/lose/push");

        foreach (bool playerFolds in new[] { false, true })
        {
            var wallet = new Wallet { Balance = 100 };
            var dealer = new Dealer { Opening = playerFolds ? DuelAction.Raise : DuelAction.Check, Response = DuelAction.Fold };
            var game = new HighCardDuelGame(wallet, dealer, 3);
            game.StartRound(10); Drain(game); Reveal(game);
            Check(!game.CanAct(playerFolds ? DuelAction.Raise : DuelAction.Call), "Illegal betting action");
            Check(game.Act(playerFolds ? DuelAction.Fold : DuelAction.Raise), "Fold path action"); Drain(game);
            Check(game.Result == (playerFolds ? DuelResult.DealerWin : DuelResult.PlayerWin), "Fold winner");
            Check(wallet.Balance == (playerFolds ? 90 : 110), "Unequal pot fold payout");
        }

        foreach (bool openingRaise in new[] { false, true })
        {
            var wallet = new Wallet { Balance = 5 };
            var dealer = new Dealer { Opening = openingRaise ? DuelAction.Raise : DuelAction.Check };
            var game = new HighCardDuelGame(wallet, dealer, 6);
            game.StartRound(10); Drain(game);
            Check(game.State == DuelState.WaitingForFunds && game.Shortage == 5 && game.PlayerBet == 0, "Initial shortage");
            Check(!game.ResumeAfterFunding(), "Insufficient resume");
            wallet.Balance += 5; Check(game.ResumeAfterFunding(), "Initial funded resume"); Drain(game);
            for (int phase = 1; phase <= 2; phase++)
            {
                Reveal(game);
                game.Act(openingRaise ? DuelAction.Call : DuelAction.Raise); Drain(game);
                long opponentBet = game.DealerBet;
                Check(game.State == DuelState.WaitingForFunds && game.RequiredFunds == 10 && game.Shortage == 10, "Action shortage");
                Check(!game.Act(DuelAction.Fold) && !game.SelectCard(default), "Input during funding wait");
                wallet.Balance += 10; Check(game.ResumeAfterFunding(), "Funded action resume");
                Check(!game.ResumeAfterFunding(), "Duplicate resume"); Drain(game);
                Check(game.PlayerBet == (phase + 1) * 10 && game.DealerBet == game.PlayerBet, "Resumed bet counted twice");
                Check(game.DealerBet == opponentBet + (openingRaise ? 0 : 10), "Dealer bet duplicate");
            }
            Reveal(game);
            Check(game.State == DuelState.Completed && game.PlayerBet == 30 && game.DealerBet == 30, "Maximum betting round");
        }

        var settings = new HighCardDealerSettings { raiseWhenAhead = 1, raiseWhenEven = 0, raiseWhenBehind = 0,
            foldWhenAhead = 0, foldWhenEven = 0, foldWhenBehind = 1 };
        var policy = new HighCardDealer(settings, 1);
        Check(policy.ChooseOpening(new DealerDecisionContext(1, 2, 14, 10, 10)) == DuelAction.Raise, "Ahead probability");
        Check(policy.ChooseOpening(new DealerDecisionContext(1, 14, 2, 10, 10)) == DuelAction.Check, "Behind probability");
        Check(policy.RespondToRaise(new DealerDecisionContext(1, 14, 2, 20, 10)) == DuelAction.Fold, "Fold probability");
        settings.foldWhenBehind = 0;
        Check(policy.RespondToRaise(new DealerDecisionContext(1, 14, 2, 20, 10)) == DuelAction.Call, "Live balance adjustment");
        Console.WriteLine("PASS: " + checks + " assertions");
    }
}
