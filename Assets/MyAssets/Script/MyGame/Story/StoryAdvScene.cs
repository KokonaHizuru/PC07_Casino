using System;
using System.Collections;
using Casino.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Casino.Story
{
    /// <summary>シーン専用の宴イベントを再生して次のシーンへ移る。</summary>
    public sealed class StoryAdvScene : MonoBehaviour
    {
        [SerializeField] private string scenarioLabel;
        [SerializeField] private string nextScene;
        private AdvScenarioPlayback playback;
        private bool running;
        private bool finished;
        /// <summary>シーン開始時にADVを再生する。</summary>
        private void Start() => Retry();
        /// <summary>失敗したADVを再試行し、完了済みの再生は繰り返さない。</summary>
        public void Retry()
        {
            if (!running && !finished) StartCoroutine(Run());
        }
        /// <summary>再生失敗を報告し、正常終了後だけシーンを遷移する。</summary>
        private IEnumerator Run()
        {
            running = true;
            bool failed = false;
            if (playback == null) playback = new AdvScenarioPlayback(null);
            yield return PresentationRoutine.Run(playback.Play(scenarioLabel, "Story"), exception =>
            { failed = true; Debug.LogException(exception, this); });
            running = false;
            if (failed) yield break;
            if (!Application.CanStreamedLevelBeLoaded(nextScene))
            { Debug.LogError("遷移先をBuild Settingsへ登録してください: " + nextScene, this); yield break; }
            finished = true;
            SceneManager.LoadScene(nextScene);
        }
    }
}
