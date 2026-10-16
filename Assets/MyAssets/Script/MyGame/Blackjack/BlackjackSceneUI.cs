using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.Blackjack
{
    /// <summary>ブラックジャックの確認用起動と行動UI。本番資金にも接続できる。</summary>
    public sealed class BlackjackSceneUI : MonoBehaviour
    {
        [SerializeField] private BlackjackController controller;
        [SerializeField] private bool previewMode = true;
        [Tooltip("正の偶数を指定する。")]
        [SerializeField, Min(2)] private int initialBet = 10;
        [SerializeField, Min(0)] private int previewMoney = 1000;
        [SerializeField] private Text status;
        [SerializeField] private Button hit;
        [SerializeField] private Button stand;
        [SerializeField] private Button doubleDown;
        [SerializeField] private Button nextRound;
        [SerializeField] private Button funding;
        [SerializeField] private Button retry;
        private PlayerData data;
        private bool presentationFailed;

        /// <summary>ボタンと状態通知を一度接続する。</summary>
        private void Awake()
        {
            hit.onClick.AddListener(Hit); stand.onClick.AddListener(Stand); doubleDown.onClick.AddListener(DoubleDown);
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
        /// <summary>Hitを要求する。</summary>
        private void Hit() => controller.Act(BlackjackAction.Hit);
        /// <summary>Standを要求する。</summary>
        private void Stand() => controller.Act(BlackjackAction.Stand);
        /// <summary>Double Downを要求する。</summary>
        private void DoubleDown() => controller.Act(BlackjackAction.DoubleDown);
        /// <summary>次のラウンドを開始し、不正なBET設定は表示する。</summary>
        private void NextRound()
        {
            if (initialBet < 2 || initialBet % 2 != 0)
            {
                status.text = "Initial Bet must be a positive even number.";
                nextRound.interactable = true;
                Debug.LogError("Initial Betには正の偶数を指定してください。", this); return;
            }
            controller.StartRound(initialBet);
        }
        /// <summary>確認用資金を補充するか、本番で補充した資金で再開する。</summary>
        private void ResumeFunding()
        {
            if (controller.Game.State != BlackjackState.WaitingForFunds) return;
            if (previewMode) data.casinoMoney += controller.Game.Shortage;
            controller.ResumeAfterFunding();
        }
        /// <summary>修正した演出を再試行する。</summary>
        private void Retry() { presentationFailed = false; controller.RetryPresentation(); Refresh(controller.Game.State); }
        /// <summary>失敗表示を有効にする。</summary>
        /// <param name="exception">例外。</param>
        private void Failed(Exception exception) { presentationFailed = true; Refresh(controller.Game.State); }
        /// <summary>点数・所持金・行動ボタンを更新する。</summary>
        /// <param name="state">進行状態。</param>
        private void Refresh(BlackjackState state)
        {
            var game = controller.Game; if (game == null) return;
            hit.interactable = game.CanAct(BlackjackAction.Hit); stand.interactable = game.CanAct(BlackjackAction.Stand);
            doubleDown.interactable = game.CanAct(BlackjackAction.DoubleDown);
            nextRound.interactable = state == BlackjackState.Completed;
            funding.gameObject.SetActive(state == BlackjackState.WaitingForFunds); retry.gameObject.SetActive(presentationFailed);
            string opponent = game.DealerVisibleScore.Total + (!game.DealerHoleRevealed && game.DealerCards.Count > 1 ? " + ?" : "");
            string prompt = presentationFailed ? "Presentation stopped - Retry" : state == BlackjackState.ChoosingAction ? "Hit / Stand / Double Down"
                : state == BlackjackState.WaitingForFunds ? $"Required {game.RequiredFunds} / Shortage {game.Shortage}"
                : state == BlackjackState.Completed ? "Round complete" : "";
            status.text = $"{(previewMode ? "PREVIEW  " : "")}Money {game.Balance}   Bet {game.PlayerBet}   PLAYER {game.PlayerScore.Total} / DEALER {opponent}\n{prompt}";
        }
        /// <summary>通知の購読を解除する。</summary>
        private void OnDestroy()
        {
            if (controller == null) return;
            controller.StateChanged -= Refresh; controller.PresentationFailed -= Failed;
        }
    }
}
