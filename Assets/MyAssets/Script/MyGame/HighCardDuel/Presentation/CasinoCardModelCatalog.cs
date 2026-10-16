using System;
using Casino.Cards;
using UnityEngine;

namespace Casino.HighCardDuel
{
    /// <summary>Casinoの52枚のプレハブを直接参照して保持する。</summary>
    [CreateAssetMenu(menuName = "Casino/High Card Duel/Card Model Catalog")]
    public sealed class CasinoCardModelCatalog : ScriptableObject
    {
        [Tooltip("Club、Diamond、Heart、Spadeの順。各スートA～Kの13枚。")]
        [SerializeField] private GameObject[] models = new GameObject[52];

        /// <summary>カードに対応する3Dモデルを取得する。</summary>
        /// <param name="card">対象カード。</param>
        public GameObject GetModel(PlayingCard card)
        {
            int index = (int)card.Suit * 13 + card.Rank - 1;
            if (card.Rank < 1 || card.Rank > 13 || index < 0 || index >= models.Length || models[index] == null)
                throw new InvalidOperationException($"カードモデルが未登録です: {card}");
            return models[index];
        }
    }
}
