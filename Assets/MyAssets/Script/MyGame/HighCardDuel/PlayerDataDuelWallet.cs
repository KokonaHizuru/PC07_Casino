using System;

namespace Casino.HighCardDuel
{
    /// <summary>ゲームの資金を保存対象のPlayerDataへ接続する。</summary>
    public sealed class PlayerDataDuelWallet : IDuelWallet
    {
        private readonly PlayerData data;
        public long Balance => data.casinoMoney;

        /// <summary>使用する保存データを保持する。</summary>
        /// <param name="data">現在のプレイヤーデータ。</param>
        public PlayerDataDuelWallet(PlayerData data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (data.casinoMoney < 0) throw new ArgumentOutOfRangeException(nameof(data));
        }

        /// <summary>残高が十分な場合だけ資金を差し引く。</summary>
        /// <param name="amount">支払額。</param>
        public bool TryDebit(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (data.casinoMoney < amount) return false;
            data.casinoMoney -= amount;
            return true;
        }

        /// <summary>資金を加算する。</summary>
        /// <param name="amount">加算額。</param>
        public void Credit(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            data.casinoMoney = checked(data.casinoMoney + amount);
        }
    }
}
