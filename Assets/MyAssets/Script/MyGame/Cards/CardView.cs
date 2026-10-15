using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Casino.Cards
{
    /// <summary>カード1枚の表裏表示とホバー・ドラッグ操作。</summary>
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RawImage artworkImage;
        [SerializeField] private RectTransform visual;
        [SerializeField, Min(0f)] private float hoverLift = 28f;
        [SerializeField, Min(0.01f)] private float hoverDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float returnDuration = 0.25f;
        private CardHandUI owner;
        private CardArtwork artwork;
        private RectTransform rect;
        private RectTransform homeParent;
        private Image hitArea;
        private Vector2 homePosition;
        private int homeIndex;
        private int dragPointer;
        private bool dragging;
        private bool returning;
        private bool restorePending;
        private Tween hoverTween;
        private Tween returnTween;
        public PlayingCard Card { get; private set; }
        public bool FaceUp { get; private set; }
        public RectTransform Rect => rect;
        public bool IsDragging => dragging;

        /// <summary>UIコンポーネントの参照を保持する。</summary>
        private void Awake()
        {
            rect = (RectTransform)transform;
            hitArea = GetComponent<Image>();
        }

        /// <summary>所属する手札と表示内容を設定する。</summary>
        /// <param name="hand">所属する手札。</param>
        /// <param name="cardArtwork">カードの画像設定。</param>
        /// <param name="card">表示するカード。</param>
        /// <param name="faceUp">表向きにするか。</param>
        public void Initialize(CardHandUI hand, CardArtwork cardArtwork, PlayingCard card, bool faceUp)
        {
            owner = hand;
            artwork = cardArtwork;
            homeParent = (RectTransform)hand.transform;
            Card = card;
            SetFaceUp(faceUp);
        }

        /// <summary>カードの表裏表示を切り替える。</summary>
        /// <param name="faceUp">表向きにするか。</param>
        public void SetFaceUp(bool faceUp)
        {
            FaceUp = faceUp;
            if (artwork != null) artwork.Apply(artworkImage, Card, faceUp);
        }

        /// <summary>手札内での通常位置と描画順を設定する。</summary>
        /// <param name="position">通常の表示位置。</param>
        /// <param name="index">通常の描画順。</param>
        internal void SetHome(Vector2 position, int index)
        {
            homePosition = position;
            homeIndex = index;
            if (dragging) return;
            if (returning) { ReturnHome(); return; }
            rect.anchoredPosition = position;
            rect.SetSiblingIndex(index);
        }

        /// <summary>当たり判定を移動させず絵柄だけを持ち上げる。</summary>
        /// <param name="selected">持ち上げるか。</param>
        internal void SetHighlighted(bool selected)
        {
            hoverTween?.Kill();
            if (visual == null) return;
            Vector2 target = selected && !dragging ? Vector2.up * hoverLift : Vector2.zero;
            if (!isActiveAndEnabled || restorePending) { visual.anchoredPosition = target; return; }
            hoverTween = DOTween.To(() => visual.anchoredPosition, value => visual.anchoredPosition = value,
                target, hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true);
            if (selected) rect.SetAsLastSibling();
            else if (!dragging && !returning) rect.SetSiblingIndex(homeIndex);
        }

        /// <summary>マウスが重なったカードを選択する。</summary>
        /// <param name="eventData">ポインター情報。</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner == null || !owner.Interactable || dragging || returning || restorePending) return;
            if (owner.SelectedCard != null && owner.SelectedCard.IsDragging) return;
            owner.Select(this);
        }

        /// <summary>マウスが離れたら選択を解除する。</summary>
        /// <param name="eventData">ポインター情報。</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!dragging && owner != null && owner.SelectedCard == this) owner.Select(null);
        }

        /// <summary>左ボタンのドラッグを開始して最前面へ移す。</summary>
        /// <param name="eventData">ポインター情報。</param>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (owner == null || !owner.Interactable || returning || dragging || restorePending || eventData.button != PointerEventData.InputButton.Left) return;
            if (owner.SelectedCard != null && owner.SelectedCard.IsDragging) return;
            owner.Select(this);
            dragging = true;
            dragPointer = eventData.pointerId;
            hoverTween?.Kill();
            visual.anchoredPosition = Vector2.zero;
            hitArea.raycastTarget = false;
            rect.SetParent(owner.DragLayer, true);
            rect.SetAsLastSibling();
            FollowPointer(eventData);
        }

        /// <summary>ドラッグ中のカードをカーソルへ追従させる。</summary>
        /// <param name="eventData">ポインター情報。</param>
        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || eventData.pointerId != dragPointer) return;
            if (!owner.Interactable) { CancelInteraction(); return; }
            FollowPointer(eventData);
        }

        /// <summary>ドラッグ用UIの平面へカーソル位置を変換する。</summary>
        /// <param name="eventData">ポインター情報。</param>
        private void FollowPointer(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(owner.DragLayer,
                eventData.position, eventData.pressEventCamera, out Vector3 position)) rect.position = position;
        }

        /// <summary>範囲内ならプレイし、それ以外は元の位置へ戻す。</summary>
        /// <param name="eventData">ポインター情報。</param>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging || eventData.pointerId != dragPointer) return;
            dragging = false;
            if (owner.TryPlay(this, eventData.position)) return;
            owner.Select(null);
            ReturnHome();
        }

        /// <summary>移動先の手札位置へアニメーションしながら戻す。</summary>
        private void ReturnHome()
        {
            returnTween?.Kill();
            rect.SetParent(homeParent, true);
            rect.SetAsLastSibling();
            returning = true;
            hitArea.raycastTarget = false;
            returnTween = DOTween.To(() => rect.anchoredPosition, value => rect.anchoredPosition = value,
                homePosition, returnDuration).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(FinishReturn);
        }

        /// <summary>復帰完了後に描画順と当たり判定を戻す。</summary>
        private void FinishReturn()
        {
            returning = false;
            hitArea.raycastTarget = true;
            rect.SetSiblingIndex(homeIndex);
        }

        /// <summary>操作を中止して即座に通常位置へ戻す。</summary>
        internal void CancelInteraction()
        {
            hoverTween?.Kill();
            returnTween?.Kill();
            dragging = false;
            returning = false;
            restorePending = false;
            if (rect == null) return;
            if (homeParent != null)
            {
                if (rect.parent != homeParent) rect.SetParent(homeParent, true);
                rect.anchoredPosition = homePosition;
                rect.SetSiblingIndex(homeIndex);
            }
            if (visual != null) visual.anchoredPosition = Vector2.zero;
            if (hitArea != null) hitArea.raycastTarget = true;
            if (artworkImage != null) artworkImage.enabled = true;
        }

        /// <summary>階層の有効状態が切り替わる間は再配置せず、表示と操作を停止する。</summary>
        internal void SuspendInteraction()
        {
            hoverTween?.Kill();
            returnTween?.Kill();
            dragging = false;
            returning = false;
            restorePending = true;
            if (hitArea != null) hitArea.raycastTarget = false;
            if (artworkImage != null) artworkImage.enabled = false;
        }

        /// <summary>有効状態の変更が完了したフレームで元の階層と位置へ復帰する。</summary>
        private void LateUpdate()
        {
            if (restorePending) CancelInteraction();
        }

        /// <summary>非表示時に進行中の操作を終了する。階層変更は次の更新へ遅延する。</summary>
        private void OnDisable()
        {
            SuspendInteraction();
            if (owner != null && owner.SelectedCard == this) owner.Select(null);
        }

        /// <summary>破棄時にアニメーションを停止する。</summary>
        private void OnDestroy()
        {
            hoverTween?.Kill();
            returnTween?.Kill();
        }

        /// <summary>フォーカスを失った際にドラッグを中止する。</summary>
        /// <param name="hasFocus">フォーカスがあるか。</param>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || !dragging) return;
            CancelInteraction();
            if (owner != null) owner.Select(null);
        }
    }
}
