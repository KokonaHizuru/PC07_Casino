using System;

namespace Casino.Cards
{
    public enum CardSuit { Club, Diamond, Heart, Spade }

    /// <summary>ゲーム固有の点数を持たないトランプの識別情報。</summary>
    [Serializable]
    public readonly struct PlayingCard : IEquatable<PlayingCard>
    {
        public CardSuit Suit { get; }
        public int Rank { get; }

        /// <summary>スートとランクからカードを作成する。</summary>
        /// <param name="suit">スート。</param>
        /// <param name="rank">Aを1、Jを11、Qを12、Kを13とするランク。</param>
        public PlayingCard(CardSuit suit, int rank)
        {
            if ((int)suit < 0 || (int)suit > 3) throw new ArgumentOutOfRangeException(nameof(suit));
            if (rank < 1 || rank > 13) throw new ArgumentOutOfRangeException(nameof(rank));
            Suit = suit;
            Rank = rank;
        }

        /// <summary>同じカードか判定する。</summary>
        /// <param name="other">比較するカード。</param>
        public bool Equals(PlayingCard other) => Suit == other.Suit && Rank == other.Rank;

        /// <summary>同じカードを表すオブジェクトか判定する。</summary>
        /// <param name="obj">比較対象。</param>
        public override bool Equals(object obj) => obj is PlayingCard other && Equals(other);

        /// <summary>カードの識別用ハッシュを返す。</summary>
        public override int GetHashCode() => (int)Suit * 13 + Rank;

        /// <summary>ログ表示用のカード名を返す。</summary>
        public override string ToString() => $"{Suit} {Rank}";
    }
}
