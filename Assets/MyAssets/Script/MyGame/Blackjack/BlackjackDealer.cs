namespace Casino.Blackjack
{
    /// <summary>16以下でHit、Soft 17を含む17以上でStandする固定ルール。</summary>
    public sealed class BlackjackDealer
    {
        /// <summary>公開後の手札点数から次の行動を決める。</summary>
        /// <param name="score">手札の評価結果。</param>
        public BlackjackAction Decide(BlackjackScore score) => score.Total < 17 ? BlackjackAction.Hit : BlackjackAction.Stand;
    }
}
