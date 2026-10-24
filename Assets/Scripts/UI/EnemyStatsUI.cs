using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Aeternal.Stats;
using Aeternal.Enemies;
using TMPro;

namespace Aeternal.UI
{
    /// <summary>
    /// UI component that displays enemy stats (Health, Shield, Name) in the game window.
    /// Attach this to a GameObject with Text/TextMeshPro components for each stat.
    /// Subscribes directly to the enemy's HealthComponent for accurate updates.
    /// </summary>
    public class EnemyStatsUI : MonoBehaviour
    {
        [Header("Enemy Reference")]
        [Tooltip("The enemy GameObject with HealthComponent and EnemyController")]
        public GameObject enemyObject;

        [Header("Health Display")]
        public TMP_Text healthText;
        public Slider healthSlider;

        [Header("Shield Display")]
        public TMP_Text shieldText;

        [Header("Enemy Name Display")]
        public TMP_Text enemyNameText;

        [Header("Intent Display (Optional)")]
        [Tooltip("Shows the enemy's attack direction/intent")]
        public TMP_Text intentText;

        [Header("Status Effect Display (Optional)")]
        [Tooltip("Shows the enemy's active status effects")]
        public TMP_Text activeStatusEffectText;

        private HealthComponent enemyHealth;
        private EnemyController enemyController;
        private Aeternal.Status.StatusManager statusManager;

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Initialize()
        {
            if (enemyObject == null) return;

            // Get components
            enemyHealth = enemyObject.GetComponent<HealthComponent>();
            enemyController = enemyObject.GetComponent<EnemyController>();
            statusManager = enemyObject.GetComponent<Aeternal.Status.StatusManager>();

            if (enemyHealth == null)
            {
                Debug.LogWarning($"[EnemyStatsUI] Enemy object {enemyObject.name} doesn't have a HealthComponent!");
                return;
            }

            // Subscribe to health component events
            enemyHealth.OnHealthChanged += UpdateHealthUI;
            enemyHealth.OnShieldChanged += UpdateShieldUI;

            // Subscribe to enemy controller events if available
            if (enemyController != null)
            {
                enemyController.OnIntentChanged += UpdateIntentUI;
            }

            // Subscribe to status manager events if available and status text is enabled
            if (statusManager != null && activeStatusEffectText != null)
            {
                statusManager.OnStatusChanged += UpdateStatusEffectsUI;
            }

            // Initial update
            UpdateHealthUI(enemyHealth.getHealth, enemyHealth.GetMaxHealth());
            UpdateShieldUI(enemyHealth.GetTotalShield());
            UpdateEnemyName();
            UpdateIntentUI();
            UpdateStatusEffectsUI();
        }

        private void UnsubscribeFromEvents()
        {
            if (enemyHealth != null)
            {
                enemyHealth.OnHealthChanged -= UpdateHealthUI;
                enemyHealth.OnShieldChanged -= UpdateShieldUI;
            }

            if (enemyController != null)
            {
                enemyController.OnIntentChanged -= UpdateIntentUI;
            }

            if (statusManager != null)
            {
                statusManager.OnStatusChanged -= UpdateStatusEffectsUI;
            }
        }

        private void UpdateHealthUI(int current, int max)
        {
            string healthString = $"Health: {current}/{max}";

            if (healthText != null)
                healthText.text = healthString;

            // Keep the name in sync: ApplyEncounter (run mode) changes the enemy identity and
            // fires a health change, but the name is not otherwise event-driven.
            UpdateEnemyName();


            if (healthSlider != null)
            {
                healthSlider.maxValue = max;
                healthSlider.value = current;
            }
        }

        private void UpdateShieldUI(int totalShield)
        {
            string shieldString = $"+ Shield: {totalShield}";

            if (shieldText != null)
                shieldText.text = shieldString;
        }

        private void UpdateEnemyName()
        {
            string nameString = "";

            if (enemyController != null && !string.IsNullOrEmpty(enemyController.displayName))
            {
                nameString = enemyController.displayName;
            }
            else if (enemyController != null && !string.IsNullOrEmpty(enemyController.enemyId))
            {
                nameString = enemyController.enemyId;
            }
            else if (enemyObject != null)
            {
                nameString = enemyObject.name;
            }

            if (enemyNameText != null)
                enemyNameText.text = nameString;

        }

        private void UpdateIntentUI()
        {
            if (enemyController == null) return;

            string[] directions = { "↑", "→", "↓", "←" };
            var attackDirections = enemyController.GetAttackDirections();
            
            string intentString;
            string dmgStr = $" ({enemyController.baseDamage} dmg)";
            if (attackDirections.Count > 1)
            {
                // AOE attack - show all directions and the damage it will deal
                var directionNames = attackDirections.Select(d => directions[d]).ToArray();
                intentString = $"AOE Intent: {string.Join(", ", directionNames)}{dmgStr}";
            }
            else if (attackDirections.Count == 1)
            {
                // Single direction attack with damage preview
                intentString = $"Intent: {directions[attackDirections[0]]}{dmgStr}";
            }
            else
            {
                intentString = "Intent: None";
            }

            if (intentText != null)
                intentText.text = intentString;

        }

        private void UpdateStatusEffectsUI()
        {
            if (activeStatusEffectText == null || statusManager == null) return;
            
            activeStatusEffectText.text = statusManager.GetStatusEffectsString();
        }

        /// <summary>
        /// Manually refresh all UI elements (useful for initialization or debugging)
        /// </summary>
        public void RefreshUI()
        {
            if (enemyHealth != null)
            {
                UpdateHealthUI(enemyHealth.getHealth, enemyHealth.GetMaxHealth());
                UpdateShieldUI(enemyHealth.GetTotalShield());
            }
            UpdateEnemyName();
            UpdateIntentUI();
            UpdateStatusEffectsUI();
        }
    }
}

