using UnityEngine;

namespace Aeternal.UI
{
    /// <summary>
    /// Fades a UI element in and lifts it into place over a fifth of a second, in unscaled time
    /// (it plays while paused). Purely cosmetic: it never blocks clicks, and it moves an inner
    /// element so a layout group that places the outer one is not fought.
    /// </summary>
    public class UIIntro : MonoBehaviour
    {
        const float Seconds = 0.22f;

        CanvasGroup group;
        RectTransform mover;
        Vector2 restPosition;
        float rise;
        float delay;
        float t;

        /// <summary>
        /// Starts the intro on target. mover is what slides up by rise UI units (default the
        /// target itself; pass a child when a layout group positions the target).
        /// </summary>
        public static void Play(RectTransform target, float rise = 40f, float delay = 0f, RectTransform mover = null)
        {
            if (target == null) return;
            var intro = target.gameObject.AddComponent<UIIntro>();
            intro.group = target.GetComponent<CanvasGroup>();
            if (intro.group == null) intro.group = target.gameObject.AddComponent<CanvasGroup>();
            intro.mover = mover != null ? mover : target;
            intro.restPosition = intro.mover.anchoredPosition;
            intro.rise = rise;
            intro.delay = delay;
            intro.Apply(0f);
        }

        void Update()
        {
            if (delay > 0f) { delay -= Time.unscaledDeltaTime; return; }
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Seconds);
            Apply(t);
            if (t >= 1f) Destroy(this);
        }

        void Apply(float k)
        {
            float e = 1f - (1f - k) * (1f - k); // ease out
            if (group != null) group.alpha = e;
            if (mover != null) mover.anchoredPosition = restPosition + new Vector2(0f, -rise * (1f - e));
        }

        void OnDestroy()
        {
            if (group != null) group.alpha = 1f;
            if (mover != null) mover.anchoredPosition = restPosition;
        }
    }
}
