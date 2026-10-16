using System;
using System.Collections.Generic;
using Casino.Cards;

namespace Casino.HighCardDuel
{
    /// <summary>表示方法に依存せず、演出の完了を待ちながら1ラウンドを進める。</summary>
    public sealed class HighCardDuelGame
    {
        private readonly IDuelWallet wallet;
        private readonly IHighCardDealer dealer;
        private readonly CardDeck deck;
        private readonly Random selectionRandom;
        private readonly List<PlayingCard> playerHand = new List<PlayingCard>();
        private readonly List<PlayingCard> dealerHand = new List<PlayingCard>();
        private readonly List<PlayingCard> playerOpen = new List<PlayingCard>();
        private readonly List<PlayingCard> dealerOpen = new List<PlayingCard>();
        private Action continuation;
        private Action retryPayment;
        private long nextCueId;
        private bool dealerRaised;

        public DuelState State { get; private set; } = DuelState.Idle;
        public DuelResult Result { get; private set; }
        public DuelCue PendingCue { get; private set; }
        public int InitialBet { get; private set; }
        public int Phase { get; private set; }
        public long PlayerBet { get; private set; }
        public long DealerBet { get; private set; }
        public long Payout { get; private set; }
        public long Balance => wallet.Balance;
        public long RequiredFunds { get; private set; }
        public long Shortage => Math.Max(0, RequiredFunds - Balance);
        // 非公開手札は表示側専用。ディーラー判断には渡さない。
        public IReadOnlyList<PlayingCard> PlayerHand { get; }
        public IReadOnlyList<PlayingCard> DealerHand { get; }
        public IReadOnlyList<PlayingCard> PlayerRevealed { get; }
        public IReadOnlyList<PlayingCard> DealerRevealed { get; }
        public int PlayerScore => Score(playerOpen);
        public int DealerScore => Score(dealerOpen);
        public event Action<DuelState> StateChanged;
        public event Action<DuelCue> CueRaised;

        /// <summary>資金処理とディーラー判断を接続し、山札を初期化する。</summary>
        /// <param name="wallet">保存データなどへ接続する資金処理。</param>
        /// <param name="dealer">公開情報だけで行動を決める判断処理。</param>
        /// <param name="seed">テスト用の再現シード。</param>
        public HighCardDuelGame(IDuelWallet wallet, IHighCardDealer dealer, int? seed = null)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.dealer = dealer ?? throw new ArgumentNullException(nameof(dealer));
            deck = new CardDeck(seed);
            selectionRandom = seed.HasValue ? new Random(unchecked(seed.Value + 1)) : new Random();
            PlayerHand = playerHand.AsReadOnly(); DealerHand = dealerHand.AsReadOnly();
            PlayerRevealed = playerOpen.AsReadOnly(); DealerRevealed = dealerOpen.AsReadOnly();
        }

        /// <summary>掛け金を指定して新しいラウンドを開始する。</summary>
        /// <param name="initialBet">開始時およびRaise1回分の金額。正の整数。</param>
        public bool StartRound(int initialBet)
        {
            if (State != DuelState.Idle && State != DuelState.Completed) return false;
            if (initialBet <= 0) return false;
            InitialBet = initialBet; Phase = 0; PlayerBet = 0; DealerBet = 0; Payout = 0;
            Result = DuelResult.None; RequiredFunds = 0; retryPayment = null; dealerRaised = false;
            playerHand.Clear(); dealerHand.Clear(); playerOpen.Clear(); dealerOpen.Clear();
            Present(DuelTiming.RoundStarted, PayInitialBet);
            return true;
        }

        /// <summary>両者の初期BETを確定し、配布へ進む。</summary>
        private void PayInitialBet()
        {
            PayPlayer(InitialBet, () =>
            {
                PlayerBet = InitialBet; DealerBet = InitialBet;
                Present(DuelTiming.InitialBetPaid, DealCards, amount: InitialBet);
            });
        }

        /// <summary>新しい山札から双方へ3枚ずつ配る。</summary>
        private void DealCards()
        {
            deck.Reset(); deck.Shuffle();
            for (int i = 0; i < 3; i++)
            {
                deck.TryDraw(out PlayingCard player); playerHand.Add(player);
                deck.TryDraw(out PlayingCard opponent); dealerHand.Add(opponent);
            }
            Present(DuelTiming.CardsDealt, AwaitCard);
        }

        /// <summary>次のカード選択を受け付ける。</summary>
        private void AwaitCard()
        {
            Phase = playerOpen.Count + 1;
            dealerRaised = false;
            Present(DuelTiming.BeforeCardSelection, () => ChangeState(DuelState.SelectingCard), DuelActor.Player);
        }

