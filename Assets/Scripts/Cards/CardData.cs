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
        
        [TextArea]
        [Tooltip("Card description text")]
        public string description;
        
        [Tooltip("Card artwork sprite")]
        public Sprite artwork;
        
        [Header("Cost")]
        [Tooltip("Action Points cost to play this card")]
        public int costAP = 1;
        
        [Header("Effects")]
        [Tooltip("List of effects this card applies when played")]
        public List<EffectPayload> effects = new List<EffectPayload>();
    }
}
