using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Casino.Story
{
    /// <summary>精算と宴が完了したラウンドを数え、専用エンディングへ進む。</summary>
    public sealed class CasinoRoundProgress : MonoBehaviour
    {
        [SerializeField, Min(1)] private int roundsToPlay = 5;
        [SerializeField] private string endingScene;
        public int CompletedRounds { get; private set; }
        public bool IsFinished => CompletedRounds >= roundsToPlay;
        private bool wasCompleted;
        private bool transitioning;

        /// <summary>完了状態への変化だけを数え、最終ラウンドで遷移を予約する。</summary>
        /// <param name="completed">ゲームが全演出を終えた状態か。</param>
        public void Observe(bool completed)
        {
            if (completed && !wasCompleted) CompletedRounds++;
            wasCompleted = completed;
            if (IsFinished && !transitioning)
            {
                transitioning = true;
                StartCoroutine(GoToEnding());
            }
        }

        /// <summary>ゲームの完了通知を抜けてから専用エンディングを読み込む。</summary>
        private IEnumerator GoToEnding()
        {
            yield return null;
            if (string.IsNullOrWhiteSpace(endingScene) || !Application.CanStreamedLevelBeLoaded(endingScene))
                throw new InvalidOperationException("エンディングシーンをBuild Settingsへ登録してください: " + endingScene);
            SceneManager.LoadScene(endingScene);
        }
    }
}
