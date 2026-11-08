using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Cards;
using Aeternal.Core;

namespace Aeternal.UI
{
    /// <summary>
    /// The region's between-fights event (MapData.regionEvent; GDD: after every two enemies).
    /// The player picks one boon: restore health, remove a card from the deck, or, while the
    /// region's god deck is sealed, take a Storm Shard of their choice. Cards are shown as card
    /// faces (hover to enlarge). Built at runtime over the combat scene or the map; calls onDone
    /// when the player has chosen.
    /// </summary>
    public class RegionEventPanel : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.82f, 0.36f, 1f);
        static readonly Color ButtonColor = new Color(0.12f, 0.1f, 0.12f, 0.95f);
        const float ShardCardWidth = 230f;

        RunManager run;
        int maxHealth;
        Action onDone;
        RectTransform box;

        public static void Show(RunManager run, int maxHealth, Action onDone)
        {
            var go = new GameObject("Region Event", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = go.AddComponent<RegionEventPanel>();
            panel.run = run;
            panel.maxHealth = maxHealth;
            panel.onDone = onDone;
            panel.Build();
            panel.ShowChoices();
        }

        void Build()
        {
            var dim = Rect("Dim", transform);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            box = Rect("Box", dim);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(1100f, 760f);
            box.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.045f, 0.06f, 0.98f);
            var edge = box.gameObject.AddComponent<Outline>();
            edge.effectColor = Gold;
            edge.effectDistance = new Vector2(2f, -2f);
        }

        void Clear()
        {
            foreach (Transform child in box)
            {
                child.gameObject.SetActive(false); // out of the layout now; destroyed at frame end
                Destroy(child.gameObject);
            }
        }

        void ShowChoices()
        {
            Clear();
            var ev = run.CurrentMap.regionEvent;
            var column = Column(box);
            var title = Text(column, ev.title, 56f, Gold);
            title.fontStyle = FontStyles.Bold;
            Size(title.gameObject, 1000f, 80f);
            var body = Text(column, ev.text, 28f, new Color(1f, 1f, 1f, 0.85f));
            body.fontStyle = FontStyles.Italic;
            Size(body.gameObject, 960f, 120f);

            Choice(column, $"Pray\n<size=70%>Restore {ev.healAmount} health</size>", () =>
            {
                run.HealBetweenFights(ev.healAmount, maxHealth);
                Finish();
            });
            Choice(column, "Offer a card\n<size=70%>Remove a card from your deck</size>", OfferCard);
            if (run.CollectsStormShards)
            {
                var shards = run.CurrentMap.stormShardPool.Where(c => c != null).Distinct()
                    .OrderBy(_ => UnityEngine.Random.value).Take(Mathf.Max(1, ev.shardChoices)).ToList();
                string deck = run.CurrentMap.blessingDeck.deckId;
                string shardName = run.CurrentMap.ShardName;
                Choice(column, $"Take a {shardName}\n<size=70%>Choose one for the sealed {deck} deck</size>", () =>
                    PickCard($"Choose a {shardName} for the {deck} deck", shards, card =>
                    {
                        run.AddStormShard(card);
                        Finish();
                    }));
            }
        }

        /// <summary>
        /// Every deck the player owns as card faces (the starting deck, then each deck awakened by a
        /// blessing; it can be long, so a scrolling grid); pick a card from any of them to remove.
        /// </summary>
        void OfferCard()
        {
            var sections = new List<CardGridPanel.Section>();
            foreach (var deck in run.OwnedDecks())
            {
                var d = deck;
                var cards = run.CardsIn(d);
                sections.Add(new CardGridPanel.Section
                {
                    title = run.UnlockedDecks.Count == 0 ? $"Your deck  ({cards.Count} cards)" : $"{run.OwnedDeckName(d)} deck  ({cards.Count} cards)",
                    cards = cards.OrderBy(c => c.costAP).ThenBy(c => c.DisplayName).ToList(),
                    onPick = card =>
                    {
                        run.RemoveCardFrom(d, card);
                        Finish();
                    },
                });
            }
            CardGridPanel.Show($"Offer a card at the {run.CurrentMap.regionEvent.title}", sections, null, null, sortingOrder: 260);
        }

        /// <summary>A few cards side by side as card faces; click one to take it.</summary>
        void PickCard(string heading, List<CardData> cards, Action<CardData> picked)
        {
            Clear();
            var column = Column(box);
            var title = Text(column, heading, 40f, Gold);
            title.fontStyle = FontStyles.Bold;
            Size(title.gameObject, 1000f, 64f);
            var hint = Text(column, "Hover a card to read it, click to take it", 24f, new Color(1f, 1f, 1f, 0.6f));
            Size(hint.gameObject, 1000f, 34f);

            var row = Rect("Cards", column);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Size(row.gameObject, 1000f, ShardCardWidth / CardView.Width * CardView.Height + 20f);

            foreach (var card in cards)
            {
                var c = card;
                CardView.Spawn(row, c, ShardCardWidth, () => picked(c));
            }
            Choice(column, "Back", ShowChoices, height: 64f);
        }

        void Finish()
        {
            Destroy(gameObject);
            onDone?.Invoke();
        }

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static RectTransform Column(Transform parent)
        {
            var rect = Rect("Column", parent);
            Stretch(rect);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 36, 36);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return rect;
        }

        static void Choice(Transform parent, string label, Action onClick, float height = 96f)
        {
            var button = MakeButton(parent, label, 32f, onClick);
            Size(button.gameObject, 620f, height);
        }

        static Button MakeButton(Transform parent, string label, float size, Action onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ButtonColor;
            var edge = go.GetComponent<Outline>();
            edge.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            edge.effectDistance = new Vector2(2f, -2f);
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            go.AddComponent<HoverScale>().hoverScale = 1.03f;

            var text = Text(go.transform, label, size, Color.white);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 6f);
            rect.offsetMax = new Vector2(-12f, -6f);
            text.fontSizeMin = 14f;
            text.fontSizeMax = size;
            return button;
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

        static void Size(GameObject go, float width, float height)
        {
            var layout = go.GetComponent<LayoutElement>();
            if (layout == null) layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
        }
    }
}
