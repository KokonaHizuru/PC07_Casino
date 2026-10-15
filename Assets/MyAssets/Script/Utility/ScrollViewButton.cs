using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utage;

public class ScrollViewButton : Button,
	IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
	[SerializeField] private float clickCancelDistance = 10f;
	[SerializeField] private float wheelScrollMultiplier = 3f;

	[Header("Sound")]
	[SerializeField] private AudioClip hoverSE;
	[SerializeField] private AudioClip clickSE;
	[SerializeField] private float hoverSEInterval = 0.08f;
	[SerializeField] private float clickSEInterval = 0.08f;

	private static ScrollViewButton lastHoverButton;
	private static float lastHoverSETime;
	private static float lastClickSETime;

	private ScrollRect scrollRect;
	private RectTransform viewportRect;

	private Vector2 pointerDownPosition;
	private bool isDragging;

	protected override void Awake()
	{
		base.Awake();

		scrollRect = GetComponentInParent<ScrollRect>();

		if (scrollRect != null)
		{
			viewportRect = scrollRect.viewport != null
				? scrollRect.viewport
				: scrollRect.GetComponent<RectTransform>();
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();

		if (lastHoverButton == this)
			lastHoverButton = null;
	}

	public override void OnPointerEnter(PointerEventData eventData)
	{
		base.OnPointerEnter(eventData);

		if (!IsActive() || !interactable)
			return;

		if (!IsPointerInsideViewport(eventData))
			return;

		if (lastHoverButton == this)
			return;

		if (Time.unscaledTime - lastHoverSETime < hoverSEInterval)
			return;

		lastHoverButton = this;
		lastHoverSETime = Time.unscaledTime;

		PlaySE(hoverSE);
	}

	public override void OnPointerExit(PointerEventData eventData)
	{
		base.OnPointerExit(eventData);

		if (lastHoverButton == this)
			lastHoverButton = null;
	}

	public override void OnPointerDown(PointerEventData eventData)
	{
		StopScrollMovement();

		base.OnPointerDown(eventData);

		pointerDownPosition = eventData.position;
		isDragging = false;
	}

	public override void OnPointerClick(PointerEventData eventData)
	{
		// Scroll View 内でのみ、スクロール操作後の誤クリックを防ぐ。
		// Scroll View 外では通常の Button と同じクリック判定にする。
		if (scrollRect != null)
		{
			float distance = Vector2.Distance(pointerDownPosition, eventData.position);

			if (distance >= clickCancelDistance || isDragging)
				return;
		}

		if (Time.unscaledTime - lastClickSETime >= clickSEInterval)
		{
			lastClickSETime = Time.unscaledTime;

			if (interactable)
				PlaySE(clickSE);
		}

		base.OnPointerClick(eventData);
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		isDragging = true;

		StopScrollMovement();
		scrollRect?.OnBeginDrag(eventData);
	}

	public void OnDrag(PointerEventData eventData)
	{
		scrollRect?.OnDrag(eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		scrollRect?.OnEndDrag(eventData);
	}

	public void OnScroll(PointerEventData eventData)
	{
		if (scrollRect == null)
			return;

		Vector2 original = eventData.scrollDelta;
		eventData.scrollDelta *= wheelScrollMultiplier;

		scrollRect.OnScroll(eventData);

		eventData.scrollDelta = original;
	}

	private bool IsPointerInsideViewport(PointerEventData eventData)
	{
		if (viewportRect == null)
			return true;

		Camera eventCamera = eventData.enterEventCamera != null
			? eventData.enterEventCamera
			: eventData.pressEventCamera;

		return RectTransformUtility.RectangleContainsScreenPoint(
			viewportRect,
			eventData.position,
			eventCamera
		);
	}

	private void StopScrollMovement()
	{
		if (scrollRect == null)
			return;

		scrollRect.StopMovement();
		scrollRect.velocity = Vector2.zero;
	}

	private void PlaySE(AudioClip clip)
	{
		if (clip == null)
			return;

		AudioManager.Instance.systemSeManager.PlayOneShot(clip);
	}
}
