using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Cards;
using Aeternal.FX;

namespace Aeternal.UI
{
    /// <summary>
    /// Shows a card face outside combat (map deck view, merchant, rewards) using the same Card
    /// prefab as the hand, so a card looks the same everywhere. Optionally clickable, with a
    /// caption under it (price, "STORM SHARD", ...). The card can't be played from here.
    /// </summary>
    public static class CardView
    {
        public const float Width = 220f;
        public const float Height = 308f;

        /// <summary>
        /// Adds a card face to parent, scaled to the given width. onClick null makes it
        /// display-only. Returns the root of the card and its caption.
        /// </summary>
        public static RectTransform Spawn(Transform parent, CardData card, float width, Action onClick = null,
            string caption = null, Color? captionColor = null)
        {
            float scale = width / Width;
            var slot = new GameObject(card != null ? card.name : "Card", typeof(RectTransform), typeof(LayoutElement));
            var slotRect = (RectTransform)slot.transform;
            slotRect.SetParent(parent, false);
            slotRect.sizeDelta = new Vector2(width, Height * scale + (caption != null ? 40f : 0f));
            var layout = slot.GetComponent<LayoutElement>();
            layout.preferredWidth = slotRect.sizeDelta.x;
            layout.preferredHeight = slotRect.sizeDelta.y;

            var prefab = BattlefieldStyle.Load().cardPrefab;
            GameObject face;
            if (prefab != null)
            {
                face = UnityEngine.Object.Instantiate(prefab, slotRect);
                var ui = face.GetComponent<CardUI>();
                if (ui != null)
                {
                    ui.displayOnly = true;
                    if (card != null) ui.Setup(card, card.artwork);
                }
            }
            else
            {
                // No prefab configured: a plain labelled box, so screens still work.
                face = new GameObject("Card Face", typeof(RectTransform), typeof(Image));
                face.transform.SetParent(slotRect, false);
                face.GetComponent<Image>().color = new Color(0.25f, 0.2f, 0.15f, 1f);
                var label = Text(face.transform, card != null ? $"{card.name}\n{card.costAP} AP\n\n{card.description}" : "", 22f, Color.white);
                Fill(label.rectTransform);
            }

            var faceRect = (RectTransform)face.transform;
            faceRect.anchorMin = faceRect.anchorMax = new Vector2(0.5f, 1f);
            faceRect.pivot = new Vector2(0.5f, 1f);
            faceRect.anchoredPosition = Vector2.zero;
            faceRect.sizeDelta = new Vector2(Width, Height);
            faceRect.localScale = new Vector3(scale, scale, 1f);

            var button = face.GetComponent<Button>();
            if (button == null) button = face.AddComponent<Button>();
            button.interactable = onClick != null;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
                face.AddComponent<HoverScale>().hoverScale = 1.06f;
            }

            if (caption != null)
            {
                var text = Text(slotRect, caption, 22f, captionColor ?? Color.white);
                text.fontStyle = FontStyles.Bold;
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(40f, 36f);
            }
            return slotRect;
        }

        /// <summary>
        /// Darkens a spawned card (0 clears it). Use this instead of fading the card: a faded
        /// card lets the artwork's printed text show through the description strip.
        /// </summary>
        public static void SetDarkness(RectTransform slot, float darkness)
        {
            if (slot == null || slot.childCount == 0) return;
            var face = slot.GetChild(0);
            var shade = face.Find("Shade");
            if (shade == null)
            {
                if (darkness <= 0f) return;
                var go = new GameObject("Shade", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(face, false);
                Fill((RectTransform)go.transform);
                go.GetComponent<Image>().raycastTarget = false;
                shade = go.transform;
            }
            shade.SetAsLastSibling();
            shade.GetComponent<Image>().color = new Color(0f, 0f, 0f, darkness);
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
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 255);
            return text;
        }

        static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
