using System.Collections.Generic;
using Casino.Cards;

namespace Casino.ThreeCardPoker
{
    /// <summary>ディーラーの勝負成立条件と交換判断をゲーム進行から分離する。</summary>
    public sealed class PokerDealer
    {
        /// <summary>Queen High以上またはPair以上なら勝負成立とする。</summary>
        /// <param name="hand">ディーラーの役。</param>
        public bool Qualifies(PokerHandValue hand) => hand.Rank != PokerHandRank.HighCard || hand.First >= 12;

        /// <summary>自分の手札だけで交換位置を決める。Flush以上は維持、Pairは残り1枚、それ以外は最小札を選ぶ。</summary>
        /// <param name="cards">ディーラー自身の3枚。山札や相手の札は渡さない。</param>
        public int ChooseExchange(IReadOnlyList<PlayingCard> cards)
        {
            PokerHandValue hand = PokerHandEvaluator.Evaluate(cards);
            if (hand.Rank >= PokerHandRank.Flush) return -1;
            int lowest = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                int rank = cards[i].Rank == 1 ? 14 : cards[i].Rank;
                if (hand.Rank == PokerHandRank.Pair && rank != hand.First) return i;
                int current = cards[lowest].Rank == 1 ? 14 : cards[lowest].Rank;
                if (rank < current) lowest = i;
            }
            return lowest;
        }
    }
}
