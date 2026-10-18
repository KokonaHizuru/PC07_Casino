# 円形の中央選択肢

共通プレハブ：`Assets/MyAssets/Prefabs/Cards/CircularChoiceButton.prefab`

ハイカードのCheck／Raise／Call／Fold、ブラックジャックのHit／Stand／Double Down、ポーカーのPlay／Fold、および各ゲームの中央に出るResume Funds／Retryに使用する。表示条件と操作は従来どおり。右下のKeep Hand／Next Roundには適用しない。

## Inspectorでの調整

試用中の強め設定：Amplitude 213.75、BPM 180、Beat Strength 0.95、Wave Speed 9、Sub Amplitude 57、Sub Speed 20、Randomness 0.8、Random Speed 5、Random Scale 7、Line Width 7.125、Rim Gap 4.75。2倍に拡大した設定から全体を約5％縮小し、RectTransformを617.5×855、Radius Ratioを0.313、文字サイズを55にしている。上下の伸び幅の上限はRectTransformの高さで決まり、幅と背景円の大きさを変えずに拡張できる。

ホバー時はButtonのColor Tintで背景と波形に暖色の色味を重ねる。Highlighted Colorで色、Fade Durationで遷移時間を調整できる（現在0.08秒）。カーソルが離れると通常色に戻る。文字は白を維持する。

プレハブのルートにある `Circular Choice Graphic` で設定する。

- **Color**：円の背景色と透明度。既定は従来と同じRGBA `(0.12, 0.18, 0.22, 0.95)`。
- **Wave Color**：縁の波形色と透明度。
- **Background Gradient / Wave Gradient**：背景円／波形それぞれの上下グラデーション。左のキーが下、右のキーが上。Color／Wave Colorに乗算され、アルファも乗算する。グラデーションの色をそのまま使う場合は基本色を白にする。初期値は白で、従来の色を維持する。
- **Radius Ratio**：ボタン領域に対する背景円の半径。
- **Teeth**：ぎざぎざの山の数。
- **Amplitude**：波形の最大振幅。
- **Line Width / Rim Gap**：波形線の太さ・円からの間隔。
- **Beats Per Minute / Beat Strength**：鼓動の速さ・強さ。
- **Wave Speed / Wave Frequency / Phase Offset**：円周のうねりの速さ・数・位相。
- **Sub Amplitude / Sub Speed / Sub Frequency**：メインに重ねる細かな揺れの振幅・速さ・数。Sub Amplitudeを0にすると無効。
- **Randomness / Random Speed / Random Scale / Random Seed**：滑らかなランダム変化の強さ・時間変化の速さ・円周上の細かさ・種。Randomnessを0にすると規則的な波形になる。Seedを変えるとパターンが変わる。ゲームの乱数には影響しない。
- **Animate**：波形アニメーションの有効／無効。
- **Instance Variation**：インスタンス固有の位相・速度・ノイズ・配色のずれ。初期値1、0で同じ設定のボタンが同期する。生成時に一度だけ決まり、非表示・再表示で再抽選しない。ゲームの乱数には影響しない。
- **Beat Timing Variation**：ビートだけの位相差の幅（周期単位）。初期値0.08で、180 BPMではボタン間のずれは最大約0.027秒。ビートのBPMは共通にし、時間経過でずれが拡大しない。0でビートのみ同期する。他の揺れや色の個体差は維持する。
- **Rainbow Strength / Rainbow Cycles**：波形に混ぜる虹色の強さ／円周上での繰り返し回数。Strengthが0なら従来の波形色、1なら虹色のみになる。透明度は既存のWave ColorとWave Gradientを維持する。
- **Color Speed / Color Saturation**：色が流れる速さ／鮮やかさ。複数の色を円周上に同時に表示し、滑らかに混ぜながら動かす。背景円と文字には適用しない。

音源・AudioSourceは不要。時間停止中も表示中の選択肢は動き、非表示のボタンは更新しない。波形はボタン領域からはみ出さない範囲に制限する。四隅の透明部分はクリック不可で、円内だけが反応する。

ルートのRectTransformで全体サイズ、子のLabelで文字の色・フォント・大きさを調整できる。シーン固有のラベルだけを上書きし、背景・波形などは共通プレハブから継承する。各SceneUIのChoice Layoutによる中央揃えと間隔設定も引き続き使う。

## シーンへの適用

波形のX座標は固定し、上半分は上へ、下半分は下へ伸び縮みする。背景円と文字の位置・形状は固定。メイン・サブ・ランダム変化を合成し、振幅が大きい場合はボタン領域内に収まるよう制限する。

設定済みの3ゲームはプレハブ接続済み。新しくコピーしたカードゲームシーンを更新する場合は、ゲーム側のセットアップ後に **Casino → UI → Apply Circular Choices** を実行する。中央のボタン参照と文字・クリックイベントを引き継ぎ、右下のボタンは維持する。再実行しても同じボタンを増やさず、プレハブの編集内容も上書きしない。
