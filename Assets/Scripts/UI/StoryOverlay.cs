using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Encounters;

namespace Aeternal.UI
{
    /// <summary>
    /// Top-most story layer for combat: a dialogue box over the battlefield, full-screen
    /// narration on black, and a title card. Each step types its text out and waits for a
    /// click (or Space / Enter); clicking while typing completes the line first. While it is
    /// up, it swallows clicks so the game underneath can't be played. Built at runtime.
    /// </summary>
    public class StoryOverlay : MonoBehaviour
    {
        const float CharsPerSecond = 45f;
        const float FadeDuration = 1.2f;
        static readonly Color SpeakerColor = new Color(1f, 0.8f, 0.35f, 1f);

        GameObject blocker;
        Image blockerImage;
        GameObject dialogueBox;
        TMP_Text speakerText;
        TMP_Text lineText;
        TMP_Text narrationText;
        TMP_Text titleText;
        TMP_Text promptText;
        TMP_Text hintText;
        TMP_Text warningText;

        public static StoryOverlay Create(Transform parent)
        {
            var go = new GameObject("Story Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var overlay = go.AddComponent<StoryOverlay>();
            overlay.Build();
            return overlay;
        }

        void Build()
        {
            blocker = Stretch(new GameObject("Blocker", typeof(RectTransform), typeof(Image)), transform);
            blockerImage = blocker.GetComponent<Image>();
            blockerImage.color = Color.clear;

            // Dialogue: a box along the bottom, over the hand.
            dialogueBox = new GameObject("Dialogue", typeof(RectTransform), typeof(Image), typeof(Outline));
            var box = (RectTransform)dialogueBox.transform;
            box.SetParent(blocker.transform, false);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
            box.pivot = new Vector2(0.5f, 0f);
            box.anchoredPosition = new Vector2(0f, 60f);
            box.sizeDelta = new Vector2(1400f, 230f);
            dialogueBox.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.05f, 0.92f);
            var edge = dialogueBox.GetComponent<Outline>();
            edge.effectColor = new Color(SpeakerColor.r, SpeakerColor.g, SpeakerColor.b, 0.6f);
            edge.effectDistance = new Vector2(2f, -2f);

            speakerText = MakeText(box, 34f, FontStyles.Bold, TextAlignmentOptions.TopLeft, SpeakerColor);
            Place(speakerText.rectTransform, 22f, 44f);
            lineText = MakeText(box, 30f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white);
            Place(lineText.rectTransform, 74f, 130f);
            hintText = MakeText(box, 20f, FontStyles.Italic, TextAlignmentOptions.BottomRight, new Color(1f, 1f, 1f, 0.55f));
            Place(hintText.rectTransform, 190f, 28f);
            hintText.text = "Click to continue >";

            // Narration and title: centred on the screen.
            narrationText = MakeText(blocker.transform, 36f, FontStyles.Italic, TextAlignmentOptions.Center, Color.white);
            Centre(narrationText.rectTransform, new Vector2(1400f, 600f), 0f);
            titleText = MakeText(blocker.transform, 84f, FontStyles.Bold, TextAlignmentOptions.Center, SpeakerColor);
            Centre(titleText.rectTransform, new Vector2(1600f, 140f), 40f);
            promptText = MakeText(blocker.transform, 26f, FontStyles.Italic, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.6f));
            Centre(promptText.rectTransform, new Vector2(1200f, 60f), -80f);

            // Warning banner: outside the blocker, so it shows without stopping play.
            warningText = MakeText(transform, 72f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            Centre(warningText.rectTransform, new Vector2(1600f, 220f), 220f);
            warningText.outlineWidth = 0.2f;
            warningText.outlineColor = new Color32(0, 0, 0, 255);
            warningText.gameObject.SetActive(false);

            HideAll();
        }

        /// <summary>
        /// A large title with a subtitle across the upper screen for a few seconds, without
        /// blocking input. Runs on its own; start it and carry on.
        /// </summary>
        public IEnumerator ShowWarning(string title, string subtitle, Color color, float seconds)
        {
            warningText.gameObject.SetActive(true);
            warningText.text = $"{title}\n<size=40%><color=#FFFFFF>{subtitle}</color></size>";
            warningText.color = color;
            const float fade = 0.25f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float a = Mathf.Min(Mathf.Clamp01(t / fade), Mathf.Clamp01((seconds - t) / fade));
                warningText.alpha = a;
                float s = 1f + 0.15f * (1f - Mathf.Clamp01(t / fade));     // slams in slightly large
                warningText.rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            warningText.gameObject.SetActive(false);
        }

