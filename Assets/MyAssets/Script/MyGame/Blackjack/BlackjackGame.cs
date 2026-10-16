using System;
using System.Collections.Generic;
using Casino.Cards;
using Casino.Common;

namespace Casino.Blackjack
{
    /// <summary>描画と独立したブラックジャック。各演出の完了まで進行を保留する。</summary>
    public sealed class BlackjackGame
    {
        private readonly ICasinoWallet wallet;
        private readonly CardDeck deck;
        private readonly BlackjackDealer dealer = new BlackjackDealer();
        private readonly List<PlayingCard> playerCards = new List<PlayingCard>();
        private readonly List<PlayingCard> dealerCards = new List<PlayingCard>();
        private Action continuation;
        private Action retryPayment;
        private long nextCueId;
        public BlackjackState State { get; private set; } = BlackjackState.Idle;
        public BlackjackResult Result { get; private set; }
        public BlackjackCue PendingCue { get; private set; }
        public IReadOnlyList<PlayingCard> PlayerCards { get; }
        public IReadOnlyList<PlayingCard> DealerCards { get; }
        public BlackjackScore PlayerScore => BlackjackScore.Evaluate(PlayerCards);
        public BlackjackScore DealerScore => BlackjackScore.Evaluate(DealerCards);
        public BlackjackScore DealerVisibleScore => BlackjackScore.Evaluate(DealerCards, !DealerHoleRevealed);
        public bool DealerHoleRevealed { get; private set; }
        public int InitialBet { get; private set; }
        public long PlayerBet { get; private set; }
        public long Payout { get; private set; }
        public long Balance => wallet.Balance;
        public long RequiredFunds { get; private set; }
        public long Shortage => Math.Max(0, RequiredFunds - Balance);
        public event Action<BlackjackState> StateChanged;
        public event Action<BlackjackCue> CueRaised;

        /// <summary>共通の資金と山札を初期化する。</summary>
        /// <param name="wallet">資金接続。</param>
        /// <param name="seed">再現用シード。</param>
        public BlackjackGame(ICasinoWallet wallet, int? seed = null)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            deck = new CardDeck(seed);
            PlayerCards = playerCards.AsReadOnly(); DealerCards = dealerCards.AsReadOnly();
        }

        /// <summary>偶数の掛け金でラウンドを開始する。</summary>
        /// <param name="initialBet">正の偶数。</param>
        public bool StartRound(int initialBet)
        {
            if (State != BlackjackState.Idle && State != BlackjackState.Completed || initialBet <= 0 || initialBet % 2 != 0) return false;
            InitialBet = initialBet; PlayerBet = 0; Payout = 0; RequiredFunds = 0;
            DealerHoleRevealed = false; Result = BlackjackResult.None; retryPayment = null;
            playerCards.Clear(); dealerCards.Clear(); deck.Reset(); deck.Shuffle();
            Present(BlackjackTiming.RoundStarted, () => Pay(InitialBet, () =>
            {
                PlayerBet = InitialBet;
                Present(BlackjackTiming.BetPaid, () => DealInitial(0), BlackjackActor.Player, amount: InitialBet);
            }));
            return true;
        }

        /// <summary>プレイヤー、ディーラーの順に交互に2枚ずつ配る。</summary>
        /// <param name="step">配布済み枚数。</param>
        private void DealInitial(int step)
        {
            if (step == 4) { Present(BlackjackTiming.CardsDealt, CheckNaturals); return; }
            Draw(step % 2 == 0, step != 3, () => DealInitial(step + 1));
        }

        /// <summary>山札から重複しないカードを配り、表示完了を待つ。</summary>
        /// <param name="player">プレイヤーへ配るか。</param>
        /// <param name="faceUp">表向きか。</param>
        /// <param name="next">配布演出の後の処理。</param>
        private void Draw(bool player, bool faceUp, Action next)
        {
            if (!deck.TryDraw(out PlayingCard card)) throw new InvalidOperationException("山札が不足しました。");
            var hand = player ? playerCards : dealerCards; hand.Add(card);
            Present(BlackjackTiming.CardDealt, next, player ? BlackjackActor.Player : BlackjackActor.Dealer,
                card, hand.Count - 1, faceUp);
        }

