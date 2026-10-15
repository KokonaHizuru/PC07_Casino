# 共通カード・山札・UI手札

## 動作確認

`Assets/Scenes/Scene_HightAndLow.unity` を開いてPlayすると、`CardHandDebug` が52枚の山札をシャッフルし、自分へ表向き3枚、相手へ裏向き3枚を配る。相手の手札は操作不可。

自分のカードはマウスオーバーで28px持ち上がり、左ボタンのドラッグでカードの中心がカーソルへ追従する。ドロップ先はまだ設定していないため、離すと0.25秒で手札へ戻る。戻り中は再ドラッグできない。時間停止中もUIアニメーションは動作する。

これは共通基盤の確認用であり、ハイカード・デュエルの実際の配布・公開順・BET・勝敗は実装していない。

## 他シーンへの配置

プレハブは `Assets/MyAssets/Prefabs/Cards` にある。

- `CardTableUI.prefab`：Canvas、PlayerHand、DealerHand、DragLayerのセット。別シーンへ配置して使用できる。デバッグ配布処理は含まない。
- `CardHandUI.prefab`：既存Canvas内へ追加する手札単体。
- `CardUI.prefab`：手札が生成するカード1枚。幅180、高さ約251。3Dカードの6.3:8.8に合わせている。
- `CasinoCardArtwork.asset`：Casinoのスート別テクスチャと裏面模様の参照、UV範囲。

CanvasにはGraphicRaycaster、シーンには使用中の入力方式に対応するEventSystemが必要。サンプルシーンでは既存プロジェクトのBoth設定に合わせてStandaloneInputModuleを使用する。既存EventSystemがあるシーンで重複作成しない。

単体手札は最初の初期化時に親Canvasを取得し、ルートCanvasをドラッグ先にする。必要なら `SetDragLayer(RectTransform)` またはInspectorのDrag Layerへ、同じCanvas内の最前面の空UIを指定する。レイアウトを変更する際は、ドラッグ先をMaskの外へ配置するとカードが切れない。

## ゲーム側から使用するAPI

名前空間は `Casino.Cards`。共通スクリプトは `Assets/MyAssets/Script/MyGame/Cards`。

```csharp
var deck = new CardDeck();
deck.Shuffle();
if (deck.TryDraw(out PlayingCard card))
    playerHand.AddCard(card, faceUp: true);
```

- `CardDeck`：生成時に52枚を用意する。`Shuffle()` は残りのカードだけを並べ替える。`TryDraw(out card)` は空ならfalseを返し、自動補充しない。`Reset()` は52枚へ戻すので、ゲーム側でラウンドの切り替え時に明示的に呼ぶ。再現用に `new CardDeck(seed)` も使える。
- `PlayingCard`：`Suit` と `Rank` を保持。A=1、J=11、Q=12、K=13という識別情報であり、ゲーム固有の点数ではない。ハイカードのA=14などは各ゲームで変換する。
- `CardHandUI`：`AddCard`、`RemoveCard`、`Clear`、`Count`、`GetCard(index)`。同一手札への重複追加は禁止。別の手札との重複は、同じ山札から配布して防ぐ。
- `GetCard(index).SetFaceUp(bool)`：表裏を切り替える。
- `SetInteractable(bool)`：操作可否を設定する。falseへの変更時は進行中のドラッグも中止する。
- `SelectedCard`、`SelectionChanged`：ホバー選択を取得・購読する。選択解除時はnullを通知する。

## プレイ範囲の接続

後からCanvas内に空のUIオブジェクトを用意し、その `RectTransform` を設定する。Imageやドロップ専用コンポーネントは不要。

```csharp
playerHand.SetPlayArea(playAreaRect);
playerHand.CardPlayed += OnCardPlayed;
```

InspectorのPlay Areaへ直接割り当ててもよい。実行中に参照先を差し替える場合は `SetPlayArea` を使う。nullでプレイ判定を無効化する。

判定基準はドロップしたカーソル位置であり、カード全体の収まりではない。範囲内なら、カードを手札から除外して表示を破棄した後、`CardPlayed(PlayingCard)` を1回通知する。場札表示、勝敗処理、次のターンへの進行はゲーム側が通知を受けて行う。UIにImageがなくても判定でき、無効化された範囲にはプレイできない。Screen Space Camera/World Spaceでは範囲を含むCanvasにWorld Cameraを設定する。

購読したイベントはゲーム側の終了・無効化時に解除する。プレイを一時禁止する場合は `SetInteractable(false)`、範囲へのドロップだけを無効にする場合は `SetPlayArea(null)` を使う。

## 素材と調整

表面は `Assets/Casino/Textures/PlayingCards/T_PC{Club,Diamond,Heart,Spade}_BC.png`、裏面は `T_PCards_M.png` の模様をRawImageで表示する。元のテクスチャやインポート設定は変更していない。UVは3Dプレハブが参照するFBXの第2UVチャンネルを基準とする。

持ち上げ量・ホバー時間・戻り時間はCardUIプレハブのCardView、間隔は手札のSpacing、位置やサイズは各RectTransformで調整できる。手札枚数が増えると手札領域の幅に合わせて間隔を詰める。

今回専用の `Assets/MyAssets/Script/MyGame/Debug/CardHandDebug.cs` はシーンの独立オブジェクトにだけ付いている。本番の配布処理へ切り替える際は、このオブジェクトを外す。
