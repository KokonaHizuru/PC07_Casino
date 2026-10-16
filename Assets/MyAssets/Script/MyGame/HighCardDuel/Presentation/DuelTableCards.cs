using System;
using System.Collections;
using Casino.Common;

namespace Casino.HighCardDuel
{
    /// <summary>既存の3枚用演出通知を共通カード表示へ変換する。</summary>
    public sealed class DuelTableCards : TableCardPresenter
    {
        /// <summary>選択したカードを裏向きで机へ置く。</summary>
        /// <param name="cue">選択通知。</param>
        public IEnumerator Place(DuelCue cue)
        {
            Validate(cue);
            yield return base.Place(cue.Card.Value, cue.Actor == DuelActor.Player, cue.Phase - 1);
        }
        /// <summary>通知されたカードを表へ返す。</summary>
        /// <param name="cue">公開通知。</param>
        public IEnumerator Reveal(DuelCue cue)
        {
            Validate(cue);
            yield return base.Reveal(cue.Card.Value);
        }
        /// <summary>ハイカードの対象者と公開順を検証する。</summary>
        /// <param name="cue">通知。</param>
        private static void Validate(DuelCue cue)
        {
            if (!cue.Card.HasValue || cue.Phase < 1 || cue.Phase > 3 || cue.Actor == DuelActor.None)
                throw new ArgumentException("カード、対象者、公開順が必要です。", nameof(cue));
        }
    }
}
