using System;
using System.Collections.Generic;

namespace Casino.Story
{
    public enum CasinoGameKind { HightCard, Poker, Blackjack }

    /// <summary>シーンをまたいでADVから変更された基本BETを保持する。</summary>
    public sealed class CasinoStorySession
    {
        private readonly Dictionary<CasinoGameKind, int> bets = new Dictionary<CasinoGameKind, int>();
        /// <summary>New Game時にBET変更を初期化する。</summary>
        public void Reset() => bets.Clear();
        /// <summary>変更済みBETまたはInspectorの初期値を取得する。</summary>
        /// <param name="game">対象ゲーム。</param>
        /// <param name="defaultBet">Inspectorで設定した初期値。</param>
        public int GetBaseBet(CasinoGameKind game, int defaultBet) => bets.TryGetValue(game, out int amount) ? amount : defaultBet;
        /// <summary>次ラウンドから使用する基本BETを設定する。</summary>
        /// <param name="game">対象ゲーム。</param>
        /// <param name="amount">正の整数。ブラックジャックは偶数。</param>
        public void SetBaseBet(CasinoGameKind game, int amount)
        {
            if (amount <= 0 || game == CasinoGameKind.Blackjack && amount % 2 != 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "BETは正の整数、Blackjackは偶数で指定してください。");
            bets[game] = amount;
        }
    }
}
