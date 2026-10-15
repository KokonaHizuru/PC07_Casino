using System;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// キャラクターのSpine表示に適用する初期設定
/// </summary>
[Serializable]
public class SpineSettings
{
    [Tooltip("表示するSpineのスケルトンデータ")]
    public SkeletonDataAsset skeletonDataAsset;

    [Min(0.001f), Tooltip("表示用オブジェクトに適用する等倍基準のスケール。反転はFlipで指定する")]
    public float scale = 1f;

    [Tooltip("初期表示を左右反転する")]
    public bool flipX;

    [Tooltip("初期表示を上下反転する")]
    public bool flipY;

    [Tooltip("初期スキン名。空欄の場合はデフォルトスキンを使用する")]
    [SpineSkin(dataField: nameof(skeletonDataAsset), includeNone: true, defaultAsEmptyString: true)]
    public string initialSkinName = string.Empty;

    [Tooltip("トラック0で再生する初期アニメーション名。空欄の場合はセットアップポーズで表示する")]
    [SpineAnimation(dataField: nameof(skeletonDataAsset), includeNone: true)]
    public string initialAnimationName = string.Empty;

    [Tooltip("初期アニメーションをループ再生する")]
    public bool loop = true;

    [Min(0f), Tooltip("アニメーションの再生速度。1で通常速度、0で停止する")]
    public float timeScale = 1f;
}
