using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Utage;

public class UIInfoWindow : MonoBehaviour
{
	// 簡易アクセス用
	public static GameObject InstanceObject { get; private set; }
	public static UIInfoWindow Instance { get; private set; }

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
        modalTransforms = GetComponentsInChildren<Transform>(true);
        originalLayers = new int[modalTransforms.Length];
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
	float fadeTime = 0.2f;

	Tween fadeTween;
    public event System.Action Closed;
    Transform[] modalTransforms;
    int[] originalLayers;
    Transform originalParent;
    int originalSibling;
    bool isMenuMessage;

    /// <summary>メニューの手前に案内を表示する。</summary>
    /// <param name="message">表示する案内</param>
    /// <param name="displayParent">メニューの表示階層</param>
    public void ShowMessage(string message, Transform displayParent)
    {
        if (!isMenuMessage)
        {
            originalParent = transform.parent;
            originalSibling = transform.GetSiblingIndex();
            for (int i = 0; i < modalTransforms.Length; i++)
            {
                originalLayers[i] = modalTransforms[i].gameObject.layer;
                modalTransforms[i].gameObject.layer = displayParent.gameObject.layer;
            }
            transform.SetParent(displayParent, false);
            transform.SetAsLastSibling();
            isMenuMessage = true;
        }
        SetText(message);
        OpenWidnow();
    }

    /// <summary>メニュー破棄時に案内を閉じ、共有ウィンドウを元の階層へ戻す。</summary>
    /// <param name="displayParent">終了するメニューの表示階層</param>
    public void CancelMessage(Transform displayParent)
    {
        if (!isMenuMessage || transform.parent != displayParent) return;
        fadeTween?.Kill();
        panelParent.SetActive(false);
        RestoreMessage();
    }

    /// <summary>案内の親階層と描画レイヤーを復元する。</summary>
    void RestoreMessage()
    {
        if (!isMenuMessage) return;
        transform.SetParent(originalParent, false);
        transform.SetSiblingIndex(originalSibling);
        for (int i = 0; i < modalTransforms.Length; i++)
            if (modalTransforms[i] != null) modalTransforms[i].gameObject.layer = originalLayers[i];
        isMenuMessage = false;
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
		panelParent.SetActive(true);
		canvasGroup.alpha = 0.0f;

		guardPanel.SetActive(true);

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
	/// <param name="isCallEvent"></param>
	public void OnClickOk()
	{
		OnOk?.Invoke();

		OnCloseWindow();
	}

	/// <summary>
	/// 閉じるボタンをクリックした時の処理
	/// </summary>
	/// <param name="isCallEvent"></param>
	public void OnClickCancel()
	{
		OnCancel?.Invoke();

		OnCloseWindow();
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
	void OnCloseWindow()
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
                RestoreMessage();
                Closed?.Invoke();
			});
	}
}
