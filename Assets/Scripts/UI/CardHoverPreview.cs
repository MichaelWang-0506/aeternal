using UnityEngine;

namespace Aeternal.UI
{
    /// <summary>
    /// Shows an enlarged, non-interactive copy of the card under the mouse on a top-most
    /// overlay canvas, so the card text is readable. The copy ignores raycasts, so the
    /// original card underneath stays clickable. Created on first use; one per scene.
    /// </summary>
    public class CardHoverPreview : MonoBehaviour
    {
        [Tooltip("Size of the preview relative to the card in hand.")]
        public float scale = 1.8f;
        [Tooltip("Gap kept between the preview and the screen edges, in pixels.")]
        public float screenMargin = 12f;

        static CardHoverPreview instance;

        Transform holder;
        GameObject preview;
        CardUI owner;

        /// <summary>Show the enlarged preview for this card, replacing any current one.</summary>
        public static void Show(CardUI card)
        {
            if (card == null) return;
            if (instance == null) instance = Create();
            instance.ShowFor(card);
        }

        /// <summary>Hide the preview if it belongs to this card.</summary>
        public static void Hide(CardUI card)
        {
            if (instance != null && instance.owner == card) instance.Clear();
        }

        static CardHoverPreview Create()
        {
            var go = new GameObject("Card Hover Preview", typeof(RectTransform), typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above every panel that shows cards (shop, deck view, rewards)
            return go.AddComponent<CardHoverPreview>();
        }

        void Awake()
        {
            // Inactive holder: the copy's scripts must not wake up before they are stripped.
            var go = new GameObject("Holder", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            holder = go.transform;
        }

        void ShowFor(CardUI card)
        {
            Clear();
            owner = card;

            preview = Instantiate(card.gameObject, holder);
            DestroyImmediate(preview.GetComponent<CardUI>());
            // Off-combat card faces carry a HoverScale, which would pull the copy back to scale 1.
            var hover = preview.GetComponent<HoverScale>();
            if (hover != null) DestroyImmediate(hover);
            var group = preview.GetComponent<CanvasGroup>();
            if (group == null) group = preview.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 1f;

            // Both canvases are screen-space overlays, so world space is screen pixels.
            var source = (RectTransform)card.transform;
            var corners = new Vector3[4];
            source.GetWorldCorners(corners);
            Vector2 size = (corners[2] - corners[0]) * scale;
            float x = (corners[0].x + corners[2].x) * 0.5f;
            float y = corners[0].y + size.y * 0.5f; // grow upward from the card's bottom edge
            x = Mathf.Clamp(x, size.x * 0.5f + screenMargin, Screen.width - size.x * 0.5f - screenMargin);
            y = Mathf.Clamp(y, size.y * 0.5f + screenMargin, Screen.height - size.y * 0.5f - screenMargin);

            var rect = (RectTransform)preview.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = source.rect.size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = new Vector3(
                source.lossyScale.x * scale / transform.lossyScale.x,
                source.lossyScale.y * scale / transform.lossyScale.y, 1f);

            preview.transform.SetParent(transform, true);
            rect.position = new Vector3(x, y, 0f);
        }

        void Clear()
        {
            if (preview != null) Destroy(preview);
            preview = null;
            owner = null;
        }
    }
}
