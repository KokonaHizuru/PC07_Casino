using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TextCore.Text;

[Serializable]
public class PlayerData
{
	// カジノの所持金。New Game時にSetDefaultDataで初期化する。
	public long casinoMoney;
	// フラグ
	public FlagData flag = new();

	// Hステータス
	public HiroineSexStatus sexStatus = new();

	/// <summary>
	/// セーブ情報のリセット
	/// </summary>
	public void  Reset()
	{
		casinoMoney = 0;
		flag.Clear();
		sexStatus.Reset();

		SetDefaultData();
	}

	/// <summary>
	/// 初期情報の設定
	/// </summary>
	public void SetDefaultData()
	{
		casinoMoney = 1000;
	}

	/// <summary>
	/// セーブのまとめ情報を取得
	/// </summary>
	/// <returns></returns>
	public PlayerDataSummary GetSaveSummary()
	{
		PlayerDataSummary summary = new();

		return summary;
	}
}

/// <summary>
/// セーブ画面に表示する用の情報
/// </summary>
public class PlayerDataSummary
{
	// セーブ日時
	public string saveTime;
}

/// <summary>
/// Hステータス
/// </summary>
[Serializable]
public class HiroineSexStatus
{
	public string virgin;
	public string virginAnal;

	public int workNum;

	public int ejacMouse;
	public int ejacVagina;
	public int ejacAnal;

	public int orgasmNipple;
	public int orgasmMouse;
	public int orgasmVirgin;
	public int orgasmAnal;

	public int sexNumHumanMouse;
	public int sexNumHumanVagina;
	public int sexNumHumanAnal;

	public int sexNumMonsterMouse;
	public int sexNumMonsterVagina;
	public int sexNumMonsterAnal;

	public void Reset()
	{
		virgin = "純潔";
		virginAnal = "純潔";

		workNum = 0;

		ejacMouse = 0;
		ejacVagina = 0;
		ejacAnal = 0;

		orgasmNipple = 0;
		orgasmMouse = 0;
		orgasmVirgin = 0;
		orgasmAnal = 0;

		sexNumHumanMouse = 0;
		sexNumHumanVagina = 0;
		sexNumHumanAnal = 0;

		sexNumMonsterMouse = 0;
		sexNumMonsterVagina = 0;
		sexNumMonsterAnal = 0;
	}
}
