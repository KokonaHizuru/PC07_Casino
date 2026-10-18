using UnityEngine;
using UnityEngine.UI;

namespace Casino.Common
{
    /// <summary>円形背景と、音源に依存せず鼓動するぎざぎざの縁を描く。</summary>
    [AddComponentMenu("Casino/UI/Circular Choice Graphic")]
    public sealed class CircularChoiceGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        [Tooltip("波形の基本色と透明度。Wave Gradientと乗算する。グラデーションの色をそのまま使う場合は白にする。背景の基本色はColorで指定する。")]
        [SerializeField] private Color waveColor = new Color(.48f, .72f, .78f, .85f);
        [Header("上下のグラデーション（既存の色に乗算）")]
        [Tooltip("背景円の上下グラデーション。左端が下、右端が上。Colorの色・透明度と乗算する。白一色なら元の色を維持する。")]
        [SerializeField] private Gradient backgroundGradient = new Gradient();
        [Tooltip("波形の上下グラデーション。左端がボタン領域の下、右端が上。Wave Colorの色・透明度と乗算する。")]
        [SerializeField] private Gradient waveGradient = new Gradient();
        [Tooltip("ボタン領域の短辺に対する背景円の半径の比率。大きくすると円が広がり、波形が伸びる余白は狭くなる。")]
        [SerializeField, Range(.15f, .42f)] private float radiusRatio = .37f;
        [Tooltip("円周に並ぶぎざぎざの山の数。大きいほど細かくなり、描画する頂点数も増える。")]
        [SerializeField, Range(12, 128)] private int teeth = 64;
        [Tooltip("メインの上下の揺れの振幅（UI座標単位）。0でメインの揺れを無効にする。サブと合成後、領域内に収まるよう制限される。")]
        [SerializeField, Min(0)] private float amplitude = 14;
        [Tooltip("波形の輪郭線の太さ（UI座標単位）。ボタン領域に収まる範囲で適用する。")]
        [SerializeField, Min(.5f)] private float lineWidth = 2;
        [Tooltip("背景円から波形までの基本の間隔（UI座標単位）。揺れによって上下の間隔がさらに広がる。")]
        [SerializeField, Min(0)] private float rimGap = 2;
        [Tooltip("メインの揺れが強くなるビートの回数（1分あたり）。0でビートの時間変化を止める。波やサブの動きは別設定。")]
        [SerializeField, Min(0)] private float beatsPerMinute = 90;
        [Tooltip("ビートによるメイン振幅の強弱。0で強弱なし、1でビートの谷で振幅がほぼ0になる。サブの振幅には影響しない。")]
        [SerializeField, Range(0, 1)] private float beatStrength = .7f;
        [Tooltip("メインのうねりが変化する速さ（ラジアン毎秒）。0でうねりの時間変化を止める。ビートは別に動く。")]
        [SerializeField, Min(0)] private float waveSpeed = 1.1f;
        [Tooltip("円周1周に含まれるメインのうねりの数。整数に丸めて使用する。大きいほど細かいうねりになる。")]
        [SerializeField, Range(1, 24)] private float waveFrequency = 7;
        [Header("細かなサブの揺れ")]
        [Tooltip("メインに重ねる細かな揺れの振幅（UI座標単位）。0でサブの揺れを無効にする。")]
        [SerializeField, Min(0)] private float subAmplitude = 4;
        [Tooltip("サブのうねりが変化する速さ（ラジアン毎秒）。0でサブの時間変化を止める。メインとは逆向きに位相が進む。")]
        [SerializeField, Min(0)] private float subSpeed = 5;
        [Tooltip("円周1周に含まれるサブのうねりの数。整数に丸めて使用する。メインより大きくすると細かな揺れを重ねられる。")]
        [SerializeField, Range(1, 64)] private float subFrequency = 23;
        [Header("滑らかなランダム変化")]
        [Tooltip("メインとサブを合成した振幅に加えるランダムな強弱。0で無効、1で最大。滑らかなノイズを使い、ゲームの乱数には影響しない。")]
        [SerializeField, Range(0, 1)] private float randomness = .35f;
        [Tooltip("ランダムな強弱が時間で変化する速さ。0でランダム模様を固定する。メインとサブの動きは継続する。")]
        [SerializeField, Min(0)] private float randomSpeed = 2;
        [Tooltip("円周上のランダム模様の細かさ。大きいほど近くの点でも異なる強弱になり、小さいほど広い範囲で似た強弱になる。")]
        [SerializeField, Min(.01f)] private float randomScale = 4;
        [Tooltip("ランダム模様の基準となる整数。Instance Variationが0なら同じ値・設定・時刻で同じ模様になる。")]
        [SerializeField] private int randomSeed = 17;
        [Tooltip("ビート・メイン・サブの開始位相をずらす値。ビートでは1が1周期、うねりではラジアンとして使う。ランダム模様はRandom Seedで変更する。")]
        [SerializeField] private float phaseOffset;
        [Tooltip("再生中の波形アニメーションを有効にする。無効時は時刻0の形を表示する。時間停止中も有効なら動き、背景円と文字は動かない。")]
        [SerializeField] private bool animate = true;
        [Header("インスタンスごとの個体差")]
        [Tooltip("ボタンごとの位相・速度・ノイズ・配色のずれ。0で同じ設定のボタンが同期し、1で個体差が最大になる。ゲームの乱数には影響しない。")]
        [SerializeField, Range(0, 1)] private float instanceVariation = 1;
        [Tooltip("ビートのタイミングの個体差の幅（1が1周期）。0で同期する。BPMは共通のままなので時間経過でずれが広がらない。Instance Variationも乗算する。")]
        [SerializeField, Range(0, 1)] private float beatTimingVariation = .08f;
        [Header("波形の多色アニメーション")]
        [Tooltip("波形に混ぜる虹色の強さ。0で従来のWave ColorとWave Gradientのみ、1で虹色になる。透明度は既存設定を維持する。")]
        [SerializeField, Range(0, 1)] private float rainbowStrength = .85f;
        [Tooltip("円周1周に含まれる虹色の繰り返し回数。整数に丸めて使用する。")]
        [SerializeField, Range(1, 6)] private int rainbowCycles = 2;
        [Tooltip("色が流れる速さ（1秒あたりの色相の周回数）。0で色の時間変化を停止する。")]
        [SerializeField, Min(0)] private float colorSpeed = .18f;
        [Tooltip("虹色の鮮やかさ。0で白、1で鮮やかな色になる。")]
        [SerializeField, Range(0, 1)] private float colorSaturation = .75f;
        private Vector4 instanceOffsets;

