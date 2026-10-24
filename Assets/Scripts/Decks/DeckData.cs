using System.Collections.Generic;
using UnityEngine;
using Aeternal.Cards;

namespace Aeternal.Decks
{
    
    
    [System.Serializable]
    public struct CardEntry
    {
        public CardData card;
        public int count;
    }

    [CreateAssetMenu(menuName = "Aeternal/Deck")]
    public class DeckData : ScriptableObject
    {
        public string deckId;
        [TextArea] public string description;
        public Sprite artwork;
        public List<CardEntry> defaultCards = new List<CardEntry>();


        [Header("Shift Effects - serialized as simple card-effect lists")]
        public List<EffectPayload> onShiftIn = new List<EffectPayload>();
        public List<EffectPayload> onShiftOut = new List<EffectPayload>();
    }
}
