using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using Aeternal.Cards;

namespace Aeternal.Decks
{
    public class DeckManager : MonoBehaviour
    {
        [Header("Runtime")]
        public DeckData activeDeck;
        public List<CardData> drawPile = new List<CardData>();
        public List<CardData> discardPile = new List<CardData>();
        public List<CardData> hand = new List<CardData>();

        [Header("Config")]
        public int handSize = 5;
        [Tooltip("Number of cards to recover from discard pile at the start of each round")]
        public int cardsRecoveredPerRound = 2;

        // Events
        public event Action OnDrawPileChanged; // Fired when draw pile changes (card drawn, shuffled, etc.)
        public event Action<CardData> OnCardAddedToHand; // Fired when a card is added to hand
        public event Action<CardData> OnCardRemovedFromHand; // Fired when a card is removed from hand

        public void InitializeDeck(DeckData deck, bool shuffle = true)
        {
            activeDeck = deck;
            drawPile.Clear();
            discardPile.Clear();
            hand.Clear();

            // populate draw pile based on card counts
            foreach (var entry in deck.defaultCards)
            {
                for (int i = 0; i < entry.count; i++)
                    drawPile.Add(entry.card);
            }

            if (shuffle) ShuffleDrawPile();
            Debug.Log($"[DeckManager] Initialized draw pile with {drawPile.Count} cards.");
            OnDrawPileChanged?.Invoke();
        }


        /// <summary>
        /// Initialize the draw pile directly from a flat list of cards (used by the run deck,
        /// which mutates as the player collects rewards and can't be expressed as fixed counts).
        /// </summary>
        public void InitializeDeckFromCards(IEnumerable<CardData> cards, bool shuffle = true)
        {
            drawPile.Clear();
            discardPile.Clear();
            hand.Clear();

            if (cards != null)
            {
                foreach (var card in cards)
                    if (card != null) drawPile.Add(card);
            }

            if (shuffle) ShuffleDrawPile();
            Debug.Log($"[DeckManager] Initialized run draw pile with {drawPile.Count} cards.");
            OnDrawPileChanged?.Invoke();
        }

        public void ShuffleDrawPile()
        {
            for (int i = 0; i < drawPile.Count; i++)
            {
                var tmp = drawPile[i];
                int r = UnityEngine.Random.Range(i, drawPile.Count);
                drawPile[i] = drawPile[r];
                drawPile[r] = tmp;
            }
            OnDrawPileChanged?.Invoke();
        }

        public CardData DrawOne()
        {
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0) return null;
                // If draw pile is empty during a round, recover the configured amount
                // (This is a fallback - main recovery happens at round start)
                RecoverCardsAtRoundStart();
            }

            var card = drawPile[0];
            drawPile.RemoveAt(0);
            OnDrawPileChanged?.Invoke();
            return card;
        }

        public List<CardData> DrawToHand()
        {
            while (hand.Count < handSize)
            {
                if (!TryDrawCardToHand(true))
                    break;
            }

            return hand.ToList();
        }

        public bool TryDrawCardToHand(bool respectHandSize = false)
        {
            if (respectHandSize && hand.Count >= handSize)
            {
                Debug.LogWarning("[DeckManager] Cannot draw: hand is already at configured handSize.");
                return false;
            }

            var card = DrawOne();
            if (card == null) return false;

            hand.Add(card);
            Debug.Log($"[DeckManager] Drew {card.id} into hand (hand count: {hand.Count}/{handSize})");
            OnCardAddedToHand?.Invoke(card);
            return true;
        }

        public void Discard(CardData card)
        {
            if (card == null) return;
            if (hand.Remove(card))
            {
                discardPile.Add(card);
                OnCardRemovedFromHand?.Invoke(card);
            }
        }

        public void Exhaust(CardData card)
        {
            if (card == null) return;
            if (hand.Remove(card))
            {
                OnCardRemovedFromHand?.Invoke(card);
            }
            // exhausted cards are simply removed from piles for run duration
            bool removed = drawPile.Remove(card) || discardPile.Remove(card);
            if (removed)
            {
                OnDrawPileChanged?.Invoke();
            }
        }

        [ContextMenu("Draw One Card")]
        private void InspectorDrawOneCard()
        {
            if (!TryDrawCardToHand())
            {
                Debug.LogWarning("[DeckManager] Inspector draw failed - check deck contents or hand size.");
            }
        }

        public void SwitchActiveDeck(DeckData newDeck, bool keepHand = true)
        {
            // Simple rule: switch active deck for future draws; optionally keep current hand
            activeDeck = newDeck;

            drawPile.Clear();
            discardPile.Clear();
            if (!keepHand) hand.Clear();

            // Populate draw pile based on card counts in the new deck
            foreach (var entry in newDeck.defaultCards)
            {
                for (int i = 0; i < entry.count; i++)
                {
                    drawPile.Add(entry.card);
                }
            }

            ShuffleDrawPile(); // This already fires OnDrawPileChanged
        }


        /// <summary>
        /// Recycles cards from discard pile to the end of draw pile
        /// Takes the most recently discarded cards (from the end of discard pile)
        /// </summary>
        /// <param name="count">Number of cards to recycle</param>
        /// <returns>Number of cards actually recycled</returns>
        public int RecycleCardsFromDiscard(int count)
        {
            if (count <= 0 || discardPile.Count == 0)
            {
                return 0;
            }

            int recycled = Mathf.Min(count, discardPile.Count);
            
            // Take cards from the end of discard pile (most recently discarded)
            // and add them to the end of draw pile
            // Remove from end backwards to avoid index shifting issues
            for (int i = 0; i < recycled; i++)
            {
                int chosenIndex = UnityEngine.Random.Range(0, discardPile.Count);
                CardData card = discardPile[chosenIndex];
                discardPile.RemoveAt(chosenIndex);
                drawPile.Add(card); // Add to end of draw pile
            }

            Debug.Log($"[DeckManager] Recycled {recycled} card{(recycled > 1 ? "s" : "")} from discard to draw pile");
            OnDrawPileChanged?.Invoke();
            return recycled;
        }

        /// <summary>
        /// Recovers cards from discard pile to draw pile at the start of a round
        /// Uses the configured cardsRecoveredPerRound value
        /// </summary>
        /// <returns>Number of cards actually recovered</returns>
        public int RecoverCardsAtRoundStart()
        {
            if (discardPile.Count == 0)
            {
                Debug.Log("[DeckManager] No cards in discard pile to recover");
                return 0;
            }

            int recovered = Mathf.Min(cardsRecoveredPerRound, discardPile.Count);
            
            // Randomly select cards from discard pile to recover
            for (int i = 0; i < recovered; i++)
            {
                int chosenIndex = UnityEngine.Random.Range(0, discardPile.Count);
                CardData card = discardPile[chosenIndex];
                discardPile.RemoveAt(chosenIndex);
                drawPile.Add(card); // Add to end of draw pile
            }

            // Shuffle the draw pile after recovery
            ShuffleDrawPile();
            
            Debug.Log($"[DeckManager] Recovered {recovered} card{(recovered > 1 ? "s" : "")} from discard pile at round start");
            return recovered;
        }

        public int GetDrawCount() => drawPile.Count;
        public int GetDiscardCount() => discardPile.Count;
    }
}
