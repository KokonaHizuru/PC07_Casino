using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Live2DController : MonoBehaviour, ICubismUpdatable
{
	CubismModel cubismModel;

	[HideInInspector]
	public Animator animator;

	// 最初に非表示にするパーツ
	[SerializeField]
	List<string> initHidePart;

	[SerializeField]
	List<string> initHideOpacityPart;

	// パーツセットの情報をインスペクタから登録する用
	[SerializeField]
	List<Live2DPartSetStateData> partSetDataList;

	// パーツセット
	Dictionary<string, Live2DPartSetStateData> partDataDictionary = new Dictionary<string, Live2DPartSetStateData>();

	// LateUpdateで更新する情報を記録する変数
	Dictionary<CubismPart, float> changeOpacity = new Dictionary<CubismPart, float>();
	Dictionary<CubismParameter, float> changeParam = new Dictionary<CubismParameter, float>();

	// パーツの表示状態
	Dictionary<CubismPart, float> partsOpacity = new Dictionary<CubismPart, float>();
	Dictionary<CubismPart, bool> partsVisibility = new Dictionary<CubismPart, bool>();

	// 初回フラグ
	bool isInit = true;

	// ICubismUpdatableに必要な情報
	public bool HasUpdateController { get; set; }
	public bool NeedsUpdateOnEditing => false;
	public int ExecutionOrder => 120;   // 公式フォーラム回答の範囲：101〜149

	void Awake()
	{
		// アタッチされているLive2D情報を取得
		cubismModel = GetComponentInChildren<CubismModel>(true);
		animator = GetComponentInChildren<Animator>(true);

		// パーツ情報をアクセス用のDictionaryに格納
		int idx = -1;
		foreach (Live2DPartSetStateData part in partSetDataList)
		{
			idx++;

			// IDが重複しているなら処理しない
			if (partDataDictionary.ContainsKey(part.partStateId))
			{
				Debug.LogError($"ERROR Live2DController: Duplicate ID. idx = {idx} , ID = {part.partStateId}");
				continue;
			}

			part.onSetPartSetVisibility += OnSetPartSetVicibility;
			part.onSetPartSetOpacity += OnSetPartSetOpacity;
			part.Build();
			partDataDictionary.Add(part.partStateId, part);
		}
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	IEnumerator Start()
	{
		// 最初に非表示にするパーツ
		foreach (string partId in initHidePart)
		{
			SetPartVisibility(partId, false);
		}

		// 最初にOpacityで非表示にするパーツ
		foreach (string partId in initHideOpacityPart)
		{
			SetPartOpacity(partId, 0.0f);
		}

		// 初期化
		foreach (var pair in partDataDictionary)
		{
			pair.Value.UpdateState();
			pair.Value.SetPartOpacity(pair.Value.initialOpacity, true);
		}

		yield return null;

		isInit = false;
	}

	// Update is called once per frame
	void Update()
    {
        
    }

	void LateUpdate()
	{
		// CubismUpdateControllerが無い場合の保険
		if (!HasUpdateController)
		{
			OnLateUpdate();
		}
	}

	/// <summary>
	/// ICubismUpdatableの仮想関数
	/// </summary>
	public void OnLateUpdate()
	{
		foreach (var item in changeOpacity)
		{
			item.Key.Opacity = item.Value;
		}

		changeOpacity.Clear();

		foreach (var item in changeParam)
		{
			item.Key.Value = item.Value;
		}

		changeParam.Clear();
	}

	/// <summary>
	/// パーツを取得
	/// </summary>
	/// <param name="partSetId"></param>
	public Live2DPartSetStateData GetPartStateData(string partSetId)
	{
		if (!partDataDictionary.ContainsKey(partSetId))
			return null;

		return partDataDictionary[partSetId];
	}

	/// <summary>
	/// パーツの状態を変更
	/// </summary>
	/// <param name="partSetId"></param>
	public void SetPartSetState<T>(string partSetId, T state) where T : Enum
	{
		if (!partDataDictionary.ContainsKey(partSetId))
			return;

		partDataDictionary[partSetId].SetState(state);
	}
	public void SetPartSetState(string partSetId, int state)
	{
		if (!partDataDictionary.ContainsKey(partSetId))
			return;

		partDataDictionary[partSetId].SetState(state);
	}

	public void SetPartSetStateOneShot<T>(string partSetId, T state, float time = 2.0f) where T : Enum
	{
		if (!partDataDictionary.ContainsKey(partSetId))
			return;

		partDataDictionary[partSetId].SetStateOneShot(state, time);
	}

	/// <summary>
	/// 状態の取得
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public T GetPartSetState<T>(string partSetId) where T : Enum
	{
		if (!partDataDictionary.ContainsKey(partSetId))
			return (T)(object)0;

		return partDataDictionary[partSetId].GetState<T>();
	}

	/// <summary>
	/// パーツセットの透明度設定
	/// </summary>
	public void SetPartSetOpacity(string partSetId, float opacity)
	{
		if (!partDataDictionary.ContainsKey(partSetId))
			return;

		partDataDictionary[partSetId].SetPartOpacity(opacity);
	}

	/// <summary>
	/// パーツの表示切替
	/// </summary>
	/// <param name="partId"></param>
	/// <param name="visibility"></param>
	/// <returns></returns>
	public bool SetPartVisibility(string partId, bool visibility)
	{
		CubismPart part = cubismModel.Parts.FindById(partId);

		if (part == null)
			return false;

		// キーがないなら追加
		if (!partsOpacity.ContainsKey(part))
			partsOpacity[part] = 1.0f;

		// 値を保存
		partsVisibility[part] = visibility;

		if (isInit)
		{
			part.Opacity = partsVisibility[part] ? partsOpacity[part] : 0.0f;
		}
		else
		{
			changeOpacity[part] = partsVisibility[part] ? partsOpacity[part] : 0.0f;
		}

		return true;
	}

	/// <summary>
	/// 透明度設定
	/// </summary>
	/// <param name="partId"></param>
	/// <param name="opacity"></param>
	/// <returns></returns>
	public bool SetPartOpacity(string partId, float opacity)
	{
		CubismPart part = cubismModel.Parts.FindById(partId);

		if (part == null)
			return false;

		// キーがないなら追加
		if (!partsVisibility.ContainsKey(part))
			partsVisibility[part] = true;

		// 値を保存
		partsOpacity[part] = opacity;

		if (isInit)
		{
			part.Opacity = partsVisibility[part] ? partsOpacity[part] : 0.0f;
		}
		else
		{
			changeOpacity[part] = partsVisibility[part] ? partsOpacity[part] : 0.0f;
		}

		return true;
	}

	/// <summary>
	/// パラメータの設定
	/// </summary>
	/// <param name="paramId"></param>
	/// <param name="value"></param>
	/// <returns></returns>
	public bool SetParameter(string paramId, float value)
	{
		CubismParameter param = cubismModel.Parameters.FindById(paramId);

		if (param == null)
			return false;

		changeParam[param] = value;

		return true;
	}

	/// <summary>
	/// Live2DAnimatorから指定のStateMachineBehaviourを取得する
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public T GetStateBehaviour<T>() where T : StateMachineBehaviour
	{
		T[] behaviours = animator.GetBehaviours<T>();
		foreach (T state in behaviours)
		{
			return state;
		}

		return null;
	}

	/// <summary>
	/// パーツセットの表示切替時に呼び出される処理
	/// </summary>
	/// <param name="partId"></param>
	/// <param name="visibility"></param>
	void OnSetPartSetVicibility(string partId, bool visibility)
	{
		SetPartVisibility(partId, visibility);
	}

	/// <summary>
	/// パーツセットの透明度変更時に呼び出される処理
	/// </summary>
	/// <param name="partId"></param>
	/// <param name="opacity"></param>
	void OnSetPartSetOpacity(string partId, float opacity)
	{
		SetPartOpacity(partId, opacity);
	}
}
