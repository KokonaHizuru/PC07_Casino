using Casino.Common;
using UnityEngine;

namespace Casino.Blackjack
{
    public enum BlackjackEventStage { BeforeVisual, AfterVisual }
    public enum BlackjackActionFilter { Any, Hit, Stand, DoubleDown }

    /// <summary>ブラックジャックのタイミングに会話と視点を対応付ける。</summary>
    [CreateAssetMenu(menuName = "Casino/Blackjack/Presentation Event")]
    public sealed class BlackjackPresentationEvent : ScriptableObject
    {
        [SerializeField] private bool eventEnabled = true;
        [SerializeField] private BlackjackTiming timing = BlackjackTiming.BeforePlayerDecision;
        [SerializeField] private BlackjackEventStage stage;
        [Tooltip("Noneは対象者を限定しない。")]
        [SerializeField] private BlackjackActor actor;
        [SerializeField] private BlackjackActionFilter action;
        [Tooltip("Noneは結果を限定しない。")]
        [SerializeField] private BlackjackResult result;
        [SerializeField] private bool oncePerRound;
        [SerializeField] private CardCameraView cameraView;
        [SerializeField] private string scenarioLabel;
        public bool OncePerRound => oncePerRound;
        public CardCameraView CameraView => cameraView;
        public string ScenarioLabel => scenarioLabel;

        /// <summary>通知が設定した条件に一致するか確認する。</summary>
        /// <param name="cue">通知。</param>
        /// <param name="eventStage">標準演出の前後。</param>
        public bool Matches(BlackjackCue cue, BlackjackEventStage eventStage) => eventEnabled && timing == cue.Timing && stage == eventStage
            && (actor == BlackjackActor.None || actor == cue.Actor) && (result == BlackjackResult.None || result == cue.Result)
            && (action == BlackjackActionFilter.Any || cue.Action.HasValue && (int)action - 1 == (int)cue.Action.Value);
    }
}
