# ImageOutline の使い方

UI の Image または RawImage の Material に ImageOutline.mat を設定してください。

- Outline Color: 線の色。初期値は黒。
- Outline Width (Texture Pixels): 元テクスチャのピクセル単位での線幅（0～32）。初期値は2。表示倍率に応じて画面上の太さも変わります。太くするほど描画負荷が増えます。
- Tint: 元画像の色。Image / RawImage の Color の透明度や CanvasGroup によるフェードは線にも適用されます。

透明部分に画像のアルファを広げて外側の線を描きます。画像の端に線幅以上の透明な余白を用意してください。余白がない画像や、透明部分のない画像では外側の線を描けません。

Image は Type = Simple、Use Sprite Mesh = OFF、スプライトの Mesh Type = Full Rect を使用してください。Sprite Atlas に入っていない単独画像を対象にしています。Sliced / Tiled / Filled、アトラス内の画像、SpriteRenderer は対象外です。RawImage は UV Rect の初期値 (0, 0, 1, 1) を使用してください。

Mask / RectMask2D と RectMask2D の Softness に対応しています。マテリアルを複製すれば画像ごとに線の色と太さを設定できます。
