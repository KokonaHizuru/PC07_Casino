using System;
using System.Collections;
using System.Collections.Generic;
using Casino.Cards;
using UnityEngine;

namespace Casino.HighCardDuel
{
    /// <summary>手札UIとゲーム進行をつなぎ、登録された演出を順番に待つ。</summary>
    public sealed class HighCardDuelController : MonoBehaviour
    {
        [SerializeField] private CardHandUI playerHand;
        [SerializeField] private CardHandUI dealerHand;
        [SerializeField] private RectTransform playArea;
        [SerializeField] private HighCardDealerSettings dealerSettings = new HighCardDealerSettings();
        private bool presentationRunning;
        private long appliedCueId;
        public HighCardDuelGame Game { get; private set; }
        public event Action<DuelCue> TimingReached;
        public event Action<DuelState> StateChanged;
        // 複数登録された処理は登録順に実行し、全処理が終了してから進行する。
        public event Func<DuelCue, IEnumerator> Presentation;
        public event Action<Exception> PresentationFailed;

        /// <summary>初期化まで手札操作を止め、ドラッグ通知を購読する。</summary>
        private void Awake()
        {
            if (playerHand != null)
            {
                playerHand.SetInteractable(false);
                playerHand.CardPlayed += OnCardPlayed;
                if (playArea != null) playerHand.SetPlayArea(playArea);
            }
            if (dealerHand != null) dealerHand.SetInteractable(false);
        }

        /// <summary>保存データと任意のディーラー判断を接続する。進行中の差し替えは拒否する。</summary>
        /// <param name="data">現在のプレイヤーデータ。初期所持金は呼び出し側で設定する。</param>
        /// <param name="dealer">省略時はInspectorの確率設定を使用する。</param>
        /// <param name="seed">再現用シード。</param>
        public void Initialize(PlayerData data, IHighCardDealer dealer = null, int? seed = null)
        {
            if (Game != null && Game.State != DuelState.Idle && Game.State != DuelState.Completed)
                throw new InvalidOperationException("進行中のゲームは再初期化できません。");
            UnsubscribeGame();
            Game = new HighCardDuelGame(new PlayerDataDuelWallet(data), dealer ?? new HighCardDealer(dealerSettings, seed), seed);
            appliedCueId = 0;
            Game.CueRaised += OnCue;
            Game.StateChanged += OnState;
            OnState(Game.State);
        }

        /// <summary>外部から空UIのドロップ範囲を設定する。</summary>
        /// <param name="area">プレイ範囲。nullでドロップ不可にする。</param>
        public void SetPlayArea(RectTransform area)
        {
            playArea = area;
            if (playerHand != null) playerHand.SetPlayArea(area);
        }

        /// <summary>新しいラウンドを開始する。</summary>
        /// <param name="initialBet">開始時の掛け金。</param>
        public bool StartRound(int initialBet) => isActiveAndEnabled && Game != null && Game.StartRound(initialBet);

        /// <summary>UIからBET行動を渡す。</summary>
        /// <param name="action">選択した行動。</param>
        public bool Act(DuelAction action) => isActiveAndEnabled && Game != null && Game.Act(action);

        /// <summary>外部の資金補充後に保留中の支払いを再開する。</summary>
        public bool ResumeAfterFunding() => isActiveAndEnabled && Game != null && Game.ResumeAfterFunding();

        /// <summary>プレイヤーの表向き手札からドロップされたカードをゲームへ渡す。</summary>
        /// <param name="card">選択したカード。</param>
        private void OnCardPlayed(PlayingCard card)
        {
            if (isActiveAndEnabled && Game != null && Game.SelectCard(card)) return;
            // 状態が切り替わった直後などに拒否された場合、UIだけがカードを失わないよう戻す。
            if (playerHand != null) playerHand.AddCard(card, true);
        }

        /// <summary>ゲーム状態に合わせて入力可否を更新し、外部UIへ通知する。</summary>
        /// <param name="state">現在の進行状態。</param>
        private void OnState(DuelState state)
        {
            if (playerHand != null) playerHand.SetInteractable(isActiveAndEnabled && state == DuelState.SelectingCard);
            if (dealerHand != null) dealerHand.SetInteractable(false);
            StateChanged?.Invoke(state);
        }

