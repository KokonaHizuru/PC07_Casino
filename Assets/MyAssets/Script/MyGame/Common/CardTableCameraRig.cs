using System;
using System.Collections;
using Casino.Cards;
using Unity.Cinemachine;
using UnityEngine;

namespace Casino.Common
{
    public enum CardCameraView { Unchanged, Normal, Table }
    /// <summary>同じ描画カメラを維持してCinemachineの視点だけを切り替える。</summary>
    public class CardTableCameraRig : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private CinemachineCamera normalCamera;
        [SerializeField] private CinemachineCamera tableCamera;
        [SerializeField] private CardHandUI playerHand;
        [SerializeField] private CardHandUI dealerHand;
        [SerializeField, Min(0)] private float handSlideDuration = .35f;
        [SerializeField, Min(0)] private float hiddenPadding = 40f;
        [SerializeField, Min(1)] private float transitionTimeout = 15;
        private HandSlide playerSlide;
        private HandSlide dealerSlide;
        private bool handsHidden;

        /// <summary>手札とCanvasの参照および通常位置を保持する。</summary>
        protected virtual void Awake()
        {
            if (playerHand != null) playerSlide = new HandSlide(playerHand);
            if (dealerHand != null) dealerSlide = new HandSlide(dealerHand);
        }

        /// <summary>手札の通常位置と画面外への移動を管理する。</summary>
        private sealed class HandSlide
        {
            private readonly RectTransform rect;
            private readonly RectTransform canvas;
            private readonly Transform parent;
            private Vector2 home;
            private Vector2 start;
            private Vector2 target;
            public bool Moving => (target - start).sqrMagnitude > .01f;

            /// <summary>移動対象と座標変換用の参照を一度取得する。</summary>
            /// <param name="hand">移動する手札。</param>
            public HandSlide(CardHandUI hand)
            {
                rect = (RectTransform)hand.transform;
                parent = rect.parent;
                Canvas ownerCanvas = hand.GetComponentInParent<Canvas>();
                if (ownerCanvas == null) throw new InvalidOperationException("手札の親にCanvasが必要です。");
                canvas = (RectTransform)ownerCanvas.rootCanvas.transform;
                home = rect.anchoredPosition;
            }

            /// <summary>現在位置から通常位置または画面外への移動を準備する。</summary>
            /// <param name="hide">画面外へ移動するか。</param>
            /// <param name="up">上方向へ退避するか。</param>
            /// <param name="padding">画面端からの余白。</param>
            /// <param name="rememberHome">現在位置を通常位置として保持するか。</param>
            public void Prepare(bool hide, bool up, float padding, bool rememberHome)
            {
                if (rememberHome) home = rect.anchoredPosition;
                start = rect.anchoredPosition;
                target = home;
                if (!hide) return;
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, rect);
                float distance = up ? canvas.rect.yMax + padding - bounds.min.y
                    : canvas.rect.yMin - padding - bounds.max.y;
                Vector3 delta = parent.InverseTransformVector(canvas.TransformVector(Vector3.up * distance));
                target = start + new Vector2(delta.x, delta.y);
            }

            /// <summary>手札の表示位置を補間する。</summary>
            /// <param name="progress">0から1の補間率。</param>
            public void Apply(float progress)
            {
                if (rect != null) rect.anchoredPosition = Vector2.LerpUnclamped(start, target, progress);
            }
        }

        /// <summary>指定視点を有効にし、ブレンド完了まで待つ。</summary>
        /// <param name="view">切り替える視点。</param>
        public IEnumerator SwitchTo(CardCameraView view)
        {
            if (view == CardCameraView.Unchanged) yield break;
            if (brain == null || normalCamera == null || tableCamera == null)
                throw new InvalidOperationException("CinemachineBrainと通常・俯瞰カメラを設定してください。");
            CinemachineCamera target = view == CardCameraView.Table ? tableCamera : normalCamera;
            normalCamera.Priority = view == CardCameraView.Normal ? 100 : 0;
            tableCamera.Priority = view == CardCameraView.Table ? 100 : 0;
            if (!target.isActiveAndEnabled || !brain.isActiveAndEnabled)
                throw new InvalidOperationException("切り替え先カメラとBrainを有効にしてください。");
            bool hide = ShouldHideHands(view);
            Canvas.ForceUpdateCanvases();
            playerSlide?.Prepare(hide, false, hiddenPadding, hide && !handsHidden);
            dealerSlide?.Prepare(hide, true, hiddenPadding, hide && !handsHidden);
            handsHidden = hide;
            float slideDuration = playerSlide?.Moving == true || dealerSlide?.Moving == true ? handSlideDuration : 0f;
            float started = Time.realtimeSinceStartup;
            float deadline = Time.realtimeSinceStartup + transitionTimeout;
            // 同じコルーチンで手札も動かし、カメラと手札の両方が完了するまで入力を再開しない。
            do
            {
                float progress = slideDuration > 0f ? Mathf.Clamp01((Time.realtimeSinceStartup - started) / slideDuration) : 1f;
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                playerSlide?.Apply(eased);
                dealerSlide?.Apply(eased);
                if (Time.realtimeSinceStartup > deadline)
                    throw new InvalidOperationException("カメラ切替が完了しません。PriorityとOutput Channelを確認してください。");
                yield return null;
            }
            while (Time.realtimeSinceStartup - started < slideDuration || !ReferenceEquals(brain.ActiveVirtualCamera, target) || brain.IsBlending);
            playerSlide?.Apply(1f);
            dealerSlide?.Apply(1f);
        }

        /// <summary>切替後の視点で手札を画面外へ退避するかを判定する。</summary>
        /// <param name="view">切り替える視点。</param>
        protected virtual bool ShouldHideHands(CardCameraView view) => view == CardCameraView.Table;
    }
}
