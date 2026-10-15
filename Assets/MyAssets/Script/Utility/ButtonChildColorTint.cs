using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonChildColorTint : MonoBehaviour,
	IPointerEnterHandler,
	IPointerExitHandler,
	IPointerDownHandler,
	IPointerUpHandler,
	ISelectHandler,
	IDeselectHandler
{
	public enum FocusMode
	{
		MouseOnly,
		KeyboardAndGamepad
	}

	[SerializeField] FocusMode focusMode = FocusMode.MouseOnly;
	[SerializeField] Color normalColor = Color.white;
	[SerializeField] Color highlightedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
	[SerializeField] Color pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
	[SerializeField] Color disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);

	Button button;
	Graphic[] graphics;
	Color[] baseColors;
	bool isPointerOver;
	bool isPointerDown;
	bool isSelected;
	bool previousInteractable;

	void Awake()
	{
		button = GetComponent<Button>();
		graphics = GetComponentsInChildren<Graphic>(true);
		baseColors = new Color[graphics.Length];

		for (int i = 0; i < graphics.Length; i++)
			baseColors[i] = graphics[i].color;

		previousInteractable = button.interactable;
		RefreshColor();
	}

	void Update()
	{
		if (previousInteractable == button.interactable)
			return;

		previousInteractable = button.interactable;
		RefreshColor();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		isPointerOver = true;
		RefreshColor();
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		isPointerOver = false;
		isPointerDown = false;
		RefreshColor();
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		isPointerDown = true;
		RefreshColor();
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		isPointerDown = false;

		if (focusMode == FocusMode.MouseOnly &&
			EventSystem.current != null &&
			EventSystem.current.currentSelectedGameObject == gameObject)
		{
			EventSystem.current.SetSelectedGameObject(null);
		}

		RefreshColor();
	}

	public void OnSelect(BaseEventData eventData)
	{
		isSelected = true;
		RefreshColor();
	}

	public void OnDeselect(BaseEventData eventData)
	{
		isSelected = false;
		RefreshColor();
	}

	void RefreshColor()
	{
		Color tint;
		if (!button.interactable)
			tint = disabledColor;
		else if (isPointerDown)
			tint = pressedColor;
		else if (isPointerOver ||
			(focusMode == FocusMode.KeyboardAndGamepad && isSelected))
			tint = highlightedColor;
		else
			tint = normalColor;

		for (int i = 0; i < graphics.Length; i++)
		{
			Color baseColor = baseColors[i];
			graphics[i].color = new Color(
				baseColor.r * tint.r,
				baseColor.g * tint.g,
				baseColor.b * tint.b,
				baseColor.a * tint.a);
		}
	}
}
