# LiveAnalyticsGraph

Canvas内にLiveAnalyticsGraphプレハブを配置し、参照したコンポーネントに値を渡します。

```csharp
graph.AddValue(4);   // 現在のLiveManager.LiveTimeに記録。縦軸上限10。
// LiveTimeが進んだあと
graph.AddValue(14);  // 縦軸上限50。既存の点も新しいスケールで描画。
graph.Clear();      // 履歴と現在値を初期化。
```

`AddValueAtTime(value, liveTime)`で時刻を直接指定することもできます。同時刻への入力は置換され、時刻順に結線します。時刻は0～LiveTimeMax、負の値は0へ補正します。AddValueに必要なLiveManagerは自動検索されます。複数存在する場合はインスペクタで対象を指定してください。

現在のLiveManagerにはLiveTimeを進める処理がありません。AddValueのみでは時刻0の点が更新されるため、時間進行側の実装、または時刻指定APIが必要です。

## インスペクタ

- Title：TitleオブジェクトのTextMeshProに反映。
- Visible Time Range：横軸の表示時間幅（LiveTimeと同じ単位）。10なら時刻5でも0～10を表示し、時刻10を超えてからスクロール（時刻30なら20～30）。0以下なら従来の0～LiveTimeMax。時刻指定で追加した最新データが現在時刻より先なら、その時刻を現在時刻として扱います。履歴は削除せず、全期間表示に戻せます。
- Future Time Padding：表示範囲の先に追加する余白。Visible Time Rangeが10、余白が5なら、時刻5では0～15、時刻30では20～35の計15を表示。Visible Time Rangeが0以下の場合は余白を適用せず、従来どおり0～LiveTimeMaxを表示します。
- Text Color：Text配下（既存プレハブではTetx）のタイトル・区切り・現在値の色。プレハブの設定は黒。
- Graph Number Color：Graph配下の最低値・中央値・最大値の色。プレハブの設定は白。
- Vertical Steps：縦軸上限の候補。既定値10、50、100、500、1000。履歴の最大値を収める最小の候補を使用。最大候補を超える場合は履歴の最大値まで拡張。
- 未入力時（Clear直後を含む）の上限：Vertical Stepsの正の最小値。既定設定では10。空配列や正の値がない場合は1。
- Empty Maximum：入力済みの全データが0の場合の上限。既定値100。
- Show Middle：中央（表示上限の半分）の基準線とラベルを表示するか。最小値0と最大値は常時表示。
- Reference Line Color / Graph Line Color / Background Color：基準線・折れ線・背景の色。
- Reference Line Width / Graph Line Width：線幅（UI単位、0.1～8）。小さい描画領域では領域内に収めるためさらに制限。
- Anti Aliasing Width：基準線・折れ線・単独の点の縁を滑らかにする幅（画面ピクセル、0～2）。既定値1。0で無効。折れ線は直線のままで、データの値や時刻は変更しません。

背景・基準線・折れ線はGraphのUIメッシュ、縦軸ラベルはGraphの子オブジェクトです。横軸の数値ラベルは作りません。NumTextには最後に渡した値を表示します。履歴はメモリ上のみで保持します。

## 確認

既存プロジェクトの参照を使ったC#コンパイルとプレハブ内fileIDの整合性を確認済み。Unityの編集時Gameビューで、4つのグラフの白い目盛りと黒いタイトル・現在値、およびインスペクタからのText Color変更の反映を確認済み。再生中の動作確認は未実施です。
