using System;
using System.Collections.Generic;
using Casino.Cards;

namespace Casino.ThreeCardPoker
{
    public enum PokerHandRank { HighCard, Pair, Flush, Straight, ThreeOfAKind, StraightFlush }

    /// <summary>役と比較用の数字を保持する変更不能な評価結果。</summary>
    public readonly struct PokerHandValue : IComparable<PokerHandValue>
    {
        public PokerHandRank Rank { get; }
        public int First { get; }
        public int Second { get; }
        public int Third { get; }
        /// <summary>役と同役比較の優先順を保持する。</summary>
        /// <param name="rank">役。</param>
        /// <param name="first">最優先の数字。</param>
        /// <param name="second">次に比較する数字。</param>
        /// <param name="third">最後に比較する数字。</param>
        internal PokerHandValue(PokerHandRank rank, int first, int second = 0, int third = 0)
        { Rank = rank; First = first; Second = second; Third = third; }
        /// <summary>役、数字の順で比較し、スートによる優劣は付けない。</summary>
        /// <param name="other">比較相手。</param>
        public int CompareTo(PokerHandValue other)
        {
            int result = Rank.CompareTo(other.Rank); if (result != 0) return result;
            result = First.CompareTo(other.First); if (result != 0) return result;
            result = Second.CompareTo(other.Second); return result != 0 ? result : Third.CompareTo(other.Third);
        }
        /// <summary>UI向けに役名を返す。</summary>
        public override string ToString()
        {
            switch (Rank)
            {
                case PokerHandRank.Pair: return "Pair";
                case PokerHandRank.Flush: return "Flush";
                case PokerHandRank.Straight: return "Straight";
                case PokerHandRank.ThreeOfAKind: return "Three of a Kind";
                case PokerHandRank.StraightFlush: return "Straight Flush";
                default: return "High Card";
            }
        }
    }

    /// <summary>スリーカード固有の役順とAの例外を評価する。</summary>
    public static class PokerHandEvaluator
    {
        /// <summary>重複のない3枚から役と同役比較の値を求める。</summary>
        /// <param name="cards">評価する3枚。</param>
        public static PokerHandValue Evaluate(IReadOnlyList<PlayingCard> cards)
        {
            if (cards == null || cards.Count != 3) throw new ArgumentException("役判定には3枚必要です。", nameof(cards));
            if (cards[0].Equals(cards[1]) || cards[0].Equals(cards[2]) || cards[1].Equals(cards[2]))
                throw new ArgumentException("同じカードが重複しています。", nameof(cards));
            int[] ranks = { High(cards[0]), High(cards[1]), High(cards[2]) }; Array.Sort(ranks);
            int low = ranks[0], middle = ranks[1], high = ranks[2];
            bool flush = cards[0].Suit == cards[1].Suit && cards[1].Suit == cards[2].Suit;
            bool wheel = low == 2 && middle == 3 && high == 14;
            bool straight = wheel || middle == low + 1 && high == middle + 1;
            int straightHigh = wheel ? 3 : high;
            if (straight && flush) return new PokerHandValue(PokerHandRank.StraightFlush, straightHigh);
            if (low == high) return new PokerHandValue(PokerHandRank.ThreeOfAKind, high);
            if (straight) return new PokerHandValue(PokerHandRank.Straight, straightHigh);
            if (flush) return new PokerHandValue(PokerHandRank.Flush, high, middle, low);
            if (low == middle) return new PokerHandValue(PokerHandRank.Pair, low, high);
            if (middle == high) return new PokerHandValue(PokerHandRank.Pair, high, low);
            return new PokerHandValue(PokerHandRank.HighCard, high, middle, low);
        }
        /// <summary>Aを通常比較では14として扱う。</summary>
        /// <param name="card">カード。</param>
        private static int High(PlayingCard card) => card.Rank == 1 ? 14 : card.Rank;
    }
}
