using Casino.Common;
namespace Casino.HighCardDuel
{
    /// <summary>既存の資金APIを共通の保存データ接続へ委譲する。</summary>
    public sealed class PlayerDataDuelWallet : PlayerDataCasinoWallet, IDuelWallet
    {
        /// <summary>現在の保存データへ接続する。</summary>
        /// <param name="data">保存データ。</param>
        public PlayerDataDuelWallet(PlayerData data) : base(data) { }
    }
}
