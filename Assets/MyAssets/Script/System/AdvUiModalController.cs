using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utage;

/// <summary>
/// 全シーンの通常UIを宴カメラより先に描画し、宴表示中の背面入力を制限する
/// </summary>
[DefaultExecutionOrder(-10000)]
public class AdvUiModalController : MonoBehaviour
{
    readonly Dictionary<Canvas, Camera> convertedCanvases = new();
    readonly Dictionary<GameObject, int> changedLayers = new();
    readonly HashSet<BaseRaycaster> blockedRaycasters = new();
    readonly HashSet<Selectable> blockedSelectables = new();
    readonly HashSet<ScrollRect> blockedScrollRects = new();
    AdvEngine engine;
    Transform advRoot;
    bool requested;
    int releaseFrame = -1;
    public Camera UiCamera { get; private set; }
    public bool IsBlocking { get; private set; }

    /// <summary>
    /// 共通UIカメラと宴への参照を初期化する
    /// </summary>
    /// <param name="advEngine">入力を優先する宴エンジン</param>
    public void Initialize(AdvEngine advEngine)
    {
        engine = advEngine;
        advRoot = engine.transform.root;
        GameObject cameraObject = new GameObject("Game UI Camera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.position = new Vector3(0, 0, -10);
        UiCamera = cameraObject.AddComponent<Camera>();
        UiCamera.orthographic = true;
        UiCamera.orthographicSize = 5f;
        UiCamera.nearClipPlane = 0.1f;
        UiCamera.farClipPlane = 100f;
        UiCamera.cullingMask = 1 << 5;
        UiCamera.clearFlags = CameraClearFlags.Depth;
        UiCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// シーン内および常駐UIを宴より先に描画する
    /// </summary>
    /// <param name="scene">追加されたシーン</param>
    /// <param name="mode">読み込み方式</param>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshCanvases();
        if (IsBlocking) BlockBackgroundInput();
    }

    /// <summary>
    /// Overlay形式の通常UIを共通UIカメラへ接続する
    /// </summary>
    public void RefreshCanvases()
    {
        if (UiCamera == null) return;
        // 実行中に新しいルートCanvasが生成されるため、宴表示中も追加分を検出する。
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (BelongsToAdv(canvas) || canvas.renderMode != RenderMode.ScreenSpaceOverlay || !canvas.isRootCanvas) continue;

            if (!convertedCanvases.ContainsKey(canvas)) convertedCanvases.Add(canvas, canvas.worldCamera);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = UiCamera;
            canvas.planeDistance = 1f;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (!changedLayers.ContainsKey(child.gameObject)) changedLayers.Add(child.gameObject, child.gameObject.layer);
                child.gameObject.layer = 5;
            }
        }
        if (IsBlocking)
        {
            // 開始前から継続中のドラッグも停止する。Raycasterの無効化だけでは既存のドラッグ先は解除されない。
            foreach (ScrollRect scroll in FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
            {
                if (!scroll.enabled || BelongsToAdv(scroll)) continue;
                blockedScrollRects.Add(scroll);
                scroll.StopMovement();
                scroll.enabled = false;
            }
        }
    }

    /// <summary>
    /// シナリオ開始フレームから背面入力を止める
    /// </summary>
    public void Begin()
    {
        requested = true;
        IsBlocking = true;
        releaseFrame = -1;
        RefreshCanvases();
        BlockBackgroundInput();
    }

    /// <summary>
    /// 終了操作の入力が背面へ流れないよう解除を予約する
    /// </summary>
    public void End()
    {
        requested = false;
        releaseFrame = Time.frameCount + 1;
    }

    /// <summary>
    /// シナリオ開始・終了の明示的な通知に従って入力制限を更新する
    /// </summary>
    void Update()
    {
        if (requested)
        {
            IsBlocking = true;
            releaseFrame = -1;
        }
        else if (IsBlocking)
        {
            if (releaseFrame < 0) releaseFrame = Time.frameCount + 1;
            if (Time.frameCount > releaseFrame) RestoreInput();
        }
        if (IsBlocking) BlockBackgroundInput();
    }

    /// <summary>
    /// 宴表示中に生成されたCanvasも描画前に接続する
    /// </summary>
    void LateUpdate()
    {
        if (!IsBlocking) return;
        RefreshCanvases();
        BlockBackgroundInput();
    }

    /// <summary>
    /// 対象が宴の階層に属するか判定する
    /// </summary>
    /// <param name="component">判定するコンポーネント</param>
    bool BelongsToAdv(Component component) => component != null && component.transform.IsChildOf(advRoot);

    /// <summary>
    /// 通常UIのポインター入力とボタンの決定・ナビゲーション入力を止める
    /// </summary>
    void BlockBackgroundInput()
    {
        // 有効オブジェクトの登録一覧を使い、モーダル中に追加されたUIも対象にする。
        List<BaseRaycaster> raycasters = RaycasterManager.GetRaycasters();
        for (int i = raycasters.Count - 1; i >= 0; i--)
        {
            BaseRaycaster raycaster = raycasters[i];
            if (raycaster == null || !raycaster.enabled || BelongsToAdv(raycaster)) continue;
            blockedRaycasters.Add(raycaster);
            raycaster.enabled = false;
        }
        foreach (Selectable selectable in Selectable.allSelectablesArray)
        {
            if (!selectable.interactable || BelongsToAdv(selectable)) continue;
            blockedSelectables.Add(selectable);
            selectable.interactable = false;
        }
        EventSystem events = EventSystem.current;
        if (events != null && events.currentSelectedGameObject != null
            && !events.currentSelectedGameObject.transform.IsChildOf(advRoot))
            events.SetSelectedGameObject(null);
    }

    /// <summary>
    /// この管理クラスが制限した入力だけを元へ戻す
    /// </summary>
    void RestoreInput()
    {
        foreach (BaseRaycaster raycaster in blockedRaycasters)
            if (raycaster != null) raycaster.enabled = true;
        foreach (Selectable selectable in blockedSelectables)
            if (selectable != null) selectable.interactable = true;
        foreach (ScrollRect scroll in blockedScrollRects)
            if (scroll != null) scroll.enabled = true;
        blockedRaycasters.Clear();
        blockedSelectables.Clear();
        blockedScrollRects.Clear();
        IsBlocking = false;
        releaseFrame = -1;
    }

    /// <summary>
    /// 管理終了時に入力制限を解除する
    /// </summary>
    void OnDisable()
    {
        requested = false;
        RestoreInput();
    }

    /// <summary>
    /// 宴シーン破棄時に常駐UIの設定と入力を復元する
    /// </summary>
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreInput();
        foreach (var pair in convertedCanvases)
        {
            if (pair.Key == null) continue;
            pair.Key.renderMode = RenderMode.ScreenSpaceOverlay;
            pair.Key.worldCamera = pair.Value;
        }
        foreach (var pair in changedLayers)
            if (pair.Key != null) pair.Key.layer = pair.Value;
    }
}
