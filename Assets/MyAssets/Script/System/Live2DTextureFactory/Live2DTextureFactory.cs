using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 最大四体のLive2Dを専用テクスチャとして生成する常駐窓口
/// </summary>
public sealed class Live2DTextureFactory : MonoBehaviour
{
    public static Live2DTextureFactory Instance { get; private set; }
    public const int Capacity = 4;
    public bool IsInitialized { get; private set; }

    [SerializeField] Live2DTextureSettings defaultSettings = new Live2DTextureSettings();
    readonly int[] layers = new int[Capacity];
    readonly Live2DTextureUnit[] units = new Live2DTextureUnit[Capacity];
    readonly Live2DTextureHandle[] handles = new Live2DTextureHandle[Capacity];
    int live2DLayerMask;
    Transform factoryTransform;

    /// <summary>
    /// 単一のFactoryを登録する
    /// </summary>
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        factoryTransform = transform;
    }

    /// <summary>
    /// GameInitializerから予約レイヤーと描画イベントを初期化する
    /// </summary>
    public void Initialize()
    {
        if (IsInitialized || Instance != this) return;
        live2DLayerMask = 0;
        for (int i = 0; i < Capacity; i++)
        {
            layers[i] = LayerMask.NameToLayer("Live2D_" + i);
            if (layers[i] < 0) throw new InvalidOperationException("Live2D_" + i + "レイヤーが未登録です。");
            live2DLayerMask |= 1 << layers[i];
        }
        IsInitialized = true;
        Camera.onPreCull += HandleCameraPreCull;
        Camera.onPostRender += HandleCameraPostRender;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    /// <summary>
    /// 空き枠にLive2Dを生成し、満杯の場合は失敗を返す
    /// </summary>
    /// <param name="modelPrefab"></param>
    /// <param name="handle"></param>
    /// <param name="settings"></param>
    public bool TryCreate(GameObject modelPrefab, out Live2DTextureHandle handle, Live2DTextureSettings settings = null)
    {
        handle = null;
        if (!IsInitialized) throw new InvalidOperationException("Factoryが初期化されていません。");
        if (modelPrefab == null) throw new ArgumentNullException(nameof(modelPrefab));
        settings = settings ?? defaultSettings;
        settings.Validate();
        int slot = Array.FindIndex(handles, item => item == null);
        if (slot < 0) return false;

        GameObject root = new GameObject("Live2D_" + slot);
        root.SetActive(false);
        root.transform.SetParent(factoryTransform, false);
        Live2DTextureUnit unit = root.AddComponent<Live2DTextureUnit>();
        try
        {
            unit.Initialize(modelPrefab, settings, layers[slot]);
            units[slot] = unit;
            handle = new Live2DTextureHandle(this, unit);
            handles[slot] = handle;
            root.SetActive(true);
            if (settings.InitialAnimation != null)
                unit.PlayAnimation(settings.InitialAnimation, settings.Loop, 1f);
            return true;
        }
        catch
        {
            unit.Release();
            Destroy(root);
            handles[slot]?.Invalidate();
            units[slot] = null;
            handles[slot] = null;
            handle = null;
            throw;
        }
    }

    /// <summary>
    /// 指定ハンドルのリソースを解放して枠を返却する
    /// </summary>
    /// <param name="handle"></param>
    internal void Release(Live2DTextureHandle handle)
    {
        int slot = Array.IndexOf(handles, handle);
        if (slot < 0) return;
        Live2DTextureUnit unit = units[slot];
        if (unit != null)
        {
            unit.Release();
            Destroy(unit.gameObject);
        }
        handle.Invalidate();
        units[slot] = null;
        handles[slot] = null;
    }

    /// <summary>
    /// Built-inのカリング前に予約レイヤーを除外する
    /// </summary>
    /// <param name="camera"></param>
    void HandleCameraPreCull(Camera camera)
    {
        ExcludeLive2DLayers(camera);
    }

    /// <summary>
    /// SRPのカメラ描画前に予約レイヤーを除外する
    /// </summary>
    /// <param name="context"></param>
    /// <param name="camera"></param>
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        ExcludeLive2DLayers(camera);
    }

    /// <summary>
    /// Factory配下以外の実行時カメラから予約レイヤーを除外する
    /// </summary>
    /// <param name="camera"></param>
    void ExcludeLive2DLayers(Camera camera)
    {
        if (camera == null || camera.cameraType == CameraType.SceneView || camera.cameraType == CameraType.Preview)
            return;
        // 専用カメラは保持済み参照で判定し、外部カメラだけ親階層を確認する。
        for (int i = 0; i < Capacity; i++)
            if (units[i] != null && units[i].RenderCamera == camera) return;
        if (camera.transform.IsChildOf(factoryTransform)) return;
        camera.cullingMask &= ~live2DLayerMask;
    }

    /// <summary>
    /// Built-inの描画完了をハンドルへ反映する
    /// </summary>
    /// <param name="camera"></param>
    void HandleCameraPostRender(Camera camera)
    {
        MarkRendered(camera);
    }

    /// <summary>
    /// SRPの描画完了をハンドルへ反映する
    /// </summary>
    /// <param name="context"></param>
    /// <param name="camera"></param>
    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        MarkRendered(camera);
    }

    /// <summary>
    /// 対応する描画ユニットを表示可能にする
    /// </summary>
    /// <param name="camera"></param>
    void MarkRendered(Camera camera)
    {
        for (int i = 0; i < Capacity; i++)
            if (units[i] != null && units[i].RenderCamera == camera) units[i].MarkRendered();
    }

    /// <summary>
    /// 終了時にイベントと全ハンドルを解放する
    /// </summary>
    void OnDestroy()
    {
        if (Instance != this) return;
        Camera.onPreCull -= HandleCameraPreCull;
        Camera.onPostRender -= HandleCameraPostRender;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        for (int i = 0; i < Capacity; i++)
            if (handles[i] != null) Release(handles[i]);
        IsInitialized = false;
        Instance = null;
    }
}
