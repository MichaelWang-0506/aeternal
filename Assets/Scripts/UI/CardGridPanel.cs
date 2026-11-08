using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Cards;

namespace Aeternal.UI
{
    /// <summary>
    /// A full-screen, scrollable grid of card faces in sections (e.g. "Deck", "Sealed Storm
    /// Shards"). View-only, or pick one card. Used for the map's deck view and for choosing a
    /// card to remove. Built at runtime; closes itself.
    /// </summary>
    public class CardGridPanel : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.82f, 0.36f, 1f);
        const float CardWidth = 176f;
        const int Columns = 6;

        /// <summary>A titled group of cards. A caption per card is optional.</summary>
        public class Section
        {
            public string title;
            public List<CardData> cards = new List<CardData>();
            public List<string> captions;
            public bool dimmed;
            /// <summary>Picking a card of this section calls this instead of the panel's onPick.</summary>
            public Action<CardData> onPick;
        }

        Action<CardData> onPick;
        Action onClose;

        /// <summary>
        /// Opens the panel. onPick null makes it view-only; picking a card closes the panel.
        /// onClose runs when it closes either way (after onPick).
        /// </summary>
        public static CardGridPanel Show(string title, List<Section> sections, Action<CardData> onPick, Action onClose,
            int sortingOrder = 260)
        {
            var go = new GameObject("Card Grid", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = go.AddComponent<CardGridPanel>();
            panel.onPick = onPick;
            panel.onClose = onClose;
            panel.Build(title, sections);
            return panel;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void Build(string title, List<Section> sections)
        {
            var dim = Rect("Dim", transform);
            Fill(dim);
            // Opaque: whatever opened the panel (an event, the merchant, the map) mustn't read through the cards.
            dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 1f);

            var heading = Text(dim, title, 52f, Gold);
            heading.fontStyle = FontStyles.Bold;
            Top(heading.rectTransform, -30f, 70f);

            // Scrolling area: viewport (masked) + content that grows with the cards.
            var viewport = Rect("Viewport", dim);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(Columns * (CardWidth + 24f) + 40f, -230f);
            viewport.anchoredPosition = new Vector2(0f, -110f);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f); // catches scroll drags

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 16f;
            column.padding = new RectOffset(20, 20, 10, 20);
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = column.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            foreach (var section in sections)
            {
                if (section.cards.Count == 0 && section.title == null) continue;
                if (section.title != null)
                {
                    var label = Text(content, section.title, 30f, new Color(1f, 1f, 1f, 0.85f));
                    label.alignment = TextAlignmentOptions.Left;
                    Size(label.gameObject, Columns * (CardWidth + 24f), 44f);
                }
                var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
                gridGo.transform.SetParent(content, false);
                var grid = gridGo.GetComponent<GridLayoutGroup>();
                bool hasCaptions = section.captions != null;
                grid.cellSize = new Vector2(CardWidth, CardWidth / CardView.Width * CardView.Height + (hasCaptions ? 40f : 0f));
                grid.spacing = new Vector2(24f, 24f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Columns;
                grid.childAlignment = TextAnchor.UpperLeft;
                int rows = Mathf.CeilToInt(section.cards.Count / (float)Columns);
                Size(gridGo, Columns * (CardWidth + 24f), Mathf.Max(1, rows) * (grid.cellSize.y + 24f));

                if (section.cards.Count == 0)
                {
                    Text(gridGo.transform, "None yet", 24f, new Color(1f, 1f, 1f, 0.5f));
                    continue;
                }
                for (int i = 0; i < section.cards.Count; i++)
                {
                    var card = section.cards[i];
                    string caption = hasCaptions && i < section.captions.Count ? section.captions[i] : null;
                    var sectionPick = section.onPick;
                    Action click = (onPick != null || sectionPick != null) && !section.dimmed ? () => Pick(card, sectionPick) : (Action)null;
                    var view = CardView.Spawn(gridGo.transform, card, CardWidth, click, caption, Gold);
                    if (section.dimmed) CardView.SetDarkness(view, 0.45f);
                }
            }

            bool picking = onPick != null || sections.Any(s => s.onPick != null);
            var close = MakeButton(dim, picking ? "Cancel" : "Close", Close);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(0.5f, 0f);
            close.anchoredPosition = new Vector2(0f, 30f);
            close.sizeDelta = new Vector2(320f, 70f);
        }

        void Pick(CardData card, Action<CardData> sectionPick = null)
        {
            var pick = sectionPick ?? onPick;
            onPick = null;
            pick?.Invoke(card);
            Close();
        }

        void Close()
        {
            if (this == null) return;
            var closing = onClose;
            onClose = null;
            Destroy(gameObject);
            closing?.Invoke();
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

        static void Top(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, height);
        }

        static void Size(GameObject go, float width, float height)
        {
            var layout = go.GetComponent<LayoutElement>();
            if (layout == null) layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
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
            var text = Text(go.transform, label, 32f, Color.white);
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
