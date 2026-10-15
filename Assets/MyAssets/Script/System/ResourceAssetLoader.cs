using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Resources ロードの現在の状態。</summary>
public enum AssetLoadState
{
	Pending,
	Loading,
	Succeeded,
	Failed,
	Canceled
}

/// <summary>型が異なるアセットをまとめてロードする際の指定。</summary>
public sealed class ResourceLoadTarget
{
	public string Path { get; }
	public Type AssetType { get; }

	public ResourceLoadTarget(string path, Type assetType)
	{
		if (string.IsNullOrWhiteSpace(path))
			throw new ArgumentException("Resources のパスを指定してください。", nameof(path));
		if (assetType == null || !typeof(UnityEngine.Object).IsAssignableFrom(assetType))
			throw new ArgumentException("UnityEngine.Object の派生型を指定してください。", nameof(assetType));

		Path = path;
		AssetType = assetType;
	}

	public static ResourceLoadTarget Create<T>(string path) where T : UnityEngine.Object
	{
		return new ResourceLoadTarget(path, typeof(T));
	}
}

/// <summary>単体アセットのロード結果と状態を保持するハンドル。</summary>
public class AssetLoadHandle
{
	Action<AssetLoadHandle> completed;
	Action<AssetLoadHandle> failed;
	Action<AssetLoadHandle> finished;

	public string Path { get; }
	public Type AssetType { get; }
	public AssetLoadState State { get; private set; } = AssetLoadState.Pending;
	public UnityEngine.Object Asset { get; private set; }
	public string Error { get; private set; }
	public float Progress { get; internal set; }
	public bool IsDone => State == AssetLoadState.Succeeded || State == AssetLoadState.Failed || State == AssetLoadState.Canceled;
	public bool IsSuccess => State == AssetLoadState.Succeeded;
	public bool IsCanceled => State == AssetLoadState.Canceled;

	internal AssetLoadHandle(string path, Type assetType)
	{
		Path = path;
		AssetType = assetType;
	}

	/// <summary>成功時の処理を登録します。既に成功済みなら直ちに呼ばれます。</summary>
	public AssetLoadHandle OnCompleted(Action<AssetLoadHandle> action)
	{
		if (action == null) return this;
		if (IsSuccess) InvokeSafely(action);
		else if (!IsDone) completed += action;
		return this;
	}

	/// <summary>失敗時の処理を登録します。エラー内容は Error から取得できます。</summary>
	public AssetLoadHandle OnFailed(Action<AssetLoadHandle> action)
	{
		if (action == null) return this;
		if (State == AssetLoadState.Failed) InvokeSafely(action);
		else if (!IsDone) failed += action;
		return this;
	}

	/// <summary>成功・失敗・キャンセルのいずれでも完了時に呼ばれる処理を登録します。</summary>
	public AssetLoadHandle OnFinished(Action<AssetLoadHandle> action)
	{
		if (action == null) return this;
		if (IsDone) InvokeSafely(action);
		else finished += action;
		return this;
	}

	/// <summary>結果を指定型として取得します。型違いまたは未完了時は null です。</summary>
	public T GetAsset<T>() where T : UnityEngine.Object => Asset as T;

	/// <summary>非同期ロードをキャンセル扱いにします（Unity 内部の読み込み自体は停止できません）。</summary>
	public void Cancel()
	{
		if (IsDone) return;
		State = AssetLoadState.Canceled;
		Error = "ロードがキャンセルされました。";
		Progress = 1f;
		InvokeSafely(finished);
		ClearCallbacks();
	}

	internal void Start() => State = AssetLoadState.Loading;

	internal void Complete(UnityEngine.Object asset)
	{
		if (IsDone) return;
		Asset = asset;
		State = AssetLoadState.Succeeded;
		Progress = 1f;
		InvokeSafely(completed);
		InvokeSafely(finished);
		ClearCallbacks();
	}

	internal void Fail(string error)
	{
		if (IsDone) return;
		Error = error;
		State = AssetLoadState.Failed;
		Progress = 1f;
		InvokeSafely(failed);
		InvokeSafely(finished);
		ClearCallbacks();
	}

	void ClearCallbacks()
	{
		completed = null;
		failed = null;
		finished = null;
	}

	void InvokeSafely(Action<AssetLoadHandle> actions)
	{
		if (actions == null) return;
		foreach (Action<AssetLoadHandle> action in actions.GetInvocationList())
		{
			try { action(this); }
			catch (Exception exception) { Debug.LogException(exception); }
		}
	}
}

/// <summary>同じ型を扱いやすくする単体ロードハンドル。</summary>
public sealed class AssetLoadHandle<T> : AssetLoadHandle where T : UnityEngine.Object
{
	public new T Asset => base.Asset as T;

	internal AssetLoadHandle(string path) : base(path, typeof(T)) { }

	public AssetLoadHandle<T> OnCompleted(Action<AssetLoadHandle<T>> action)
	{
		base.OnCompleted(action == null ? null : handle => action((AssetLoadHandle<T>)handle));
		return this;
	}

