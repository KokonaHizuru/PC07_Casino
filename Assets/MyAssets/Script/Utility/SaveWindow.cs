using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveWindow : MonoBehaviour
{
	// 簡易アクセス用
	public static GameObject InstanceObject { get; private set; }
	public static SaveWindow Instance { get; private set; }

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


	// ウィンドウを開く際に設定する種類
	public enum ESaveWindowType
	{
		Save,
		Load,
		Load_Title,
	}

	[SerializeField]
	UiSelectWindow selectWindow;

	[SerializeField]
	UIInfoWindow infoWindow;

	[SerializeField]
	TextMeshProUGUI titleText;

	[SerializeField]
	GameObject saveButtonObject;

	[SerializeField]
	GameObject blockPanel;

	GameObject parentObject;
	CanvasGroup canvasGroup;

	List<SaveButton> saveButtons = new List<SaveButton>();

	ESaveWindowType saveType;

	Tween fadeTween;
	const float fadeTime = 0.2f;

	int clickSlot;

	public Action onClose;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		parentObject = transform.GetChild(0).gameObject;
		canvasGroup = GetComponent<CanvasGroup>();

		// スロットボタンの取得と初期化
		foreach (Transform child in saveButtonObject.transform)
		{
			SaveButton button = child.GetComponent<SaveButton>();
			if (button == null)
				continue;

			saveButtons.Add(button);

			button.OnClickButton += OnClickButton;
		}

		// その他初期化
		selectWindow.OnOk.AddListener(OnClickOk);

		// 非表示処理
		canvasGroup.alpha = 0.0f;
		parentObject.SetActive(false);
	}

    // Update is called once per frame
    void Update()
    {
        
    }

	/// <summary>
	/// セーブボタンクリック時の処理
	/// </summary>
	/// <param name="slot"></param>
	public void OnClickButton(int slot)
	{
		clickSlot = slot;

		// セーブ時の処理
		if (saveType == ESaveWindowType.Save)
		{
			if (SaveManager.HasSaveData(clickSlot))
			{
				selectWindow.SetText("スロット" + clickSlot + "にはデータが存在します。\r\n上書き保存しますか？");
			}
			else
			{
				selectWindow.SetText("スロット" + clickSlot + "にセーブしますか？");
			}

			selectWindow.isWindowCloseOk = true;
			selectWindow.OpenWidnow();
		}
		// ロード時の処理
		else
		{
			if (SaveManager.HasSaveData(clickSlot))
			{
				if (saveType == ESaveWindowType.Load)
					selectWindow.SetText("スロット" + clickSlot + "をロードします。\r\n保存前の情報は破棄されますが\r\nよろしいですか？");
				else
					selectWindow.SetText("スロット" + clickSlot + "をロードします。\r\nよろしいですか？");

				selectWindow.isWindowCloseOk = true;
				selectWindow.OpenWidnow();
			}
			else
			{
				infoWindow.SetText("セーブデータがありません");
				infoWindow.OpenWidnow();
			}
		}
	}

	/// <summary>
	/// ウィンドウを開く処理
	/// </summary>
	/// <param name="type"></param>
	public void Open(ESaveWindowType type)
	{
		// ボタン情報の初期化
		RefreshSaveButtons();

		// セーブモード毎の処理
		if (type == ESaveWindowType.Save)
		{
			titleText.text = "セーブ";
		}
		else
		{
			titleText.text = "ロード";
		}

		saveType = type;

		parentObject.SetActive(true);
		blockPanel.SetActive(true);

		// フェードイン
		canvasGroup.alpha = 0.0f;
		fadeTween?.Kill();
		fadeTween = canvasGroup
			.DOFade(1.0f, fadeTime)
			.OnComplete(() => 
			{
				blockPanel.SetActive(false);
			});
	}

	/// <summary>
	/// 戻るボタンクリック時の処理
	/// </summary>
	public void OnClickRerutn()
	{
		blockPanel.SetActive(true);

		fadeTween?.Kill();
		fadeTween = canvasGroup
			.DOFade(0.0f, fadeTime)
			.OnComplete(() =>
			{
				onClose?.Invoke();
				parentObject.SetActive(false);
				blockPanel.SetActive(false);
			});
	}

	/// <summary>
	/// 「はい」を選択した時の処理
	/// </summary>
	public void OnClickOk()
	{
		if (saveType == ESaveWindowType.Save)
		{
			SaveManager.Save(
				clickSlot, 
				GameInstance.Instance.playerData,
				System.DateTime.Now.ToString("yyyy/MM/dd HH:mm")
			);

			RefreshSaveButtons();

			onClose?.Invoke();
		}
		else
		{
			FadeManager.Instance.FadeOut(
				() =>
				{
					AudioManager.Instance.bgmManager.StopAll();
					GameInstance.Instance.playerData = SaveManager.Load(clickSlot);
					SceneManager.LoadScene("LobbyScene");
				},
				true,
				1.0f
			);
		}
	}

	/// <summary>
	/// ボタン情報のリフレッシュ
	/// </summary>
	public void RefreshSaveButtons()
	{
		foreach (SaveButton button in saveButtons)
		{
			button?.Refresh();
		}
	}
}