        /// <summary>演出要求をコルーチンとして開始する。</summary>
        /// <param name="cue">演出情報。</param>
        private void OnCue(DuelCue cue)
        {
            if (!isActiveAndEnabled) return;
            presentationRunning = true;
            StartCoroutine(RunPresentation(cue));
        }

        /// <summary>ネストした演出も待ち、例外時は完了通知を送らず停止する。</summary>
        /// <param name="cue">演出情報。</param>
        private IEnumerator RunPresentation(DuelCue cue)
        {
            bool failed = false;
            try
            {
                yield return Casino.Common.PresentationRoutine.Run(Present(cue), exception =>
                {
                    failed = true;
                    Debug.LogException(exception, this);
                    PresentationFailed?.Invoke(exception);
                });
            }
            finally { presentationRunning = false; }
            if (!failed) Game.CompletePresentation(cue.Id);
        }

        /// <summary>手札を同期し、通知と登録順の演出処理を実行する。</summary>
        /// <param name="cue">演出情報。</param>
        private IEnumerator Present(DuelCue cue)
        {
            if (appliedCueId != cue.Id)
            {
                ApplyHandState(cue);
                appliedCueId = cue.Id;
            }
            TimingReached?.Invoke(cue);
            Func<DuelCue, IEnumerator> handlers = Presentation;
            if (handlers == null) yield break;
            foreach (Func<DuelCue, IEnumerator> handler in handlers.GetInvocationList())
            {
                IEnumerator routine = handler(cue);
                if (routine != null) yield return routine;
            }
        }

        /// <summary>配布・選択通知に応じて手札UIを同期する。</summary>
        /// <param name="cue">演出情報。</param>
        private void ApplyHandState(DuelCue cue)
        {
            if (cue.Timing == DuelTiming.RoundStarted)
            {
                playerHand?.Clear(); dealerHand?.Clear();
            }
            else if (cue.Timing == DuelTiming.CardsDealt)
            {
                if (playerHand != null) foreach (PlayingCard card in Game.PlayerHand) playerHand.AddCard(card, true);
                if (dealerHand != null) foreach (PlayingCard card in Game.DealerHand) dealerHand.AddCard(card, false);
            }
            else if (cue.Timing == DuelTiming.CardSelected && cue.Card.HasValue)
            {
                CardHandUI hand = cue.Actor == DuelActor.Player ? playerHand : dealerHand;
                hand?.RemoveCard(cue.Card.Value);
            }
        }

        /// <summary>失敗・中断した演出を同じIDで再実行する。</summary>
        public bool RetryPresentation()
        {
            if (!isActiveAndEnabled || presentationRunning || Game?.PendingCue == null) return false;
            OnCue(Game.PendingCue);
            return true;
        }

        /// <summary>再表示後に進行中の演出を再開する。</summary>
        private void OnEnable()
        {
            if (Game == null) return;
            OnState(Game.State);
            StartCoroutine(ResumeAfterEnable());
        }

        /// <summary>同じオブジェクトの演出購読が復帰してから保留中の演出を再開する。</summary>
        private IEnumerator ResumeAfterEnable()
        {
            yield return null;
            RetryPresentation();
        }

        /// <summary>無効化時は演出を止め、ゲームの完了通知を保留する。</summary>
        private void OnDisable()
        {
            StopAllCoroutines();
            presentationRunning = false;
            if (playerHand != null) playerHand.SetInteractable(false);
        }

        /// <summary>ゲームのイベント購読を解除する。</summary>
        private void UnsubscribeGame()
        {
            if (Game == null) return;
            Game.CueRaised -= OnCue;
            Game.StateChanged -= OnState;
        }

        /// <summary>参照先へのイベント購読を解除する。</summary>
        private void OnDestroy()
        {
            UnsubscribeGame();
            if (playerHand != null) playerHand.CardPlayed -= OnCardPlayed;
        }
    }
}
