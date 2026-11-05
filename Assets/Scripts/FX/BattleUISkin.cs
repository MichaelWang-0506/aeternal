using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.UI;

namespace Aeternal.FX
{
    /// <summary>
    /// Restyles the combat scene's UGUI (bottom panels, hand tray, End Turn / Guard / discard
    /// buttons, combat log) to the main menu's look: dark ink panels with a gold edge and
    /// bronze buttons. Colours only, plus a caption on the discard counter; nothing moves and
    /// no game state changes. Called by BattlefieldStyler once the scene's UI exists.
    /// </summary>
    public static class BattleUISkin
    {
        /// <summary>Applies the style's battle UI colours to the current combat scene.</summary>
        public static void Apply(BattlefieldStyle style)
        {
            Panel("Bottom Panel", style.panelColor, style.panelEdgeColor);
            Panel("Player Status Panel", style.panelColor, style.panelEdgeColor);
            Panel("Card Panel", Color.clear, Color.clear);
            Panel("Card Hand", style.handTrayColor, style.panelEdgeColor);
            Panel("Discard Pile Card Panel", style.panelColor, style.panelEdgeColor);

            foreach (var endTurn in Object.FindObjectsByType<EndTurnButton>(FindObjectsSortMode.None))
                Button(endTurn.gameObject, style.primaryButtonColor, style, bold: true);
            foreach (var guard in Object.FindObjectsByType<GuardButton>(FindObjectsSortMode.None))
                Button(guard.gameObject, style.buttonColor, style, bold: false);
            foreach (var discard in Object.FindObjectsByType<DiscardPileButton>(FindObjectsSortMode.None))
            {
                Button(discard.gameObject, style.buttonColor, style, bold: true);
                Caption(discard.transform as RectTransform, "Discard", style.buttonTextColor);
            }

            var log = ActionLog.Instance;
            if (log != null)
            {
                log.entryFontSize = style.logFontSize;
                log.entryColor = style.logColor;
                // Lines logged before this ran (the first "Round Start") get the same look.
                if (log.logParent != null)
                {
                    foreach (var line in log.logParent.GetComponentsInChildren<TMP_Text>())
                    {
                        line.enableAutoSizing = false;
                        line.fontSize = style.logFontSize;
                        line.color = style.logColor;
                    }
                }
            }
        }

        static void Panel(string objectName, Color fill, Color edge)
        {
            var go = GameObject.Find(objectName);
            var image = go != null ? go.GetComponent<Image>() : null;
            if (image == null) return;
            image.color = fill;
            if (edge.a <= 0f) return;
            var outline = go.GetComponent<Outline>();
            if (outline == null) outline = go.AddComponent<Outline>();
            outline.effectColor = edge;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>Same tints as MainMenuController's buttons: rest slightly dark, light up on hover.</summary>
        static void Button(GameObject go, Color fill, BattlefieldStyle style, bool bold)
        {
            var image = go.GetComponent<Image>();
            if (image != null) image.color = fill;
            var outline = go.GetComponent<Outline>();
            if (outline == null) outline = go.AddComponent<Outline>();
            outline.effectColor = style.panelEdgeColor;
            outline.effectDistance = new Vector2(2f, -2f);

            var button = go.GetComponent<Button>();
            if (button != null)
            {
                var colors = button.colors;
                colors.normalColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                colors.highlightedColor = Color.white;
                colors.selectedColor = colors.normalColor;
                colors.pressedColor = new Color(0.65f, 0.62f, 0.58f, 1f);
                colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
                button.colors = colors;
            }
            if (go.GetComponent<HoverScale>() == null) go.AddComponent<HoverScale>();

            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = style.buttonTextColor;
                if (bold) text.fontStyle |= FontStyles.Bold;
            }
        }

        /// <summary>A small label above a counter button, so a bare number says what it counts.</summary>
        static void Caption(RectTransform target, string label, Color color)
        {
            if (target == null || target.parent == null) return;
            var go = new GameObject(label + " Caption", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(target.parent, false);
            rect.anchorMin = target.anchorMin;
            rect.anchorMax = target.anchorMax;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(Mathf.Max(90f, target.rect.width), 24f);
            var top = target.anchoredPosition.y + target.rect.height * (1f - target.pivot.y);
            var centre = target.anchoredPosition.x + target.rect.width * (0.5f - target.pivot.x);
            rect.anchoredPosition = new Vector2(centre, top + 4f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
        }
    }
}
