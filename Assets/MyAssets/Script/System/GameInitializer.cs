using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class GameInitializer
{
	const string AdvEngineSceneName = "AdvEngineScene";
	const string AdvSpriteCameraName = "SpriteCamera";
	const string AdvUiCameraName = "UICamera";
	static Camera advBaseCamera;
	static Camera gameUiCamera;

	/// <summary>
	/// ゲーム開始時の処理
	/// ここ以外でRuntimeInitializeOnLoadMethodはつくらないこと
	/// </summary>
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void GameInitialize()
	{
		advBaseCamera = null;
		gameUiCamera = null;
		// Domain Reloadが無効でもイベントが重複登録されないようにする
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneLoaded += OnSceneLoaded;

		// 常駐オブジェクトの生成（他の初期化処理から利用できるよう先に生成）
		CreateGameInstance("Live2DTextureFactory");
		CreateGameInstance("GameInstance");
		CreateGameInstance("SystemCanvas");
	}

	/// <summary>
	/// 通常シーンのロード後に、ADVエンジン用シーンを追加ロードする
	/// </summary>
	static void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
	{
		if (scene.name == AdvEngineSceneName)
		{
			SetupAdvCameraStack();
			return;
		}

		// ADVシーンが既にロード済みの場合は、新しいMain Cameraへ積み直す
		if (SceneManager.GetSceneByName(AdvEngineSceneName).isLoaded)
			SetupAdvCameraStack();
		else
			LoadAdvEngineScene();
	}

	static void LoadAdvEngineScene()
	{
		Scene advEngineScene = SceneManager.GetSceneByName(AdvEngineSceneName);
		if (advEngineScene.isLoaded)
			return;

		SceneManager.LoadScene(
			AdvEngineSceneName,
			LoadSceneMode.Additive);
	}

	/// <summary>
	/// 現在のMain CameraにADV描画用カメラを追加する
	/// </summary>
	public static void RefreshAdvCameraStack()
	{
		SetupAdvCameraStack();
	}

	/// <summary>
	/// 現在のMain CameraにADV描画用カメラを接続する
	/// </summary>
	static void SetupAdvCameraStack()
	{
		Camera mainCamera = FindCamera(SceneManager.GetActiveScene(), "Main Camera", activeOnly: true);
		if (mainCamera == null) mainCamera = Camera.main;
		if (mainCamera == null)
		{
			// 管理専用シーンにはカメラがなく、追加シーンの読み込み後に接続する
			return;
		}

		Scene advEngineScene = SceneManager.GetSceneByName(AdvEngineSceneName);
		Camera spriteCamera = FindCamera(advEngineScene, AdvSpriteCameraName);
		Camera uiCamera = FindCamera(advEngineScene, AdvUiCameraName);

		if (spriteCamera == null || uiCamera == null)
		{
			Debug.LogError("ADV cameras not found.");
			return;
		}

		UniversalAdditionalCameraData mainCameraData =
			mainCamera.GetUniversalAdditionalCameraData();
		if (advBaseCamera != null && advBaseCamera != mainCamera)
		{
			var previousData = advBaseCamera.GetUniversalAdditionalCameraData();
			previousData.cameraStack.Remove(spriteCamera);
			previousData.cameraStack.Remove(uiCamera);
			if (gameUiCamera != null) previousData.cameraStack.Remove(gameUiCamera);
		}
		advBaseCamera = mainCamera;
		mainCameraData.renderType = CameraRenderType.Base;

		spriteCamera.GetUniversalAdditionalCameraData().renderType =
			CameraRenderType.Overlay;
		uiCamera.GetUniversalAdditionalCameraData().renderType =
			CameraRenderType.Overlay;

		// シーン固有のUIカメラの後に、ADVのSprite、UIの順で描画する
		mainCameraData.cameraStack.Remove(spriteCamera);
		mainCameraData.cameraStack.Remove(uiCamera);
		AdvUiModalController uiModal = AdvEngineController.Instance != null ? AdvEngineController.Instance.UiModal : null;
		if (uiModal != null)
		{
			gameUiCamera = uiModal.UiCamera;
			mainCameraData.cameraStack.Remove(gameUiCamera);
			mainCameraData.cameraStack.Add(gameUiCamera);
			uiModal.RefreshCanvases();
		}
		mainCameraData.cameraStack.Add(spriteCamera);
		mainCameraData.cameraStack.Add(uiCamera);
	}

	/// <summary>指定シーンから名前に一致するカメラを取得する。</summary>
	/// <param name="scene">検索対象のシーン</param>
	/// <param name="cameraName">取得するカメラ名</param>
	/// <param name="activeOnly">有効な描画先だけを検索する場合は真</param>
	static Camera FindCamera(Scene scene, string cameraName, bool activeOnly = false)
	{
		if (!scene.isLoaded)
			return null;

		foreach (GameObject rootObject in scene.GetRootGameObjects())
		{
			Camera[] cameras = rootObject.GetComponentsInChildren<Camera>(true);
			foreach (Camera camera in cameras)
			{
				if (camera.name == cameraName && (!activeOnly || camera.isActiveAndEnabled))
					return camera;
			}
		}

		return null;
	}

	/// <summary>
	/// 常駐オブジェクトを生成して初期化する
	/// </summary>
	/// <param name="path"></param>
	static void CreateGameInstance(string path)
	{
		GameObject prefab = Resources.Load<GameObject>(path);

		if (prefab == null)
		{
			Debug.LogError("Prefab not found in Resources!");
			return;
		}

		GameObject instance = Object.Instantiate(prefab);

		// 破棄されないよう設定
		Object.DontDestroyOnLoad(instance);

		// GameInstanceの初期化
		GameInstance gameInstance = instance.GetComponent<GameInstance>();
		if (gameInstance)
			gameInstance.Initialize();

		Live2DTextureFactory live2DFactory = instance.GetComponent<Live2DTextureFactory>();
		if (live2DFactory != null)
			live2DFactory.Initialize();
	}
}
