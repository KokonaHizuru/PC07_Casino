using System;
using System.Collections;
using Casino.Cards;
using Casino.Common;
using UnityEngine;

namespace Casino.ThreeCardPoker
{
    /// <summary>スリーカードポーカーの入力と手札を同期し、共通処理で演出を待つ。</summary>
    public sealed class PokerController : MonoBehaviour
    {
        [SerializeField] private CardHandUI playerHand;
        [SerializeField] private CardHandUI dealerHand;
        [SerializeField] private RectTransform exchangeArea;
        [SerializeField] private TableCardPresenter handTransfers;
        private bool presentationRunning;
        private long appliedCueId;
        public PokerGame Game { get; private set; }
        public event Action<PokerCue> TimingReached;
        public event Action<PokerState> StateChanged;
        public event Func<PokerCue, IEnumerator> Presentation;
        public event Action<Exception> PresentationFailed;

        /// <summary>交換先とドロップ通知を一度接続し、入力は交換ターンまで止める。</summary>
        private void Awake()
        {
            if (handTransfers == null) handTransfers = GetComponentInChildren<TableCardPresenter>();
            if (playerHand != null) { playerHand.SetPlayArea(exchangeArea); playerHand.CardPlayed += OnCardDropped; }
            playerHand?.SetInteractable(false); dealerHand?.SetInteractable(false);
        }