        /// <summary>プレイヤーが裏向きの手札から選んだカードを受け付ける。</summary>
        /// <param name="card">ドラッグで選択した手札内のカード。</param>
        public bool SelectCard(PlayingCard card)
        {
            if (State != DuelState.SelectingCard || !playerHand.Contains(card)) return false;
            playerHand.Remove(card);
            Present(DuelTiming.CardSelected, () =>
                Present(DuelTiming.BeforeCardReveal, () => RevealPlayer(card), DuelActor.Player, card), DuelActor.Player, card);
            return true;
        }

        /// <summary>選択済みのカードを公開情報へ移す。</summary>
        /// <param name="card">公開するカード。</param>
        private void RevealPlayer(PlayingCard card)
        {
            playerOpen.Add(card);
            Present(DuelTiming.CardRevealed, RevealDealer, DuelActor.Player, card);
        }

        /// <summary>内容を評価せずランダムに選んだディーラー手札を公開する。</summary>
        private void RevealDealer()
        {
            int index = selectionRandom.Next(dealerHand.Count);
            PlayingCard card = dealerHand[index];
            dealerHand.RemoveAt(index);
            Present(DuelTiming.CardSelected, () =>
                Present(DuelTiming.BeforeCardReveal, () =>
            {
                dealerOpen.Add(card);
                Present(DuelTiming.CardRevealed, AfterReveal, DuelActor.Dealer, card);
            }, DuelActor.Dealer, card), DuelActor.Dealer, card);
        }

        /// <summary>公開枚数に応じてBETまたは最終判定へ進む。</summary>
        private void AfterReveal()
        {
            Present(DuelTiming.CardsCompared, AfterComparison);
        }

        /// <summary>比較演出が終わってからBETまたは最終判定へ進む。</summary>
        private void AfterComparison()
        {
            if (Phase == 3)
            {
                DecideResult(PlayerScore > DealerScore ? DuelResult.PlayerWin
                    : PlayerScore < DealerScore ? DuelResult.DealerWin : DuelResult.Push);
                return;
            }
            Present(DuelTiming.BetStarted, () =>
                Present(DuelTiming.BeforeBetDecision, DealerOpening, DuelActor.Dealer));
        }

        /// <summary>ディーラーが先手でCheckまたはRaiseを行う。</summary>
        private void DealerOpening()
        {
            DuelAction action = dealer.ChooseOpening(Context());
            if (action != DuelAction.Check && action != DuelAction.Raise)
                throw new InvalidOperationException("ディーラーの先手はCheckまたはRaiseです。");
            Present(DuelTiming.BeforeAction, () =>
            {
                dealerRaised = action == DuelAction.Raise;
                if (dealerRaised) DealerBet += InitialBet;
                Present(DuelTiming.ActionTaken, () =>
                    Present(DuelTiming.BeforeBetDecision, () => ChangeState(DuelState.ChoosingBet), DuelActor.Player),
                    DuelActor.Dealer, action: action, amount: dealerRaised ? InitialBet : 0);
            }, DuelActor.Dealer, action: action, amount: action == DuelAction.Raise ? InitialBet : 0);
        }

        /// <summary>現在選べるBET行動か判定する。</summary>
        /// <param name="action">確認する行動。</param>
        public bool CanAct(DuelAction action)
        {
            if (State != DuelState.ChoosingBet) return false;
            return dealerRaised ? action == DuelAction.Call || action == DuelAction.Fold
                : action == DuelAction.Check || action == DuelAction.Raise;
        }

        /// <summary>プレイヤーのBET判断を受け付ける。</summary>
        /// <param name="action">Check、Raise、Call、Foldのうち許可された行動。</param>
        public bool Act(DuelAction action)
        {
            if (!CanAct(action)) return false;
            // 支払い開始前に入力を閉じ、同フレームの二重操作を防ぐ。
            Present(DuelTiming.BeforeAction, () =>
            {
                if (action == DuelAction.Call || action == DuelAction.Raise)
                    PayPlayer(InitialBet, () => FinishPlayerAction(action));
                else FinishPlayerAction(action);
            }, DuelActor.Player, action: action,
                amount: action == DuelAction.Call || action == DuelAction.Raise ? InitialBet : 0);
            return true;
        }

        /// <summary>支払い後の行動演出とその後の進行を設定する。</summary>
        /// <param name="action">確定したプレイヤーの行動。</param>
        private void FinishPlayerAction(DuelAction action)
        {
            bool paid = action == DuelAction.Call || action == DuelAction.Raise;
            if (paid) PlayerBet += InitialBet;
            Present(DuelTiming.ActionTaken, () =>
            {
                if (action == DuelAction.Fold) DecideResult(DuelResult.DealerWin);
                else if (action == DuelAction.Raise)
                    Present(DuelTiming.BeforeBetDecision, DealerResponse, DuelActor.Dealer);
                else EndBet();
            }, DuelActor.Player, action: action, amount: paid ? InitialBet : 0);
        }

