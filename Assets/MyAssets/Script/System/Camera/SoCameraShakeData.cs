using UnityEngine;

/// <summary>
/// カメラ揺れの時間、強さ、再生方法を保持します。
/// </summary>
[CreateAssetMenu(fileName = "SoCameraShakeData", menuName = "Scriptable Objects/SoCameraShakeData")]
public class SoCameraShakeData : ScriptableObject
{
    [Min(0.01f), Tooltip("揺れの再生時間（秒）")]
    public float duration = 0.4f;

    [Tooltip("各軸の位置の最大揺れ幅（ローカル座標）")]
    public Vector3 positionStrength = new Vector3(0.15f, 0.15f, 0f);

    [Tooltip("各軸の回転の最大揺れ幅（度）")]
    public Vector3 rotationStrength = new Vector3(0f, 0f, 1f);

    [Min(0.01f), Tooltip("1秒あたりに揺れの目標を切り替える回数")]
    public float frequency = 25f;

    [Tooltip("終了に向けて揺れ幅を小さくする")]
    public bool fadeOut = true;

    [Tooltip("Time.timeScaleが0のときも再生する")]
    public bool useUnscaledTime = false;
}
