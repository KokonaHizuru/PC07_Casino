using System.Collections.Generic;
using Casino.Cards;

namespace Casino.Blackjack
{
    /// <summary>Aの最適な値とナチュラルBlackjackを区別する評価結果。</summary>
    public readonly struct BlackjackScore
    {
        public int Total { get; }
        public bool IsSoft { get; }
        public bool IsBlackjack { get; }
        public bool IsBust => Total > 21;

        /// <summary>評価値を保持する。</summary>
        /// <param name="total">合計。</param>
        /// <param name="soft">11として数えるAがあるか。</param>
        /// <param name="natural">初期2枚の21か。</param>
        private BlackjackScore(int total, bool soft, bool natural)
        { Total = total; IsSoft = soft; IsBlackjack = natural; }

        /// <summary>複数のAを減算し、21以下で最大となる合計を算出する。</summary>
        /// <param name="cards">手札。</param>
        /// <param name="hideHole">2枚目のカードを表示用の計算から除くか。</param>
        public static BlackjackScore Evaluate(IReadOnlyList<PlayingCard> cards, bool hideHole = false)
        {
            int total = 0, aces = 0, count = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (hideHole && i == 1) continue;
                int rank = cards[i].Rank;
                total += rank == 1 ? 11 : System.Math.Min(rank, 10);
                if (rank == 1) aces++;
                count++;
            }
            while (total > 21 && aces > 0) { total -= 10; aces--; }
            return new BlackjackScore(total, aces > 0, count == 2 && total == 21);
        }
    }
}
