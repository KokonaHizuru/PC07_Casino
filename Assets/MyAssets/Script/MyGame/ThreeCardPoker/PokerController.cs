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
        private bool presentationRunning;
        private long appliedCueId;
        public PokerGame Game { get; private set; }
        public event Action<PokerCue> TimingReached;
        public event Action<PokerState> StateChanged;
        public event Func<PokerCue, IEnumerator> Presentation;
        public event Action<Exception> PresentationFailed;

        /// <summary>スリーカードポーカーでは手札をドラッグせず、行動ボタンで操作する。</summary>
        private void Awake() { playerHand?.SetInteractable(false); dealerHand?.SetInteractable(false); }

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
        /// <summary>外部の資金補充後に再開する。</summary>
        public bool ResumeAfterFunding() => isActiveAndEnabled && Game != null && Game.ResumeAfterFunding();
        /// <summary>状態を操作UIへ通知する。</summary>
        /// <param name="state">状態。</param>
        private void OnState(PokerState state) => StateChanged?.Invoke(state);
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
                    (cue.Actor == PokerActor.Player ? playerHand : dealerHand)?.AddCard(cue.Card.Value, cue.FaceUp);
                else if (cue.Timing == PokerTiming.CardRevealed && dealerHand != null)
                    dealerHand.GetCard(cue.CardIndex).SetFaceUp(true);
                appliedCueId = cue.Id;
            }
            TimingReached?.Invoke(cue);
            var handlers = Presentation;
            if (handlers == null) yield break;
            foreach (Func<PokerCue, IEnumerator> handler in handlers.GetInvocationList())
            { IEnumerator routine = handler(cue); if (routine != null) yield return routine; }
        }
        /// <summary>失敗または中断した演出を同じIDで再開する。</summary>
        public bool RetryPresentation()
        {
            if (!isActiveAndEnabled || presentationRunning || Game?.PendingCue == null) return false;
            OnCue(Game.PendingCue); return true;
        }
        /// <summary>購読の復帰を待ってから再開する。</summary>
        private void OnEnable() { if (Game != null) StartCoroutine(Resume()); }
        /// <summary>他コンポーネントのOnEnableを1フレーム待つ。</summary>
        private IEnumerator Resume() { yield return null; RetryPresentation(); }
        /// <summary>演出を止め、ゲームは現在の通知で保留する。</summary>
        private void OnDisable() { StopAllCoroutines(); presentationRunning = false; }
        /// <summary>ゲーム通知を解除する。</summary>
        private void Unsubscribe()
        {
            if (Game == null) return;
            Game.CueRaised -= OnCue; Game.StateChanged -= OnState;
        }
        /// <summary>破棄時の購読を解除する。</summary>
        private void OnDestroy() => Unsubscribe();
    }
}
