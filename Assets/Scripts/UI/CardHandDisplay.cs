using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Aeternal.Cards;
using Aeternal.Decks;
using Aeternal.Combat;
using UnityEngine.UI;

namespace Aeternal.UI
{
    /// <summary>
    /// Shows the cards in hand (or, with isDiscardPileDisplay, in the discard pile) as card
    /// faces. In the hand, newly drawn cards fly in from the draw pile one after another, cards
    /// that stay slide to their new places (the sliding is DisplayCards easing them), and a shuffle of the discard pile back into the
    /// draw pile sends a stream of card backs from one pile to the other.
    /// </summary>
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

        [Header("Motion (hand only)")]
        [Tooltip("Seconds a drawn card takes to grow to full size as it leaves the draw pile.")]
        public float drawFlyTime = 0.35f;
        [Tooltip("Delay between drawn cards, so they arrive one after another.")]
        public float drawStagger = 0.08f;
        [Tooltip("Card backs flying from the discard pile to the draw pile on a reshuffle (at most).")]
        public int reshuffleCardsShown = 8;
        public Color cardBackColor = new Color(0.36f, 0.24f, 0.13f, 1f);
        public Color cardBackEdgeColor = new Color(1f, 0.8f, 0.35f, 0.8f);

        private List<GameObject> activeCards = new List<GameObject>();
        private bool isInitialized = false;

        private void Awake()
        {
            // Subscribe to deck manager events
            if (deckManager != null)
            {
                deckManager.OnCardAddedToHand += OnCardAddedToHand;
                deckManager.OnCardRemovedFromHand += OnCardRemovedFromHand;
                // The discard list also empties when it is shuffled into the draw pile.
                if (isDiscardPileDisplay) deckManager.OnDrawPileChanged += SyncHandDisplay;
                else deckManager.OnDiscardReshuffled += OnDiscardReshuffled;
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
                deckManager.OnDrawPileChanged -= SyncHandDisplay;
                deckManager.OnDiscardReshuffled -= OnDiscardReshuffled;
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
                ui.popIn = isDiscardPileDisplay; // the hand flies its cards in instead
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
                var previous = isDiscardPileDisplay ? null : SnapshotPositions();
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
                if (previous != null) AnimateIntoPlace(previous);
            }
        }

        /// <summary>Where each displayed card is now (local to the hand), per card, in hand order.</summary>
        private Dictionary<CardData, Queue<Vector2>> SnapshotPositions()
        {
            var positions = new Dictionary<CardData, Queue<Vector2>>();
            foreach (var go in activeCards)
            {
                var ui = go != null ? go.GetComponent<CardUI>() : null;
                if (ui == null || ui.card == null) continue;
                if (!positions.TryGetValue(ui.card, out var queue)) positions[ui.card] = queue = new Queue<Vector2>();
                queue.Enqueue(((RectTransform)go.transform).anchoredPosition);
            }
            return positions;
        }

        /// <summary>
        /// Starts each card where it was, so the hand's layout (DisplayCards) slides it to its
        /// new slot; new draws start hidden on the draw pile and are dealt one after another.
        /// </summary>
        private void AnimateIntoPlace(Dictionary<CardData, Queue<Vector2>> previous)
        {
            if (!(handParent is RectTransform parent) || !isActiveAndEnabled) return;
            var drawPile = FindFirstObjectByType<DeckDisplay>();
            int drawn = 0;
            foreach (var go in activeCards)
            {
                var ui = go != null ? go.GetComponent<CardUI>() : null;
                if (ui == null || ui.card == null) continue;
                var rect = (RectTransform)go.transform;
                if (previous.TryGetValue(ui.card, out var queue) && queue.Count > 0)
                    rect.anchoredPosition = queue.Dequeue();
                else if (drawPile != null)
                    StartCoroutine(Deal(rect, PileInHand(parent, drawPile.transform), drawn++ * drawStagger));
            }
        }

        /// <summary>The draw pile's position as a card position in the hand (cards anchor at the hand's centre).</summary>
        private static Vector2 PileInHand(RectTransform hand, Transform pile)
        {
            return (Vector2)hand.InverseTransformPoint(pile.position) - hand.rect.center;
        }

        /// <summary>Hidden on the draw pile until its turn, then grows and turns upright as it flies out.</summary>
        private IEnumerator Deal(RectTransform card, Vector2 pileLocal, float delay)
        {
            Vector3 scale = card.localScale;
            card.localScale = Vector3.zero; // the layout leaves hidden cards where they are
            for (float t = 0f; t < delay && card != null; t += Time.deltaTime) yield return null;
            if (card == null) yield break;
            card.anchoredPosition = pileLocal;
            for (float t = 0f; t < drawFlyTime && card != null; t += Time.deltaTime)
            {
                float k = t / drawFlyTime;
                float eased = 1f - (1f - k) * (1f - k) * (1f - k);
                card.localScale = scale * Mathf.Lerp(0.45f, 1f, eased);
                card.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-14f, 0f, eased));
                yield return null;
            }
            if (card == null) yield break;
            card.localScale = scale;
            card.localRotation = Quaternion.identity;
        }

        private void OnDiscardReshuffled(int count)
        {
            if (!isActiveAndEnabled) return;
            var drawPile = FindFirstObjectByType<DeckDisplay>();
            var discard = FindFirstObjectByType<DiscardPileButton>();
            var canvas = handParent != null ? handParent.GetComponentInParent<Canvas>() : null;
            if (drawPile == null || discard == null || canvas == null) return;
            int shown = Mathf.Min(count, reshuffleCardsShown);
            for (int i = 0; i < shown; i++)
                StartCoroutine(FlyCardBack(canvas.rootCanvas.transform, discard.transform.position, drawPile.transform.position, i * 0.06f));
        }

        private IEnumerator FlyCardBack(Transform canvas, Vector3 from, Vector3 to, float delay)
        {
            for (float t = 0f; t < delay; t += Time.deltaTime) yield return null;
            var go = new GameObject("Reshuffle Card", typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(54f, 76f);
            var image = go.GetComponent<Image>();
            image.color = cardBackColor;
            image.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = cardBackEdgeColor;
            outline.effectDistance = new Vector2(2f, -2f);
            const float time = 0.4f;
            float spin = Random.Range(-200f, 200f);
            for (float t = 0f; t < time && go != null; t += Time.deltaTime)
            {
                float k = t / time;
                float eased = k * k * (3f - 2f * k);
                rt.position = Vector3.Lerp(from, to, eased) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 120f * rt.lossyScale.y);
                rt.localRotation = Quaternion.Euler(0f, 0f, spin * (1f - k));
                yield return null;
            }
            if (go != null) Destroy(go);
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
                if (c == null) continue;
                c.SetActive(false); // out of the layout now; Destroy only happens at frame end
                Destroy(c);
            }
            activeCards.Clear();
        }

    }
}
