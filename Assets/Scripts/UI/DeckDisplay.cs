using System.Collections.Generic;
using UnityEngine;
using Aeternal.Cards;
using Aeternal.Decks;

namespace Aeternal.UI
{
    /// <summary>
    /// Displays the draw pile as a stack of cards with offset positioning.
    /// The top card is the next card to be drawn (first in drawPile).
    /// </summary>
    public class DeckDisplay : MonoBehaviour
    {
        [Header("References")]
        public DeckManager deckManager;

        [Header("Prefabs")]
        public GameObject cardPrefab; // Card prefab with CardUI component

        [Header("Display Settings")]
        [Tooltip("Parent transform where deck cards will be instantiated")]
        public Transform deckParent;
        
        [Tooltip("Offset between stacked cards (X, Y)")]
        public Vector2 cardOffset = new Vector2(3f, 3f);
        
        [Tooltip("Maximum number of cards to display (0 = show all)")]
        public int maxCardsToShow = 0;
        
        [Tooltip("Z offset for each card to ensure proper stacking order")]
        public float zOffsetPerCard = -0.1f;

        private List<GameObject> displayedCards = new List<GameObject>();

        private void Awake()
        {
            
            // Subscribe to deck changes
            if (deckManager != null)
            {
                deckManager.OnDrawPileChanged += RefreshDeckDisplay;
            }
        }

        void Start()
        {
            
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (deckManager != null)
            {
                deckManager.OnDrawPileChanged -= RefreshDeckDisplay;
            }
            
            ClearDisplayedCards();
        }

        /// <summary>
        /// Refreshes the deck display to show current draw pile state
        /// </summary>
        public void RefreshDeckDisplay()
        {
            if (deckManager == null)
            {
                Debug.LogWarning("[DeckDisplay] DeckManager not assigned!");
                return;
            }

            // Clear existing displayed cards
            ClearDisplayedCards();

            // Get draw pile (first card is next to be drawn)
            var drawPile = deckManager.drawPile;
            if (drawPile == null || drawPile.Count == 0)
            {
                Debug.Log("[DeckDisplay] Draw pile is empty");
                return;
            }

            // Determine how many cards to show
            int cardsToShow = maxCardsToShow > 0 
                ? Mathf.Min(maxCardsToShow, drawPile.Count) 
                : drawPile.Count;

            // Display cards in reverse order so first card (next to draw) is on top
            // We iterate backwards so the first card gets the highest z-index
            for (int i = cardsToShow - 1; i >= 0; i--)
            {
                var cardData = drawPile[i];
                if (cardData == null) continue;

                // Create card visual
                var cardObj = Instantiate(cardPrefab, deckParent);
                
                // Position with offset (first card at base position, subsequent cards offset)
                RectTransform cardRect = cardObj.GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    float xOffset = i * cardOffset.x;
                    float yOffset = i * cardOffset.y;
                    float zOffset = (cardsToShow - 1 - i) * zOffsetPerCard; // First card has highest z
                    
                    cardRect.anchoredPosition = new Vector3(xOffset, yOffset, zOffset);
                }

                // Setup card UI (but disable interaction since these are just visual)
                var cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.Setup(cardData, cardData.artwork);
                    // Disable button/interaction for deck display cards
                    var button = cardObj.GetComponent<UnityEngine.UI.Button>();
                    if (button != null)
                    {
                        button.interactable = false;
                    }
                }

                // Set sibling index so first card (index 0) renders on top
                cardObj.transform.SetSiblingIndex(cardsToShow - 1 - i);

                displayedCards.Add(cardObj);
            }

            Debug.Log($"[DeckDisplay] Displaying {cardsToShow} cards from draw pile (total: {drawPile.Count})");
        }

        /// <summary>
        /// Clears all displayed deck cards
        /// </summary>
        private void ClearDisplayedCards()
        {
            foreach (var card in displayedCards)
            {
                if (card != null)
                    Destroy(card);
            }
            displayedCards.Clear();
        }

    }
}

