# 共通の配布・捨て札演出

3ゲームのControllerは初期化時に子の `TableCardPresenter`（ハイカードは派生の `DuelTableCards`）を一度保持する。Inspectorの `Hand Transfers` に別の表示先を明示することもできる。

## 配布

`yield return presenter.DrawToHand(hand, card, player, index, faceUp)` で、通常視点への移動、DeckAnchorから手元への3D配布、UI手札への挿入アニメーションを順に待つ。3D配布は弧を描かず滑らかに直進し、裏向きを保つ。UIの表裏はfaceUpで指定する。ディーラー（player=false）は3D配布とUI追加アニメーションを省き、即時追加する。途中で再試行しても同じカードを二重生成しない。

共通手札の `AddCardAnimated(card, faceUp, index, player)` はUI追加だけにも利用できる。既存の `AddCard` は即時追加用として維持している。演出中の入力禁止とゲーム進行待機は呼び出し元で行う。

## 破棄

`yield return presenter.DiscardFromHand(hand, card, player)` でUIの札を即座に削除してから、手元からDiscardAnchorへ裏向きの3Dカードを移動する。捨て札は少しずつ高さをずらして残す。既にドラッグでUIから取り除かれた札にも使用できる。公開カードを机へ出す操作は既存のPlaceを使用し、捨て札にはしない。

`Clear()` は次ラウンド開始時に場札・配布中のモデル・捨て札・再試行状態をまとめて片付ける。`DiscardCount` は捨て札数、`TableCardCount` は通常の場札数。モデル親のchildCountには演出中のカードと捨て札も含まれるため、通常の場札数には使用しない。

## 位置と時間の調整

- 各シーンの `DeckAnchor`：対面のディーラー側にある配布開始位置。ここからプレイヤーの手元へ直進する。
- `DiscardAnchor`：捨て札の位置と机面の向き。
- 既存の `PlayerOrigin` / `DealerOrigin`：各手元の受け取り・投げ始め位置。
- TableCardPresenterの `Draw Transfer Duration`：3D配布の移動時間（0.25秒）。`Transfer Duration` / `Transfer Arc` / `Discard Thickness`：捨て札の移動時間（0.45秒）・弧の高さ・積み幅。
- `Draw Surface Clearance`：場札の基準面から配布カードを浮かせる高さ（既定0.008）。配布の開始・終了をこの高さへ揃える。DeckAnchorとPlayerOriginは配布方向・水平位置を指定し、配布時の高さはこの値を使う。
- 各CardHandUIの `Draw Duration` / `Draw Offset`：プレイヤーのUIへ入る時間（0.15秒）・開始位置からのずれ。ディーラーのUI追加は即時。

既存シーンにはアンカーを設定済み。新しいシーンでアンカー未指定の場合、配布開始位置はDealerOrigin、捨て札位置は最初のプレイヤー用スロットを基準に起動時に作る。任意のTransformを設定すればその位置を使用する。

ポーカーは初期配布・交換に、ハイカードは初期配布に、ブラックジャックは初期配布と双方の追加ドローに接続済み。現在破棄するルールはポーカーの交換だけで、他ゲームに新たな破棄ルールは追加していない。
