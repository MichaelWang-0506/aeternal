using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Cards;
using Aeternal.Combat;
using Aeternal.Core;
using Aeternal.Decks;

namespace Aeternal.UI
{
    /// <summary>
    /// The Shift page (GDD: opened before combat with Space): each deck the player has or is
    /// sealing, what Shifting into and out of it does, and its Shift level. Aether from fights
    /// and the merchant raises a level, adding to the deck's damage, shield and heal Shift
    /// effects. Built at runtime on the map; Esc or Close shuts it and calls onClose.
    /// </summary>
    public class ShiftPanel : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.82f, 0.36f, 1f);
        static readonly Color AetherColor = new Color(0.62f, 0.89f, 1f, 1f);

        RunManager run;
        Action onClose;
        RectTransform list;
        TMP_Text aetherText;

        /// <summary>Opens the page over the map.</summary>
        public static ShiftPanel Show(RunManager run, Action onClose)
        {
            var go = new GameObject("Shift Page", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 240;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = go.AddComponent<ShiftPanel>();
            panel.run = run;
            panel.onClose = onClose;
            panel.Build();
            return panel;
        }

        void Update()
        {
            if (!PauseMenu.IsPaused && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void Close()
        {
            if (this == null) return;
            var closing = onClose;
            onClose = null;
            Destroy(gameObject);
            closing?.Invoke();
        }

        void Build()
        {
            var dim = Rect("Dim", transform);
            Fill(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.92f);

            var title = Text(dim, "DIVINE SHIFT", 54f, Gold);
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(0f, 440f), new Vector2(1400f, 70f));
            var line = Text(dim, "Upgrade what Shifting does with Aether, earned from every fight and sold by Hermus.",
                26f, new Color(1f, 1f, 1f, 0.7f));
            line.fontStyle = FontStyles.Italic;
            Place(line.rectTransform, new Vector2(0f, 385f), new Vector2(1500f, 40f));

            aetherText = Text(dim, "", 36f, AetherColor);
            aetherText.fontStyle = FontStyles.Bold;
            aetherText.alignment = TextAlignmentOptions.Right;
            var aetherRect = aetherText.rectTransform;
            aetherRect.anchorMin = aetherRect.anchorMax = aetherRect.pivot = new Vector2(1f, 1f);
            aetherRect.anchoredPosition = new Vector2(-50f, -40f);
            aetherRect.sizeDelta = new Vector2(400f, 50f);

            list = Rect("Decks", dim);
            Place(list, new Vector2(0f, 20f), new Vector2(1400f, 600f));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var note = Text(dim, "In battle, press Z (or the Shift button) to Shift to your next deck; each Shift costs Divine Focus.\n" +
                                 "Shifting opens up once you hold a second deck: a god's blessing awakens its deck.",
                22f, new Color(1f, 1f, 1f, 0.6f));
            Place(note.rectTransform, new Vector2(0f, -330f), new Vector2(1500f, 70f));

            var close = MakeButton(dim, "Close", 260f, Close);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 50f);

            Refresh();
        }

        void Refresh()
        {
            aetherText.text = $"Aether: {run.Aether}";
            foreach (Transform child in list)
            {
                child.gameObject.SetActive(false); // out of the layout now; destroyed at frame end
                Destroy(child.gameObject);
            }

            var decks = run.ShiftUpgradeDecks();
            if (decks.Count == 0)
            {
                var empty = Text(list, "No decks yet.", 30f, Color.white);
                empty.rectTransform.sizeDelta = new Vector2(1300f, 60f);
            }
            for (int i = 0; i < decks.Count; i++)
                Row(decks[i].deck, decks[i].isSealed, i == 0);
        }

        void Row(DeckData deck, bool isSealed, bool isStartingDeck)
        {
            var row = new GameObject(deck.name, typeof(RectTransform), typeof(Image), typeof(Outline));
            var rect = (RectTransform)row.transform;
            rect.SetParent(list, false);
            rect.sizeDelta = new Vector2(1300f, 160f);
            row.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.09f, 0.97f);
            var edge = row.GetComponent<Outline>();
            edge.effectColor = new Color(Gold.r, Gold.g, Gold.b, isSealed ? 0.25f : 0.6f);
            edge.effectDistance = new Vector2(2f, -2f);

            int level = run.ShiftLevel(deck);
            int bonus = run.ShiftBonus(deck);
            int cards = 0;
            if (deck.defaultCards != null) foreach (var entry in deck.defaultCards) cards += Mathf.Max(1, entry.count);
            string tag = isSealed ? "  <size=65%><color=#FF8A6B>SEALED until the blessing (upgrades are kept)</color></size>"
                : isStartingDeck ? "  <size=65%><color=#DDDDDD>your deck</color></size>" : "";
            string deckName = string.IsNullOrEmpty(deck.deckId) ? deck.name : deck.deckId;

            var heading = Text(rect, $"<b>{deckName}</b>{tag}", 34f, Gold);
            heading.alignment = TextAlignmentOptions.TopLeft;
            Anchor(heading.rectTransform, new Vector2(28f, -16f), new Vector2(900f, 46f));

            var info = Text(rect, $"Shift level <color=#9FE3FF>{level}</color>/{deck.MaxShiftLevel}" +
                                  (isStartingDeck ? "" : $"   <color=#BBBBBB>{cards} cards</color>"), 24f, Color.white);
            info.alignment = TextAlignmentOptions.TopLeft;
            Anchor(info.rectTransform, new Vector2(28f, -64f), new Vector2(900f, 34f));

            var effects = Text(rect, Effects(deck, bonus), 24f, new Color(1f, 1f, 1f, 0.85f));
            effects.alignment = TextAlignmentOptions.TopLeft;
            Anchor(effects.rectTransform, new Vector2(28f, -100f), new Vector2(900f, 56f));

            int cost = run.NextShiftUpgradeCost(deck);
            bool upgradable = HasUpgradableEffect(deck);
            string label = !upgradable ? "No effect to upgrade"
                : cost < 0 ? "Max level"
                : $"Upgrade  ·  {cost} Aether\n<size=70%>+{deck.shiftUpgradeBonus} to each effect</size>";
            var button = MakeButton(rect, label, 320f, () =>
            {
                if (run.TryUpgradeShift(deck)) Refresh();
            });
            button.interactable = upgradable && cost >= 0 && run.Aether >= cost;
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-24f, 0f);
            buttonRect.sizeDelta = new Vector2(320f, 96f);
        }

        /// <summary>"Shift in: deal 5 damage · Shift out: -", with the next level's value.</summary>
        string Effects(DeckData deck, int bonus)
        {
            int next = run.NextShiftUpgradeCost(deck) >= 0 ? bonus + deck.shiftUpgradeBonus : bonus;
            return $"Shift in: {Describe(deck.onShiftIn, bonus, next)}\nShift out: {Describe(deck.onShiftOut, bonus, next)}";
        }

        static string Describe(List<EffectPayload> effects, int bonus, int nextBonus)
        {
            if (effects == null || effects.Count == 0) return "<color=#888888>nothing</color>";
            var parts = new List<string>();
            foreach (var eff in effects)
            {
                if (eff == null) continue;
                bool raised = CombatManager.IsShiftUpgradable(eff.effectType);
                int value = eff.magnitude + (raised ? bonus : 0);
                string amount = raised && nextBonus > bonus
                    ? $"{value} <color=#9FE3FF>(next {eff.magnitude + nextBonus})</color>"
                    : value.ToString();
                parts.Add(Phrase(eff, amount));
            }
            return string.Join(", ", parts);
        }

        static string Phrase(EffectPayload eff, string amount)
        {
            switch (eff.effectType)
            {
                case EffectType.Damage: return $"deal {amount} damage" + (eff.target == EffectTarget.Self ? " to yourself" : "");
                case EffectType.AddShield: return $"gain {amount} shield";
                case EffectType.Heal: return $"heal {amount}";
                case EffectType.ModifyAP: return $"{amount} AP";
                case EffectType.ModifyDF: return $"{amount} Divine Focus";
                case EffectType.DrawCards: return $"draw {amount}";
                case EffectType.RecycleCards: return $"recycle {amount}";
                case EffectType.Move: return $"move {amount}";
                case EffectType.ApplyStatus:
                    return eff.statusEffectData != null ? $"apply {eff.statusEffectData.statusId}" : "apply a status";
                default: return eff.effectType.ToString();
            }
        }

        static bool HasUpgradableEffect(DeckData deck)
        {
            foreach (var list in new[] { deck.onShiftIn, deck.onShiftOut })
            {
                if (list == null) continue;
                foreach (var eff in list)
                    if (eff != null && CombatManager.IsShiftUpgradable(eff.effectType)) return true;
            }
            return false;
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

        /// <summary>Places a rect from its parent's top-left corner.</summary>
        static void Anchor(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
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
            var text = Text(go.transform, label, 28f, Color.white);
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
