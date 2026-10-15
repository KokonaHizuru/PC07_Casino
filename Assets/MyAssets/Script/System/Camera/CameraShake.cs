using UnityEngine;

/// <summary>
/// 初期ローカル座標を基準に位置と回転を揺らします。
/// カメラ追従と併用する場合は、追従用Transformの子に配置してください。
/// </summary>
[DisallowMultipleComponent]
public class CameraShake : MonoBehaviour
{
    [SerializeField] SoCameraShakeData shakeData;

    Transform cachedTransform;
    Vector3 defaultPosition;
    Quaternion defaultRotation;
    Vector3 positionOffset;
    Vector3 rotationOffset;
    Vector3 fromPosition;
    Vector3 fromRotation;
    Vector3 toPosition;
    Vector3 toRotation;
    Vector3 positionStrength;
    Vector3 rotationStrength;
    float duration;
    float frequency;
    float elapsed;
    float segmentTime;
    bool fadeOut;
    bool useUnscaledTime;

    public bool IsPlaying { get; private set; }

    /// <summary>
    /// 使用するTransformと初期位置・回転を保持します。
    /// </summary>
    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// Awakeより先に呼び出された場合も含め、初回だけ参照と初期状態を取得します。
    /// </summary>
    void Initialize()
    {
        if (cachedTransform != null) return;
        cachedTransform = transform;
        defaultPosition = cachedTransform.localPosition;
        defaultRotation = cachedTransform.localRotation;
    }

    /// <summary>
    /// 設定済みデータで揺れを開始し、再生時間をリセットします。
    /// </summary>
    /// <param name="continueCurrentShake">再生中なら現在の揺れ位置・回転から継続し、falseなら初期状態から再開します。</param>
    public void Play(bool continueCurrentShake = true)
    {
        if (!isActiveAndEnabled || shakeData == null) return;
        Initialize();

        if (!continueCurrentShake || !IsPlaying)
        {
            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
            RestoreDefault();
        }

        // 再生中に共有アセットを書き換えても、その再生の設定は維持します。
        duration = Mathf.Max(0.01f, shakeData.duration);
        frequency = Mathf.Max(0.01f, shakeData.frequency);
        positionStrength = shakeData.positionStrength;
        rotationStrength = shakeData.rotationStrength;
        fadeOut = shakeData.fadeOut;
        useUnscaledTime = shakeData.useUnscaledTime;
        elapsed = 0f;
        segmentTime = 0f;
        fromPosition = positionOffset;
        fromRotation = rotationOffset;
        ChooseTarget();
        IsPlaying = true;
    }

    /// <summary>
    /// 指定したデータを設定して揺れを開始します。
    /// </summary>
    /// <param name="data">今回から使用する揺れ設定。</param>
    /// <param name="continueCurrentShake">再生中の位置・回転を引き継ぐかどうか。</param>
    public void Play(SoCameraShakeData data, bool continueCurrentShake = true)
    {
        if (data == null) return;
        shakeData = data;
        Play(continueCurrentShake);
    }

    /// <summary>
    /// 揺れの目標間を補間し、位置と回転に反映します。
    /// </summary>
    void LateUpdate()
    {
        if (!IsPlaying) return;
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        elapsed += deltaTime;
        if (elapsed >= duration)
        {
            Stop();
            return;
        }

        segmentTime += deltaTime * frequency;
        if (segmentTime >= 1f)
        {
            // 大きなフレーム落ちでも大量の乱数生成をせず、次の目標へ進めます。
            segmentTime %= 1f;
            fromPosition = toPosition;
            fromRotation = toRotation;
            ChooseTarget();
        }

        float t = Mathf.SmoothStep(0f, 1f, segmentTime);
        float envelope = fadeOut ? Mathf.Clamp01(1f - elapsed / duration) : 1f;
        positionOffset = Vector3.Lerp(fromPosition, toPosition, t) * envelope;
        rotationOffset = Vector3.Lerp(fromRotation, toRotation, t) * envelope;
        cachedTransform.localPosition = defaultPosition + positionOffset;
        cachedTransform.localRotation = defaultRotation * Quaternion.Euler(rotationOffset);
    }

    /// <summary>
    /// 設定された振幅から次の揺れ目標を決定します。
    /// </summary>
    void ChooseTarget()
    {
        toPosition = RandomOffset(positionStrength);
        toRotation = RandomOffset(rotationStrength);
    }

    /// <summary>
    /// 指定した各軸の範囲内でランダムな変位を生成します。
    /// </summary>
    /// <param name="strength">各軸の最大揺れ幅。</param>
    static Vector3 RandomOffset(Vector3 strength)
    {
        return Vector3.Scale(strength, new Vector3(
            Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
    }

    /// <summary>
    /// 揺れを停止して初期位置・回転へ戻します。
    /// </summary>
    public void Stop()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        positionOffset = Vector3.zero;
        rotationOffset = Vector3.zero;
        RestoreDefault();
    }

    /// <summary>
    /// 初期ローカル位置・回転を復元します。
    /// </summary>
    void RestoreDefault()
    {
        cachedTransform.SetLocalPositionAndRotation(defaultPosition, defaultRotation);
    }

    /// <summary>
    /// 無効化時に揺れを停止し、変位を残さないようにします。
    /// </summary>
    void OnDisable()
    {
        Stop();
    }
}
