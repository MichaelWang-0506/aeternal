using UnityEngine;
using System.Collections.Generic;

namespace Aeternal.Cards
{
    // Context passed to CardResolver for execution. Can be extended as needed.
    public class CardContext
    {
        public GameObject caster; // e.g., player GameObject
        public GameObject explicitTarget; // e.g., a chosen enemy
        public GameObject enemy; // The enemy GameObject (for Enemy target effects)
        public List<GameObject> explicitTargets;
        public Vector2Int chosenDirection; // optional for movement cards (e.g., (0,1) up)
        public Decks.DeckData activeDeck;
        public object extra; // generic slot for extensions
        public int startSquare = -1; // where the caster stood when the card was played (-1: unknown)
    }
}
