# Live2DTextureFactory

GameInitializerがResources直下のLive2DTextureFactoryプレハブを生成し、
GameInstanceと同じ経路でDontDestroyOnLoadとInitializeを適用します。

## 使用方法

Unityのメインスレッドから、CubismModelを含むプレハブを渡します。
生成時にモデルの子オブジェクトも含めて予約レイヤーへ設定します。

```csharp
Live2DTextureHandle handle;

var settings = new Live2DTextureSettings
{
    Width = 1024,
    Height = 1024,
    OrthographicSize = 1.5f,
    ViewCenter = new Vector2(0f, 0.5f),
    InitialAnimation = animationClip,
    Loop = true
};

if (!Live2DTextureFactory.Instance.TryCreate(modelPrefab, out handle, settings))
    return; // 使用中のモデルが4体の場合。

// Textureは同じ参照のまま更新される。初回描画前は透明。
rawImage.texture = handle.Texture;

// 初回描画が必要な処理はhandle.IsReadyがtrueになるまで待つ。
handle.PlayAnimation(otherClip, loop: true);

// 表示終了時。シーン終了時にも取得側が必ず実行する。
rawImage.texture = null;
handle.Dispose();
handle = null;
```

- 設定を省略するとFactoryプレハブのdefaultSettingsを使用します。
- 独立したモデルは最大4体。同じモデルのプレハブも複数生成できます。
- 満杯の場合はTryCreateがfalse、handleがnullになります。
- 不正な引数・モデル・描画先の生成失敗は例外になります。確保途中の枠は解放します。
- ハンドルは取得側が保持し、OnDestroyなどでDisposeしてください。共有参照カウントはありません。
- Disposeは複数回呼べます。破棄後のTexture取得・アニメーション変更はObjectDisposedExceptionになります。
- Textureを利用側でRelease/Destroyしないでください。
- Factory配下のTransform、カメラ、レイヤーは外部から変更しないでください。

## 描画

Live2D_0〜Live2D_3を専用予約し、各カメラは割り当てられたレイヤーだけを描画します。
Factory配下以外のカメラには描画前に4レイヤーの除外を適用します。
SceneビューとPreviewカメラには適用しません。
URPのbeginCameraRendering / endCameraRenderingとBuilt-inのonPreCull / onPostRenderに対応します。
URPの標準イベントを発火しない独自の手動描画経路は対象外です。

カメラは正投影、透明背景、HDR・MSAA・ポストプロセスなしです。
撮影中心はユニットのローカルXY座標、カメラZは-10、描画範囲はnear=0.01〜far=100です。
モデルプレハブのローカルTransformは保持します。構図はモデルごとに設定してください。
RenderTextureはARGB32です。半透明部分の合成は表示先マテリアルにも依存します。
通常の透明合成で生成されたRGBはプリマルチプライドになるため、
輪郭が暗くなる場合は表示先で対応する合成マテリアルを使用してください。

## アニメーション

既存のDebugLive2DControllerと同様にAnimationClipを直接再生します。
複製したモデルのAnimatorControllerとCubismのMotion/Fade制御を無効にし、
独自のPlayableGraphで再生位置を制御します。元プレハブは変更しません。
InitialAnimationを省略した場合は、クリップを指定するまで初期姿勢で表示します。
ループ、非ループ（終端姿勢を保持）、正の再生速度を指定できます。
フェード・Animatorステート遷移は今回のAPIの対象外です。

## Unity上での確認項目

1. 任意のシーンから起動し、DontDestroyOnLoadにFactoryが1つだけ存在すること。
2. 同じモデルを含む4体を生成し、専用カメラとTextureがそれぞれ独立していること。
3. 5体目がfalseになり、1体を破棄した同フレームでも新たに生成できること。
4. 実行中に生成したカメラやURP Overlayカメラからモデル本体が見えないこと。
5. 初回描画後にIsReadyがtrueになり、クリップ切り替え・ループ・終端保持が動くこと。
6. 表示先で背景透過、輪郭、マスク、物理演算、構図に問題がないこと。
7. シーンを切り替えても保持したハンドルが有効で、Disposeしたモデルが残らないこと。
