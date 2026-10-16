namespace Casino.Common
{
    /// <summary>各ゲームから共通して使用する整数資金の接続口。</summary>
    public interface ICasinoWallet
    {
        long Balance { get; }
        /// <summary>資金が足りる場合だけ支払う。</summary>
        /// <param name="amount">支払額。</param>
        bool TryDebit(long amount);
        /// <summary>受取額を加算する。</summary>
        /// <param name="amount">受取額。</param>
        void Credit(long amount);
    }
}
