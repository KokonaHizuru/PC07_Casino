using System;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.MotionFade;
using Live2D.Cubism.Rendering;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Live2D一体分のモデル、カメラ、テクスチャを管理する
/// </summary>
[DefaultExecutionOrder(-10000)]
[AddComponentMenu("")]
public sealed class Live2DTextureUnit : MonoBehaviour
{
    public Camera RenderCamera { get; private set; }
    public RenderTexture Texture { get; private set; }
    public bool IsReady { get; private set; }

    Animator animator;
    PlayableGraph animationGraph;
    AnimationClipPlayable clipPlayable;
    float clipLength;
    float playbackSpeed;
    double animationTime;
    bool loopAnimation;
    bool released;

    /// <summary>
    /// 非アクティブな描画ユニットにモデルと描画先を構築する
    /// </summary>
    /// <param name="prefab"></param>
    /// <param name="settings"></param>
    /// <param name="layer"></param>
    internal void Initialize(GameObject prefab, Live2DTextureSettings settings, int layer)
    {
        GameObject modelObject = Instantiate(prefab, transform, false);
        modelObject.name = prefab.name;
        modelObject.SetActive(true);
        CubismModel model = modelObject.GetComponentInChildren<CubismModel>(true);
        if (model == null) throw new ArgumentException("CubismModelを含むプレハブを指定してください。", nameof(prefab));
        animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.gameObject.AddComponent<Animator>();
        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;

        // 既存のデバッグ表示と同様に、クリップを直接再生してSDK側との二重更新を防ぐ。
        foreach (CubismMotionController motion in modelObject.GetComponentsInChildren<CubismMotionController>(true))
            motion.enabled = false;
        foreach (CubismFadeController fade in modelObject.GetComponentsInChildren<CubismFadeController>(true))
            fade.enabled = false;
        animator.runtimeAnimatorController = null;

        foreach (Transform child in modelObject.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
        foreach (Camera embeddedCamera in modelObject.GetComponentsInChildren<Camera>(true))
            embeddedCamera.enabled = false;

        Texture = new RenderTexture(settings.Width, settings.Height, 24, RenderTextureFormat.ARGB32)
        {
            name = name + "_Texture",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        if (!Texture.Create()) throw new InvalidOperationException("RenderTextureを生成できませんでした。");
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = Texture;
            GL.Clear(true, true, Color.clear);
        }
        finally { RenderTexture.active = previous; }

        GameObject cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.localPosition = new Vector3(settings.ViewCenter.x, settings.ViewCenter.y, -10f);
        RenderCamera = cameraObject.AddComponent<Camera>();
        RenderCamera.orthographic = true;
        RenderCamera.orthographicSize = settings.OrthographicSize;
        RenderCamera.nearClipPlane = 0.01f;
        RenderCamera.farClipPlane = 100f;
        RenderCamera.clearFlags = CameraClearFlags.SolidColor;
        RenderCamera.backgroundColor = Color.clear;
        RenderCamera.cullingMask = 1 << layer;
        RenderCamera.targetTexture = Texture;
        RenderCamera.allowHDR = false;
        RenderCamera.allowMSAA = false;
        RenderCamera.useOcclusionCulling = false;
        RenderCamera.depth = -100f;
        UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.volumeLayerMask = 0;
        cameraData.antialiasing = AntialiasingMode.None;
        foreach (CubismRenderController controller in modelObject.GetComponentsInChildren<CubismRenderController>(true))
            controller.CameraToFace = RenderCamera;
    }

    /// <summary>
    /// 指定クリップを先頭から再生する
    /// </summary>
    /// <param name="clip"></param>
    /// <param name="loop"></param>
    /// <param name="speed"></param>
    internal void PlayAnimation(AnimationClip clip, bool loop, float speed)
    {
        if (released) throw new ObjectDisposedException(nameof(Live2DTextureUnit));
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        if (clip.legacy) throw new ArgumentException("Animator用のクリップを指定してください。", nameof(clip));
        if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
            throw new ArgumentOutOfRangeException(nameof(speed));
        StopGraph();
        animationGraph = PlayableGraph.Create(name + "_Animation");
        animationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        clipPlayable = AnimationClipPlayable.Create(animationGraph, clip);
        clipPlayable.SetSpeed(0);
        AnimationPlayableOutput.Create(animationGraph, "Live2D", animator).SetSourcePlayable(clipPlayable);
        clipLength = clip.length;
        playbackSpeed = speed;
        loopAnimation = loop;
        animationTime = 0;
        animationGraph.Play();
        animationGraph.Evaluate(0f);
    }

    /// <summary>
    /// パラメータ復元後、Cubismの保存・物理演算より前にクリップを評価する
    /// </summary>
    void LateUpdate()
    {
        if (!animationGraph.IsValid()) return;
        // CubismModel.Updateによる復元が済んでから評価する。
        // 実行順を早めたLateUpdateで、CubismUpdateControllerの保存・物理演算へ渡す。
        animationTime += Time.deltaTime * playbackSpeed;
        if (clipLength > 0f)
            animationTime = loopAnimation ? animationTime % clipLength : Math.Min(animationTime, clipLength);
        else
            animationTime = 0;
        clipPlayable.SetTime(animationTime);
        animationGraph.Evaluate(0f);
    }

    /// <summary>
    /// 初回の描画完了を記録する
    /// </summary>
    internal void MarkRendered()
    {
        if (!released) IsReady = true;
    }

    /// <summary>
    /// 描画を停止してからリソースを解放する
    /// </summary>
    internal void Release()
    {
        if (released) return;
        released = true;
        IsReady = false;
        gameObject.SetActive(false);
        StopGraph();
        if (RenderCamera != null) RenderCamera.targetTexture = null;
        if (Texture != null)
        {
            Texture.Release();
            Destroy(Texture);
            Texture = null;
        }
    }

    /// <summary>
    /// アニメーション再生用グラフを破棄する
    /// </summary>
    void StopGraph()
    {
        if (animationGraph.IsValid()) animationGraph.Destroy();
    }

    /// <summary>
    /// オブジェクト破棄時にも描画リソースを解放する
    /// </summary>
    void OnDestroy()
    {
        Release();
    }
}
