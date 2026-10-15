using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class UiSelectWindow : MonoBehaviour
{
	// 簡易アクセス用
	public static GameObject InstanceObject { get; private set; }
	public static UiSelectWindow Instance { get; private set; }

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
        // 入力ガードは描画せず、フェード中のクリック遮断だけを行う。
        CanvasGroup guardGroup = guardPanel.GetComponent<CanvasGroup>();
        if (guardGroup == null) guardGroup = guardPanel.AddComponent<CanvasGroup>();
        guardGroup.alpha = 0f;
        guardGroup.blocksRaycasts = true;
        guardGroup.interactable = true;
        guardGroup.ignoreParentGroups = false;
	}

	[SerializeField]
	GameObject panelParent;

	[SerializeField]
	CanvasGroup canvasGroup;

	[SerializeField]
	GameObject guardPanel;

	[SerializeField]
	TextMeshProUGUI textMeshProUGUI;

	[SerializeField]
	public UnityEvent OnOk;

	[SerializeField]
	public UnityEvent OnCancel;

	[SerializeField]
	public bool isWindowCloseOk;

	[SerializeField]
	float fadeTime = 0.2f;

	Tween fadeTween;
    Action<bool> confirmation;
    Transform confirmationOwner;
    bool isConfirming;

    /// <summary>既存の親階層のまま確認を表示し、閉じた後に選択結果を通知する。</summary>
    /// <param name="text">確認文</param>
    /// <param name="displayParent">確認を要求した画面（終了時の照合用）</param>
    /// <param name="onDecision">はいの場合true、いいえの場合falseを受け取る処理</param>
    public bool ShowConfirmation(string text, Transform displayParent, Action<bool> onDecision)
    {
        if (isConfirming || panelParent.activeSelf) return false;
        isConfirming = true;
        confirmation = onDecision;
        confirmationOwner = displayParent;
        SetText(text);
        OpenWidnow();
        return true;
    }

    /// <summary>確認ウィンドウを閉じ、選択結果を通知する。</summary>
    /// <param name="accepted">はいが選ばれた場合はtrue</param>
    void CompleteConfirmation(bool accepted)
    {
        if (confirmation == null) return;
        Action<bool> callback = confirmation;
        confirmation = null;
        CloseWindow();
        fadeTween.OnComplete(() =>
        {
            ResetConfirmation();
            callback(accepted);
        });
    }

    /// <summary>要求元の画面が終了した際に、その画面の確認を破棄する。</summary>
    /// <param name="displayParent">終了する要求元の画面</param>
    public void CancelConfirmation(Transform displayParent)
    {
        if (!isConfirming || confirmationOwner != displayParent) return;
        fadeTween?.Kill();
        confirmation = null;
        ResetConfirmation();
    }

    /// <summary>確認表示を閉じて要求元と確認状態を解除する。</summary>
    void ResetConfirmation()
    {
        panelParent.SetActive(false);
        confirmationOwner = null;
        isConfirming = false;
    }

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		panelParent.SetActive(false);
		canvasGroup.alpha = 0.0f;
	}

    // Update is called once per frame
    void Update()
    {
        
    }

	/// <summary>
	/// ウィンドウを開く
	/// </summary>
	public void OpenWidnow()
	{
        fadeTween?.Kill();
		canvasGroup.alpha = 0.0f;
		guardPanel.SetActive(true);
		panelParent.SetActive(true);

		fadeTween = canvasGroup
			.DOFade(1f, fadeTime)
			.SetUpdate(true)
			.SetEase(Ease.OutQuad)
			.OnComplete(() =>
			{
				guardPanel.SetActive(false);
			});
	}

	/// <summary>
	/// 「はい」を選択した時の処理
	/// </summary>
	public void OnClickOk()
	{
        if (isConfirming) { CompleteConfirmation(true); return; }
		OnOk?.Invoke();

		if (isWindowCloseOk)
			CloseWindow();
	}

	/// <summary>
	/// 閉じるボタンをクリックした時の処理
	/// </summary>
	public void OnClickCancel()
	{
        if (isConfirming) { CompleteConfirmation(false); return; }
		OnCancel?.Invoke();

		CloseWindow();
	}

	/// <summary>
	/// テキストの設定
	/// </summary>
	/// <param name="text"></param>
	public void SetText(string text)
	{
		textMeshProUGUI.text = text;
	}

	/// <summary>
	/// ウィンドウを閉じる処理
	/// </summary>
	public void CloseWindow()
	{
		fadeTween?.Kill();

		guardPanel.SetActive(true);

		fadeTween = canvasGroup
			.DOFade(0f, fadeTime)
			.SetUpdate(true)
			.SetEase(Ease.InQuad)
			.OnComplete(() =>
			{
				panelParent.SetActive(false);
			});
	}
}
