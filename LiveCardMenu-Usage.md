# LiveCardMenu

## シーン設定

- `LiveCardMenu` の `Live Card Prefab` に既存の `LiveCard.prefab` を設定する。
- 手札の幅・中心には、LiveCardMenuをアタッチしたオブジェクト自身のRectTransformを使用する。別途参照を設定する必要はない。
- `Display` は画面の範囲。空欄なら祖先の `Display`、なければ直近の親を使う。
- `Play Area` にプレイ確定用の RectTransform を設定する。左クリックを離した時点でカード中心がこの範囲内なら確定する。ドラッグで通過するだけでは確定しない。
- プレイ確定後はカード中心を `Play Area` のPivot位置に配置する。Pivotが(0.5, 0.5)なら中央、(0.5, 0)なら下辺中央となる。プレイ可否の判定は引き続き矩形全体で行う。
- `Play Card Scale` は配置されたカードの倍率。1で手札と同じ大きさ。Pivot位置への移動中に指定倍率まで変化し、待機・効果発動・上への退場でも維持する。
- `Start Visible` をオフにすると、初期カードを追加してから `Show` でまとめて登場させられる。
- Canvas の GraphicRaycaster と EventSystem が必要。カードの親には手札外への移動を遮る Mask / RectMask2D を置かない。
- 同じオブジェクトのLayoutGroupは実行時に無効化される。現在の LiveScene の CardMenu にある仮配置カードは、このクラスでは管理しないため、削除または非表示にしておく。
- 弧の高さは `Arc Height`、端の傾きは `Max Card Angle`、選択時の高さは `Selected Lift` で調整する。
- 最大枚数は `Max Card Count`（初期値10）で設定する。コードからは `menu.MaxCardCount = 15;` のように変更できる。0は追加不可、負数は0扱い。上限を下げても既存カードは削除せず、枚数が上限を下回るまで追加を制限する。

## 呼び出し例

### Backの高さ

LiveCardMenuのBackに対象のRectTransformを指定し、Back Open Height（開いた高さ）、
Back Closed Height（閉じた高さ）、Back Height Duration（変更秒数）を設定する。
Show/Hide開始と同時に高さを変更し、手札と背景の両方が完了してからコールバックを呼ぶ。
Start Visibleに応じて初期の高さも設定する。未指定なら従来どおり手札のみ動作する。
高さは位置やスケールではなくRectTransformの実寸。下端を固定するにはPivot Yを0にする。
対象の高さを制御するLayoutGroupやContentSizeFitterとの併用は避ける。
LiveSceneではDisplay直下のBackを設定済み。初期値は開380・閉0・0.35秒。

```csharp
// InspectorでStart Visibleをオフにしておく。
menu.onPlayCard += data => ApplyCardEffect(data);
menu.AddCard("SoLiveCard_Talk");
menu.AddCard("SoLiveCard_Talk");
menu.onSelectionCompleted += () => menu.Hide(() => OnHandHidden());
menu.Show(() =>
{
    OnHandShown();
    // 表示完了後に追加するカードは右からスライドする。
    // 戻り値がnullなら追加されていない。最大枚数到達、演出中、設定・ID不備の場合。
    UiLiveCard card = menu.AddCard("SoLiveCard_Talk");
});
```

`onPlayCard` はPlayAreaのPivot位置への移動と `Play Hold Duration` の待機後に通知される。
UiLiveCard.OnPlayCard → UiLiveCard.onPlayCard → LiveCardMenu.onPlayCard の順で転送するため、
同じ効果をカードとメニュー両方へ登録しない。

`onSelectionCompleted` は上への退場・手札からの削除後に、1枚のプレイにつき一度だけ通知する。
`onPlayCard` と同様に `+=` で一度登録すれば次のターンも維持される。解除は `-=` を使う。
完了後は非表示からの `Show` まで入力を受け付けない。次ターンは `Show` だけでよく、再登録は不要。
`BeginSelection` は廃止。旧コードの登録は `menu.onSelectionCompleted += 完了処理;` に置き換える。
`Show(Action)` / `Hide(Action)` は演出中に呼ばれると順に実行される。
既に要求した表示状態ならコールバックのみ呼ぶ。ドラッグ中の要求はドラッグ終了後に実行する。
非表示中のカード追加は登場演出を行わない。

矩形外でドラッグを離すと、光・最前面・角度ゼロの選択状態を保って戻る。
別カードへのマウスオーバーで選択が切り替わる。持ち上げによるちらつきを避けるため、
ポインターが外れるだけでは選択を解除しない。

UiLiveCard は単独でも利用可能。Initialize(string) と onClick を使う。
既存PrefabにButtonは不要。Buttonを追加する場合、OnClickをButtonにも重複登録しない。
LiveCard.prefabにはSelectionGlow（RectTransform・CanvasRenderer・UiCardGlow）を配置済み。
UiLiveCardのSelection Glowから参照し、選択時に表示する。実行時の自動生成は行わない。
独自のカードPrefabでも同様に子オブジェクトを配置して参照を設定する。未指定なら光は表示しない。
光はカード外周から透明へ減衰する。Bloomや画像素材は不要。
UiLiveCardのSelection Colorで色、Glow Spreadで広がり、Glow Corner Radiusで角丸、
Glow Intensityで強さ、Glow Fade Durationで選択・解除のフェード時間を調整する。
光はRaycast Targetを無効にしており、入力判定には影響しない。親のMaskの範囲外は表示されない。

LiveCardMenuのGameObjectを直接無効化すると演出はキャンセルされ、完了通知は行わない。
プレイ途中のカードは取り除く。通常の非表示には `Hide` を使う。

## Unityでの確認項目（未実施）

マウスオーバーが反応しない場合、再生中にLiveCardMenuコンポーネントのコンテキストメニューから
`Debug/Log Hand Raycast Targets`を実行する。各カード中央のRaycast結果がConsoleに表示される。
`[0]`が最優先の入力先。カードまたはカードの子以外なら、そのUIが入力を遮っている。
装飾だけのImageならRaycast Targetをオフにし、モーダルやADVのUIなら開閉状態を確認する。
Visible/Busy/SelectionFinished/InteractableとInputModuleも同時に出力する。

1. 1枚が中央、複数枚が左右対称に配置される。設定した最大枚数を超えて追加されない。実行中に上限を増減しても既存カードは維持される。
2. 幅を狭めた際はカードが重なり、右側ほど手前に描画される。
3. マウスオーバーで1枚だけが光り、最前面・角度ゼロ・上方向へ移動する。
4. ドラッグがポインターに追従し、矩形外で離すと選択状態のまま戻る。
5. 矩形内で左クリックを離すとPlayAreaのPivot位置へ移動、待機、効果1回、上へ退場、選択完了1回となる。矩形に入ってから外へ戻して離した場合は発動しない。
6. Showは左の画面外から移動してから展開し、Hideはカードをまとめてから左の画面外へ退場する。演出時間はMove DurationとSpread Durationの合計で、終了後に完了通知される。AddCardの追加演出は従来どおり右から。
7. 空手札、連続Show/Hide、プレイコールバックからHide、Time.timeScale=0でも完了する。
8. CanvasのスケールやDisplayのサイズを変えても、ドラッグ位置と画面外移動が正しい。
9. 一度だけコールバックを登録して2ターン以上Show/Hideを繰り返す。各ターン1枚だけプレイでき、発動・完了通知が各1回届く。

ビルド確認: dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet 成功。
