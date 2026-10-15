using System;
using UnityEngine;

/// <summary>
/// Live2Dの生成時に使用する描画設定
/// </summary>
[Serializable]
public sealed class Live2DTextureSettings
{
    [Min(1)] public int Width = 1024;
    [Min(1)] public int Height = 1024;
    [Min(0.01f)] public float OrthographicSize = 1f;
    public Vector2 ViewCenter = Vector2.zero;
    public AnimationClip InitialAnimation;
    public bool Loop = true;

    /// <summary>
    /// 描画設定の値を検証する
    /// </summary>
    internal void Validate()
    {
        if (Width < 1 || Height < 1 || Width > SystemInfo.maxTextureSize || Height > SystemInfo.maxTextureSize)
            throw new ArgumentOutOfRangeException(nameof(Width), "テクスチャサイズが対応範囲外です。");
        if (float.IsNaN(OrthographicSize) || float.IsInfinity(OrthographicSize) || OrthographicSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(OrthographicSize));
        if (float.IsNaN(ViewCenter.x) || float.IsInfinity(ViewCenter.x) ||
            float.IsNaN(ViewCenter.y) || float.IsInfinity(ViewCenter.y))
            throw new ArgumentOutOfRangeException(nameof(ViewCenter));
    }
}
