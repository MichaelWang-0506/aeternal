using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Cards;
using Aeternal.FX;

namespace Aeternal.UI
{
    /// <summary>
    /// After a won fight: about a second of victory (the title slams in, a burst of stars,
    /// the gold and Aether earned rise), then the card choices shown as real card faces. Click a card to
    /// take it, or skip. Storm Shards are marked as sealed for the god's deck. In a region with
    /// deck-choice rewards (ShowDeckChoice): pick which owned deck to grow, 1 of 3 from its pool,
    /// then 1 of a few sealed god-deck cards; each step can be skipped. Built at runtime.
    /// </summary>
    public class RewardScreen : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.82f, 0.36f, 1f);
        const float VictorySeconds = 1.1f;
        const float CardWidth = 250f;
        const int Sparks = 14;

        public struct Choice
        {
            public CardData card;
            public bool isShard;
        }

        /// <summary>An owned deck the reward can grow: its name, size and the cards offered for it.</summary>
        public class DeckOffer
        {
            public string name;
            public int size;
            public List<CardData> cards = new List<CardData>();
            public object deck;   // passed back to the caller
        }

        Action<Choice> onPick;
        Action onSkip;
        RectTransform root;
        bool chosen;

        /// <summary>Plays the victory beat, then offers the choices.</summary>
        public static void Show(int goldEarned, int aetherEarned, List<Choice> choices, string shardDeck, Action<Choice> onPick, Action onSkip)
        {
            var screen = Create();
            screen.onPick = onPick;
            screen.onSkip = onSkip;
            screen.StartCoroutine(screen.Play(goldEarned, aetherEarned, choices, shardDeck));
        }

        /// <summary>
        /// Deck-choice reward: the victory beat, then a deck to grow (skipped with only one), 1 of
        /// its offered cards, then 1 of the sealed cards. onDeckCard and onSealed get each pick;
        /// onDone runs at the end.
        /// </summary>
        public static void ShowDeckChoice(int goldEarned, int aetherEarned, List<DeckOffer> decks, List<CardData> sealedCards,
            string sealedDeck, Action<DeckOffer, CardData> onDeckCard, Action<CardData> onSealed, Action onDone)
        {
            var screen = Create();
            screen.StartCoroutine(screen.PlayDeckChoice(goldEarned, aetherEarned, decks, sealedCards, sealedDeck,
                onDeckCard, onSealed, onDone));
        }

        static RewardScreen Create()
        {
            var go = new GameObject("Reward", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 230;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var screen = go.AddComponent<RewardScreen>();
            screen.root = (RectTransform)go.transform;
            return screen;
        }

        IEnumerator PlayDeckChoice(int gold, int aether, List<DeckOffer> decks, List<CardData> sealedCards, string sealedDeck,
            Action<DeckOffer, CardData> onDeckCard, Action<CardData> onSealed, Action onDone)
        {
            yield return Victory(gold, aether);
            decks = decks?.FindAll(d => d.cards.Count > 0) ?? new List<DeckOffer>();

            // 1. Which deck to grow, then 1 of its cards. Back returns to the deck choice.
            while (decks.Count > 0)
            {
                DeckOffer deck = decks.Count == 1 ? decks[0] : null;
                bool skipped = false;
                if (deck == null)
                {
                    var stage = Stage("Choose a deck to grow");
                    var row = Row(stage, 1500f, 200f, 40f);
                    foreach (var offer in decks)
                    {
                        var o = offer;
                        var button = MakeButton(row, $"<b>{o.name.ToUpperInvariant()}</b>\n<size=70%>{o.size} cards</size>", () => deck = o);
                        button.sizeDelta = new Vector2(380f, 150f);
                    }
                    var skip = MakeButton(stage, "Skip", () => skipped = true);
                    Bottom(skip);
                    yield return new WaitUntil(() => deck != null || skipped);
                    Destroy(stage.gameObject);
                    if (skipped) break;
                }

                CardData picked = null;
                bool back = false;
                var cardStage = Stage($"Choose a card for the {deck.name} deck");
                var cardRow = Row(cardStage, 1500f, 460f, 60f);
                foreach (var card in deck.cards)
                {
                    var c = card;
                    var slot = CardView.Spawn(cardRow, c, CardWidth, () => picked = c, null, Gold);
                    UIIntro.Play(slot, rise: 60f, mover: slot.childCount > 0 ? slot.GetChild(0) as RectTransform : null);
                    yield return new WaitForSecondsRealtime(0.08f);
                }
                var skipCard = MakeButton(cardStage, "Skip", () => skipped = true);
                Bottom(skipCard);
                if (decks.Count > 1)
                {
                    var backButton = MakeButton(cardStage, "Back", () => back = true);
                    Bottom(backButton);
                    backButton.anchoredPosition = new Vector2(-300f, 60f);
                    skipCard.anchoredPosition = new Vector2(300f, 60f);
                }
                yield return new WaitUntil(() => picked != null || skipped || back);
                Destroy(cardStage.gameObject);
                if (back) continue;
                if (picked != null) onDeckCard?.Invoke(deck, picked);
                break;
            }

            // 2. A card set aside for the sealed god deck.
            if (sealedCards != null && sealedCards.Count > 0)
            {
                var region = Aeternal.Core.RunManager.Instance != null ? Aeternal.Core.RunManager.Instance.CurrentMap : null;
                string shardName = region != null ? region.ShardName : "Storm Shard";
                CardData picked = null;
                bool skipped = false;
                var stage = Stage($"Choose a {shardName} for the sealed {sealedDeck} deck");
                var row = Row(stage, 1500f, 460f, 60f);
                foreach (var card in sealedCards)
                {
                    var c = card;
                    var slot = CardView.Spawn(row, c, CardWidth, () => picked = c, $"{shardName.ToUpperInvariant()} · SEALED", Gold);
                    UIIntro.Play(slot, rise: 60f, mover: slot.childCount > 0 ? slot.GetChild(0) as RectTransform : null);
                    yield return new WaitForSecondsRealtime(0.08f);
                }
                var skip = MakeButton(stage, "Skip", () => skipped = true);
                Bottom(skip);
                yield return new WaitUntil(() => picked != null || skipped);
                Destroy(stage.gameObject);
                if (picked != null) onSealed?.Invoke(picked);
            }

            chosen = true;
            Destroy(gameObject);
            onDone?.Invoke();
        }

        /// <summary>A step of the reward: a container with its heading, under the victory title.</summary>
        RectTransform Stage(string heading)
        {
            var stage = Rect("Stage", root);
            Fill(stage);
            var text = Text(stage, heading, 34f, new Color(1f, 1f, 1f, 0.85f));
            Place(text.rectTransform, new Vector2(0f, 270f), new Vector2(1400f, 50f));
            return stage;
        }

        static RectTransform Row(RectTransform parent, float width, float height, float spacing)
        {
            var row = Rect("Choices", parent);
            Place(row, new Vector2(0f, -20f), new Vector2(width, height));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        static void Bottom(RectTransform button)
        {
            button.anchorMin = button.anchorMax = button.pivot = new Vector2(0.5f, 0f);
            button.anchoredPosition = new Vector2(0f, 60f);
            button.sizeDelta = new Vector2(260f, 64f);
        }

        /// <summary>The victory beat: the title slams in, stars burst, the gold and Aether earned rise; then they move up.</summary>
        IEnumerator Victory(int gold, int aether)
        {
            var dim = Rect("Dim", root);
            Fill(dim);
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = Color.clear;

            var title = Text(root, "VICTORY", 120f, Gold);
            title.fontStyle = FontStyles.Bold;
            title.outlineWidth = 0.15f;
            title.outlineColor = new Color32(60, 30, 0, 255);
            Place(title.rectTransform, new Vector2(0f, 120f), new Vector2(1400f, 180f));

            var earned = new List<string>();
            if (gold > 0) earned.Add($"+{gold} Gold");
            if (aether > 0) earned.Add($"<color=#9FE3FF>+{aether} Aether</color>");
            var goldText = Text(root, string.Join("   ", earned), 44f, Gold);
            goldText.fontStyle = FontStyles.Bold;
            Place(goldText.rectTransform, new Vector2(0f, -10f), new Vector2(600f, 60f));

            var sparks = new List<(RectTransform rect, Vector2 dir)>();
            for (int i = 0; i < Sparks; i++)
            {
                var spark = Rect("Spark", root);
                Place(spark, new Vector2(0f, 120f), new Vector2(34f, 34f));
                var image = spark.gameObject.AddComponent<Image>();
                image.sprite = ProceduralSprites.Star();
                image.color = Gold;
                image.raycastTarget = false;
                float a = i * Mathf.PI * 2f / Sparks;
                sparks.Add((spark, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(260f, 420f)));
            }

            // The victory beat.
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / VictorySeconds)
            {
                dimImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.9f, Mathf.Clamp01(t * 3f)));
                float slam = t < 0.18f ? Mathf.Lerp(2.2f, 0.92f, t / 0.18f) : Mathf.Lerp(0.92f, 1f, Mathf.Clamp01((t - 0.18f) / 0.15f));
                title.rectTransform.localScale = new Vector3(slam, slam, 1f);
                title.alpha = Mathf.Clamp01(t * 6f);
                goldText.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-60f, -10f, Mathf.Clamp01(t * 2f)));
                goldText.alpha = Mathf.Clamp01((t - 0.2f) * 4f);
                foreach (var (rect, dir) in sparks)
                {
                    float e = 1f - (1f - t) * (1f - t);
                    rect.anchoredPosition = new Vector2(0f, 120f) + dir * e;
                    rect.localRotation = Quaternion.Euler(0f, 0f, t * 360f);
                    rect.GetComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 1f - t);
                }
                yield return null;
            }
            foreach (var (rect, _) in sparks) Destroy(rect.gameObject);

            // Make room for the cards: the title and gold move up.
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                float e = t * t * (3f - 2f * t);
                title.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(120f, 410f, e));
                float s = Mathf.Lerp(1f, 0.6f, e);
                title.rectTransform.localScale = new Vector3(s, s, 1f);
                goldText.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-10f, 330f, e));
                yield return null;
            }
        }

        IEnumerator Play(int gold, int aether, List<Choice> choices, string shardDeck)
        {
            yield return Victory(gold, aether);

            if (choices == null || choices.Count == 0)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                Finish(null);
                yield break;
            }

            var heading = Text(root, "Choose a card", 34f, new Color(1f, 1f, 1f, 0.85f));
            Place(heading.rectTransform, new Vector2(0f, 270f), new Vector2(800f, 50f));

            var row = Rect("Choices", root);
            Place(row, new Vector2(0f, -20f), new Vector2(1500f, 460f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 60f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            foreach (var choice in choices)
            {
                var c = choice;
                var region = Aeternal.Core.RunManager.Instance != null ? Aeternal.Core.RunManager.Instance.CurrentMap : null;
                string shardName = region != null ? region.ShardName : "Storm Shard";
                string caption = c.isShard ? $"{shardName.ToUpperInvariant()} · SEALED" : null;
                var slot = CardView.Spawn(row, c.card, CardWidth, () => Finish(c), caption, Gold);
                // The row's layout places the slot, so the face inside it is what rises.
                UIIntro.Play(slot, rise: 60f, mover: slot.childCount > 0 ? slot.GetChild(0) as RectTransform : null);
                yield return new WaitForSecondsRealtime(0.08f); // deal them one after another
            }

            var skip = MakeButton(root, "Skip", () => Finish(null));
            skip.anchorMin = skip.anchorMax = skip.pivot = new Vector2(0.5f, 0f);
            skip.anchoredPosition = new Vector2(0f, 60f);
            skip.sizeDelta = new Vector2(260f, 64f);
        }

        void Finish(Choice? choice)
        {
            if (chosen) return;
            chosen = true;
            Destroy(gameObject);
            if (choice.HasValue) onPick?.Invoke(choice.Value);
            else onSkip?.Invoke();
        }

        // ------------------------------------------------------------------

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static RectTransform MakeButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.12f, 0.95f);
            var edge = go.GetComponent<Outline>();
            edge.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
            edge.effectDistance = new Vector2(2f, -2f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            go.AddComponent<HoverScale>();
            var text = Text(go.transform, label, 30f, Color.white);
            Fill(text.rectTransform);
            return (RectTransform)go.transform;
        }

        static TMP_Text Text(Transform parent, string content, float size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
    }
}
