using UnityEngine;
using UnityEngine.UI;

namespace Casino.Cards
{
    /// <summary>Casino素材の表裏テクスチャと3Dカード由来のUV配置。</summary>
    [CreateAssetMenu(menuName = "Casino/Card Artwork")]
    public sealed class CardArtwork : ScriptableObject
    {
        [SerializeField] private Texture2D club;
        [SerializeField] private Texture2D diamond;
        [SerializeField] private Texture2D heart;
        [SerializeField] private Texture2D spade;
        [SerializeField] private Texture2D back;
        [SerializeField] private Rect firstFaceUV = new Rect(0.04644308f, 0.7651838f, 0.1572408f, 0.219638f);
        [SerializeField] private Rect backUV = new Rect(0.2963984f, 0.01520687f, 0.1573036f, 0.2197246f);

        /// <summary>カードの表裏を指定されたUIへ反映する。</summary>
        /// <param name="image">表示先。</param>
        /// <param name="card">表示するカード。</param>
        /// <param name="faceUp">表向きにするか。</param>
        public void Apply(RawImage image, PlayingCard card, bool faceUp)
        {
            if (!faceUp)
            {
                image.texture = back;
                image.uvRect = backUV;
                return;
            }
            switch (card.Suit)
            {
                case CardSuit.Club: image.texture = club; break;
                case CardSuit.Diamond: image.texture = diamond; break;
                case CardSuit.Heart: image.texture = heart; break;
                default: image.texture = spade; break;
            }
            int index = card.Rank - 1;
            Rect uv = firstFaceUV;
            uv.x += index % 4 * 0.25f;
            uv.y -= index / 4 * 0.25f;
            image.uvRect = uv;
        }
    }
}
