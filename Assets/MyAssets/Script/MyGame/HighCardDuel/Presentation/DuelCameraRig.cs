using System.Collections;
using Casino.Common;
namespace Casino.HighCardDuel
{
    /// <summary>既存シーンの参照を維持して共通カメラ演出へ接続する。</summary>
    public sealed class DuelCameraRig : CardTableCameraRig
    {
        /// <summary>場札の公開後から次のカード選択まで、通常視点でも手札を退避したままにする。</summary>
        public bool KeepHandsHidden { get; set; }

        /// <summary>俯瞰視点またはカード公開後の判断中は手札を非表示にする。</summary>
        /// <param name="view">切り替える視点。</param>
        protected override bool ShouldHideHands(CardCameraView view) => KeepHandsHidden || base.ShouldHideHands(view);

        /// <summary>ハイカード用の視点指定を共通処理へ渡す。</summary>
        /// <param name="view">切り替える視点。</param>
        public IEnumerator SwitchTo(DuelCameraView view) => base.SwitchTo((CardCameraView)view);
    }
}
