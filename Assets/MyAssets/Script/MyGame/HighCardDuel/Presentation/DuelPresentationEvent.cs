using UnityEngine;

namespace Casino.HighCardDuel
{
    public enum DuelCameraView { Unchanged, Normal, Table }
    public enum DuelEventStage { BeforeVisual, AfterVisual }
    public enum DuelActionFilter { Any, Check, Raise, Call, Fold }

    /// <summary>演出タイミングと宴のシナリオラベルを対応付ける設定。</summary>
    [CreateAssetMenu(menuName = "Casino/High Card Duel/Presentation Event")]
    public sealed class DuelPresentationEvent : ScriptableObject
    {
        [SerializeField] private bool eventEnabled = true;
        [SerializeField] private DuelTiming timing = DuelTiming.BeforeCardSelection;
        [SerializeField] private DuelEventStage stage;
        [Tooltip("Noneで両者を対象にする。")]
        [SerializeField] private DuelActor actor;
        [SerializeField] private DuelActionFilter action;
        [Tooltip("0で全フェーズを対象にする。")]
        [SerializeField, Range(0, 3)] private int phase;
        [Tooltip("Noneで勝敗を限定しない。")]
        [SerializeField] private DuelResult result;
        [SerializeField] private bool oncePerRound;
        [SerializeField] private DuelCameraView cameraView;
        [Tooltip("宴のシナリオラベル。空欄なら会話を再生せずカメラ切替のみ行う。")]
        [SerializeField] private string scenarioLabel;
        public bool OncePerRound => oncePerRound;
        public DuelCameraView CameraView => cameraView;
        public string ScenarioLabel => scenarioLabel;

        /// <summary>現在の通知がイベントの発生条件を満たすか判定する。</summary>
        /// <param name="cue">ゲームの演出通知。</param>
        /// <param name="eventStage">標準演出の前後。</param>
        public bool Matches(DuelCue cue, DuelEventStage eventStage)
        {
            return eventEnabled && timing == cue.Timing && stage == eventStage
                && (actor == DuelActor.None || actor == cue.Actor)
                && (phase == 0 || phase == cue.Phase)
                && (result == DuelResult.None || result == cue.Result)
                && (action == DuelActionFilter.Any || cue.Action.HasValue && (int)action - 1 == (int)cue.Action.Value);
        }
    }
}
