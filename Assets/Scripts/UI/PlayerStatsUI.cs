using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Aeternal.UI
{
    /// <summary>
    /// UI component that displays player stats (Health, AP, DF, Shield) in the game window.
    /// Attach this to a GameObject with Text/TextMeshPro components for each stat.
    /// </summary>
    public class PlayerStatsUI : MonoBehaviour
    {
        [Header("Player Reference (Optional)")]
        [Tooltip("The player GameObject with StatusManager. If not set, will try to find it automatically.")]
        public GameObject playerObject;

        [Header("Health Display")]
        public TMP_Text healthText;

        public Slider healthSlider;

        [Header("Action Points Display")]
        public TMP_Text apText;

        public Slider apSlider;

        [Header("Divine Focus Display")]
        public TMP_Text dfText;

        public Slider dfSlider;

        [Header("Shield Display")]
        public TMP_Text shieldText;

        [Header("Status Effect Display (Optional)")]
        [Tooltip("Shows the player's active status effects. Leave null to disable.")]
        public TMP_Text activeStatusEffectText;

        private Aeternal.Status.StatusManager statusManager;

        private void Start()
        {
            InitializeStatusManager();
        }

        private void OnEnable()
        {
            InitializeStatusManager();
        }

        private void OnDisable()
        {
            if (statusManager != null)
            {
                statusManager.OnStatusChanged -= UpdateStatusEffectsUI;
            }
        }

        private void InitializeStatusManager()
        {
            // Only initialize if status text is enabled
            if (activeStatusEffectText == null) return;

            // Try to find StatusManager
            if (playerObject != null)
            {
                statusManager = playerObject.GetComponent<Aeternal.Status.StatusManager>();
            }
            
            if (statusManager == null)
            {
                // Try to find it in parent
                statusManager = GetComponentInParent<Aeternal.Status.StatusManager>();
            }
            
            if (statusManager == null)
            {
                // Try to find it on the same GameObject
                statusManager = GetComponent<Aeternal.Status.StatusManager>();
            }
            
            if (statusManager == null)
            {
                // Last resort: try to find any player object with StatusManager
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    statusManager = player.GetComponent<Aeternal.Status.StatusManager>();
                }
            }
            
            if (statusManager != null)
            {
                statusManager.OnStatusChanged += UpdateStatusEffectsUI;
                UpdateStatusEffectsUI(); // Initial update
            }
        }

        private void UpdateStatusEffectsUI()
        {
            if (activeStatusEffectText == null || statusManager == null) return;
            
            activeStatusEffectText.text = statusManager.GetStatusEffectsString();
        }

        /// <summary>
        /// Updates the health display with current and max values
        /// </summary>
        public void UpdateHealth(int current, int max)
        {
            string healthString = $"Health: {current}/{max}";
            
            // Update Text component if available
            if (healthText != null)
                healthText.text = healthString;
            

            
            // Update slider if available
            if (healthSlider != null)
            {
                healthSlider.maxValue = max;
                healthSlider.value = current;
            }
        }

        /// <summary>
        /// Updates the Action Points display with current and max values
        /// </summary>
        public void UpdateAP(int current, int max)
        {
            string apString = $"Action Points:  {current}/{max}";
            
            if (apText != null)
                apText.text = apString;
            

            
            if (apSlider != null)
            {
                apSlider.maxValue = max;
                apSlider.value = current;
            }
        }

        /// <summary>
        /// Updates the Divine Focus display with current and max values
        /// </summary>
        public void UpdateDF(int current, int max)
        {
            string dfString = $"Divine Focus:  {current}/{max}";
            
            if (dfText != null)
                dfText.text = dfString;

            
            if (dfSlider != null)
            {
                dfSlider.maxValue = max;
                dfSlider.value = current;
            }
        }

        /// <summary>
        /// Updates the Shield display with total shield value
        /// </summary>
        public void UpdateShield(int totalShield)
        {
            string shieldString = $"+ Shield: {totalShield}";
            
            if (shieldText != null)
                shieldText.text = shieldString;
        }
    }
}

