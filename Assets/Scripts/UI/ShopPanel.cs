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
    /// Hermus, the divine merchant (a map node): cards for gold, a Storm Shard while the
    /// region's god deck is sealed, Aether for Shift upgrades (GDD: "trade/purchase upgrading
    /// materials"), and a once-per-visit card removal. Prices and stock size come from MapData.
    /// Built at runtime; calls onDone when the player leaves.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.82f, 0.36f, 1f);
        const float CardWidth = 200f;

        RunManager run;
        Action onDone;
        TMP_Text goldText;
        TMP_Text removalLabel;
        Button removalButton;
        bool removalUsed;
        int removalPrice;
        TMP_Text aetherLabel;
        Button aetherButton;
        int aetherPrice;
        int aetherLeft;
        readonly List<(RectTransform view, int price, bool sold)> stock = new List<(RectTransform, int, bool)>();

        public static void Show(RunManager run, Action onDone)
        {
            var go = new GameObject("Merchant", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 240;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var shop = go.AddComponent<ShopPanel>();
            shop.run = run;
            shop.onDone = onDone;
            shop.Build();
            UIIntro.Play((RectTransform)go.transform, rise: 0f);
        }

        void Build()
        {
            var map = run.CurrentMap;
            removalPrice = map.removalPrice;
            aetherPrice = map.aetherPrice;
            aetherLeft = Mathf.Max(0, map.shopAetherStock);

            var dim = Rect("Dim", transform);
            Fill(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 0.92f);

            var title = Text(dim, "HERMUS, THE DIVINE MERCHANT", 54f, Gold);
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(0f, 440f), new Vector2(1400f, 70f));
            var line = Text(dim, "\"Everything has a price, mortal. Even a second chance.\"", 28f, new Color(1f, 1f, 1f, 0.7f));
            line.fontStyle = FontStyles.Italic;
            Place(line.rectTransform, new Vector2(0f, 385f), new Vector2(1400f, 40f));

            goldText = Text(dim, "", 36f, Gold);
            goldText.fontStyle = FontStyles.Bold;
            goldText.alignment = TextAlignmentOptions.Right;
            var goldRect = goldText.rectTransform;
            goldRect.anchorMin = goldRect.anchorMax = goldRect.pivot = new Vector2(1f, 1f);
            goldRect.anchoredPosition = new Vector2(-50f, -40f);
            goldRect.sizeDelta = new Vector2(400f, 50f);

            // The wares: normal cards, then a Storm Shard if the god's deck is still sealed.
            var row = Rect("Wares", dim);
            Place(row, new Vector2(0f, 40f), new Vector2(1600f, 360f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 36f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var rng = new System.Random();
            var wares = map.cardRewardPool.Where(c => c != null).Distinct().OrderBy(_ => rng.Next())
                .Take(Mathf.Max(0, map.shopCardCount)).Select(c => (card: c, shard: false)).ToList();
            if (run.CollectsStormShards)
            {
                var shard = map.stormShardPool.Where(c => c != null).OrderBy(_ => rng.Next()).FirstOrDefault();
                if (shard != null) wares.Add((shard, true));
            }
            foreach (var (card, shard) in wares)
            {
                int price = (shard ? map.shardPrice : map.cardPrice) + rng.Next(-map.priceSpread, map.priceSpread + 1);
                int index = stock.Count;
                string caption = shard ? $"{price} gold  ·  STORM SHARD" : $"{price} gold";
                var view = CardView.Spawn(row, card, CardWidth, () => Buy(index, card, shard), caption, Gold);
                stock.Add((view, price, false));
            }

            // Services.
            var services = Rect("Services", dim);
            Place(services, new Vector2(0f, -250f), new Vector2(1600f, 90f));
            var serviceRow = services.gameObject.AddComponent<HorizontalLayoutGroup>();
            serviceRow.spacing = 40f;
            serviceRow.childAlignment = TextAnchor.MiddleCenter;
            serviceRow.childControlWidth = serviceRow.childControlHeight = false;
            removalButton = MakeButton(services, "", 480f, OpenRemoval);
            removalLabel = removalButton.GetComponentInChildren<TMP_Text>();
            if (aetherLeft > 0)
            {
                aetherButton = MakeButton(services, "", 460f, BuyAether);
                aetherLabel = aetherButton.GetComponentInChildren<TMP_Text>();
            }
            MakeButton(services, "Leave", 260f, Leave);

            Refresh();
        }

        void Buy(int index, CardData card, bool shard)
        {
            var (view, price, sold) = stock[index];
            if (sold || !run.SpendGold(price)) return;
            if (shard) run.AddStormShard(card);
            else run.AddCardToDeck(card);
            stock[index] = (view, price, true);
            Refresh();
        }

        void BuyAether()
        {
            if (aetherLeft <= 0 || !run.BuyAether(aetherPrice)) return;
            aetherLeft--;
            Refresh();
        }

        void OpenRemoval()
        {
            if (removalUsed || run.Gold < removalPrice) return;
            var deck = new CardGridPanel.Section { title = "Your deck", cards = run.RunDeck.OrderBy(c => c.costAP).ThenBy(c => c.name).ToList() };
            CardGridPanel.Show($"Remove a card ({removalPrice} gold)", new List<CardGridPanel.Section> { deck }, card =>
            {
                if (!run.SpendGold(removalPrice)) return;
                run.RemoveCardFromDeck(card);
                removalUsed = true;
                Refresh();
            }, null, sortingOrder: 250);
        }

        void Refresh()
        {
            goldText.text = $"Gold: {run.Gold}   <color=#9FE3FF>Aether: {run.Aether}</color>";
            for (int i = 0; i < stock.Count; i++)
            {
                var (view, price, sold) = stock[i];
                if (view == null) continue;
                var group = view.GetComponent<CanvasGroup>();
                if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();
                group.interactable = !sold && run.Gold >= price;
                CardView.SetDarkness(view, sold ? 0.75f : run.Gold >= price ? 0f : 0.5f);
                if (sold)
                {
                    var caption = view.GetComponentsInChildren<TMP_Text>().LastOrDefault();
                    if (caption != null) caption.text = "SOLD";
                }
            }
            bool canRemove = !removalUsed && run.Gold >= removalPrice;
            removalButton.interactable = canRemove;
            removalLabel.text = removalUsed ? "Card removed" : $"Remove a card  ·  {removalPrice} gold";
            if (aetherButton != null)
            {
                aetherButton.interactable = aetherLeft > 0 && run.Gold >= aetherPrice;
                aetherLabel.text = aetherLeft > 0 ? $"Buy 1 Aether  ·  {aetherPrice} gold  <size=75%>({aetherLeft} left)</size>" : "Aether sold out";
            }
        }

        void Leave()
        {
            Destroy(gameObject);
            onDone?.Invoke();
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

        static Button MakeButton(Transform parent, string label, float width, Action onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, 76f);
            go.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.12f, 0.95f);
            var edge = go.GetComponent<Outline>();
            edge.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
            edge.effectDistance = new Vector2(2f, -2f);
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            go.AddComponent<HoverScale>();
            var text = Text(go.transform, label, 30f, Color.white);
            Fill(text.rectTransform);
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
    }
}
