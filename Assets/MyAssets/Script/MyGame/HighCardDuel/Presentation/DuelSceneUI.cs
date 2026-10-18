using Casino.Common;
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
        [SerializeField] private CardGameChoiceLayout choiceLayout = new CardGameChoiceLayout();
        private Casino.Story.CasinoRoundProgress progression;
        private Text callLabel;
        private Text raiseLabel;
        private PlayerData data;
        private bool presentationFailed;
        public int CurrentBaseBet => !previewMode && GameInstance.Instance != null
            ? GameInstance.Instance.casinoStory.GetBaseBet(Casino.Story.CasinoGameKind.HightCard, initialBet) : initialBet;

        /// <summary>次のラウンドから使う基本BETを変更する。</summary>
        /// <param name="amount">変更後の基本BET。</param>
        public void SetBaseBet(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (previewMode) initialBet = amount;
            else GameInstance.Instance.casinoStory.SetBaseBet(Casino.Story.CasinoGameKind.HightCard, amount);
        }

        /// <summary>各ボタンとゲーム通知を一度接続する。</summary>
        private void Awake()
        {
            progression = GetComponent<Casino.Story.CasinoRoundProgress>();
            callLabel = call != null ? call.GetComponentInChildren<Text>(true) : null;
            raiseLabel = raise != null ? raise.GetComponentInChildren<Text>(true) : null;
            choiceLayout.Initialize(nextRound, check, raise, call, fold, funding, retry);
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
        private void NextRound()
        {
            if (progression != null && progression.IsFinished) return;
            controller.StartRound(CurrentBaseBet);
        }
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
            if (progression != null) progression.Observe(state == DuelState.Completed);
            HighCardDuelGame game = controller.Game;
            if (game == null) return;
            if (callLabel != null) callLabel.text = "Call\n" + game.CallAmount;
            if (raiseLabel != null) raiseLabel.text = "Raise\n" + game.InitialBet;
            CardGameChoiceLayout.SetVisible(check, !presentationFailed && game.CanAct(DuelAction.Check));
            CardGameChoiceLayout.SetVisible(raise, !presentationFailed && game.CanAct(DuelAction.Raise));
            CardGameChoiceLayout.SetVisible(call, !presentationFailed && game.CanAct(DuelAction.Call));
            CardGameChoiceLayout.SetVisible(fold, !presentationFailed && game.CanAct(DuelAction.Fold));
            CardGameChoiceLayout.SetVisible(nextRound, !presentationFailed && state == DuelState.Completed && (progression == null || !progression.IsFinished));
            CardGameChoiceLayout.SetVisible(funding, !presentationFailed && state == DuelState.WaitingForFunds);
            CardGameChoiceLayout.SetVisible(retry, presentationFailed);
            choiceLayout.Refresh(state == DuelState.SelectingCard);
            string prompt = presentationFailed ? "Presentation stopped - check Console / Retry"
                : state == DuelState.SelectingCard ? "Drag a card to the table"
                : state == DuelState.ChoosingBet ? (game.CanAct(DuelAction.Call) ? $"Call {game.CallAmount} / Fold" : $"Check / Raise {game.InitialBet}")
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
