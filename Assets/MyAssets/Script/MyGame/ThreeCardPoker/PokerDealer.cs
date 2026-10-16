namespace Casino.ThreeCardPoker
{
    /// <summary>ディーラーの勝負成立条件をゲーム進行から分離する。</summary>
    public sealed class PokerDealer
    {
        /// <summary>Queen High以上またはPair以上なら勝負成立とする。</summary>
        /// <param name="hand">ディーラーの役。</param>
        public bool Qualifies(PokerHandValue hand) => hand.Rank != PokerHandRank.HighCard || hand.First >= 12;
    }
}
