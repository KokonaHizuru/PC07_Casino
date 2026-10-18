using System;
using UnityEngine;
using Casino.HighCardDuel;
using Casino.Blackjack;
using Casino.ThreeCardPoker;

namespace Casino.Common
{
    /// <summary>指定フォルダ直下のイベント参照をエディタで収録する。</summary>
    public sealed class CasinoEventCatalog : ScriptableObject
    {
        public DuelPresentationEvent[] hightCard = Array.Empty<DuelPresentationEvent>();
        public BlackjackPresentationEvent[] blackjack = Array.Empty<BlackjackPresentationEvent>();
        public PokerPresentationEvent[] poker = Array.Empty<PokerPresentationEvent>();
    }
}
