using System.Collections;
using Casino.Common;
namespace Casino.HighCardDuel
{
    /// <summary>既存シーンの参照を維持して共通カメラ演出へ接続する。</summary>
    public sealed class DuelCameraRig : CardTableCameraRig
    {
        /// <summary>ハイカード用の視点指定を共通処理へ渡す。</summary>
        /// <param name="view">切り替える視点。</param>
        public IEnumerator SwitchTo(DuelCameraView view) => base.SwitchTo((CardCameraView)view);
    }
}