	public AssetLoadHandle<T> OnFailed(Action<AssetLoadHandle<T>> action)
	{
		base.OnFailed(action == null ? null : handle => action((AssetLoadHandle<T>)handle));
		return this;
	}

	public AssetLoadHandle<T> OnFinished(Action<AssetLoadHandle<T>> action)
	{
		base.OnFinished(action == null ? null : handle => action((AssetLoadHandle<T>)handle));
		return this;
	}
}

/// <summary>一括ロード全体の状態を保持するハンドル。</summary>
public sealed class AssetLoadBatchHandle
{
	readonly List<AssetLoadHandle> items;
	Action<AssetLoadBatchHandle> completed;
	Action<AssetLoadBatchHandle> failed;
	Action<AssetLoadBatchHandle> finished;
	Action<AssetLoadHandle> itemCompleted;
	Action<AssetLoadHandle> itemFailed;

	public IReadOnlyList<AssetLoadHandle> Items => items;
	public AssetLoadState State { get; private set; } = AssetLoadState.Pending;
	public int TotalCount => items.Count;
	public int FinishedCount => items.Count(item => item.IsDone);
	public int SuccessCount => items.Count(item => item.IsSuccess);
	public int FailedCount => items.Count(item => item.State == AssetLoadState.Failed);
	public float Progress => TotalCount == 0 ? 1f : items.Sum(item => item.Progress) / TotalCount;
	public bool IsDone => State == AssetLoadState.Succeeded || State == AssetLoadState.Failed || State == AssetLoadState.Canceled;
	public bool IsSuccess => State == AssetLoadState.Succeeded;

	internal AssetLoadBatchHandle(List<AssetLoadHandle> handles)
	{
		items = handles;
		foreach (AssetLoadHandle item in items)
		{
			item.OnCompleted(NotifyItemCompleted);
			item.OnFailed(NotifyItemFailed);
		}
	}

	public AssetLoadBatchHandle OnItemCompleted(Action<AssetLoadHandle> action)
	{
		if (action == null) return this;
		foreach (AssetLoadHandle item in items.Where(item => item.IsSuccess))
			InvokeItemSafely(action, item);
		if (!IsDone) itemCompleted += action;
		return this;
	}

	public AssetLoadBatchHandle OnItemFailed(Action<AssetLoadHandle> action)
	{
		if (action == null) return this;
		foreach (AssetLoadHandle item in items.Where(item => item.State == AssetLoadState.Failed))
			InvokeItemSafely(action, item);
		if (!IsDone) itemFailed += action;
		return this;
	}

	public AssetLoadBatchHandle OnCompleted(Action<AssetLoadBatchHandle> action)
	{
		if (action == null) return this;
		if (IsSuccess) InvokeSafely(action);
		else if (!IsDone) completed += action;
		return this;
	}

	public AssetLoadBatchHandle OnFailed(Action<AssetLoadBatchHandle> action)
	{
		if (action == null) return this;
		if (State == AssetLoadState.Failed) InvokeSafely(action);
		else if (!IsDone) failed += action;
		return this;
	}

	public AssetLoadBatchHandle OnFinished(Action<AssetLoadBatchHandle> action)
	{
		if (action == null) return this;
		if (IsDone) InvokeSafely(action);
		else finished += action;
		return this;
	}

	public AssetLoadHandle Find(string path) => items.FirstOrDefault(item => item.Path == path);

	public void Cancel()
	{
		if (IsDone) return;
		foreach (AssetLoadHandle item in items) item.Cancel();
		State = AssetLoadState.Canceled;
		InvokeSafely(finished);
		ClearCallbacks();
	}

	internal void Start() => State = AssetLoadState.Loading;

	internal void Complete()
	{
		if (IsDone) return;
		State = FailedCount > 0 ? AssetLoadState.Failed : AssetLoadState.Succeeded;
		if (IsSuccess) InvokeSafely(completed); else InvokeSafely(failed);
		InvokeSafely(finished);
		ClearCallbacks();
	}

	void NotifyItemCompleted(AssetLoadHandle handle) => InvokeItemSafely(itemCompleted, handle);
	void NotifyItemFailed(AssetLoadHandle handle) => InvokeItemSafely(itemFailed, handle);

	void ClearCallbacks()
	{
		completed = null; failed = null; finished = null;
		itemCompleted = null; itemFailed = null;
	}

	void InvokeSafely(Action<AssetLoadBatchHandle> actions)
	{
		if (actions == null) return;
		foreach (Action<AssetLoadBatchHandle> action in actions.GetInvocationList())
		{
			try { action(this); }
			catch (Exception exception) { Debug.LogException(exception); }
		}
	}

	static void InvokeItemSafely(Action<AssetLoadHandle> actions, AssetLoadHandle handle)
	{
		if (actions == null) return;
		foreach (Action<AssetLoadHandle> action in actions.GetInvocationList())
		{
			try { action(handle); }
			catch (Exception exception) { Debug.LogException(exception); }
		}
	}
}