        /// <summary>ゲームの乱数を消費せず、インスタンス固有のずれを一度だけ生成する。</summary>
        protected override void Awake()
        {
            base.Awake();
            var random = new System.Random(GetInstanceID());
            instanceOffsets = new Vector4((float)random.NextDouble(), (float)random.NextDouble(),
                (float)random.NextDouble(), (float)random.NextDouble());
        }

        /// <summary>表示中だけ、時間停止に影響されず波形を更新する。</summary>
        private void Update()
        {
            if (animate && isActiveAndEnabled && !canvasRenderer.cull) SetVerticesDirty();
        }

        /// <summary>背景の円と波形の輪郭を同じUIメッシュとして描く。</summary>
        /// <param name="vh">描画先の頂点情報。</param>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            float half = Mathf.Min(bounds.width, bounds.height) * .5f;
            if (half <= 0) return;
            float radius = half * 2 * Mathf.Clamp(radiusRatio, .15f, .42f);
            int segments = Mathf.Clamp(teeth, 12, 128) * 2;
            Vector2 center = bounds.center;
            float time = animate && Application.isPlaying ? Time.unscaledTime : 0;
            float individuality = Mathf.Clamp01(instanceVariation);
            float motionTime = time * (1 + (instanceOffsets.y - .5f) * .2f * individuality);
            float phase = phaseOffset + instanceOffsets.x * Mathf.PI * 2 * individuality;
            float beat = Mathf.Pow(.5f + .5f * Mathf.Cos((time * Mathf.Max(0, beatsPerMinute) / 60 + phaseOffset
                + (instanceOffsets.x - .5f) * individuality * Mathf.Clamp01(beatTimingVariation)) * Mathf.PI * 2), 5);
            float gain = Mathf.Lerp(1 - Mathf.Clamp01(beatStrength), 1, beat);
            float gap = Mathf.Clamp(rimGap, 0, Mathf.Max(0, half - radius - 1));
            float thickness = Mathf.Clamp(lineWidth, .5f, Mathf.Max(.5f, half - radius - gap));
            // 縦長の領域では、背景円の大きさを変えずに上下の伸び幅だけを広げる。
            float verticalHalf = bounds.height * .5f;
            float available = Mathf.Max(0, verticalHalf - radius - gap - thickness);
            // 中間の色キーも反映するため、背景を同心円状に分割する。
            vh.AddVert(center, GradientColor(backgroundGradient, color, .5f), Vector2.zero);
            const int rings = 16;
            for (int ring = 1; ring <= rings; ring++)
            {
                int start = vh.currentVertCount;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * ring / rings);
                    vh.AddVert(center + point, GradientColor(backgroundGradient, color, point.y / (2 * radius) + .5f), Vector2.zero);
                    if (i == 0) continue;
                    int current = start + i;
                    if (ring == 1) vh.AddTriangle(0, current - 1, current);
                    else
                    {
                        int previous = current - segments - 1;
                        vh.AddTriangle(previous - 1, current - 1, current);
                        vh.AddTriangle(previous - 1, current, previous);
                    }
                }
            }
            int first = vh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // 円周の終端を始端と一致させ、細かな山と大きなうねりを重ねる。
                float wave = .5f + .5f * Mathf.Sin(angle * Mathf.Round(waveFrequency) + motionTime * waveSpeed + phase);
                float spike = i % 2 == 0 ? .12f : .45f + wave * .55f;
                float sub = .5f + .5f * Mathf.Sin(angle * Mathf.Round(subFrequency) - motionTime * subSpeed + phase);
                // 円周座標でノイズを読むことで、始端・終端の継ぎ目をなくす。
                float seed = (randomSeed % 10000) * .137f + instanceOffsets.z * 100 * individuality;
                float noise = Mathf.PerlinNoise(100 + seed + direction.x * randomScale + motionTime * randomSpeed,
                    100 + seed + direction.y * randomScale + motionTime * randomSpeed * .37f);
                float variation = Mathf.Lerp(1, .25f + Mathf.Clamp01(noise) * 1.5f, randomness);
                float height = Mathf.Min(available, (Mathf.Max(0, amplitude) * gain * spike
                    + Mathf.Max(0, subAmplitude) * sub) * variation);
                // Xは固定し、上半分は上、下半分は下へ伸縮する。円と文字は動かさない。
                Vector2 offset = Vector2.up * (direction.y * height);
                Vector2 inner = direction * (radius + gap) + offset;
                Vector2 outer = direction * (radius + gap + thickness) + offset;
                float hue = angle / (Mathf.PI * 2) * Mathf.Clamp(rainbowCycles, 1, 6)
                    + time * colorSpeed * (1 + (instanceOffsets.z - .5f) * .3f * individuality)
                    + instanceOffsets.w * individuality + (noise - .5f) * .2f;
                Color rainbow = Color.HSVToRGB(Mathf.Repeat(hue, 1), Mathf.Clamp01(colorSaturation), 1);
                vh.AddVert(center + inner, WaveVertexColor(inner.y / (2 * verticalHalf) + .5f, rainbow), Vector2.zero);
                vh.AddVert(center + outer, WaveVertexColor(outer.y / (2 * verticalHalf) + .5f, rainbow), Vector2.zero);
                if (i == 0) continue;
                int j = first + (i - 1) * 2;
                vh.AddTriangle(j, j + 1, j + 3);
                vh.AddTriangle(j, j + 3, j + 2);
            }
        }

        /// <summary>既存の波形色と虹色を混ぜ、グラデーションの透明度を保持する。</summary>
        /// <param name="position">上下方向のグラデーション位置。</param>
        /// <param name="rainbow">円周上の虹色。</param>
        private Color WaveVertexColor(float position, Color rainbow)
        {
            Color original = GradientColor(waveGradient, waveColor, position);
            rainbow.a = original.a;
            return Color.Lerp(original, rainbow, Mathf.Clamp01(rainbowStrength));
        }

        /// <summary>下から上へのグラデーションに基本色と透明度を乗算する。</summary>
        /// <param name="gradient">上下方向の色設定。</param>
        /// <param name="tint">既存の基本色。</param>
        /// <param name="position">下端0、上端1の評価位置。</param>
        private static Color GradientColor(Gradient gradient, Color tint, float position)
        {
            return tint * (gradient == null ? Color.white : gradient.Evaluate(Mathf.Clamp01(position)));
        }

        /// <summary>背景の円内だけをクリック可能にし、四隅の透明部分を除外する。</summary>
        /// <param name="screenPoint">ポインターの画面位置。</param>
        /// <param name="eventCamera">Canvasのイベントカメラ。</param>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local)) return false;
            Rect bounds = rectTransform.rect;
            float radius = Mathf.Min(bounds.width, bounds.height) * Mathf.Clamp(radiusRatio, .15f, .42f);
            return (local - bounds.center).sqrMagnitude <= radius * radius;
        }
    }
}
