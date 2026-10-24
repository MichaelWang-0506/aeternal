using UnityEngine;
using UnityEngine.UI;
using Aeternal.Stats;

namespace Aeternal.UI
{
    /// <summary>
    /// Visual indicator that shows when an entity (player or enemy) is dead.
    /// Can display an overlay, grayscale effect, X mark, or other visual cues.
    /// </summary>
    public class DeathIndicator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("HealthComponent to monitor. If null, will try to find it on this GameObject.")]
        public HealthComponent healthComponent;

        [Header("Visual Elements")]
        [Tooltip("Image overlay to show when dead (e.g., semi-transparent red overlay)")]
        public Image overlayImage;

        [Tooltip("GameObject to show/hide when dead (e.g., X mark, skull icon)")]
        public GameObject deathIcon;

        [Tooltip("SpriteRenderer to modify color when dead (for grayscale effect)")]
        public SpriteRenderer spriteRenderer;

        [Header("Settings")]
        [Tooltip("Color to apply to overlay when dead")]
        public Color deathOverlayColor = new Color(0.5f, 0f, 0f, 0.5f); // Semi-transparent red

        [Tooltip("Color to apply to sprite when dead (grayscale)")]
        public Color deathSpriteColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark gray

        private Color originalSpriteColor = Color.white;
        private bool wasDead = false;

        private void Awake()
        {
            // Find health component if not assigned
            if (healthComponent == null)
            {
                healthComponent = GetComponent<HealthComponent>();
            }

            // Store original sprite color
            if (spriteRenderer != null)
            {
                originalSpriteColor = spriteRenderer.color;
            }
        }

        private void Start()
        {
            if (healthComponent != null)
            {
                // Subscribe to death event
                healthComponent.OnDeathLocal += OnEntityDied;
                // Check initial state
                UpdateDeathVisual(healthComponent.getHealth <= 0);
            }
            else
            {
                Debug.LogWarning("[DeathIndicator] HealthComponent not found!");
            }
        }

        private void OnDestroy()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDeathLocal -= OnEntityDied;
            }
        }

        private void OnEntityDied()
        {
            UpdateDeathVisual(true);
        }

        private void Update()
        {
            // Also check health directly in case death event wasn't fired
            if (healthComponent != null)
            {
                bool isDead = healthComponent.getHealth <= 0;
                if (isDead != wasDead)
                {
                    UpdateDeathVisual(isDead);
                    wasDead = isDead;
                }
            }
        }

        private void UpdateDeathVisual(bool isDead)
        {
            // Update overlay
            if (overlayImage != null)
            {
                overlayImage.enabled = isDead;
                if (isDead)
                {
                    overlayImage.color = deathOverlayColor;
                }
            }

            // Update death icon
            if (deathIcon != null)
            {
                deathIcon.SetActive(isDead);
            }

            // Update sprite color (grayscale effect)
            if (spriteRenderer != null)
            {
                spriteRenderer.color = isDead ? deathSpriteColor : originalSpriteColor;
            }
        }
    }
}

