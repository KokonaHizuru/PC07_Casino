using System;
using System.Collections.Generic;

namespace Casino.Cards
{
    /// <summary>配布や勝敗のルールを持たない共通の山札。</summary>
    public sealed class CardDeck
    {
        private readonly List<PlayingCard> cards = new List<PlayingCard>(52);
        private readonly Random random;
        public int RemainingCount => cards.Count;

        /// <summary>52枚の山札と乱数生成器を初期化する。</summary>
        /// <param name="seed">再現用の乱数シード。省略時は通常の乱数を使う。</param>
        public CardDeck(int? seed = null)
        {
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            Reset();
        }

        /// <summary>山札をジョーカーなしの52枚に戻す。シャッフルは別途行う。</summary>
        public void Reset()
        {
            cards.Clear();
            for (int suit = 0; suit < 4; suit++)
                for (int rank = 1; rank <= 13; rank++)
                    cards.Add(new PlayingCard((CardSuit)suit, rank));
        }

        /// <summary>山札に残っているカードだけをFisher–Yates法でシャッフルする。</summary>
        public void Shuffle()
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                PlayingCard card = cards[i];
                cards[i] = cards[j];
                cards[j] = card;
            }
        }

        /// <summary>山札から1枚取り出す。空の場合は補充せず失敗を返す。</summary>
        /// <param name="card">取り出したカード。失敗時は使用しない。</param>
        public bool TryDraw(out PlayingCard card)
        {
            if (cards.Count == 0)
            {
                card = default;
                return false;
            }
            int index = cards.Count - 1;
            card = cards[index];
            cards.RemoveAt(index);
            return true;
        }
    }
}
