using DG.Tweening;
using UnityEngine;
using System;
using UnityEngine.UI;

public class FadeManager : MonoBehaviour
{
	// 簡易アクセス用
	public static GameObject InstanceObject { get; private set; }
	public static FadeManager Instance { get; private set; }


	/// <summary>
	/// 初期化
	/// </summary>
	void Awake()
	{
		bool isDuplicateInstance = Instance != null && Instance != this;
		if (isDuplicateInstance)
		{
			Destroy(gameObject);
			return;
		}

		InstanceObject = gameObject;
		Instance = this;
	}

	[SerializeField] private Image image;
	[SerializeField] private CanvasGroup canvasGroup;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		canvasGroup.blocksRaycasts = false;
		canvasGroup.alpha = 0.0f;
	}

	/// <summary>
	/// 色の設定
	/// </summary>
	/// <param name="color"></param>
	public void SetColor(Color color)
	{
		image.color = color;
	}


	/// <summary>
	/// フェードアウト
	/// </summary>
	/// <param name="onComplete"></param>
	/// <param name="IsAutoFadeIn"></param>
	/// <param name="autoFadeDelay"></param>
	public void FadeOut(Action onComplete = null, bool IsAutoFadeIn = true, float autoFadeDelay = 0.0f, float duration = 0.3f)
	{
		canvasGroup.blocksRaycasts = true;

		canvasGroup
			.DOFade(1f, duration)
			.SetUpdate(true)
			.OnComplete(() =>
			{
				Debug.Log("フェードアウト完了");
				onComplete?.Invoke();

				if (IsAutoFadeIn)
				{
					DOVirtual.DelayedCall(autoFadeDelay, () =>
					{
						FadeIn();
					}
					);

				}
			}
		);
	}

	/// <summary>
	/// フェードイン
	/// </summary>
	/// <param name="onComplete"></param>
	public void FadeIn(Action onComplete = null, float duration = 0.3f)
	{
		canvasGroup.blocksRaycasts = false;

		canvasGroup
			.DOFade(0f, duration)
			.SetUpdate(true)
			.OnComplete(() =>
			{
				Debug.Log("フェードイン完了！");
				onComplete?.Invoke();
			}
		);
	}
}
