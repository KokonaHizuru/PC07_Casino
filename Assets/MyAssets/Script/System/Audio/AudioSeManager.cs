using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

public class AudioSeManager : MonoBehaviour
{
	List<AudioSource> audioSources = new List<AudioSource>();
	readonly Dictionary<AudioSource, object> owners = new();

	void Awake()
	{
		foreach (Transform child in transform)
		{
			AudioSource audioSource = child.GetComponent<AudioSource>();
			if (audioSource)
			{
				audioSources.Add(audioSource);
				audioSource.loop = false;
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
	public void PlayOneShot(AudioClip audioClip)
	{
		PlayOneShot(audioClip, null);
	}

	/// <summary>所有者を記録してSEを再生する</summary>
	/// <param name="audioClip">再生音声</param>
	/// <param name="owner">終了時に停止する単位。通常のSEはnull</param>
	public void PlayOneShot(AudioClip audioClip, object owner)
	{
		if (audioClip == null) return;
		foreach (AudioSource child in audioSources)
		{
			if (child.isPlaying)
				continue;

			child.loop = false;
			child.clip = null;
			owners[child] = owner;
			child.PlayOneShot(audioClip);
			break;
		}
	}

	public AudioSource PlayLoop(AudioClip audioClip)
	{
		foreach (AudioSource child in audioSources)
		{
			if (child.isPlaying)
				continue;

			child.loop = true;
			owners.Remove(child);
			child.clip = audioClip;
			child.Play();
			return child;
		}

		return null;
	}

	/// <summary>指定所有者が再生した音声だけを停止して参照を外す</summary>
	/// <param name="owner">再生時に指定した所有者</param>
	public void StopOwned(object owner)
	{
		if (owner == null) return;
		foreach (var source in audioSources)
		{
			if (!owners.TryGetValue(source, out var current) || !ReferenceEquals(current, owner)) continue;
			source.Stop();
			source.clip = null;
			owners.Remove(source);
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
}
