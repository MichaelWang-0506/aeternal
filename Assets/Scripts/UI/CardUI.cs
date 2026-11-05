using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Aeternal.Cards;
using Aeternal.Resources;
using Aeternal.Decks;
using Aeternal.Combat;

namespace Aeternal.UI
{
    public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>
        /// Optional hook run when a card is clicked, before it is played. Returning true
        /// consumes the click and the card is not played. Null outside the tutorial, which
        /// uses it to hold the play until the player presses Next.
        /// </summary>
        public static System.Func<CardUI, bool> PlayInterceptor;

        [Header("Card Data")]
        public CardData card;
        public Image cardImage;

        [Header("Live Text (generated from card data so display always matches the real effect)")]
        public TMPro.TMP_Text nameText;
        public TMPro.TMP_Text costText;
        public TMPro.TMP_Text descriptionText;
        public DeckManager deckManager;
        public CardResolver resolver;
        public ResourceComponent playerResources;
        public GameObject player;
        public GameObject enemy;

        [Header("Game State")]
        [Tooltip("CombatManager to check game state. If null, will try to find it.")]
        public CombatManager combatManager;

        /// <summary>
        /// Set for cards shown outside combat (rewards, merchant, deck view): they never play,
        /// and their clickability is left to whoever shows them.
        /// </summary>
        [HideInInspector] public bool displayOnly;

        private Button button;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(OnClickPlayCard);

            // Get or add CanvasGroup for disabling interaction
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            StyleFace();
        }

        static readonly Color TitleColor = new Color(1f, 0.76f, 0.2f, 1f);
        static readonly Color InkColor = new Color(0.96f, 0.84f, 0.62f, 1f);

        /// <summary>
        /// The artwork is a whole printed card, and its printed title and cost differ in
        /// lettering from card to card. Plates in the game's pixel font cover both (the
        /// description strip already covers the printed rules text), so every card reads the
        /// same and always shows the card data. Safe to run twice (hover previews copy a card).
        /// Positions are in the prefab's 220 x 308 card units, sized to cover the printed title
        /// and cost circle on every card in Assets/Images.
        /// </summary>
        void StyleFace()
        {
            if (nameText != null)
            {
                var strip = nameText.transform.parent as RectTransform;
                if (strip != null && strip != transform)
                {
                    strip.gameObject.SetActive(true);
                    // Tall enough for printed titles that wrap to a second line (Skull Smash).
                    PlaceTopLeft(strip, new Vector2(50f, -10f), new Vector2(164f, 58f));
                }
                Letter(nameText, TitleColor, TMPro.FontStyles.Bold | TMPro.FontStyles.UpperCase, 12f, 26f);
            }
            if (costText != null)
            {
                var badge = costText.transform.parent as RectTransform;
                if (badge != null && badge != transform) PlaceTopLeft(badge, new Vector2(5f, -7f), new Vector2(46f, 46f));
                Letter(costText, InkColor, TMPro.FontStyles.Bold, 14f, 26f);
            }
            if (descriptionText != null) Letter(descriptionText, InkColor, TMPro.FontStyles.Normal, 14f, 22f);
        }

        static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Letter(TMPro.TMP_Text text, Color color, TMPro.FontStyles style, float min, float max)
        {
            text.color = color;
            text.fontStyle = style;
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            text.alignment = TMPro.TextAlignmentOptions.Center;
        }

        private void Start()
        {
            // Find combat manager if not assigned
            if (combatManager == null)
            {
                combatManager = FindFirstObjectByType<CombatManager>();
            }

            UpdateInteractability();
            StartCoroutine(PopIn());
        }

        // Small scale-in when the card is dealt so drawing is visible.
        private System.Collections.IEnumerator PopIn()
        {
            var target = transform.localScale;
            float t = 0f;
            transform.localScale = target * 0.7f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.14f;
                float k = Mathf.Clamp01(t);
                float eased = 1f - (1f - k) * (1f - k);
                transform.localScale = target * Mathf.LerpUnclamped(0.7f, 1f, eased);
                yield return null;
            }
            transform.localScale = target;
        }

        private void Update()
        {
            // Update interactability based on game state
            UpdateInteractability();
        }

        private void UpdateInteractability()
        {
            if (displayOnly) return;
            bool canInteract = true;

            // Check if game is over
            if (combatManager != null && combatManager.IsGameOver)
            {
                canInteract = false;
            }

            // Update button and canvas group
            if (button != null)
            {
                button.interactable = canInteract;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = canInteract;
                canvasGroup.alpha = canInteract ? 1f : 0.5f; // Dim when disabled
            }
        }

        // Hovering shows an enlarged copy so the card text is readable.
        public void OnPointerEnter(PointerEventData eventData) => CardHoverPreview.Show(this);

        public void OnPointerExit(PointerEventData eventData) => CardHoverPreview.Hide(this);

        private void OnDisable()
        {
            CardHoverPreview.Hide(this);
        }

        public void Setup(CardData data, Sprite sprite)
        {
            card = data;
            if (cardImage != null)
                cardImage.sprite = sprite;

            // Text comes from the card DATA, never the artwork, so what the player reads is
            // always exactly what the card does.
            if (data != null)
            {
                if (nameText != null) nameText.text = data.name;
                if (costText != null) costText.text = $"{data.costAP} AP";
                if (descriptionText != null) descriptionText.text = data.description;
            }
        }

        public async void OnClickPlayCard()
        {
            if (displayOnly || card == null || resolver == null) return;
            if (PlayInterceptor != null && PlayInterceptor(this)) return;

            // Check if game is over
            if (combatManager != null && combatManager.IsGameOver)
            {
                Debug.Log("[CardUI] Cannot play card - game is over");
                return;
            }

            var ctx = new CardContext
            {
                caster = player,
                explicitTarget = enemy,
                enemy = enemy
            };


            Debug.Log($"[CardUI] Playing {card.id} on enemy: {(enemy ? enemy.name : "NULL")}");

            bool success = await resolver.ResolvePlayAsync(card, ctx, playerResources, deckManager);
            if (success)
            {
                // Spawn a ghost of the card flying at the enemy so the play is visible,
                // then remove from hand.
                Aeternal.FX.CombatFXManager.Instance?.NotifyCardPlayed(
                    card.artwork, transform.position);
                Destroy(gameObject);
            }
        }
    }
}
