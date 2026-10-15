using DG.Tweening;
using TMPro;
using UnityEngine;

public class TMPAutoScroll : MonoBehaviour
{
	[SerializeField]
	RectTransform viewport;

	[SerializeField]
	RectTransform textRect;

	[SerializeField]
	TextMeshProUGUI textMesh;

	[SerializeField]
	float scrollSpeed = 100f;

	[SerializeField]
	float startWait = 1f;

	[SerializeField]
	float endWait = 1f;

	[SerializeField]
	bool loop;

	Tween scrollTween;

	void Start()
	{
		SetText(textMesh.text);
	}

	void OnDisable()
	{
		scrollTween?.Kill();
	}

	public void SetText(string message)
	{
		scrollTween?.Kill();

		textMesh.text = message;

		Canvas.ForceUpdateCanvases();

		ResetPosition();

		float textWidth = textMesh.preferredWidth;
		float viewWidth = viewport.rect.width;

		// 入り切るならスクロールしない
		if (textWidth <= viewWidth)
			return;

		float moveAmount = textWidth - viewWidth;

		float duration = moveAmount / scrollSpeed;

		if (loop)
		{
			Sequence seq = DOTween.Sequence();

			seq.AppendInterval(startWait);

			seq.Append(
				textRect.DOAnchorPosX(-moveAmount, duration)
					.SetEase(Ease.Linear)
			);

			seq.AppendInterval(endWait);

			seq.AppendCallback(ResetPosition);

			seq.SetLoops(-1);

			scrollTween = seq;
		}
		else
		{
			scrollTween =
				textRect.DOAnchorPosX(-moveAmount, duration)
					.SetEase(Ease.Linear)
					.SetDelay(startWait);
		}
	}

	void ResetPosition()
	{
		textRect.anchoredPosition = Vector2.zero;
	}
}