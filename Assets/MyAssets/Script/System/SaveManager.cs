using System.IO;
using UnityEngine;

public static class SaveManager
{
	private static string SaveDir =>
		Path.Combine(Application.dataPath, "../SaveData");

	private static string GetUserSavePath(int slot)
	{
		Directory.CreateDirectory(SaveDir);
		return Path.Combine(SaveDir, $"UserSave{slot}.sav");
	}

    /// <summary>プレイヤーと探索中のマップ、スロットの概要を保存する。</summary>
    /// <param name="slot">保存先スロット</param>
    /// <param name="data">保存するプレイヤー</param>
    /// <param name="saveTime">表示用の保存日時</param>
	public static void Save(int slot, PlayerData data, string saveTime)
	{
		ES3.Save("SaveData", data, GetUserSavePath(slot));

		PlayerDataSummary summary = data.GetSaveSummary();
		summary.saveTime = saveTime;
		ES3.Save("SaveDataSummary", summary, GetUserSavePath(slot));
	}

	/// <summary>保存データを読み込み、スキルの派生型と所持先を復元する。</summary>
	/// <param name="slot">読み込む保存スロット</param>
	public static PlayerData Load(int slot)
	{
        PlayerData data = ES3.Load("SaveData", GetUserSavePath(slot), new PlayerData());
        return data;
	}

	public static PlayerDataSummary LoadSummary(int slot)
	{
		return ES3.Load("SaveDataSummary", GetUserSavePath(slot), new PlayerDataSummary());
	}

	public static bool HasSaveData(int slot)
	{
		string path = GetUserSavePath(slot);

		return ES3.FileExists(path) &&
		   ES3.KeyExists("SaveData", path) &&
		   ES3.KeyExists("SaveDataSummary", path);
	}




	private static string GetSystemSavePath()
	{
		Directory.CreateDirectory(SaveDir);
		return Path.Combine(SaveDir, "SystemSave.sav");
	}

	public static void SaveSystem(SystemData data)
	{
		ES3.Save("SaveData", data, GetSystemSavePath());
	}

	public static SystemData LoadSystem()
	{
		return ES3.Load("SaveData", GetSystemSavePath(), new SystemData());
	}

	public static bool HasSystemSaveData()
	{
		string path = GetSystemSavePath();

		return ES3.FileExists(path) &&
		   ES3.KeyExists("SaveData", path);
	}
}
