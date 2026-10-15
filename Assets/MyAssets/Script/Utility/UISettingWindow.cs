using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class UISettingWindow : MonoBehaviour
{
	// 簡易アクセス用
	public static GameObject InstanceObject { get; private set; }
	public static UISettingWindow Instance { get; private set; }


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


	[SerializeField]
	CanvasGroup guardCanvasGroup;

	[SerializeField]
	CanvasGroup backCanvasGroup;

	[SerializeField]
	UISettingSound uiSettingSound;

	CanvasGroup canvasGroup;

	public float fadeTime = 0.2f;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		canvasGroup = GetComponent<CanvasGroup>();

		gameObject.SetActive(false);
		guardCanvasGroup.gameObject.SetActive(false);
		backCanvasGroup.gameObject.SetActive(false);
	}

	/// <summary>
	/// ウィンドウを開く
	/// </summary>
	public void Open()
	{
		// 入力防止パネル有効化
		guardCanvasGroup.gameObject.SetActive(true);

		// 背景のフェードイン
		backCanvasGroup.gameObject.SetActive(true);
		backCanvasGroup.alpha = 0.0f;
		backCanvasGroup.DOFade(1.0f, fadeTime);

		// システムウィンドウフェードイン
		gameObject.SetActive(true);
		canvasGroup.alpha = 0.0f;
		canvasGroup
			.DOFade(1.0f, fadeTime)
			.OnComplete(
				()=>
				{
					// 入力防止パネル無効化
					guardCanvasGroup.gameObject.SetActive(false);
				}
			);

		// 子の要素の初期化
		uiSettingSound.Refresh();
	}

	/// <summary>
	/// ウィンドウを閉じる
	/// </summary>
	public void Close()
	{
		// システムデータのセーブ
		SaveManager.SaveSystem(GameInstance.Instance.systemData);

		// 入力防止パネル有効化
		guardCanvasGroup.gameObject.SetActive(true);

		// 背景のフェードイン
		backCanvasGroup.DOFade(0.0f, fadeTime);

		// システムウィンドウフェードイン
		canvasGroup
			.DOFade(0.0f, fadeTime)
			.OnComplete(
				() =>
				{
					// 各UIの無効化
					guardCanvasGroup.gameObject.SetActive(false);
					backCanvasGroup.gameObject.SetActive(false);
					gameObject.SetActive(false);
				}
			);
	}
}
