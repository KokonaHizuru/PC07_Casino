using UnityEngine;

public class AudioManager : MonoBehaviour
{
	// 簡易アクセス用
	public static GameObject InstanceObject { get; private set; }
	public static AudioManager Instance { get; private set; }

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
	public AudioBgmManager bgmManager;

	[SerializeField]
	public AudioSeManager systemSeManager;

	[SerializeField]
	public AudioSeManager hSeManager;

	[SerializeField]
	public AudioSeManager seManager;

	float masterVolume = 1.0f;
	float bgmVolume = 1.0f;
	float systemSeVolume = 1.0f;
	float hSeVolume = 1.0f;
	float seVolume = 1.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
		SystemData data = GameInstance.Instance.systemData;
		SetMasterVolume(data.soundData.masterVolume);
		SetBgmVolume(data.soundData.bgmVolume);
		SetSystemSeVolume(data.soundData.systemSeVolume);
		SetHSeVolume(data.soundData.hSeVolume);
		SetSeVolume(data.soundData.seVolume);
	}

    // Update is called once per frame
    void Update()
    {
        
    }

	/// <summary>
	/// マスター音量の設定
	/// </summary>
	/// <param name="volume"></param>
	public void SetMasterVolume(float volume)
	{
		masterVolume = volume;

		bgmManager.SetVolume(masterVolume * bgmVolume);
		systemSeManager.SetVolume(masterVolume * systemSeVolume);
		hSeManager.SetVolume(masterVolume * hSeVolume);
		seManager.SetVolume(masterVolume * seVolume);
	}

	/// <summary>
	/// BGMの音量設定
	/// </summary>
	/// <param name="volume"></param>
	public void SetBgmVolume(float volume)
	{
		bgmVolume = volume;

		bgmManager.SetVolume(masterVolume * bgmVolume);
	}

	/// <summary>
	/// SEの音量設定
	/// </summary>
	/// <param name="volume"></param>
	public void SetSystemSeVolume(float volume)
	{
		systemSeVolume = volume;

		systemSeManager.SetVolume(masterVolume * systemSeVolume);
	}

	public void SetHSeVolume(float volume)
	{
		hSeVolume = volume;

		hSeManager.SetVolume(masterVolume * hSeVolume);
	}

	public void SetSeVolume(float volume)
	{
		seVolume = volume;

		seManager.SetVolume(masterVolume * seVolume);
	}

	/// <summary>
	/// 音量の取得
	/// </summary>
	/// <returns></returns>
	public float GetMasterVolume()
	{
		return masterVolume;
	}
	public float GetSeVolume()
	{
		return seVolume * masterVolume;
	}
}
