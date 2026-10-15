using System;
using UnityEngine;

/// <summary>
/// 生成したLive2Dの描画と寿命を操作するハンドル
/// </summary>
public sealed class Live2DTextureHandle : IDisposable
{
    Live2DTextureFactory factory;
    Live2DTextureUnit unit;

    public bool IsDisposed => unit == null;
    public bool IsReady => unit != null && unit.IsReady;
    public RenderTexture Texture => GetUnit().Texture;

    /// <summary>
    /// 描画ユニットの操作権を保持する
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="unit"></param>
    internal Live2DTextureHandle(Live2DTextureFactory factory, Live2DTextureUnit unit)
    {
        this.factory = factory;
        this.unit = unit;
    }

    /// <summary>
    /// アニメーションクリップを切り替えて再生する
    /// </summary>
    /// <param name="clip"></param>
    /// <param name="loop"></param>
    /// <param name="speed"></param>
    public void PlayAnimation(AnimationClip clip, bool loop = true, float speed = 1f)
    {
        GetUnit().PlayAnimation(clip, loop, speed);
    }

    /// <summary>
    /// モデルと描画リソースを解放する
    /// </summary>
    public void Dispose()
    {
        if (factory != null) factory.Release(this);
        Invalidate();
    }

    /// <summary>
    /// Factoryによる解放後にハンドルを無効化する
    /// </summary>
    internal void Invalidate()
    {
        factory = null;
        unit = null;
    }

    /// <summary>
    /// 有効な描画ユニットを取得する
    /// </summary>
    Live2DTextureUnit GetUnit()
    {
        if (unit == null) throw new ObjectDisposedException(nameof(Live2DTextureHandle));
        return unit;
    }
}
