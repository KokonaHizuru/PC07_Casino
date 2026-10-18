using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Casino.Cards
{
    /// <summary>ゲームから渡されたカードの表示・選択・プレイ通知を管理する手札。</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CardHandUI : MonoBehaviour
    {
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private CardArtwork artwork;
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private RectTransform playArea;
        [SerializeField] private bool interactable = true;
        [SerializeField, Min(1f)] private float spacing = 200f;
        [Tooltip("両端のカードを下げる量。0なら水平に並べる。")]
        [SerializeField, Min(0f)] private float curveDepth;
        [Tooltip("両端のカードの傾き。中央は直立し、左右へ扇状に開く。")]
        [SerializeField, Range(0f, 30f)] private float fanAngle;
        [Tooltip("配られたカードがUI手札へ入る時間（秒）。3Dでの配布が終わった後に実行する。")]
        [SerializeField, Min(.01f)] private float drawDuration = .15f;
        [SerializeField] private Vector2 drawOffset = new Vector2(0, 160);
        private readonly Dictionary<PlayingCard, Arrival> arrivals = new Dictionary<PlayingCard, Arrival>();
        private sealed class Arrival { public CardView View; public float Elapsed; public bool Finished; }

        private readonly List<CardView> cards = new List<CardView>();
        private RectTransform rect;
        private Canvas playAreaCanvas;
        private bool initialized;
        public int Count => cards.Count;
        public CardView SelectedCard { get; private set; }
        public bool Interactable => interactable && isActiveAndEnabled;
        public RectTransform DragLayer => dragLayer;
        public event Action<CardView> SelectionChanged;
        public event Action<PlayingCard> CardPlayed;

        /// <summary>参照先を初期化する。</summary>
        private void Awake() => Initialize();

        /// <summary>UIの参照を一度取得して保持する。</summary>
        private void Initialize()
        {
            if (initialized) return;
            rect = (RectTransform)transform;
            if (dragLayer == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                dragLayer = canvas != null ? (RectTransform)canvas.rootCanvas.transform : rect;
            }
            SetPlayArea(playArea);
            initialized = true;
        }

        /// <summary>ドラッグ中の表示先を指定する。</summary>
        /// <param name="layer">同じCanvas内の表示先。nullの場合は手札内に表示する。</param>
        public void SetDragLayer(RectTransform layer)
        {
            Initialize();
            dragLayer = layer != null ? layer : rect;
        }

        /// <summary>外部のUIをプレイ可能範囲として設定する。nullで無効にする。</summary>
        /// <param name="area">ドロップ判定に使うRectTransform。Imageは不要。</param>
        public void SetPlayArea(RectTransform area)
        {
            playArea = area;
            playAreaCanvas = area != null ? area.GetComponentInParent<Canvas>() : null;
        }

        /// <summary>手札全体の操作可否を変更する。</summary>
        /// <param name="value">操作を許可するか。</param>
        public void SetInteractable(bool value)
        {
            interactable = value;
            if (value) return;
            foreach (CardView card in cards) card.CancelInteraction();
            Select(null);
        }

        /// <summary>外部で配られたカードを手札に追加する。</summary>
        /// <param name="card">追加するカード。</param>
        /// <param name="faceUp">表向きに表示するか。</param>
        public CardView AddCard(PlayingCard card, bool faceUp = true)
        {
            Initialize();
            if (card.Rank < 1 || card.Rank > 13) throw new ArgumentException("無効なカードです。", nameof(card));
            if (cardPrefab == null || artwork == null) throw new InvalidOperationException("Card PrefabとArtworkを設定してください。");
            foreach (CardView existing in cards)
                if (existing.Card.Equals(card)) throw new InvalidOperationException("同じカードは手札に追加できません。");
            CardView view = Instantiate(cardPrefab, rect);
            view.Initialize(this, artwork, card, faceUp);
            cards.Add(view);
            LayoutCards();
            return view;
        }

        /// <summary>指定位置のカード表示を取得する。</summary>
        /// <param name="index">手札内の位置。</param>
        public CardView GetCard(int index) => cards[index];

        /// <summary>指定位置へカードを挿入し、手元から扇状の配置へ入る演出を待つ。</summary>
        /// <param name="card">追加するカード。</param>
        /// <param name="faceUp">表向きにするか。</param>
        /// <param name="index">挿入位置。</param>
        /// <param name="player">画面下のプレイヤー側か。</param>
        public IEnumerator AddCardAnimated(PlayingCard card, bool faceUp, int index, bool player)
        {
            if (!arrivals.TryGetValue(card, out Arrival arrival))
            {
                CardView view = cards.Find(item => item.Card.Equals(card));
                if (view == null)
                {
                    view = AddCard(card, faceUp);
                    cards.Remove(view); cards.Insert(Mathf.Clamp(index, 0, cards.Count), view);
                }
                arrival = new Arrival { View = view }; arrivals.Add(card, arrival);
            }
            if (arrival.Finished) yield break;
            if (!player) { LayoutCards(); arrival.Finished = true; yield break; }
            float duration = Mathf.Max(.01f, drawDuration);
            while (arrival.Elapsed < duration)
            {
                LayoutCards();
                float t = Mathf.SmoothStep(0, 1, arrival.Elapsed / duration);
                arrival.View.Rect.anchoredPosition += new Vector2(drawOffset.x, player ? drawOffset.y : -drawOffset.y) * (1 - t);
                arrival.Elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            LayoutCards(); arrival.Finished = true;
        }

        /// <summary>カードを手札から削除する。プレイ通知は行わない。</summary>
        /// <param name="card">削除するカード。</param>
        public bool RemoveCard(PlayingCard card)
        {
            CardView view = cards.Find(item => item.Card.Equals(card));
            if (view == null) return false;
            cards.Remove(view);
            arrivals.Remove(card);
            if (SelectedCard == view) Select(null);
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
            LayoutCards();
            return true;
        }

        /// <summary>全カードを削除して手札を空にする。</summary>
        public void Clear()
        {
            Select(null);
            foreach (CardView card in cards)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            cards.Clear();
            arrivals.Clear();
        }

        /// <summary>ホバー中のカードを選択して外部へ通知する。</summary>
        /// <param name="view">選択するカード。nullで解除する。</param>
        internal void Select(CardView view)
        {
            if (SelectedCard == view) return;
            CardView previous = SelectedCard;
            SelectedCard = view;
            if (previous != null) previous.SetHighlighted(false);
            if (view != null) view.SetHighlighted(true);
            SelectionChanged?.Invoke(view);
        }

        /// <summary>カーソルのドロップ位置を判定し、成立したカードを手札から取り除いて通知する。</summary>
        /// <param name="view">ドラッグされたカード。</param>
        /// <param name="screenPoint">画面座標のドロップ位置。</param>
        internal bool TryPlay(CardView view, Vector2 screenPoint)
        {
            if (!Interactable || !cards.Contains(view) || playArea == null || !playArea.gameObject.activeInHierarchy)
                return false;
            Camera camera = playAreaCanvas != null && playAreaCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? playAreaCanvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(playArea, screenPoint, camera)) return false;
            PlayingCard card = view.Card;
            RemoveCard(card);
            CardPlayed?.Invoke(card);
            return true;
        }

        /// <summary>手札領域の幅に合わせてカードを中央揃えし、扇状に配置する。</summary>
        private void LayoutCards()
        {
            if (rect == null || cards.Count == 0) return;
            Vector2 size = cards[0].Rect.sizeDelta;
            float radians = fanAngle * Mathf.Deg2Rad;
            float width = Mathf.Max(size.x, size.x * Mathf.Cos(radians) + size.y * Mathf.Sin(radians));
            float step = cards.Count > 1 ? Mathf.Min(spacing, Mathf.Max(0f, rect.rect.width - width) / (cards.Count - 1)) : 0f;
            float middle = (cards.Count - 1) * 0.5f;
            for (int i = 0; i < cards.Count; i++)
            {
                float offset = middle > 0f ? (i - middle) / middle : 0f;
                cards[i].SetHome(new Vector2((i - middle) * step, -curveDepth * offset * offset), i, -fanAngle * offset);
            }
        }

        /// <summary>Inspectorで変更した曲率と間隔を再生中の手札へ反映する。</summary>
        private void OnValidate() => LayoutCards();

        /// <summary>解像度やUIサイズ変更時に並びを更新する。</summary>
        private void OnRectTransformDimensionsChange() => LayoutCards();

        /// <summary>非表示時にドラッグと選択を解除する。</summary>
        private void OnDisable()
        {
            foreach (CardView card in cards) if (card != null) card.SuspendInteraction();
            Select(null);
        }

        /// <summary>ドラッグ用レイヤーに移動したカードも破棄する。</summary>
        private void OnDestroy()
        {
            foreach (CardView card in cards)
                if (card != null) Destroy(card.gameObject);
        }
    }
}