        /// <summary>配布直後に双方のナチュラルを判定し、追加BET前に決着する。</summary>
        private void CheckNaturals()
        {
            bool player = PlayerScore.IsBlackjack, opponent = DealerScore.IsBlackjack;
            if (!player && !opponent) { AwaitPlayer(); return; }
            RevealHole(() => Present(BlackjackTiming.BlackjackDetected,
                () => Decide(player && opponent ? BlackjackResult.Push : player ? BlackjackResult.PlayerWin : BlackjackResult.DealerWin),
                player && opponent ? BlackjackActor.None : player ? BlackjackActor.Player : BlackjackActor.Dealer));
        }

        /// <summary>行動前演出を終えてから入力を受け付ける。</summary>
        private void AwaitPlayer() => Present(BlackjackTiming.BeforePlayerDecision, () => ChangeState(BlackjackState.ChoosingAction), BlackjackActor.Player);

        /// <summary>現在選択できる行動か判定する。</summary>
        /// <param name="action">行動。</param>
        public bool CanAct(BlackjackAction action) => State == BlackjackState.ChoosingAction &&
            (action == BlackjackAction.Hit || action == BlackjackAction.Stand || action == BlackjackAction.DoubleDown && playerCards.Count == 2);

        /// <summary>プレイヤーの行動を受け付け、確定前演出へ進める。</summary>
        /// <param name="action">選択行動。</param>
        public bool Act(BlackjackAction action)
        {
            if (!CanAct(action)) return false;
            Present(BlackjackTiming.BeforeAction, () =>
            {
                if (action == BlackjackAction.DoubleDown)
                    Pay(InitialBet, () => { PlayerBet += InitialBet; CommitPlayer(action); });
                else CommitPlayer(action);
            }, BlackjackActor.Player, action: action, amount: action == BlackjackAction.DoubleDown ? InitialBet : 0);
            return true;
        }

        /// <summary>成立した行動を通知し、Hitまたはディーラーターンへ進む。</summary>
        /// <param name="action">成立した行動。</param>
        private void CommitPlayer(BlackjackAction action)
        {
            Present(BlackjackTiming.ActionTaken, () =>
            {
                if (action == BlackjackAction.Stand) BeginDealer();
                else Draw(true, true, () => AfterPlayerDraw(action == BlackjackAction.DoubleDown));
            }, BlackjackActor.Player, action: action, amount: action == BlackjackAction.DoubleDown ? InitialBet : 0);
        }

        /// <summary>バースト、Double Down、21到達を判定する。</summary>
        /// <param name="doubled">Double Downで引いたか。</param>
        private void AfterPlayerDraw(bool doubled)
        {
            if (PlayerScore.IsBust)
                Present(BlackjackTiming.Busted, () => Decide(BlackjackResult.DealerWin), BlackjackActor.Player);
            else if (doubled || PlayerScore.Total == 21) BeginDealer();
            else AwaitPlayer();
        }

        /// <summary>ディーラーターンの開始演出と伏せ札公開を行う。</summary>
        private void BeginDealer() => Present(BlackjackTiming.DealerTurnStarted, () => RevealHole(DealerStep), BlackjackActor.Dealer);

        /// <summary>伏せ札の公開前後を通知し、Flipを待つ。</summary>
        /// <param name="next">公開後の処理。</param>
        private void RevealHole(Action next)
        {
            if (DealerHoleRevealed) { next(); return; }
            Present(BlackjackTiming.BeforeCardReveal, () =>
            {
                DealerHoleRevealed = true;
                Present(BlackjackTiming.CardRevealed, next, BlackjackActor.Dealer, dealerCards[1], 1, true);
            }, BlackjackActor.Dealer, dealerCards[1], 1, false);
        }

