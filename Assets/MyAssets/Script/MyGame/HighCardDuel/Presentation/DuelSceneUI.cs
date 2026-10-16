using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Casino.HighCardDuel
{
    /// <summary>演出確認シーンのBET操作と所持金表示。本番データへの接続にも対応する。</summary>
    public sealed class DuelSceneUI : MonoBehaviour
    {
        [SerializeField] private HighCardDuelController controller;
        [Tooltip("有効時は保存データを変更しない独立した確認用資金を使う。")]
        [SerializeField] private bool previewMode = true;
        [SerializeField, Min(1)] private int initialBet = 10;
        [SerializeField, Min(0)] private int previewMoney = 1000;
        [SerializeField] private Text status;
        [SerializeField] private Button check;
        [SerializeField] private Button raise;
        [SerializeField] private Button call;
        [SerializeField] private Button fold;
        [SerializeField] private Button nextRound;
        [SerializeField] private Button funding;
        [SerializeField] private Button retry;
        private PlayerData data;
        private bool presentationFailed;

        /// <summary>各ボタンとゲーム通知を一度接続する。</summary>
        private void Awake()
        {
            check.onClick.AddListener(Check);
            raise.onClick.AddListener(Raise);
            call.onClick.AddListener(Call);
            fold.onClick.AddListener(Fold);
            nextRound.onClick.AddListener(NextRound);
            funding.onClick.AddListener(ResumeFunding);
            retry.onClick.AddListener(Retry);
            controller.StateChanged += Refresh;
            controller.PresentationFailed += Failed;
        }

        /// <summary>確認用または本番のデータを接続してラウンドを開始する。</summary>
        private IEnumerator Start()
        {
            if (previewMode) data = new PlayerData { casinoMoney = previewMoney };
            else
            {
                float deadline = Time.realtimeSinceStartup + 15;
                while (GameInstance.Instance == null)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("GameInstanceが起動していません。");
                    yield return null;
                }
                data = GameInstance.Instance.playerData;
            }
            controller.Initialize(data);
            NextRound();
        }

        /// <summary>Checkを実行する。</summary>
        private void Check() => controller.Act(DuelAction.Check);
        /// <summary>Raiseを実行する。</summary>
        private void Raise() => controller.Act(DuelAction.Raise);
        /// <summary>Callを実行する。</summary>
        private void Call() => controller.Act(DuelAction.Call);
        /// <summary>Foldを実行する。</summary>
        private void Fold() => controller.Act(DuelAction.Fold);
        /// <summary>次のラウンドを開始する。</summary>
        private void NextRound() => controller.StartRound(initialBet);
        /// <summary>確認用資金を補充するか、本番側での補充後に再開する。</summary>
        private void ResumeFunding()
        {
            if (controller.Game.State != DuelState.WaitingForFunds) return;
            if (previewMode) data.casinoMoney += controller.Game.Shortage;
            controller.ResumeAfterFunding();
        }
        /// <summary>停止した演出を再試行する。</summary>
        private void Retry()
        {
            presentationFailed = false;
            controller.RetryPresentation();
            Refresh(controller.Game.State);
        }
        /// <summary>演出失敗を操作UIへ表示する。</summary>
        /// <param name="exception">演出側の例外。</param>
        private void Failed(Exception exception)
        {
            presentationFailed = true;
            Refresh(controller.Game.State);
        }
        /// <summary>状態に応じて必要なボタンだけを操作可能にする。</summary>
        /// <param name="state">現在の進行状態。</param>
        private void Refresh(DuelState state)
        {
            HighCardDuelGame game = controller.Game;
            if (game == null) return;
            check.interactable = game.CanAct(DuelAction.Check);
            raise.interactable = game.CanAct(DuelAction.Raise);
            call.interactable = game.CanAct(DuelAction.Call);
            fold.interactable = game.CanAct(DuelAction.Fold);
            nextRound.interactable = state == DuelState.Completed;
            funding.gameObject.SetActive(state == DuelState.WaitingForFunds);
            retry.gameObject.SetActive(presentationFailed);
            string prompt = presentationFailed ? "Presentation stopped - check Console / Retry"
                : state == DuelState.SelectingCard ? "Drag a card to the table"
                : state == DuelState.ChoosingBet ? "Choose your bet"
                : state == DuelState.WaitingForFunds ? $"Required {game.RequiredFunds} / Shortage {game.Shortage}"
                : state == DuelState.Completed ? "Round complete" : "";
            status.text = $"{(previewMode ? "PREVIEW  " : "")}Money {game.Balance}   Bet {game.PlayerBet}   Phase {game.Phase}/3\n{prompt}";
        }
        /// <summary>ゲーム通知の購読を解除する。</summary>
        private void OnDestroy()
        {
            if (controller == null) return;
            controller.StateChanged -= Refresh;
            controller.PresentationFailed -= Failed;
        }
    }
}
