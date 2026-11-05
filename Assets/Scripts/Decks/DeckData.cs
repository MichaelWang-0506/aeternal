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

        [Header("Shift upgrades (GDD: upgrade the shifting effect with materials from fights)")]
        [Tooltip("Aether cost of each Shift level, in order. Its length is the deck's top level.")]
        public List<int> shiftUpgradeCosts = new List<int> { 2, 3, 4 };
        [Tooltip("Added to every damage, shield and heal Shift effect of this deck, per level.")]
        public int shiftUpgradeBonus = 2;

        /// <summary>The highest Shift level this deck can reach.</summary>
        public int MaxShiftLevel => shiftUpgradeCosts != null ? shiftUpgradeCosts.Count : 0;

        /// <summary>Copies the Shift upgrade settings from another deck (for decks built at runtime).</summary>
        public void CopyShiftUpgrades(DeckData source)
        {
            shiftUpgradeCosts = new List<int>(source.shiftUpgradeCosts ?? new List<int>());
            shiftUpgradeBonus = source.shiftUpgradeBonus;
        }
    }
}
