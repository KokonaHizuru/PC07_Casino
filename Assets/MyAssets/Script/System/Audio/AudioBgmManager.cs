using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class AudioBgmManager : MonoBehaviour
{
	List<AudioSource> audioSources = new List<AudioSource>();

	void Awake()
	{
		foreach (Transform child in transform)
		{
			AudioSource audioSource = child.GetComponent<AudioSource>();
			if (audioSource)
			{
				audioSources.Add(audioSource);
				audioSource.loop = true;
				audioSource.playOnAwake = false;
			}
		}
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{

	}

	/// <summary>
	/// 音の再生
	/// </summary>
	/// <param name="audioClip"></param>
	/// <param name="isLoop"></param>
	public void Play(AudioClip audioClip, bool isLoop = true)
	{
		StopAll();

		foreach (AudioSource child in audioSources)
		{
			if (child.isPlaying)
				continue;

			child.clip = audioClip;
			child.loop = isLoop;
			child.Play();
			break;
		}
	}

	/// <summary>
	/// 全ての音楽の停止
	/// </summary>
	public void StopAll()
	{
		foreach (AudioSource child in audioSources)
		{
			if (child != null &&  child.isPlaying)
			{
				child.Stop();
			}
		}
	}

	/// <summary>
	/// 全てのAudioSourceに音量設定
	/// </summary>
	/// <param name="volume"></param>
	public void SetVolume(float volume)
	{
		foreach (AudioSource child in audioSources)
		{
			child.volume = volume;
		}
	}

	/// <summary>
	/// BGMが再生中か確認
	/// </summary>
	/// <returns></returns>
	public bool IsPlaying()
	{
		foreach (AudioSource child in audioSources)
		{
			if (child == null)
				continue;

			if (child.isPlaying)
				return true;
		}

		return false;
	}
}
