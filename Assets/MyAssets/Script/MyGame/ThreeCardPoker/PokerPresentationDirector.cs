using System;
using System.Collections;
using System.Collections.Generic;
using Casino.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.ThreeCardPoker
{
    /// <summary>共通のカメラ・3Dカード・ADVをスリーカードポーカーの進行へ接続する。</summary>
    public sealed class PokerPresentationDirector : MonoBehaviour
    {
        [SerializeField] private PokerController controller;
        [SerializeField] private CardTableCameraRig cameras;
        [SerializeField] private TableCardPresenter tableCards;
        [SerializeField] private AdvEngineController adv;
        [SerializeField] private Text comparisonText;
        [SerializeField, Min(0)] private float resultSeconds = 1.2f;
        [SerializeField] private PokerPresentationEvent[] events = Array.Empty<PokerPresentationEvent>();
        private readonly HashSet<string> completedEvents = new HashSet<string>();
        private readonly HashSet<long> completedVisuals = new HashSet<long>();
        private readonly HashSet<PokerPresentationEvent> playedThisRound = new HashSet<PokerPresentationEvent>();
        private AdvScenarioPlayback playback;
        private PokerGame currentGame;
        private long roundCueId = -1;

        /// <summary>演出要求を購読する。</summary>
        private void OnEnable() { if (controller != null) controller.Presentation += Play; }
        /// <summary>購読を解除する。</summary>
        private void OnDisable() { if (controller != null) controller.Presentation -= Play; }

        /// <summary>SO、標準演出、SOの順に待機し、再試行では完了済み処理を飛ばす。</summary>
        /// <param name="cue">通知。</param>
        private IEnumerator Play(PokerCue cue)
        {
            if (!isActiveAndEnabled) throw new InvalidOperationException("演出管理が無効です。");
            if (currentGame != controller.Game || cue.Timing == PokerTiming.RoundStarted && roundCueId != cue.Id)
            {
                currentGame = controller.Game; roundCueId = cue.Id;
                completedEvents.Clear(); completedVisuals.Clear(); playedThisRound.Clear(); playback?.Clear();
            }
            yield return PlayEvents(cue, PokerEventStage.BeforeVisual);
            if (!completedVisuals.Contains(cue.Id)) { yield return Visual(cue); completedVisuals.Add(cue.Id); }
            yield return PlayEvents(cue, PokerEventStage.AfterVisual);
        }

        /// <summary>条件に合うイベントをInspectorの順序で実行する。</summary>
        /// <param name="cue">通知。</param>
        /// <param name="stage">標準演出の前後。</param>
        private IEnumerator PlayEvents(PokerCue cue, PokerEventStage stage)
        {
            for (int i = 0; i < events.Length; i++)
            {
                var entry = events[i]; string key = cue.Id + ":" + (int)stage + ":" + i;
                if (entry == null || !entry.Matches(cue, stage) || completedEvents.Contains(key)
                    || entry.OncePerRound && playedThisRound.Contains(entry)) continue;
                yield return cameras.SwitchTo(entry.CameraView);
                if (!string.IsNullOrWhiteSpace(entry.ScenarioLabel))
                {
                    if (playback == null) playback = new AdvScenarioPlayback(adv);
                    yield return playback.Play(entry.ScenarioLabel, key);
                }
                completedEvents.Add(key);
                if (entry.OncePerRound) playedThisRound.Add(entry);
            }
        }

        /// <summary>配布、伏せ札公開、視点移動と結果表示を行う。</summary>
        /// <param name="cue">通知。</param>
        private IEnumerator Visual(PokerCue cue)
        {
            switch (cue.Timing)
            {
                case PokerTiming.RoundStarted:
                    tableCards.Clear(); SetText("");
                    yield return cameras.SwitchTo(CardCameraView.Normal); break;
                case PokerTiming.CardDealt:
                    SetText("");
                    yield return cameras.SwitchTo(CardCameraView.Table);
                    yield return tableCards.Place(cue.Card.Value, cue.Actor == PokerActor.Player, cue.CardIndex, cue.FaceUp);
                    break;
                case PokerTiming.BeforePlayerDecision:
                    SetText(""); yield return cameras.SwitchTo(CardCameraView.Normal); break;
                case PokerTiming.BeforeShowdown:
                case PokerTiming.BeforeCardReveal:
                    yield return cameras.SwitchTo(CardCameraView.Table); break;
                case PokerTiming.CardRevealed:
                    yield return tableCards.Reveal(cue.Card.Value); break;
                case PokerTiming.QualificationChecked:
                    SetText(cue.DealerQualified == true ? "DEALER QUALIFIES" : "DEALER DOES NOT QUALIFY");
                    yield return new WaitForSecondsRealtime(resultSeconds); break;
                case PokerTiming.ResultDecided:
                    yield return cameras.SwitchTo(CardCameraView.Table);
                    string opponent = controller.Game.DealerCardsRevealed ? controller.Game.DealerScore.ToString() : "???";
                    string result = cue.Result == PokerResult.PlayerWin ? "YOU WIN" : cue.Result == PokerResult.DealerWin ? "DEALER WINS"
                        : cue.Result == PokerResult.PlayerFold ? "FOLD - ANTE LOST" : cue.Result == PokerResult.DealerNotQualified
                        ? "DEALER NOT QUALIFIED - ANTE WINS / PLAY RETURNED" : "PUSH";
                    SetText($"PLAYER {controller.Game.PlayerScore} / DEALER {opponent}\n{result}");
                    yield return new WaitForSecondsRealtime(resultSeconds); break;
            }
        }

        /// <summary>比較表示を更新する。</summary>
        /// <param name="text">表示文字。</param>
        private void SetText(string text) { if (comparisonText != null) comparisonText.text = text; }
    }
}