        /// <summary>Dialogue over the battlefield, one line per click.</summary>
        public IEnumerator PlayDialogue(IList<DialogueLine> lines)
        {
            if (lines == null || lines.Count == 0) yield break;
            HideAll();
            blocker.SetActive(true);
            blockerImage.color = new Color(0f, 0f, 0f, 0.25f);
            dialogueBox.SetActive(true);
            foreach (var line in lines)
            {
                speakerText.text = line.speaker ?? "";
                yield return TypeAndWait(lineText, line.text);
            }
            HideAll();
        }

        /// <summary>Fades to black and shows each line centred. The screen stays black afterwards.</summary>
        public IEnumerator PlayNarration(IList<DialogueLine> lines)
        {
            HideAll();
            blocker.SetActive(true);
            yield return FadeBlocker(Color.black);
            if (lines == null) yield break;
            narrationText.gameObject.SetActive(true);
            foreach (var line in lines)
            {
                string text = string.IsNullOrEmpty(line.speaker) ? line.text : $"<b>{line.speaker}:</b> \"{line.text}\"";
                yield return TypeAndWait(narrationText, text);
            }
            narrationText.gameObject.SetActive(false);
        }

        /// <summary>Black screen at once, e.g. to hide a scene until its narration starts.</summary>
        public void CoverScreen()
        {
            HideAll();
            blocker.SetActive(true);
            blockerImage.color = Color.black;
        }

        /// <summary>Fades from black back to the game and gets out of the way.</summary>
        public IEnumerator FadeBackToGame()
        {
            HideAll();
            blocker.SetActive(true);
            yield return FadeBlocker(Color.clear);
            HideAll();
        }

        /// <summary>A title card on black; waits for a click.</summary>
        public IEnumerator ShowTitle(string title, string prompt)
        {
            blocker.SetActive(true);
            blockerImage.color = Color.black;
            narrationText.gameObject.SetActive(false);
            titleText.gameObject.SetActive(true);
            promptText.gameObject.SetActive(true);
            titleText.text = title;
            promptText.text = prompt;
            yield return WaitForAdvance();
        }

        IEnumerator TypeAndWait(TMP_Text target, string text)
        {
            target.text = text ?? "";
            target.ForceMeshUpdate();
            int total = target.textInfo.characterCount;
            float shown = 0f;
            target.maxVisibleCharacters = 0;
            yield return null; // don't let the click that opened this also skip it

            while (shown < total)
            {
                if (AdvancePressed()) break;
                shown += CharsPerSecond * Time.unscaledDeltaTime;
                target.maxVisibleCharacters = Mathf.FloorToInt(shown);
                yield return null;
            }
            target.maxVisibleCharacters = int.MaxValue;
            yield return null;
            yield return WaitForAdvance();
        }

        static IEnumerator WaitForAdvance()
        {
            while (!AdvancePressed()) yield return null;
            yield return null;
        }

        static bool AdvancePressed()
        {
            if (PauseMenu.IsPaused) return false;
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
        }

        IEnumerator FadeBlocker(Color to)
        {
            var from = blockerImage.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / FadeDuration)
            {
                blockerImage.color = Color.Lerp(from, to, t);
                yield return null;
            }
            blockerImage.color = to;
        }

        void HideAll()
        {
            dialogueBox.SetActive(false);
            narrationText.gameObject.SetActive(false);
            titleText.gameObject.SetActive(false);
            promptText.gameObject.SetActive(false);
            blocker.SetActive(false);
        }

        static GameObject Stretch(GameObject go, Transform parent)
        {
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>A full-width row inside the dialogue box, 40 in from each side.</summary>
        static void Place(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-80f, height);
        }

        static void Centre(RectTransform rect, Vector2 size, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = size;
        }

        static TMP_Text MakeText(Transform parent, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }
}
