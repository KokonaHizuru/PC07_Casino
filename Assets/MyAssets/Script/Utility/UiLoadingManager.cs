using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class UiLoadingManager : MonoBehaviour
{
	// 簡易アクセス用
	public static GameObject InstanceObject { get; private set; }
	public static UiLoadingManager Instance { get; private set; }
	readonly HashSet<int> activeLoads = new();
	int nextLoadId;
	float loadStartTime;
	Tween fadeTween;
	bool showing;

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
		canvasGroup.alpha = 0;
		canvasGroup.blocksRaycasts = false;
		canvasGroup.interactable = false;
	}

	// UIが表示されるのに必要なロード時間
	public float showLoadTime;

	// UIのフェード時間
	public float fadeInTime;
	public float fadeOutTime;

	[SerializeField] CanvasGroup canvasGroup;

	/// <summary>ロードが表示開始時間を超えたらUIを表示する。</summary>
	void Update()
	{
		if (activeLoads.Count > 0 && !showing && Time.realtimeSinceStartup - loadStartTime >= showLoadTime)
		{
			showing = true;
			fadeTween?.Kill();
			fadeTween = canvasGroup.DOFade(1f, Mathf.Max(0f, fadeInTime)).SetUpdate(true);
		}
	}

	/// <summary>一件のロードを監視し、終了時に破棄するトークンを返す。</summary>
	public LoadToken BeginLoading()
	{
		if (activeLoads.Count == 0) loadStartTime = Time.realtimeSinceStartup;
		int id = ++nextLoadId;
		activeLoads.Add(id);
		return new LoadToken(this, id);
	}

	/// <summary>指定したロードを終了し、全件終了後にUIを隠す。</summary>
	/// <param name="id">ロード識別番号</param>
	void EndLoading(int id)
	{
		if (!activeLoads.Remove(id) || activeLoads.Count > 0) return;
		showing = false;
		fadeTween?.Kill();
		fadeTween = canvasGroup.DOFade(0f, Mathf.Max(0f, fadeOutTime)).SetUpdate(true);
	}

	/// <summary>共有参照と進行中のフェードを解放する。</summary>
	void OnDestroy()
	{
		fadeTween?.Kill();
		if (Instance != this) return;
		Instance = null;
		InstanceObject = null;
	}

	/// <summary>ロード一件の監視期間を表す。</summary>
	public sealed class LoadToken : System.IDisposable
	{
		UiLoadingManager manager;
		readonly int id;

		/// <summary>表示管理とロード識別番号を保持する。</summary>
		/// <param name="manager">表示管理</param>
		/// <param name="id">ロード識別番号</param>
		internal LoadToken(UiLoadingManager manager, int id)
		{
			this.manager = manager;
			this.id = id;
		}

		/// <summary>ロードの監視を終了する。</summary>
		public void Dispose()
		{
			if (manager != null) manager.EndLoading(id);
			manager = null;
		}
	}
}
