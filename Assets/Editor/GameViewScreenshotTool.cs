#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public static class GameViewScreenshotTool
{
	const string SaveFolderName = "Screenshots";
	const string FilePrefix = "Screenshot";

	[MenuItem("Tools/Screenshot/Capture Game View")]
	static void CaptureGameView()
	{
		string saveDir = Path.Combine(Application.dataPath, "..", SaveFolderName);

		if (!Directory.Exists(saveDir))
		{
			Directory.CreateDirectory(saveDir);
		}

		string filePath = GetNextFilePath(saveDir);

		ScreenCapture.CaptureScreenshot(filePath, 1);

		Debug.Log($"保存しました : {filePath}");
	}

	[Shortcut(
		"Screenshot/Capture Game View",
		KeyCode.F12,
		ShortcutModifiers.Action | ShortcutModifiers.Shift)]
	static void CaptureGameViewShortcut()
	{
		CaptureGameView();
	}

	static string GetNextFilePath(string saveDir)
	{
		int index = 0;

		while (true)
		{
			string filePath = Path.Combine(
				saveDir,
				$"{FilePrefix}_{index:0000}.png");

			if (!File.Exists(filePath))
			{
				return filePath;
			}

			index++;
		}
	}
}

#endif