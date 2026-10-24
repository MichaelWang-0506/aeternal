using System.Collections.Generic;
using UnityEngine;
using Aeternal.Cards;
using Aeternal.Decks;
using Aeternal.Combat;
using UnityEngine.UI;

namespace Aeternal.UI
{
    public class CardHandDisplay : MonoBehaviour
    {
        [Header("References")]
        public bool isDiscardPileDisplay;

        [Header("References")]
        public DeckManager deckManager;
        public TurnManager turnManager;
        public CardResolver resolver;
        public GameObject player;
        public GameObject enemy;
        public Aeternal.Resources.ResourceComponent playerResources;

        [Header("Prefabs")]
        public GameObject cardPrefab; // your Card prefab with CardUI
        public Transform handParent;  // usually Card Deck Display

        private List<GameObject> activeCards = new List<GameObject>();
        private bool isInitialized = false;

        private void Awake()
        {
            // Subscribe to deck manager events
            if (deckManager != null)
            {
                deckManager.OnCardAddedToHand += OnCardAddedToHand;
                deckManager.OnCardRemovedFromHand += OnCardRemovedFromHand;
            }
            if (turnManager != null)
            {
                turnManager.OnRoundStart += SyncHandDisplay;
            }
        }

        void Start()
        {
            // Initialize on Start to ensure deck is ready
            isInitialized = true;
            // Clear any cards that might have been displayed incorrectly
            ClearAllCards();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (deckManager != null)
            {
                deckManager.OnCardAddedToHand -= OnCardAddedToHand;
                deckManager.OnCardRemovedFromHand -= OnCardRemovedFromHand;
            }
            
            ClearAllCards();
        }

        private void OnCardAddedToHand(CardData card)
        {
            // When a card is added, sync the entire display to ensure correct order
            // This handles duplicate card types correctly
            if (card != null)
            {
                SyncHandDisplay();
            }
        }

        private void OnCardRemovedFromHand(CardData card)
        {
            // When a card is removed, sync the entire display to ensure correct order
            // This handles duplicate card types correctly
            if (card != null)
            {
                SyncHandDisplay();
            }
        }

        /// <summary>
        /// Instantiates a card UI for a single card and adds it to the hand display
        /// </summary>
        private GameObject InstantiateCard(CardData card)
        {
            
            if (card == null) return null;
            if (handParent == null)
            {
                Debug.LogWarning("[CardHandDisplay] handParent is not assigned!");
                return null;
            }

            // // Verify this card is actually in the hand (safety check)
            // if (deckManager != null && deckManager.hand != null)
            // {
            //     if (!deckManager.hand.Contains(card))
            //     {
            //         Debug.LogWarning($"[CardHandDisplay] Attempted to instantiate card {card.id} that is not in hand!");
            //         return null;
            //     }
            // }

            var go = Instantiate(cardPrefab, handParent);
            var ui = go.GetComponent<CardUI>();
            if (ui != null)
            {
                ui.deckManager = deckManager;
                ui.resolver = resolver;
                ui.playerResources = playerResources;
                ui.player = player;
                ui.enemy = enemy;
                ui.Setup(card, card.artwork);
                if(isDiscardPileDisplay) ui.GetComponent<Button>().enabled = false;
            }
            
            activeCards.Add(go);
            return go;
        }

        /// <summary>
        /// Syncs the hand display to match the current hand state exactly.
        /// Handles duplicate card types by matching the hand list order.
        /// </summary>
        public void SyncHandDisplay()
        {
            // Don't sync until initialized
            if (!isInitialized) return;
            
            if (deckManager == null) return;

            List<CardData> hand;
            if (!isDiscardPileDisplay) hand = deckManager.hand;
            else hand = deckManager.discardPile;

            if (hand == null)
            {
                // If hand is null, clear display
                ClearAllCards();
                return;
            }

            // Safety check: ensure we're only displaying cards from the hand, not the draw pile
            // If hand is empty, clear all displayed cards
            if (hand.Count == 0)
            {
                ClearAllCards();
                return;
            }

            // Count how many of each card type are in hand
            var handCardCounts = new Dictionary<CardData, int>();
            foreach (var card in hand)
            {
                if (card != null)
                {
                    handCardCounts.TryGetValue(card, out int count);
                    handCardCounts[card] = count + 1;
                }
            }

            // Count how many of each card type are currently displayed
            var displayedCardCounts = new Dictionary<CardData, int>();
            foreach (var go in activeCards)
            {
                if (go != null)
                {
                    var ui = go.GetComponent<CardUI>();
                    if (ui != null && ui.card != null)
                    {
                        displayedCardCounts.TryGetValue(ui.card, out int count);
                        displayedCardCounts[ui.card] = count + 1;
                    }
                }
            }

            // If counts don't match, rebuild the display
            bool needsRebuild = activeCards.Count != hand.Count;
            if (!needsRebuild)
            {
                foreach (var kvp in handCardCounts)
                {
                    displayedCardCounts.TryGetValue(kvp.Key, out int displayedCount);
                    if (displayedCount != kvp.Value)
                    {
                        needsRebuild = true;
                        break;
                    }
                }
            }

            if (needsRebuild)
            {
                // Clear and rebuild to match hand exactly
                ClearAllCards();
                
                if (!isDiscardPileDisplay)
                {
                    Debug.Log($"[CardHandDisplay] Syncing hand display: {hand.Count} cards in hand");
                }
                else
                {
                    Debug.Log($"[CardHandDisplay] Syncing discard pile display: {hand.Count} cards in discard pile");
                }
                
                
                // Instantiate cards in the exact order they appear in hand
                foreach (var card in hand)
                {
                    if (card != null)
                    {
                        InstantiateCard(card);
                    }
                }
            }
        }

        /// <summary>
        /// Refreshes the hand by drawing cards and syncing the display
        /// </summary>
        public void RefreshHand()
        {
            if (isDiscardPileDisplay) return;
            Debug.Log($"[HAND] Drawing {deckManager.GetDrawCount()} cards, discard {deckManager.GetDiscardCount()}");

            // Temporarily unsubscribe from events to prevent double instantiation
            if (deckManager != null)
            {
                deckManager.OnCardAddedToHand -= OnCardAddedToHand;
                deckManager.OnCardRemovedFromHand -= OnCardRemovedFromHand;
            }

            // Clear old visual cards
            ClearAllCards();

            // Draw from deck (this won't trigger events since we unsubscribed)
            var hand = deckManager.DrawToHand();
            
            // Resubscribe to events
            if (deckManager != null)
            {
                deckManager.OnCardAddedToHand += OnCardAddedToHand;
                deckManager.OnCardRemovedFromHand += OnCardRemovedFromHand;
            }

            // Sync the display once (this will instantiate all cards)
            SyncHandDisplay();
        }

        /// <summary>
        /// Clears all displayed cards
        /// </summary>
        private void ClearAllCards()
        {
            foreach (var c in activeCards)
            {
                if (c != null) Destroy(c);
            }
            activeCards.Clear();
        }

    }
}
