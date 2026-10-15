using Casino.Cards;
using UnityEngine;

/// <summary>今回の動作確認専用。配布条件は共通手札へ持ち込まない。</summary>
public sealed class CardHandDebug : MonoBehaviour
{
    [SerializeField] private CardHandUI playerHand;
    [SerializeField] private CardHandUI dealerHand;
    [SerializeField, Range(1, 26)] private int cardsPerHand = 3;

    /// <summary>起動時に山札を作り、双方へ重複しないカードを配る。</summary>
    private void Start()
    {
        if (playerHand == null || dealerHand == null)
        {
            Debug.LogError("デバッグ用の手札参照を設定してください。", this);
            return;
        }
        playerHand.Clear();
        dealerHand.Clear();
        playerHand.SetInteractable(true);
        dealerHand.SetInteractable(false);
        var deck = new CardDeck();
        deck.Shuffle();
        for (int i = 0; i < Mathf.Clamp(cardsPerHand, 1, 26); i++)
        {
            if (deck.TryDraw(out PlayingCard player)) playerHand.AddCard(player, true);
            if (deck.TryDraw(out PlayingCard dealer)) dealerHand.AddCard(dealer, false);
        }
    }
}
