using Unity.Burst.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

public class GameInstance : MonoBehaviour
{
	// 簡易アクセス用
	public static GameObject InstanceObject { get; private set; }
	public static GameInstance Instance { get; private set; }

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

	public PlayerData playerData = new();
	public Casino.Story.CasinoStorySession casinoStory = new();
	public SystemData systemData = new();

	public void Initialize()
	{
		playerData.Reset();
		casinoStory.Reset();

		// システムデータのロードor作成
		if (SaveManager.HasSystemSaveData())
		{
			systemData = SaveManager.LoadSystem();
		}
		else
		{
			systemData.Reset();
			SaveManager.SaveSystem(systemData);
		}

		// 画面解像度の変更
		FullScreenMode mode = FullScreenMode.Windowed;

		if (GameInstance.Instance.systemData.windowData.isFullScreen)
			mode = FullScreenMode.FullScreenWindow;

		Screen.SetResolution(
			GameInstance.Instance.systemData.windowData.windowHight,
			GameInstance.Instance.systemData.windowData.windowWidth, 
			mode
		);

		// デバッグ機能の実行
		StartDebug();
	}

	void StartDebug()
	{
#if UNITY_EDITOR

#endif
	}
}
