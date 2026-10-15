using System;
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

        /// <summary>カードを手札から削除する。プレイ通知は行わない。</summary>
        /// <param name="card">削除するカード。</param>
        public bool RemoveCard(PlayingCard card)
        {
            CardView view = cards.Find(item => item.Card.Equals(card));
            if (view == null) return false;
            cards.Remove(view);
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

        /// <summary>手札領域の幅に合わせてカードを中央揃えする。</summary>
        private void LayoutCards()
        {
            if (rect == null || cards.Count == 0) return;
            float width = cards[0].Rect.sizeDelta.x;
            float step = cards.Count > 1 ? Mathf.Min(spacing, Mathf.Max(0f, rect.rect.width - width) / (cards.Count - 1)) : 0f;
            for (int i = 0; i < cards.Count; i++)
                cards[i].SetHome(new Vector2((i - (cards.Count - 1) * 0.5f) * step, 0f), i);
        }

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
