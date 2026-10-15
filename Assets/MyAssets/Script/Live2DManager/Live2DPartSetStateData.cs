using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class Live2DPartSetStateData
{
	// パーツの状態
	public enum ELive2DPartState
	{
		State0,
		State1,
		State2,
		State3,
		State4,
		State5,
		State6,
		State7,
		State8,
		State9,
		State10,
		State11,
		State12,
		State13,
		State14,
		State15,
	}

	[Serializable]
	public class PartSetData
	{
		public ELive2DPartState state;

		public List<string> partList;
	}

	// パーツの表示切替
	public Action<string, bool> onSetPartSetVisibility;
	public Action<string, float> onSetPartSetOpacity;

	// ステートID
	public string partStateId;

	// 初期透明度
	[SerializeField]
	public float initialOpacity = 1.0f;

	// インスペクタから登録するPartSet
	[SerializeField]
	List<PartSetData> PartSetList;

	// PartSetの簡易アクセス用
	Dictionary<ELive2DPartState, List<string>> partStateDictionary = new Dictionary<ELive2DPartState, List<string>>();

	// パーツの状態
	ELive2DPartState partState;

	ELive2DPartState partStateBase;

	// OneShotアニメーション
	Tween TweenOneShot;

	/// <summary>
	/// インスペクタで設定した情報をDictionaryに変換する
	/// </summary>
	public void Build()
	{
		Reset();

		int idx = -1;
		foreach (PartSetData partSet in PartSetList)
		{
			idx++;

			if (partStateDictionary.ContainsKey(partSet.state))
			{
				Debug.LogError($"ERROR Live2DPartSetStateData: Duplicate State. idx = {idx} , State = {partSet.state}");

				continue;
			}

			partStateDictionary[partSet.state] = partSet.partList;
		}
	}

	/// <summary>
	/// パーツ状態のリセット
	/// </summary>
	public void Reset()
	{
		partStateDictionary.Clear();

		partStateBase = ELive2DPartState.State0;
		partState = partStateBase;
	}

	/// <summary>
	/// パーツ情報の更新
	/// </summary>
	public void UpdateState()
	{
		// パーツをすべて非表示
		SetPartVisibility(false, true);

		// 状態に応じてパーツの表示状態を切り替え
		SetPartVisibility(true);
	}

	/// <summary>
	/// 状態の設定
	/// </summary>
	/// <param name="state"></param>
	public void SetState<T>(T state) where T : Enum
	{
		partStateBase = (ELive2DPartState)Convert.ToInt32(state);
		partState = partStateBase;

		UpdateState();
	}
	public void SetState(int state)
	{
		partStateBase = (ELive2DPartState)state;
		partState = partStateBase;

		UpdateState();
	}

	public void SetStateOneShot<T>(T state, float time = 2.0f) where T : Enum
	{
		partState = (ELive2DPartState)Convert.ToInt32(state);
		UpdateState();

		TweenOneShot?.Kill();
		TweenOneShot = DOVirtual.DelayedCall(time, () =>
		{
			partState = partStateBase;
			UpdateState();
		});
	}

	/// <summary>
	/// 状態の取得
	/// </summary>
	/// <param name="state"></param>
	public T GetState<T>() where T : Enum
	{
		return (T)(object)partStateBase;
	}

	/// <summary>
	/// パーツの表示切替
	/// </summary>
	/// <param name="isVisibility"></param>
	/// <param name="isAll"></param>
	public void SetPartVisibility(bool isVisibility, bool isAll = false)
	{
		foreach (var parts in partStateDictionary)
		{
			if (!isAll && partState != parts.Key)
				continue;


			foreach (string id in parts.Value)
			{
				bool isShow = isVisibility;

				onSetPartSetVisibility?.Invoke(id, isShow);
			}
		}
	}

	/// <summary>
	/// パーツの透明度切替
	/// </summary>
	/// <param name="opacity"></param>
	/// <param name="isAll"></param>
	public void SetPartOpacity(float opacity, bool isAll = false)
	{
		foreach (var parts in partStateDictionary)
		{
			if (!isAll && partStateBase != parts.Key)
				continue;

			foreach (string id in parts.Value)
			{
				onSetPartSetOpacity?.Invoke(id, opacity);
			}
		}
	}
}