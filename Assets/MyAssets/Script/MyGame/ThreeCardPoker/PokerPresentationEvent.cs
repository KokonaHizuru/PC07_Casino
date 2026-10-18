using Casino.Common;
using UnityEngine;

namespace Casino.ThreeCardPoker
{
    public enum PokerEventStage { BeforeVisual, AfterVisual }
    public enum PokerQualificationFilter { Any, Qualified, NotQualified }
    public enum PokerActionFilter { Any, Play, Fold, Exchange, KeepHand }

    /// <summary>スリーカードポーカーのタイミングに会話と視点を対応付ける。</summary>
    [CreateAssetMenu(menuName = "Casino/Poker/Presentation Event")]
    public sealed class PokerPresentationEvent : Casino.Common.PresentationEventDefinition
    {
        [SerializeField] private bool eventEnabled = true;
        [SerializeField] private PokerTiming timing = PokerTiming.BeforePlayerDecision;
        [SerializeField] private PokerEventStage stage;
        [Tooltip("Noneは対象者を限定しない。")]
        [SerializeField] private PokerActor actor;
        [SerializeField] private PokerActionFilter action;
        [Tooltip("Noneは結果を限定しない。")]
        [SerializeField] private PokerResult result;
        [SerializeField] private PokerQualificationFilter qualification;
        [SerializeField] private bool oncePerRound;
        [SerializeField] private CardCameraView cameraView;
        [SerializeField] private string scenarioLabel;
        public bool OncePerRound => oncePerRound;
        public CardCameraView CameraView => cameraView;
        public string ScenarioLabel => scenarioLabel;

        /// <summary>通知が設定した条件に一致するか確認する。</summary>
        /// <param name="cue">通知。</param>
        /// <param name="eventStage">標準演出の前後。</param>
        public bool Matches(PokerCue cue, PokerEventStage eventStage) => eventEnabled && timing == cue.Timing && stage == eventStage
            && (actor == PokerActor.None || actor == cue.Actor) && (result == PokerResult.None || result == cue.Result)
            && (qualification == PokerQualificationFilter.Any || cue.DealerQualified.HasValue && cue.DealerQualified.Value == (qualification == PokerQualificationFilter.Qualified))
            && (action == PokerActionFilter.Any || cue.Action.HasValue && (int)action - 1 == (int)cue.Action.Value);
    }
}
