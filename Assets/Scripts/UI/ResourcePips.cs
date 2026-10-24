using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Aeternal.UI
{
    /// <summary>
    /// One resource shown as a row of icons, one per point: "AP [gear][gear][empty] 2/3".
    /// Lit icons are points left, dim ones are spent; icons that change pop briefly so the
    /// player sees what was spent or regained. Polls its value, so it needs no event wiring.
    /// Created by BattlefieldStyler in place of the old text line.
    /// </summary>
    public class ResourcePips : MonoBehaviour
    {
        const float PopDuration = 0.25f;
        const float PopScale = 0.4f;

        Func<Vector2Int> read; // (current, max)
        Sprite icon;
        Color fullColor, emptyColor;
        float iconSize;
        float iconsWidth;

        RectTransform iconRow;
        TMP_Text valueText;
        readonly List<Image> icons = new List<Image>();
        readonly List<bool> lit = new List<bool>();
        readonly List<float> pop = new List<float>();
        int lastCurrent = -1, lastMax = -1;

        /// <summary>
        /// Builds the row in the place of <paramref name="replace"/> (same parent and rect),
        /// and hides it. <paramref name="read"/> returns (current, max).
        /// </summary>
        public static ResourcePips Create(RectTransform replace, string label, Sprite icon, Color full, Color empty,
            float iconSize, Func<Vector2Int> read)
        {
            var go = new GameObject(label + " Icons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(replace.parent, false);
            rect.anchorMin = replace.anchorMin;
            rect.anchorMax = replace.anchorMax;
            rect.pivot = replace.pivot;
            rect.anchoredPosition = replace.anchoredPosition;
            rect.sizeDelta = replace.sizeDelta;
            replace.gameObject.SetActive(false);

            var pips = go.AddComponent<ResourcePips>();
            pips.read = read;
            pips.icon = icon;
            pips.fullColor = full;
            pips.emptyColor = empty;
            pips.iconSize = iconSize;
            pips.Build(label, rect.sizeDelta.x);
            return pips;
        }

        void Build(string label, float width)
        {
            var row = GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            row.spacing = 10f;

            const float labelWidth = 64f;
            const float valueWidth = 96f;
            var labelText = MakeText(transform, label, 30f, Color.white, labelWidth);
            labelText.fontStyle = FontStyles.Bold;

            var iconsGo = new GameObject("Icons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            iconRow = (RectTransform)iconsGo.transform;
            iconRow.SetParent(transform, false);
            var iconLayout = iconsGo.GetComponent<HorizontalLayoutGroup>();
            iconLayout.childAlignment = TextAnchor.MiddleLeft;
            iconLayout.childControlWidth = iconLayout.childControlHeight = false;
            iconLayout.childForceExpandWidth = iconLayout.childForceExpandHeight = false;
            iconLayout.spacing = 4f;
            iconsWidth = Mathf.Max(iconSize, width - labelWidth - valueWidth - row.spacing * 2f);
            var iconsElement = iconsGo.AddComponent<LayoutElement>();
            iconsElement.preferredWidth = iconsWidth;
            iconsElement.preferredHeight = iconSize;

            valueText = MakeText(transform, "", 28f, fullColor, valueWidth);
        }

        void Update()
        {
            var value = read();
            int current = Mathf.Max(0, value.x);
            int max = Mathf.Max(0, value.y);

            if (max != lastMax) RebuildIcons(max);
            if (current != lastCurrent || max != lastMax)
            {
                for (int i = 0; i < icons.Count; i++)
                {
                    bool on = i < current;
                    if (on != lit[i] && lastCurrent >= 0) pop[i] = 1f;
                    lit[i] = on;
                    icons[i].color = on ? fullColor : emptyColor;
                }
                valueText.text = $"{current}/{max}";
                lastCurrent = current;
                lastMax = max;
            }

            for (int i = 0; i < icons.Count; i++)
            {
                if (pop[i] <= 0f) continue;
                pop[i] = Mathf.Max(0f, pop[i] - Time.unscaledDeltaTime / PopDuration);
                float s = 1f + PopScale * pop[i] * pop[i];
                icons[i].rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        void RebuildIcons(int max)
        {
            foreach (var image in icons) Destroy(image.gameObject);
            icons.Clear();
            lit.Clear();
            pop.Clear();

            // Shrink the icons if a large maximum would not fit the row.
            float spacing = iconRow.GetComponent<HorizontalLayoutGroup>().spacing;
            float size = max > 0 ? Mathf.Min(iconSize, (iconsWidth - spacing * (max - 1)) / max) : iconSize;
            for (int i = 0; i < max; i++)
            {
                var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(iconRow, false);
                var image = go.GetComponent<Image>();
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.rectTransform.sizeDelta = new Vector2(size, size);
                icons.Add(image);
                lit.Add(false);
                pop.Add(0f);
            }
            lastCurrent = -1;
        }

        static TMP_Text MakeText(Transform parent, string content, float size, Color color, float width)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            return text;
        }
    }
}
