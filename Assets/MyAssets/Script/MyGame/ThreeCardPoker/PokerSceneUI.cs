using Casino.Common;
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
        [SerializeField] private Button keepHand;
        [SerializeField] private RectTransform exchangeArea;
        [SerializeField] private Vector2 exchangeAreaOffset = Vector2.zero;
        [SerializeField] private Vector2 exchangeAreaSize = new Vector2(900, 300);
        [SerializeField] private CardGameChoiceLayout choiceLayout = new CardGameChoiceLayout();
        private Casino.Story.CasinoRoundProgress progression;
        private PlayerData data;
        private bool presentationFailed;
        public int CurrentBaseBet => !previewMode && GameInstance.Instance != null
            ? GameInstance.Instance.casinoStory.GetBaseBet(Casino.Story.CasinoGameKind.Poker, initialBet) : initialBet;

        /// <summary>次のラウンドから使う基本BETを変更する。</summary>
        /// <param name="amount">変更後の基本BET。</param>
        public void SetBaseBet(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (previewMode) initialBet = amount;
            else GameInstance.Instance.casinoStory.SetBaseBet(Casino.Story.CasinoGameKind.Poker, amount);
        }

        /// <summary>ボタンと状態通知を一度接続する。</summary>
        private void Awake()
        {
            progression = GetComponent<Casino.Story.CasinoRoundProgress>();
            choiceLayout.Initialize(nextRound, play, fold, keepHand, funding, retry);
            if (exchangeArea != null)
            {
                exchangeArea.anchoredPosition = exchangeAreaOffset;
                exchangeArea.sizeDelta = exchangeAreaSize;
                exchangeArea.gameObject.SetActive(false);
            }
            play.onClick.AddListener(Play); fold.onClick.AddListener(Fold);
            nextRound.onClick.AddListener(NextRound); funding.onClick.AddListener(ResumeFunding); retry.onClick.AddListener(Retry);
            if (keepHand != null) keepHand.onClick.AddListener(KeepHand);
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
        /// <summary>交換せずに現在の手札でPlay／Foldへ進む。</summary>
        private void KeepHand() => controller.SkipExchange();
        /// <summary>次のラウンドを開始し、不正なBET設定は表示する。</summary>
        private void NextRound()
        {
            if (progression != null && progression.IsFinished) return;
            int bet = CurrentBaseBet;
            if (bet < 1)
            {
                status.text = "Ante must be a positive integer.";
                nextRound.interactable = true;
                nextRound.gameObject.SetActive(true);
                Debug.LogError("Initial Betには正の整数を指定してください。", this); return;
            }
            controller.StartRound(bet);
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
            if (progression != null) progression.Observe(state == PokerState.Completed);
            var game = controller.Game; if (game == null) return;
            CardGameChoiceLayout.SetVisible(play, !presentationFailed && game.CanAct(PokerAction.Play));
            CardGameChoiceLayout.SetVisible(fold, !presentationFailed && game.CanAct(PokerAction.Fold));
            bool exchanging = !presentationFailed && state == PokerState.ChoosingExchange;
            CardGameChoiceLayout.SetVisible(keepHand, exchanging);
            if (exchangeArea != null) exchangeArea.gameObject.SetActive(exchanging);
            CardGameChoiceLayout.SetVisible(nextRound, !presentationFailed && state == PokerState.Completed && (progression == null || !progression.IsFinished));
            CardGameChoiceLayout.SetVisible(funding, !presentationFailed && state == PokerState.WaitingForFunds);
            CardGameChoiceLayout.SetVisible(retry, presentationFailed);
            choiceLayout.Refresh(exchanging);
            string opponent = game.DealerCardsRevealed ? game.DealerScore.ToString() : "???";
            string player = game.PlayerCards.Count == 3 ? game.PlayerScore.ToString() : "-";
            string prompt = presentationFailed ? "Presentation stopped - Retry"
                : exchanging ? "Drag one card to exchange / Keep Hand" : state == PokerState.ChoosingAction ? "Play / Fold"
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
