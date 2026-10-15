using System;
using System.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Utage;
using UtageExtensions;

public class AdvEngineController : MonoBehaviour
{
	// 簡易アクセス用
	public static GameObject InstanceObject { get; private set; }
	public static AdvEngineController Instance { get; private set; }
	public static bool IsUiInputBlocked => Instance != null && Instance.uiModal != null && Instance.uiModal.IsBlocking;
	public AdvUiModalController UiModal => uiModal;
	AdvUiModalController uiModal;


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
		uiModal = gameObject.AddComponent<AdvUiModalController>();
		uiModal.Initialize(advEngine);
	}


	// ADVエンジン
	public AdvEngine AdvEngine { get { return advEngine; } }
	[SerializeField]
	protected AdvEngine advEngine;

	//再生中かどうか
	public bool IsPlaying { get; private set; }

	float defaultSpeed = -1;

	/// <summary>
	/// 指定ラベルのシナリオを再生する
	/// </summary>
	/// <param name="label">再生するシナリオラベル</param>
	public void JumpScenario(string label)
	{
		JumpScenario(label, null, null, null);
	}

	/// <summary>
	/// 指定ラベルを再生し、正常終了を通知する
	/// </summary>
	/// <param name="label">再生するシナリオラベル</param>
	/// <param name="onComplete">正常終了時の処理</param>
	public void JumpScenario(string label, Action onComplete)
	{
		JumpScenario(label, null, onComplete, null);
	}

	/// <summary>
	/// シナリオ再生中の背面入力を制限し、終了時または中断時に解除する
	/// </summary>
	/// <param name="label">再生するシナリオラベル</param>
	/// <param name="onComplete">正常終了時の処理</param>
	IEnumerator JumpScenarioAsync(string label, Action onComplete)
	{
		if (IsPlaying)
		{
			Debug.LogWarning("シナリオ再生中のため、新しいシナリオは開始できません。", this);
			yield break;
		}
		IsPlaying = true;
		try
		{
			uiModal.Begin();
			AdvEngine.SoundManager.SeVolume = AudioManager.Instance.GetSeVolume();
			AdvEngine.JumpScenario(label);
			while (!AdvEngine.IsEndOrPauseScenario)
				yield return null;
		}
		finally
		{
			IsPlaying = false;
			if (uiModal != null) uiModal.End();
		}
		if (onComplete != null) onComplete();
	}

	/// <summary>
	/// 破棄された宴管理クラスへの共有参照を解除する
	/// </summary>
	void OnDestroy()
	{
		if (Instance != this) return;
		Instance = null;
		InstanceObject = null;
	}

	/// <summary>
	/// シナリオを再生し、終了または開始失敗を通知する
	/// </summary>
	/// <param name="label">再生するシナリオラベル</param>
	/// <param name="onComplete">正常終了時の処理</param>
	/// <param name="onFailed">開始できない場合の処理</param>
	public void JumpScenario(string label, Action onComplete, Action onFailed)
	{
		JumpScenario(label, null, onComplete, onFailed);
	}

	/// <summary>
	/// シナリオを検証し、開始・終了・開始失敗を通知する
	/// </summary>
	/// <param name="label">再生するシナリオラベル</param>
	/// <param name="onStart">開始前の処理</param>
	/// <param name="onComplete">正常終了時の処理</param>
	/// <param name="onFailed">開始できない場合の処理</param>
	public void JumpScenario(string label, Action onStart, Action onComplete, Action onFailed)
	{
		if (IsPlaying)
		{
			onFailed?.Invoke();
			Debug.LogWarning("シナリオ再生中のため、新しいシナリオは開始できません。", this);
			return;
		}
		if (string.IsNullOrEmpty(label))
		{
			if (onFailed != null) onFailed();
			Debug.LogErrorFormat("シナリオラベルが空です");
			return;
		}
		if (label[0] == '*')
		{
			label = label.Substring(1);
		}
		if (AdvEngine.DataManager.FindScenarioData(label) == null)
		{
			if (onFailed != null) onFailed();
			Debug.LogErrorFormat("{0}はまだロードされていないか、存在しないシナリオです", label);
			return;
		}

		if (onStart != null) onStart();
		StartCoroutine(JumpScenarioAsync(label, onComplete));
	}

	//シナリオの呼び出し以外に、
	//AdvEngineを操作する処理をまとめておくと、便利
	//何が必要かはプロジェクトによるので、場合によって増やしていく

	//以下、メッセージウィンドのテキスト表示速度を操作する処理のサンプル

	//メッセージウィンドのテキスト表示の速度を指定のスピードに
	public void ChangeMessageSpeed(float speed)
	{
		if (defaultSpeed < 0)
		{
			defaultSpeed = AdvEngine.Config.MessageSpeed;
		}
		AdvEngine.Config.MessageSpeed = speed;
	}
	//メッセージウィンドのテキスト表示の速度を元に戻す
	public void ResetMessageSpeed()
	{
		if (defaultSpeed >= 0)
		{
			AdvEngine.Config.MessageSpeed = defaultSpeed;
		}
	}

	public void SetParameter(string key, int value)
	{
		advEngine.Param.SetParameter(key, value);
	}
}
