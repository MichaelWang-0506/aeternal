using System.Collections.Generic;
using UnityEngine;

namespace Aeternal.Cards
{
    [CreateAssetMenu(menuName = "Aeternal/Card")]
    public class CardData : ScriptableObject
    {
        [Header("Card Information")]
        [Tooltip("Unique identifier for this card")]
        public string id;

        [Tooltip("Name shown on the card. Blank: the asset's name. Lets a copy of a card (another " +
                 "region's reward, with its own asset name) still read as the same card.")]
        public string title;

        /// <summary>The name shown on the card: its title, or the asset name.</summary>
        public string DisplayName => string.IsNullOrEmpty(title) ? name : title;
        
        [TextArea]
        [Tooltip("Card description text")]
        public string description;
        
        [Tooltip("Card artwork sprite")]
        public Sprite artwork;
        
        [Tooltip("How rare the card is. The merchant's Replace swaps a card for one of the same rarity.")]
        public CardRarity rarity = CardRarity.Common;

        [Header("Cost")]
        [Tooltip("Action Points cost to play this card")]
        public int costAP = 1;
        
        [Header("Effects")]
        [Tooltip("List of effects this card applies when played")]
        public List<EffectPayload> effects = new List<EffectPayload>();
    }

    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare
    }
}
