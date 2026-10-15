using UnityEngine;

public class UISettingResolutionButton : MonoBehaviour
{
	[SerializeField]
	int hight;

	[SerializeField]
	int width;

	[SerializeField]
	bool isFullScreen;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        
    }

	/// <summary>
	/// クリック時の処理
	/// </summary>
	public void OnClick()
	{
		FullScreenMode mode = FullScreenMode.Windowed;

		if (isFullScreen)
			mode = FullScreenMode.FullScreenWindow;

		Screen.SetResolution(hight, width, mode);

		GameInstance.Instance.systemData.windowData.windowHight = hight;
		GameInstance.Instance.systemData.windowData.windowWidth = width;
		GameInstance.Instance.systemData.windowData.isFullScreen = isFullScreen;
	}
}
