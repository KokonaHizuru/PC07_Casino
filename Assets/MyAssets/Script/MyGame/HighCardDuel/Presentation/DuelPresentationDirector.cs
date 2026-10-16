using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.HighCardDuel
{
    /// <summary>SOのADVイベント、カメラ、カードの標準演出を順番に実行する。</summary>
    public sealed class DuelPresentationDirector : MonoBehaviour
    {
        [SerializeField] private HighCardDuelController controller;
        [SerializeField] private DuelCameraRig cameras;
        [SerializeField] private DuelTableCards tableCards;
        [SerializeField] private AdvEngineController adv;
        [SerializeField] private Text comparisonText;
        [SerializeField, Min(0)] private float comparisonSeconds = 1.2f;
        [Tooltip("一致したイベントを上から順番に実行する。")]
        [SerializeField] private DuelPresentationEvent[] events = Array.Empty<DuelPresentationEvent>();
        private readonly HashSet<DuelPresentationEvent> playedThisRound = new HashSet<DuelPresentationEvent>();
        private readonly HashSet<string> completedEvents = new HashSet<string>();
        private readonly HashSet<long> completedVisuals = new HashSet<long>();
        private Casino.Common.AdvScenarioPlayback playback;
        private long roundCueId = -1;
        private HighCardDuelGame currentGame;

        /// <summary>ゲームの演出要求を購読する。</summary>
        private void OnEnable()
        {
            if (controller != null) controller.Presentation += Play;
        }

        /// <summary>ゲームの演出要求の購読を解除する。</summary>
        private void OnDisable()
        {
            if (controller != null) controller.Presentation -= Play;
        }

        /// <summary>SOイベント、標準演出、SOイベントの順で完了を待つ。</summary>
        /// <param name="cue">ゲーム進行通知。</param>
        private IEnumerator Play(DuelCue cue)
        {
            if (!isActiveAndEnabled) throw new InvalidOperationException("演出管理が無効化されています。");
            if (currentGame != controller.Game || cue.Timing == DuelTiming.RoundStarted && roundCueId != cue.Id)
            {
                currentGame = controller.Game;
                roundCueId = cue.Id;
                playedThisRound.Clear(); completedEvents.Clear(); completedVisuals.Clear(); playback?.Clear();
            }
            yield return PlayEvents(cue, DuelEventStage.BeforeVisual);
            if (!completedVisuals.Contains(cue.Id))
            {
                yield return PlayVisual(cue);
                completedVisuals.Add(cue.Id);
            }
            yield return PlayEvents(cue, DuelEventStage.AfterVisual);
        }

        /// <summary>条件に一致するSOを登録順に実行する。</summary>
        /// <param name="cue">ゲーム進行通知。</param>
        /// <param name="stage">標準演出の前後。</param>
        private IEnumerator PlayEvents(DuelCue cue, DuelEventStage stage)
        {
            for (int i = 0; i < events.Length; i++)
            {
                DuelPresentationEvent entry = events[i];
                string key = cue.Id + ":" + (int)stage + ":" + i;
                if (entry == null || !entry.Matches(cue, stage) || completedEvents.Contains(key)
                    || entry.OncePerRound && playedThisRound.Contains(entry)) continue;
                if (entry.CameraView != DuelCameraView.Unchanged)
                {
                    if (cameras == null) throw new InvalidOperationException("視点を変更するイベントにはCamera Rigが必要です。");
                    yield return cameras.SwitchTo(entry.CameraView);
                }
                if (!string.IsNullOrWhiteSpace(entry.ScenarioLabel)) yield return PlayScenario(entry.ScenarioLabel, key);
                completedEvents.Add(key);
                if (entry.OncePerRound) playedThisRound.Add(entry);
            }
        }

        /// <summary>宴を一度だけ開始し、中断・再試行時も同じ再生の終了を待つ。</summary>
        /// <param name="label">宴のシナリオラベル。</param>
        /// <param name="key">演出内で再生を識別するキー。</param>
        private IEnumerator PlayScenario(string label, string key)
        {
            if (playback == null) playback = new Casino.Common.AdvScenarioPlayback(adv);
            yield return playback.Play(label, key);
        }

        /// <summary>カード配置、Flip、視点移動、比較表示を実行する。</summary>
        /// <param name="cue">ゲーム進行通知。</param>
        private IEnumerator PlayVisual(DuelCue cue)
        {
            switch (cue.Timing)
            {
                case DuelTiming.RoundStarted:
                    tableCards.Clear();
                    if (comparisonText != null) comparisonText.text = "";
                    yield return cameras.SwitchTo(DuelCameraView.Normal);
                    break;
                case DuelTiming.BeforeCardSelection:
                case DuelTiming.BetStarted:
                    yield return cameras.SwitchTo(DuelCameraView.Normal);
                    if (comparisonText != null) comparisonText.text = "";
                    break;
                case DuelTiming.CardSelected:
                    yield return tableCards.Place(cue);
                    break;
                case DuelTiming.BeforeCardReveal:
                    yield return cameras.SwitchTo(DuelCameraView.Table);
                    break;
                case DuelTiming.CardRevealed:
                    yield return tableCards.Reveal(cue);
                    break;
                case DuelTiming.CardsCompared:
                    yield return cameras.SwitchTo(DuelCameraView.Table);
                    if (comparisonText != null)
                        comparisonText.text = $"PLAYER {controller.Game.PlayerScore}   /   DEALER {controller.Game.DealerScore}";
                    yield return new WaitForSecondsRealtime(comparisonSeconds);
                    break;
                case DuelTiming.ResultDecided:
                    yield return cameras.SwitchTo(DuelCameraView.Table);
                    if (comparisonText != null)
                        comparisonText.text = cue.Result == DuelResult.PlayerWin ? "YOU WIN" : cue.Result == DuelResult.DealerWin ? "DEALER WINS" : "PUSH";
                    yield return new WaitForSecondsRealtime(comparisonSeconds);
                    break;
            }
        }
    }
}
