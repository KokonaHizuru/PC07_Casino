using System;
using System.Collections.Generic;
using Casino.Cards;
using Casino.Common;

namespace Casino.ThreeCardPoker
{
    /// <summary>各演出の完了を待ちながら進行するスリーカードポーカー。</summary>
    public sealed class PokerGame
    {
        private readonly ICasinoWallet wallet;
        private readonly CardDeck deck;
        private readonly PokerDealer dealer = new PokerDealer();
        private readonly List<PlayingCard> playerCards = new List<PlayingCard>();
        private readonly List<PlayingCard> dealerCards = new List<PlayingCard>();
        private Action continuation;
        private Action retryPayment;
        private long nextCueId;
        public PokerState State { get; private set; } = PokerState.Idle;
        public PokerResult Result { get; private set; }
        public PokerCue PendingCue { get; private set; }
        public IReadOnlyList<PlayingCard> PlayerCards { get; }
        public IReadOnlyList<PlayingCard> DealerCards { get; }
        public PokerHandValue PlayerScore => PokerHandEvaluator.Evaluate(PlayerCards);
        public PokerHandValue DealerScore => PokerHandEvaluator.Evaluate(DealerCards);
        public int DealerRevealedCount { get; private set; }
        public bool DealerCardsRevealed => DealerRevealedCount == 3;
        public bool? DealerQualified { get; private set; }
        public int InitialAnte { get; private set; }
        public long Ante { get; private set; }
        public long PlayBet { get; private set; }
        public long PlayerBet => Ante + PlayBet;
        public long Payout { get; private set; }
        public long Balance => wallet.Balance;
        public long RequiredFunds { get; private set; }
        public long Shortage => Math.Max(0, RequiredFunds - Balance);
        public event Action<PokerState> StateChanged;
        public event Action<PokerCue> CueRaised;

