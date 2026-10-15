using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UISettingSound : MonoBehaviour
{
	List<UISettingVolumeSlider> volumeSliders;


    void Awake()
    {
		volumeSliders = GetComponentsInChildren<UISettingVolumeSlider>(true).ToList();
	}

	public void Refresh()
	{
		foreach (var volumeSlider in volumeSliders)
		{
			volumeSlider.Refresh();
		}
	}
}