        /// <summary>ディーラーの各判断と行動に演出を挟んで自動進行する。</summary>
        private void DealerStep()
        {
            Present(BlackjackTiming.BeforeDealerDecision, () =>
            {
                BlackjackAction action = dealer.Decide(DealerScore);
                Present(BlackjackTiming.BeforeAction, () => Present(BlackjackTiming.ActionTaken, () =>
                {
                    if (action == BlackjackAction.Stand) Compare();
                    else Draw(false, true, () =>
                    {
                        if (DealerScore.IsBust) Present(BlackjackTiming.Busted, () => Decide(BlackjackResult.PlayerWin), BlackjackActor.Dealer);
                        else DealerStep();
                    });
                }, BlackjackActor.Dealer, action: action), BlackjackActor.Dealer, action: action);
            }, BlackjackActor.Dealer);
        }

        /// <summary>バーストしていない双方の合計を比較する。</summary>
        private void Compare() => Decide(PlayerScore.Total > DealerScore.Total ? BlackjackResult.PlayerWin
            : PlayerScore.Total < DealerScore.Total ? BlackjackResult.DealerWin : BlackjackResult.Push);

        /// <summary>配当を確定し、結果演出の後で一度だけ精算する。</summary>
        /// <param name="result">勝敗。</param>
        private void Decide(BlackjackResult result)
        {
            Result = result;
            Payout = result == BlackjackResult.Push ? PlayerBet : result == BlackjackResult.PlayerWin
                ? PlayerScore.IsBlackjack ? PlayerBet * 5 / 2 : PlayerBet * 2 : 0;
            Present(BlackjackTiming.ResultDecided, () =>
            {
                wallet.Credit(Payout);
                Present(BlackjackTiming.Settled, () => Present(BlackjackTiming.RoundEnded,
                    () => ChangeState(BlackjackState.Completed)), amount: Payout);
            });
        }

        /// <summary>資金不足では支払いを保留し、補充を待つ。</summary>
        /// <param name="amount">必要額。</param>
        /// <param name="next">支払い後の処理。</param>
        private void Pay(long amount, Action next)
        {
            if (wallet.TryDebit(amount)) { RequiredFunds = 0; retryPayment = null; next(); return; }
            RequiredFunds = amount; retryPayment = () => Pay(amount, next);
            Present(BlackjackTiming.FundsRequired, () => ChangeState(BlackjackState.WaitingForFunds), BlackjackActor.Player, amount: amount);
        }

        /// <summary>補充された資金で保留中の支払いから再開する。</summary>
        public bool ResumeAfterFunding()
        {
            if (State != BlackjackState.WaitingForFunds || Balance < RequiredFunds) return false;
            ChangeState(BlackjackState.Presenting); retryPayment(); return true;
        }

        /// <summary>進行通知を発行し、対応する完了IDを待つ。</summary>
        /// <param name="timing">タイミング。</param>
        /// <param name="next">完了後の処理。</param>
        /// <param name="actor">対象者。</param>
        /// <param name="card">カード。</param>
        /// <param name="index">配布順。</param>
        /// <param name="faceUp">表向きか。</param>
        /// <param name="action">行動。</param>
        /// <param name="amount">関連金額。</param>
        private void Present(BlackjackTiming timing, Action next, BlackjackActor actor = BlackjackActor.None,
            PlayingCard? card = null, int index = -1, bool faceUp = true, BlackjackAction? action = null, long amount = 0)
        {
            continuation = next;
            var cue = new BlackjackCue(++nextCueId, timing, this, actor, card, index, faceUp, action, amount);
            PendingCue = cue; ChangeState(BlackjackState.Presenting);
            if (PendingCue == cue) CueRaised?.Invoke(cue);
        }

        /// <summary>最新の演出だけを完了し、古い通知や二重通知を拒否する。</summary>
        /// <param name="cueId">完了するID。</param>
        public bool CompletePresentation(long cueId)
        {
            if (State != BlackjackState.Presenting || PendingCue == null || PendingCue.Id != cueId) return false;
            Action next = continuation; PendingCue = null; continuation = null; next(); return true;
        }

        /// <summary>状態変更を外部へ通知する。</summary>
        /// <param name="state">新しい状態。</param>
        private void ChangeState(BlackjackState state) { State = state; StateChanged?.Invoke(state); }
    }
}
