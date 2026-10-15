using Spine;
using Spine.Unity;
using UnityEngine;

[DisallowMultipleComponent]
public class SpineController : MonoBehaviour
{
    [SerializeField, Tooltip("操作する子のSpine。未設定の場合は初期化時に子から取得します。")]
    private SkeletonAnimation skeletonAnimation;
    [SerializeField, Tooltip("Spineと同じ倍率を適用する兄弟オブジェクト。CharacterではPointを指定します。")]
    private Transform[] additionalScaleTargets = System.Array.Empty<Transform>();
    private Vector3[] additionalBaseScales;

    private bool initializationAttempted;
    private bool referenceResolved;

    /// <summary>
    /// 子のSpineを初期化する
    /// </summary>
    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 操作対象を一度取得して初期化する。呼び出し元のAwakeからも利用できる
    /// </summary>
    public bool Initialize()
    {
        if (!initializationAttempted)
        {
            initializationAttempted = true;
            if (!ResolveReference()) return false;
            skeletonAnimation.Initialize(false);
            if (!skeletonAnimation.IsValid)
                Debug.LogError("Spineを初期化できません。SkeletonDataAssetの設定を確認してください。", this);
        }
        return skeletonAnimation != null && skeletonAnimation.IsValid;
    }

    /// <summary>
    /// 操作対象のSpine参照を一度取得する
    /// </summary>
    private bool ResolveReference()
    {
        if (!referenceResolved)
        {
            referenceResolved = true;
            if (skeletonAnimation == null)
            {
                foreach (Transform child in transform)
                {
                    skeletonAnimation = child.GetComponentInChildren<SkeletonAnimation>(true);
                    if (skeletonAnimation != null) break;
                }
            }
            if (skeletonAnimation == null || !skeletonAnimation.transform.IsChildOf(transform)
                || skeletonAnimation.transform == transform)
            {
                Debug.LogError("操作対象のSkeletonAnimationを子オブジェクトに配置してください。", this);
                skeletonAnimation = null;
            }
        }
        return skeletonAnimation != null;
    }

    /// <summary>
    /// 表示用オブジェクトへSpineの初期設定を適用する
    /// </summary>
    /// <param name="settings">適用するスケルトン・表示・アニメーションの初期設定</param>
    public bool Initialize(SpineSettings settings)
    {
        if (settings == null || settings.skeletonDataAsset == null)
        {
            Debug.LogError("Spineの初期設定またはSkeletonDataAssetが未設定です。", this);
            return false;
        }
        if (float.IsNaN(settings.scale) || float.IsInfinity(settings.scale) || settings.scale <= 0f
            || float.IsNaN(settings.timeScale) || float.IsInfinity(settings.timeScale) || settings.timeScale < 0f)
        {
            Debug.LogError("Spineのスケールは正の有限値、再生速度は0以上の有限値を指定してください。", this);
            return false;
        }
        SkeletonData data = settings.skeletonDataAsset.GetSkeletonData(false);
        if (data == null) return false;
        if (!string.IsNullOrEmpty(settings.initialSkinName) && data.FindSkin(settings.initialSkinName) == null)
        {
            Debug.LogError($"初期スキン '{settings.initialSkinName}' がありません。", this);
            return false;
        }
        if (!string.IsNullOrEmpty(settings.initialAnimationName) && data.FindAnimation(settings.initialAnimationName) == null)
        {
            Debug.LogError($"初期アニメーション '{settings.initialAnimationName}' がありません。", this);
            return false;
        }
        if (!ResolveReference()) return false;
        SkeletonRenderer renderer = skeletonAnimation.Renderer as SkeletonRenderer;
        if (renderer == null)
        {
            Debug.LogError("SkeletonAnimationと同じオブジェクトにSkeletonRendererが必要です。", this);
            return false;
        }
        renderer.SkeletonDataAsset = settings.skeletonDataAsset;
        renderer.InitialSkinName = settings.initialSkinName ?? string.Empty;
        renderer.InitialFlipX = settings.flipX;
        renderer.InitialFlipY = settings.flipY;
        transform.localScale = Vector3.one * settings.scale;
        ApplyAdditionalScale(settings.scale);
        skeletonAnimation.loop = settings.loop;
        skeletonAnimation.timeScale = settings.timeScale;
        skeletonAnimation.Initialize(true);
        if (!skeletonAnimation.IsValid) return false;
        skeletonAnimation.AnimationName = settings.initialAnimationName ?? string.Empty;
        skeletonAnimation.AnimationState.Apply(skeletonAnimation.Skeleton);
        initializationAttempted = true;
        return true;
    }

    /// <summary>追加対象の初期スケールを保持し、設定倍率を適用する</summary>
    /// <param name="scale">Spineの設定倍率</param>
    private void ApplyAdditionalScale(float scale)
    {
        if (additionalBaseScales == null)
        {
            additionalBaseScales = new Vector3[additionalScaleTargets.Length];
            for (int i = 0; i < additionalScaleTargets.Length; i++)
                if (additionalScaleTargets[i] != null) additionalBaseScales[i] = additionalScaleTargets[i].localScale;
        }
        for (int i = 0; i < additionalBaseScales.Length; i++)
            if (additionalScaleTargets[i] != null)
                additionalScaleTargets[i].localScale = additionalBaseScales[i] * scale;
    }

    /// <summary>
    /// 指定したアニメーションを再生する。同じ再生内容は再開始しない
    /// </summary>
    /// <param name="animationName">再生するアニメーション名</param>
    /// <param name="loop">ループ再生するか</param>
    /// <param name="restart">同じアニメーションでも先頭から再生するか</param>
    /// <param name="trackIndex">再生するトラック番号</param>
    public bool PlayAnimation(string animationName, bool loop = true, bool restart = false, int trackIndex = 0)
    {
        if (!Initialize())
            return false;

        if (trackIndex < 0 || string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning("アニメーション名と0以上のトラック番号を指定してください。", this);
            return false;
        }

        Spine.Animation animation = skeletonAnimation.Skeleton.Data.FindAnimation(animationName);
        if (animation == null)
        {
            Debug.LogWarning($"Spineにアニメーション '{animationName}' がありません。", this);
            return false;
        }

        TrackEntry current = skeletonAnimation.AnimationState.GetTrack(trackIndex);
        if (!restart && current != null && current.Animation == animation && current.Loop == loop)
            return true;

        skeletonAnimation.AnimationState.SetAnimation(trackIndex, animation, loop);
        return true;
    }

    /// <summary>
    /// 指定トラックの再生と予約を解除する。ポーズは初期状態に戻さない
    /// </summary>
    /// <param name="trackIndex">停止するトラック番号</param>
    public void StopAnimation(int trackIndex = 0)
    {
        if (!Initialize())
            return;

        if (trackIndex < 0)
        {
            Debug.LogWarning("0以上のトラック番号を指定してください。", this);
            return;
        }

        skeletonAnimation.AnimationState.ClearTrack(trackIndex);
    }

    /// <summary>
    /// Spineの水平反転を設定する。ScaleXの絶対値を保持し、trueで負、falseで正にする
    /// </summary>
    /// <param name="flip">水平反転するか</param>
    public void SetFlip(bool flip)
    {
        if (!Initialize())
            return;

        Skeleton skeleton = skeletonAnimation.Skeleton;
        float scale = Mathf.Abs(skeleton.ScaleX);
        skeleton.ScaleX = flip ? -scale : scale;
    }
}
