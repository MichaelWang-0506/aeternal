using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Cards;
using Aeternal.Decks;
using Aeternal.FX;

namespace Aeternal.UI
{
    /// <summary>
    /// Displays the draw pile as a stack of face-down cards with the number left on top, so
    /// the next draw stays hidden. Clicking the pile opens its contents, sorted by cost and
    /// name rather than in draw order (as in Slay the Spire).
    /// </summary>
    public class DeckDisplay : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.82f, 0.36f, 1f);
        static readonly Color BackColor = new Color(0.16f, 0.1f, 0.06f, 1f);
        static readonly Color PanelColor = new Color(0.26f, 0.17f, 0.09f, 1f);

        [Header("References")]
        public DeckManager deckManager;

        [Header("Prefabs")]
        [Tooltip("The hand's card prefab: the backs take its size and placement.")]
        public GameObject cardPrefab;

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

            ClearDisplayedCards();

            int count = deckManager.drawPile != null ? deckManager.drawPile.Count : 0;
            int cardsToShow = maxCardsToShow > 0 ? Mathf.Min(maxCardsToShow, count) : count;
            // An empty pile still shows a faint outline with its 0, and can still be opened.
            bool empty = cardsToShow == 0;
            if (empty) cardsToShow = 1;

            // Back to front, so the top of the pile (index 0) is drawn last.
            for (int i = cardsToShow - 1; i >= 0; i--)
            {
                var back = CreateBack(i == 0 ? count : -1, empty);
                var rect = (RectTransform)back.transform;
                rect.anchoredPosition3D = new Vector3(i * cardOffset.x, i * cardOffset.y, (cardsToShow - 1 - i) * zOffsetPerCard);
                back.transform.SetSiblingIndex(cardsToShow - 1 - i);
                displayedCards.Add(back);
            }
        }

        /// <summary>A face-down card; the top one shows how many cards are left.</summary>
        GameObject CreateBack(int countLabel, bool empty)
        {
            var go = new GameObject("Card Back", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            var rect = (RectTransform)go.transform;
            rect.SetParent(deckParent != null ? deckParent : transform, false);
            var source = cardPrefab != null ? cardPrefab.transform as RectTransform : null;
            if (source != null)
            {
                rect.anchorMin = source.anchorMin;
                rect.anchorMax = source.anchorMax;
                rect.pivot = source.pivot;
                rect.sizeDelta = source.sizeDelta;
                rect.localScale = source.localScale;
            }
            else
            {
                rect.sizeDelta = new Vector2(CardView.Width, CardView.Height);
            }

            go.GetComponent<Image>().color = BackColor;
            var edge = go.GetComponent<Outline>();
            edge.effectColor = Gold;
            edge.effectDistance = new Vector2(2f, -2f);
            go.GetComponent<Button>().onClick.AddListener(ShowContents);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(rect, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(12f, 12f);
            panelRect.offsetMax = new Vector2(-12f, -12f);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = false;
            var panelEdge = panel.GetComponent<Outline>();
            panelEdge.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            panelEdge.effectDistance = new Vector2(1f, -1f);

            var emblem = new GameObject("Emblem", typeof(RectTransform), typeof(Image));
            var emblemRect = (RectTransform)emblem.transform;
            emblemRect.SetParent(panelRect, false);
            emblemRect.anchorMin = emblemRect.anchorMax = emblemRect.pivot = new Vector2(0.5f, 0.5f);
            emblemRect.sizeDelta = new Vector2(90f, 90f);
            var emblemImage = emblem.GetComponent<Image>();
            emblemImage.sprite = ProceduralSprites.Star();
            emblemImage.color = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
            emblemImage.raycastTarget = false;

            if (countLabel >= 0)
            {
                var label = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
                var labelRect = (RectTransform)label.transform;
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                var text = label.GetComponent<TextMeshProUGUI>();
                text.text = countLabel.ToString();
                text.fontSize = 72f;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.outlineWidth = 0.25f;
                text.outlineColor = new Color32(0, 0, 0, 255);
                text.raycastTarget = false;
            }

            if (empty) go.AddComponent<CanvasGroup>().alpha = 0.35f;
            return go;
        }

        /// <summary>Opens the draw pile's contents, sorted so the draw order stays hidden.</summary>
        void ShowContents()
        {
            if (deckManager == null || FindFirstObjectByType<CardGridPanel>() != null) return;
            var cards = deckManager.drawPile.Where(c => c != null).OrderBy(c => c.costAP).ThenBy(c => c.name).ToList();
            var section = new CardGridPanel.Section
            {
                title = $"{cards.Count} card{(cards.Count == 1 ? "" : "s")}  <size=70%><color=#BBBBBB>not in draw order: " +
                        "when the pile runs out, your discard pile is shuffled into it</color></size>",
                cards = cards,
            };
            CardGridPanel.Show("DRAW PILE", new List<CardGridPanel.Section> { section }, null, null);
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