        /// <summary>共通の資金と山札に接続する。</summary>
        /// <param name="wallet">資金接続。</param>
        /// <param name="seed">再現用シード。</param>
        public PokerGame(ICasinoWallet wallet, int? seed = null)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet)); deck = new CardDeck(seed);
            PlayerCards = playerCards.AsReadOnly(); DealerCards = dealerCards.AsReadOnly();
        }
        /// <summary>正の整数のAnteでラウンドを開始する。</summary>
        /// <param name="ante">Ante。</param>
        public bool StartRound(int ante)
        {
            if ((State != PokerState.Idle && State != PokerState.Completed) || ante <= 0) return false;
            InitialAnte = ante; Ante = 0; PlayBet = 0; Payout = 0; RequiredFunds = 0;
            DealerRevealedCount = 0; DealerQualified = null; Result = PokerResult.None; retryPayment = null;
            playerCards.Clear(); dealerCards.Clear(); deck.Reset(); deck.Shuffle();
            Present(PokerTiming.RoundStarted, () => Pay(ante, () =>
            {
                Ante = ante; Present(PokerTiming.AntePaid, () => Deal(0), PokerActor.Player, amount: ante);
            }));
            return true;
        }
        /// <summary>交互に3枚ずつ配り、プレイヤーだけ表向きで表示する。</summary>
        /// <param name="step">配布済み枚数。</param>
        private void Deal(int step)
        {
            if (step == 6)
            {
                Present(PokerTiming.CardsDealt, () => Present(PokerTiming.BeforePlayerDecision,
                    () => ChangeState(PokerState.ChoosingAction), PokerActor.Player)); return;
            }
            if (!deck.TryDraw(out PlayingCard card)) throw new InvalidOperationException("山札が不足しました。");
            bool player = step % 2 == 0; var hand = player ? playerCards : dealerCards; hand.Add(card);
            Present(PokerTiming.CardDealt, () => Deal(step + 1), player ? PokerActor.Player : PokerActor.Dealer, card, hand.Count - 1, player);
        }
        /// <summary>現在選択できる行動か確認する。</summary>
        /// <param name="action">選択行動。</param>
        public bool CanAct(PokerAction action) => State == PokerState.ChoosingAction && (action == PokerAction.Play || action == PokerAction.Fold);
        /// <summary>Playの追加支払いまたはFoldを演出後に確定する。</summary>
        /// <param name="action">選択行動。</param>
        public bool Act(PokerAction action)
        {
            if (!CanAct(action)) return false;
            Present(PokerTiming.BeforeAction, () =>
            {
                if (action == PokerAction.Fold)
                    Present(PokerTiming.ActionTaken, () => Decide(PokerResult.PlayerFold), PokerActor.Player, action: action);
                else Pay(InitialAnte, () =>
                {
                    PlayBet = InitialAnte;
                    Present(PokerTiming.PlayBetPaid, () => Present(PokerTiming.ActionTaken,
                        () => Present(PokerTiming.BeforeShowdown, () => Reveal(0)), PokerActor.Player, action: action),
                        PokerActor.Player, action: action, amount: InitialAnte);
                });
            }, PokerActor.Player, action: action, amount: action == PokerAction.Play ? InitialAnte : 0);
            return true;
        }
        /// <summary>ディーラーの3枚を順番に公開してからQualificationを判定する。</summary>
        /// <param name="index">公開するカード位置。</param>
        private void Reveal(int index)
        {
            if (index == 3)
            {
                DealerQualified = dealer.Qualifies(DealerScore);
                Present(PokerTiming.QualificationChecked, Compare, PokerActor.Dealer); return;
            }
            Present(PokerTiming.BeforeCardReveal, () =>
            {
                DealerRevealedCount = index + 1;
                Present(PokerTiming.CardRevealed, () => Reveal(index + 1), PokerActor.Dealer, dealerCards[index], index, true);
            }, PokerActor.Dealer, dealerCards[index], index, false);
        }
        /// <summary>成立しない場合は規定返却、成立時は役と数字で比較する。</summary>
        private void Compare()
        {
            if (DealerQualified == false) { Decide(PokerResult.DealerNotQualified); return; }
            int comparison = PlayerScore.CompareTo(DealerScore);
            Decide(comparison > 0 ? PokerResult.PlayerWin : comparison < 0 ? PokerResult.DealerWin : PokerResult.Push);
        }
        /// <summary>元金込み配当を確定し、結果演出後に一度だけ精算する。</summary>
        /// <param name="result">結果。</param>
        private void Decide(PokerResult result)
        {
            Result = result;
            Payout = result == PokerResult.DealerNotQualified ? Ante * 2 + PlayBet
                : result == PokerResult.PlayerWin ? PlayerBet * 2 : result == PokerResult.Push ? PlayerBet : 0;
            Present(PokerTiming.ResultDecided, () =>
            {
                wallet.Credit(Payout);
                Present(PokerTiming.Settled, () => Present(PokerTiming.RoundEnded, () => ChangeState(PokerState.Completed)), amount: Payout);
            });
        }
        /// <summary>不足時は徴収と進行を保留し、外部からの補充を待つ。</summary>
        /// <param name="amount">必要額。</param>
        /// <param name="next">支払い後の処理。</param>
        private void Pay(long amount, Action next)
        {
            if (wallet.TryDebit(amount)) { RequiredFunds = 0; retryPayment = null; next(); return; }
            RequiredFunds = amount; retryPayment = () => Pay(amount, next);
            Present(PokerTiming.FundsRequired, () => ChangeState(PokerState.WaitingForFunds), PokerActor.Player, amount: amount);
        }
        /// <summary>資金補充後に保留した支払いから再開する。</summary>
        public bool ResumeAfterFunding()
        {
            if (State != PokerState.WaitingForFunds || Balance < RequiredFunds) return false;
            ChangeState(PokerState.Presenting); retryPayment(); return true;
        }
        /// <summary>演出通知と続行処理を保持する。</summary>
        /// <param name="timing">タイミング。</param>
        /// <param name="next">演出後の処理。</param>
        /// <param name="actor">対象者。</param>
        /// <param name="card">カード。</param>
        /// <param name="index">カード位置。</param>
        /// <param name="faceUp">表向きか。</param>
        /// <param name="action">行動。</param>
        /// <param name="amount">金額。</param>
        private void Present(PokerTiming timing, Action next, PokerActor actor = PokerActor.None,
            PlayingCard? card = null, int index = -1, bool faceUp = true, PokerAction? action = null, long amount = 0)
        {
            continuation = next;
            var cue = new PokerCue(++nextCueId, timing, this, actor, card, index, faceUp, action, amount);
            PendingCue = cue; ChangeState(PokerState.Presenting); if (PendingCue == cue) CueRaised?.Invoke(cue);
        }
        /// <summary>最新IDだけを完了し、二重通知や古い通知で進行しないようにする。</summary>
        /// <param name="cueId">演出通知ID。</param>
        public bool CompletePresentation(long cueId)
        {
            if (State != PokerState.Presenting || PendingCue == null || PendingCue.Id != cueId) return false;
            Action next = continuation; continuation = null; PendingCue = null; next(); return true;
        }
        /// <summary>状態変更を通知する。</summary>
        /// <param name="state">新しい状態。</param>
        private void ChangeState(PokerState state) { State = state; StateChanged?.Invoke(state); }
    }
}
