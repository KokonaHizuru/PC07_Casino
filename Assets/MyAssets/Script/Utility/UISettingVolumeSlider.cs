using UnityEngine;
using UnityEngine.UI;

public class UISettingVolumeSlider : MonoBehaviour
{
	public enum EVolumeType
	{
		Master,
		Bgm,
		System,
		H,
		Se,
	}

	[SerializeField]
	EVolumeType volumeType;

	Slider volumeSlider;

	bool isStart = false;

	void Awake()
    {
		volumeSlider = GetComponentInChildren<Slider>(true);
		Refresh();
	}

	void Start()
	{
		isStart = true;
	}

	/// <summary>
	/// 初期化
	/// </summary>
	public void Refresh()
	{
		switch(volumeType)
		{
			case EVolumeType.Master:
				volumeSlider.value = GameInstance.Instance.systemData.soundData.masterVolume;
				break;
			case EVolumeType.Bgm:
				volumeSlider.value = GameInstance.Instance.systemData.soundData.bgmVolume;
				break;
			case EVolumeType.System:
				volumeSlider.value = GameInstance.Instance.systemData.soundData.systemSeVolume;
				break;
			case EVolumeType.H:
				volumeSlider.value = GameInstance.Instance.systemData.soundData.hSeVolume;
				break;
			case EVolumeType.Se:
				volumeSlider.value = GameInstance.Instance.systemData.soundData.seVolume;
				break;
		}
	}

	/// <summary>
	/// 音量調整
	/// </summary>
	public void OnChangeVolume()
	{
		if (!isStart)
			return;

		float value = volumeSlider.value;

		switch (volumeType)
		{
			case EVolumeType.Master:
				GameInstance.Instance.systemData.soundData.masterVolume = value;
				AudioManager.Instance.SetMasterVolume(value);
				break;
			case EVolumeType.Bgm:
				GameInstance.Instance.systemData.soundData.bgmVolume = value;
				AudioManager.Instance.SetBgmVolume(value);
				break;
			case EVolumeType.System:
				GameInstance.Instance.systemData.soundData.systemSeVolume = value;
				AudioManager.Instance.SetSystemSeVolume(value);
				break;
			case EVolumeType.H:
				GameInstance.Instance.systemData.soundData.hSeVolume = value;
				AudioManager.Instance.SetHSeVolume(value);
				break;
			case EVolumeType.Se:
				GameInstance.Instance.systemData.soundData.seVolume = value;
				AudioManager.Instance.SetSeVolume(value);
				break;
		}
	}
}