        /// <summary>プレイヤーのRaiseへCallまたはFoldで応じる。</summary>
        private void DealerResponse()
        {
            DuelAction action = dealer.RespondToRaise(Context());
            if (action != DuelAction.Call && action != DuelAction.Fold)
                throw new InvalidOperationException("Raiseへの応答はCallまたはFoldです。");
            Present(DuelTiming.BeforeAction, () =>
            {
                if (action == DuelAction.Call) DealerBet += InitialBet;
                Present(DuelTiming.ActionTaken, () =>
                {
                    if (action == DuelAction.Fold) DecideResult(DuelResult.PlayerWin);
                    else EndBet();
                }, DuelActor.Dealer, action: action, amount: action == DuelAction.Call ? InitialBet : 0);
            }, DuelActor.Dealer, action: action, amount: action == DuelAction.Call ? InitialBet : 0);
        }

        /// <summary>BETフェーズを閉じて次のカード選択へ進む。</summary>
        private void EndBet() => Present(DuelTiming.BetEnded, AwaitCard);

        /// <summary>資金不足なら支払いを保留して外部へ通知する。</summary>
        /// <param name="amount">必要な支払額。</param>
        /// <param name="afterPayment">支払い成立後の処理。</param>
        private void PayPlayer(long amount, Action afterPayment)
        {
            if (wallet.TryDebit(amount))
            {
                RequiredFunds = 0; retryPayment = null;
                afterPayment();
                return;
            }
            RequiredFunds = amount;
            retryPayment = () => PayPlayer(amount, afterPayment);
            Present(DuelTiming.FundsRequired, () => ChangeState(DuelState.WaitingForFunds),
                DuelActor.Player, amount: amount);
        }

        /// <summary>外部で資金を追加した後、保留した支払いを再試行する。</summary>
        public bool ResumeAfterFunding()
        {
            if (State != DuelState.WaitingForFunds || Balance < RequiredFunds) return false;
            State = DuelState.Presenting;
            Action retry = retryPayment;
            retry();
            return true;
        }

        /// <summary>勝敗を確定し、結果演出の後に精算する。</summary>
        /// <param name="result">確定した勝敗。</param>
        private void DecideResult(DuelResult result)
        {
            Result = result;
            Payout = result == DuelResult.PlayerWin ? PlayerBet + DealerBet
                : result == DuelResult.Push ? PlayerBet : 0;
            Present(DuelTiming.ResultDecided, Settle);
        }

        /// <summary>プレイヤーへの返金・配当を一度だけ反映する。</summary>
        private void Settle()
        {
            wallet.Credit(Payout);
            Present(DuelTiming.Settled, () =>
                Present(DuelTiming.RoundEnded, () => ChangeState(DuelState.Completed)), amount: Payout);
        }

        /// <summary>演出を要求し、完了通知までゲームを停止する。</summary>
        /// <param name="timing">演出タイミング。</param>
        /// <param name="next">完了後の処理。</param>
        /// <param name="actor">対象者。</param>
        /// <param name="card">対象カード。</param>
        /// <param name="action">対象行動。</param>
        /// <param name="amount">関連金額。</param>
        private void Present(DuelTiming timing, Action next, DuelActor actor = DuelActor.None,
            PlayingCard? card = null, DuelAction? action = null, long amount = 0)
        {
            continuation = next;
            var cue = new DuelCue(++nextCueId, timing, this, actor, card, action, amount);
            PendingCue = cue;
            ChangeState(DuelState.Presenting);
            if (PendingCue == cue) CueRaised?.Invoke(cue);
        }

        /// <summary>現在の演出だけを完了し、次の処理へ進める。古いIDや二重通知は拒否する。</summary>
        /// <param name="cueId">演出通知で受け取ったID。</param>
        public bool CompletePresentation(long cueId)
        {
            if (State != DuelState.Presenting || PendingCue == null || PendingCue.Id != cueId) return false;
            Action next = continuation;
            continuation = null; PendingCue = null;
            next();
            return true;
        }

        /// <summary>状態を更新してUIへ通知する。</summary>
        /// <param name="state">新しい状態。</param>
        private void ChangeState(DuelState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        /// <summary>ディーラーへ渡す公開情報を構築する。</summary>
        private DealerDecisionContext Context() => new DealerDecisionContext(Phase, PlayerScore, DealerScore, PlayerBet, DealerBet);

        /// <summary>Aを14として公開カードの点数を合計する。</summary>
        /// <param name="cards">合計対象のカード。</param>
        private static int Score(List<PlayingCard> cards)
        {
            int score = 0;
            foreach (PlayingCard card in cards) score += card.Rank == 1 ? 14 : card.Rank;
            return score;
        }
    }
}
