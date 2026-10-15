using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>子UI上のドラッグとホイール操作を親のScrollRectへ転送する。</summary>
public class ScrollEventRelay : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [SerializeField] float dragCancelDistance = 10f;
    [SerializeField] float wheelScrollMultiplier = 3f;
    ScrollRect scrollRect;
    Vector2 pointerDownPos;
    public bool WasDragged { get; private set; }

    /// <summary>スクロール先を一度取得して保持する。</summary>
    void Awake() => scrollRect = GetComponentInParent<ScrollRect>();

    /// <summary>押下位置を記録してスクロールの慣性を止める。</summary>
    /// <param name="eventData">押下情報</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        pointerDownPos = eventData.position;
        WasDragged = false;
        scrollRect?.StopMovement();
    }

    /// <summary>移動を伴った操作をクリックとして扱わないようにする。</summary>
    /// <param name="eventData">解放情報</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        WasDragged |= (pointerDownPos - eventData.position).sqrMagnitude >= dragCancelDistance * dragCancelDistance;
        if (WasDragged) eventData.eligibleForClick = false;
    }

    /// <summary>親のスクロール開始準備を行う。</summary>
    /// <param name="eventData">ドラッグ準備情報</param>
    public void OnInitializePotentialDrag(PointerEventData eventData)
        => scrollRect?.OnInitializePotentialDrag(eventData);

    /// <summary>ドラッグをクリックから分離し、親のスクロールを開始する。</summary>
    /// <param name="eventData">ドラッグ開始情報</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        WasDragged = true;
        eventData.eligibleForClick = false;
        eventData.clickCount = 0;
        scrollRect?.OnBeginDrag(eventData);
    }

    /// <summary>親へドラッグの移動量を転送する。</summary>
    /// <param name="eventData">移動情報</param>
    public void OnDrag(PointerEventData eventData) => scrollRect?.OnDrag(eventData);

    /// <summary>親のスクロールドラッグを終了する。</summary>
    /// <param name="eventData">ドラッグ終了情報</param>
    public void OnEndDrag(PointerEventData eventData) => scrollRect?.OnEndDrag(eventData);

    /// <summary>親へ倍率を適用したホイール操作を転送する。</summary>
    /// <param name="eventData">ホイール情報</param>
    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRect == null) return;
        var original = eventData.scrollDelta;
        eventData.scrollDelta *= wheelScrollMultiplier;
        scrollRect.OnScroll(eventData);
        eventData.scrollDelta = original;
    }
}
