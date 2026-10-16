using System;
using UnityEngine;

namespace Casino.HighCardDuel
{
    /// <summary>有利・同程度・不利それぞれの行動確率。初期値は調整用の仮値。</summary>
    [Serializable]
    public sealed class HighCardDealerSettings
    {
        [Min(1)] public int advantageThreshold = 3;
        [Range(0, 1)] public float raiseWhenAhead = 0.7f;
        [Range(0, 1)] public float raiseWhenEven = 0.35f;
        [Range(0, 1)] public float raiseWhenBehind = 0.15f;
        [Range(0, 1)] public float foldWhenAhead = 0.05f;
        [Range(0, 1)] public float foldWhenEven = 0.2f;
        [Range(0, 1)] public float foldWhenBehind = 0.6f;
    }

    /// <summary>公開合計の差と調整可能な確率だけを使うディーラー判断。</summary>
    public sealed class HighCardDealer : IHighCardDealer
    {
        private readonly HighCardDealerSettings settings;
        private readonly System.Random random;

        /// <summary>調整値と乱数を設定する。</summary>
        /// <param name="settings">判断確率。実行中の変更も次の判断から反映する。</param>
        /// <param name="seed">再現用シード。</param>
        public HighCardDealer(HighCardDealerSettings settings, int? seed = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        /// <summary>公開点差に対応する確率でRaiseする。</summary>
        /// <param name="context">公開情報。</param>
        public DuelAction ChooseOpening(DealerDecisionContext context)
        {
            float chance = Chance(context, settings.raiseWhenAhead, settings.raiseWhenEven, settings.raiseWhenBehind);
            return random.NextDouble() < chance ? DuelAction.Raise : DuelAction.Check;
        }

        /// <summary>公開点差に対応する確率でFoldする。</summary>
        /// <param name="context">公開情報。</param>
        public DuelAction RespondToRaise(DealerDecisionContext context)
        {
            float chance = Chance(context, settings.foldWhenAhead, settings.foldWhenEven, settings.foldWhenBehind);
            return random.NextDouble() < chance ? DuelAction.Fold : DuelAction.Call;
        }

        /// <summary>点差から使用する確率を選び、有効範囲へ補正する。</summary>
        /// <param name="context">公開情報。</param>
        /// <param name="ahead">有利な場合の確率。</param>
        /// <param name="even">同程度の場合の確率。</param>
        /// <param name="behind">不利な場合の確率。</param>
        private float Chance(DealerDecisionContext context, float ahead, float even, float behind)
        {
            int difference = context.DealerScore - context.PlayerScore;
            int threshold = Math.Max(1, settings.advantageThreshold);
            float value = difference >= threshold ? ahead : difference <= -threshold ? behind : even;
            return float.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
        }
    }
}
