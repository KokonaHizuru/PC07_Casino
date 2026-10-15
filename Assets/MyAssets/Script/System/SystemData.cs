using System;
using UnityEngine;

[Serializable]
public class SystemData
{
	public SystemSoundData soundData = new SystemSoundData();
	public SystemWindowData windowData = new SystemWindowData();

	public FlagData flagData = new FlagData();

	public void Reset()
	{
		soundData.Reset();
		windowData.Reset();

		flagData.Clear();
	}
}

[Serializable]
public class SystemSoundData
{
	public float masterVolume;
	public float bgmVolume;
	public float systemSeVolume;
	public float hSeVolume;
	public float seVolume;

	public void Reset()
	{
		masterVolume = 1.0f;
		bgmVolume = 1.0f;
		systemSeVolume = 1.0f;
		hSeVolume = 1.0f;
		seVolume = 1.0f;
	}
}

[Serializable]
public class SystemWindowData
{
	public int windowHight;
	public int windowWidth;
	public bool isFullScreen;

	public void Reset()
	{
		windowHight = 1600;
		windowWidth = 900;
		isFullScreen = false;
	}
}