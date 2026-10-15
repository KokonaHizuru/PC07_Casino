using System;
using TMPro;
using UnityEngine;

public class SaveButton : MonoBehaviour
{
	[SerializeField]
	TextMeshProUGUI saveText;

	[SerializeField]
	int saveSlot;

	public Action<int> OnClickButton;

    /// <summary>保存スロットの表示を初期化する。</summary>
    void Start()
    {
		Refresh();
	}

    /// <summary>選択したスロット番号を通知する。</summary>
	public void OnClick()
	{
		OnClickButton?.Invoke(saveSlot);
	}

    /// <summary>保存日時または空きスロットの表示を更新する。</summary>
	public void Refresh()
	{
		if (SaveManager.HasSaveData(saveSlot))
		{
			PlayerDataSummary summary = SaveManager.LoadSummary(saveSlot);
			saveText.text = "スロット" + saveSlot + "\n" + summary.saveTime;
		}
		else
		{
			saveText.text = "空きスロット" + saveSlot;
		}
	}
}
