using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.ThreeCardPoker
{
    /// <summary>スリーカードポーカーの確認用起動と行動UI。本番資金にも接続できる。</summary>
    public sealed class PokerSceneUI : MonoBehaviour
    {
        [SerializeField] private PokerController controller;
        [SerializeField] private bool previewMode = true;
        [Tooltip("正の整数を指定する。")]
        [SerializeField, Min(1)] private int initialBet = 10;
        [SerializeField, Min(0)] private int previewMoney = 1000;
        [SerializeField] private Text status;
        [SerializeField] private Button play;
        [SerializeField] private Button fold;
        [SerializeField] private Button nextRound;
        [SerializeField] private Button funding;
        [SerializeField] private Button retry;
        private PlayerData data;
        private bool presentationFailed;

        /// <summary>ボタンと状態通知を一度接続する。</summary>
        private void Awake()
        {
            play.onClick.AddListener(Play); fold.onClick.AddListener(Fold);
            nextRound.onClick.AddListener(NextRound); funding.onClick.AddListener(ResumeFunding); retry.onClick.AddListener(Retry);
            controller.StateChanged += Refresh; controller.PresentationFailed += Failed;
        }
        /// <summary>確認用または本番のデータで開始する。</summary>
        private IEnumerator Start()
        {
            if (previewMode) data = new PlayerData { casinoMoney = previewMoney };
            else
            {
                float deadline = Time.realtimeSinceStartup + 15;
                while (GameInstance.Instance == null)
                { if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("GameInstanceが起動していません。"); yield return null; }
                data = GameInstance.Instance.playerData;
            }
            controller.Initialize(data); NextRound();
        }
        /// <summary>Playを要求する。</summary>
        private void Play() => controller.Act(PokerAction.Play);
        /// <summary>Foldを要求する。</summary>
        private void Fold() => controller.Act(PokerAction.Fold);
        /// <summary>次のラウンドを開始し、不正なBET設定は表示する。</summary>
        private void NextRound()
        {
            if (initialBet < 1)
            {
                status.text = "Ante must be a positive integer.";
                nextRound.interactable = true;
                nextRound.gameObject.SetActive(true);
                Debug.LogError("Initial Betには正の整数を指定してください。", this); return;
            }
            controller.StartRound(initialBet);
        }
        /// <summary>確認用資金を補充するか、本番で補充した資金で再開する。</summary>
        private void ResumeFunding()
        {
            if (controller.Game.State != PokerState.WaitingForFunds) return;
            if (previewMode) data.casinoMoney += controller.Game.Shortage;
            controller.ResumeAfterFunding();
        }
        /// <summary>修正した演出を再試行する。</summary>
        private void Retry() { presentationFailed = false; controller.RetryPresentation(); Refresh(controller.Game.State); }
        /// <summary>失敗表示を有効にする。</summary>
        /// <param name="exception">例外。</param>
        private void Failed(Exception exception) { presentationFailed = true; Refresh(controller.Game.State); }
        /// <summary>役・所持金・行動ボタンを更新する。</summary>
        /// <param name="state">進行状態。</param>
        private void Refresh(PokerState state)
        {
            var game = controller.Game; if (game == null) return;
            play.interactable = game.CanAct(PokerAction.Play); fold.interactable = game.CanAct(PokerAction.Fold);
            nextRound.interactable = state == PokerState.Completed;
            nextRound.gameObject.SetActive(state == PokerState.Completed);
            funding.gameObject.SetActive(state == PokerState.WaitingForFunds); retry.gameObject.SetActive(presentationFailed);
            string opponent = game.DealerCardsRevealed ? game.DealerScore.ToString() : "???";
            string player = game.PlayerCards.Count == 3 ? game.PlayerScore.ToString() : "-";
            string prompt = presentationFailed ? "Presentation stopped - Retry" : state == PokerState.ChoosingAction ? "Play / Fold"
                : state == PokerState.WaitingForFunds ? $"Required {game.RequiredFunds} / Shortage {game.Shortage}"
                : state == PokerState.Completed ? "Round complete" : "";
            status.text = $"{(previewMode ? "PREVIEW  " : "")}Money {game.Balance}   Ante {game.Ante} / Play {game.PlayBet}   PLAYER {player} / DEALER {opponent}\n{prompt}";
        }
        /// <summary>通知の購読を解除する。</summary>
        private void OnDestroy()
        {
            if (controller == null) return;
            controller.StateChanged -= Refresh; controller.PresentationFailed -= Failed;
        }
    }
}