        /// <summary>保存データへ接続する。</summary>
        /// <param name="data">保存データ。</param>
        /// <param name="seed">再現用シード。</param>
        public void Initialize(PlayerData data, int? seed = null)
        {
            if (Game != null && Game.State != PokerState.Idle && Game.State != PokerState.Completed)
                throw new InvalidOperationException("進行中は初期化できません。");
            Unsubscribe();
            Game = new PokerGame(new PlayerDataCasinoWallet(data), seed);
            appliedCueId = 0; Game.CueRaised += OnCue; Game.StateChanged += OnState; OnState(Game.State);
        }
        /// <summary>正の整数のAnteで開始する。</summary>
        /// <param name="bet">掛け金。</param>
        public bool StartRound(int bet) => isActiveAndEnabled && Game != null && Game.StartRound(bet);
        /// <summary>プレイヤー行動を受け付ける。</summary>
        /// <param name="action">行動。</param>
        public bool Act(PokerAction action) => isActiveAndEnabled && Game != null && Game.Act(action);
        /// <summary>プレイヤーの交換を見送る。</summary>
        public bool SkipExchange() => isActiveAndEnabled && Game != null && Game.SkipExchange();
        /// <summary>共通手札から受け取ったカードで交換を確定する。</summary>
        /// <param name="card">交換エリアへドロップしたカード。</param>
        private void OnCardDropped(PlayingCard card)
        {
            if (!isActiveAndEnabled || Game == null || !Game.ExchangeCard(card))
            { if (Game != null) SyncHand(true); }
        }
        /// <summary>外部の資金補充後に再開する。</summary>
        public bool ResumeAfterFunding() => isActiveAndEnabled && Game != null && Game.ResumeAfterFunding();
        /// <summary>状態を操作UIへ通知する。</summary>
        /// <param name="state">状態。</param>
        private void OnState(PokerState state)
        {
            playerHand?.SetInteractable(isActiveAndEnabled && state == PokerState.ChoosingExchange);
            StateChanged?.Invoke(state);
        }
        /// <summary>演出要求を開始する。</summary>
        /// <param name="cue">通知。</param>
        private void OnCue(PokerCue cue)
        {
            if (!isActiveAndEnabled) return;
            presentationRunning = true; StartCoroutine(Run(cue));
        }
        /// <summary>共通の例外処理を使い、成功した演出だけ完了する。</summary>
        /// <param name="cue">通知。</param>
        private IEnumerator Run(PokerCue cue)
        {
            bool failed = false;
            try
            {
                yield return PresentationRoutine.Run(Present(cue), exception =>
                { failed = true; Debug.LogException(exception, this); PresentationFailed?.Invoke(exception); });
            }
            finally { presentationRunning = false; }
            if (!failed) Game.CompletePresentation(cue.Id);
        }
        /// <summary>UI手札を一度だけ同期し、演出を登録順に待つ。</summary>
        /// <param name="cue">通知。</param>
        private IEnumerator Present(PokerCue cue)
        {
            if (appliedCueId != cue.Id)
            {
                if (cue.Timing == PokerTiming.RoundStarted) { playerHand?.Clear(); dealerHand?.Clear(); }
                else if (cue.Timing == PokerTiming.CardDealt)
                {
                    var hand = cue.Actor == PokerActor.Player ? playerHand : dealerHand;
                    if (handTransfers != null) yield return handTransfers.DrawToHand(hand, cue.Card.Value, cue.Actor == PokerActor.Player, cue.CardIndex, cue.FaceUp);
                    else if (hand != null) yield return hand.AddCardAnimated(cue.Card.Value, cue.FaceUp, cue.CardIndex, cue.Actor == PokerActor.Player);
                }
                else if (cue.Timing == PokerTiming.CardRevealed && dealerHand != null)
                    dealerHand.GetCard(cue.CardIndex).SetFaceUp(true);
                else if (cue.Timing == PokerTiming.BeforeExchange)
                {
                    var hand = cue.Actor == PokerActor.Player ? playerHand : dealerHand;
                    if (handTransfers != null) yield return handTransfers.DiscardFromHand(hand, cue.Card.Value, cue.Actor == PokerActor.Player);
                    else hand?.RemoveCard(cue.Card.Value);
                }
                else if (cue.Timing == PokerTiming.CardExchanged)
                {
                    var hand = cue.Actor == PokerActor.Player ? playerHand : dealerHand;
                    if (handTransfers != null) yield return handTransfers.DrawToHand(hand, cue.Card.Value, cue.Actor == PokerActor.Player, cue.CardIndex, cue.FaceUp);
                    else if (hand != null) yield return hand.AddCardAnimated(cue.Card.Value, cue.FaceUp, cue.CardIndex, cue.Actor == PokerActor.Player);
                }
                appliedCueId = cue.Id;
            }
            TimingReached?.Invoke(cue);
            var handlers = Presentation;
            if (handlers == null) yield break;
            foreach (Func<PokerCue, IEnumerator> handler in handlers.GetInvocationList())
            { IEnumerator routine = handler(cue); if (routine != null) yield return routine; }
        }
        /// <summary>交換後もゲーム内の順序を維持して3枚の表示を同期する。</summary>
        /// <param name="player">プレイヤーの手札か。</param>
        private void SyncHand(bool player)
        {
            CardHandUI hand = player ? playerHand : dealerHand; if (hand == null) return;
            hand.Clear(); var cards = player ? Game.PlayerCards : Game.DealerCards;
            for (int i = 0; i < cards.Count; i++) hand.AddCard(cards[i], player || i < Game.DealerRevealedCount);
        }
        /// <summary>失敗または中断した演出を同じIDで再開する。</summary>
        public bool RetryPresentation()
        {
            if (!isActiveAndEnabled || presentationRunning || Game?.PendingCue == null) return false;
            OnCue(Game.PendingCue); return true;
        }
        /// <summary>購読の復帰を待ってから再開する。</summary>
        private void OnEnable() { if (Game != null) { OnState(Game.State); StartCoroutine(Resume()); } }
        /// <summary>他コンポーネントのOnEnableを1フレーム待つ。</summary>
        private IEnumerator Resume() { yield return null; RetryPresentation(); }
        /// <summary>演出を止め、ゲームは現在の通知で保留する。</summary>
        private void OnDisable() { StopAllCoroutines(); presentationRunning = false; playerHand?.SetInteractable(false); }
        /// <summary>ゲーム通知を解除する。</summary>
        private void Unsubscribe()
        {
            if (Game == null) return;
            Game.CueRaised -= OnCue; Game.StateChanged -= OnState;
        }
        /// <summary>破棄時の購読を解除する。</summary>
        private void OnDestroy()
        {
            Unsubscribe(); if (playerHand != null) playerHand.CardPlayed -= OnCardDropped;
        }
    }
}
