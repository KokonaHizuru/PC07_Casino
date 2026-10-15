using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HorizontalWheelScroll : ScrollRect
{
	public float wheelScrollSpeed = 100f;

	public override void OnScroll(PointerEventData data)
	{
		if (!horizontal)
			return;

		float delta = data.scrollDelta.y;

		horizontalNormalizedPosition -=
			delta * wheelScrollSpeed / content.rect.width;
	}
}