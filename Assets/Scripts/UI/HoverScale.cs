using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Aeternal.UI
{
    /// <summary>
    /// Grows a UI element slightly while the pointer is over it (only if its Selectable, when
    /// it has one, is interactable), and eases back when the pointer leaves.
    /// </summary>
    public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float hoverScale = 1.06f;
        public float speed = 14f;

        Selectable selectable;
        bool hovered;
        Vector3 baseScale = Vector3.one;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale = transform.localScale; // card faces are scaled to size before this is added
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;

        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        void OnDisable()
        {
            hovered = false;
            transform.localScale = baseScale;
        }

        void Update()
        {
            bool grow = hovered && (selectable == null || selectable.IsInteractable());
            float target = grow ? hoverScale : 1f;
            float s = Mathf.Lerp(transform.localScale.x / baseScale.x, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(baseScale.x * s, baseScale.y * s, 1f);
        }
    }
}