/// <summary>
/// Resources 配下の Prefab、Sprite、AudioClip 等を同期・非同期でロードします。
/// パスは拡張子を除いた Resources フォルダーからの相対パスです。
/// </summary>
public static class ResourceAssetLoader
{
	/// <summary>参照が解放された未使用アセットを安全に回収する</summary>
	public static AsyncOperation UnloadUnused() => Resources.UnloadUnusedAssets();
	public static AssetLoadHandle<T> Load<T>(string path) where T : UnityEngine.Object
	{
		AssetLoadHandle<T> handle = new AssetLoadHandle<T>(path);
		LoadSynchronously(handle);
		return handle;
	}

	public static AssetLoadHandle<T> LoadAsync<T>(string path) where T : UnityEngine.Object
	{
		AssetLoadHandle<T> handle = new AssetLoadHandle<T>(path);
		LoadAsynchronously(handle);
		return handle;
	}

	public static AssetLoadBatchHandle LoadMany<T>(IEnumerable<string> paths) where T : UnityEngine.Object
	{
		return LoadMany(CreateTargets<T>(paths));
	}

	public static AssetLoadBatchHandle LoadManyAsync<T>(IEnumerable<string> paths) where T : UnityEngine.Object
	{
		return LoadManyAsync(CreateTargets<T>(paths));
	}

	public static AssetLoadBatchHandle LoadMany(IEnumerable<ResourceLoadTarget> targets)
	{
		AssetLoadBatchHandle batch = CreateBatch(targets);
		batch.Start();
		foreach (AssetLoadHandle item in batch.Items) LoadSynchronously(item);
		batch.Complete();
		return batch;
	}

	public static AssetLoadBatchHandle LoadManyAsync(IEnumerable<ResourceLoadTarget> targets)
	{
		AssetLoadBatchHandle batch = CreateBatch(targets);
		LoadBatchAsynchronously(batch);
		return batch;
	}

	/// <summary>ロード済み Prefab からインスタンスを生成します。失敗時は null です。</summary>
	public static GameObject Instantiate(AssetLoadHandle<GameObject> handle, Transform parent = null)
	{
		if (handle == null || !handle.IsSuccess) return null;
		return UnityEngine.Object.Instantiate(handle.Asset, parent);
	}

	/// <summary>Resources.Load で読み込んだ非 GameObject アセットを明示的に解放します。</summary>
	public static void Unload(AssetLoadHandle handle)
	{
		if (handle == null || !handle.IsSuccess || handle.Asset == null || handle.Asset is GameObject) return;
		Resources.UnloadAsset(handle.Asset);
	}

	static void LoadSynchronously(AssetLoadHandle handle)
	{
		if (handle.IsDone) return;
		handle.Start();
		try
		{
			UnityEngine.Object asset = Resources.Load(handle.Path, handle.AssetType);
			if (asset != null) handle.Complete(asset);
			else handle.Fail(CreateNotFoundMessage(handle));
		}
		catch (Exception exception)
		{
			handle.Fail(exception.Message);
		}
	}

	/// <summary>非同期ロードを実行し、完了までロードUIの監視対象にする。</summary>
	/// <param name="handle">ロード結果を保持するハンドル</param>
	static async void LoadAsynchronously(AssetLoadHandle handle)
	{
		if (handle.IsDone) return;
		UiLoadingManager.LoadToken loading = UiLoadingManager.Instance != null
			? UiLoadingManager.Instance.BeginLoading() : null;
		handle.Start();
		try
		{
			ResourceRequest request = Resources.LoadAsync(handle.Path, handle.AssetType);
			while (!request.isDone)
			{
				if (handle.IsCanceled) return;
				handle.Progress = request.progress;
				await Task.Yield();
			}

			if (handle.IsCanceled) return;
			if (request.asset != null) handle.Complete(request.asset);
			else handle.Fail(CreateNotFoundMessage(handle));
		}
		catch (Exception exception)
		{
			handle.Fail(exception.Message);
		}
		finally
		{
			loading?.Dispose();
		}
	}

	static async void LoadBatchAsynchronously(AssetLoadBatchHandle batch)
	{
		batch.Start();
		foreach (AssetLoadHandle item in batch.Items) LoadAsynchronously(item);

		while (!batch.Items.All(item => item.IsDone))
		{
			if (batch.State == AssetLoadState.Canceled) return;
			await Task.Yield();
		}
		batch.Complete();
	}

	static AssetLoadBatchHandle CreateBatch(IEnumerable<ResourceLoadTarget> targets)
	{
		if (targets == null) throw new ArgumentNullException(nameof(targets));
		List<AssetLoadHandle> handles = targets
			.Select(target => target ?? throw new ArgumentException("ロード対象に null が含まれています。", nameof(targets)))
			.Select(target => new AssetLoadHandle(target.Path, target.AssetType))
			.ToList();
		return new AssetLoadBatchHandle(handles);
	}

	static IEnumerable<ResourceLoadTarget> CreateTargets<T>(IEnumerable<string> paths) where T : UnityEngine.Object
	{
		if (paths == null) throw new ArgumentNullException(nameof(paths));
		return paths.Select(ResourceLoadTarget.Create<T>);
	}

	static string CreateNotFoundMessage(AssetLoadHandle handle)
	{
		return $"Resources にアセットが見つかりません。Path: '{handle.Path}', Type: {handle.AssetType.Name}";
	}
}
