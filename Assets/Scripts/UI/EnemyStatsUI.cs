using UnityEngine;
using System.Linq;
using Aeternal.Stats;
using Aeternal.Enemies;
using TMPro;

namespace Aeternal.UI
{
    /// <summary>
    /// The enemy's name and intent in the top-right corner. Health, shield and status effects
    /// are shown by the CharacterHUD under the enemy instead.
    /// </summary>
    public class EnemyStatsUI : MonoBehaviour
    {
        [Header("Enemy Reference")]
        [Tooltip("The enemy GameObject with HealthComponent and EnemyController")]
        public GameObject enemyObject;

        [Header("Enemy Name Display")]
        public TMP_Text enemyNameText;

        [Header("Intent Display (Optional)")]
        [Tooltip("Shows the enemy's attack direction/intent")]
        public TMP_Text intentText;

        private HealthComponent enemyHealth;
        private EnemyController enemyController;

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

            if (enemyHealth == null)
            {
                Debug.LogWarning($"[EnemyStatsUI] Enemy object {enemyObject.name} doesn't have a HealthComponent!");
                return;
            }

            // ApplyEncounter (run mode) changes the enemy identity and fires a health change,
            // but the name is not otherwise event-driven.
            enemyHealth.OnHealthChanged += OnHealthChanged;

            // Subscribe to enemy controller events if available
            if (enemyController != null)
            {
                enemyController.OnIntentChanged += UpdateIntentUI;
            }

            // Initial update
            UpdateEnemyName();
            UpdateIntentUI();
        }

        private void UnsubscribeFromEvents()
        {
            if (enemyHealth != null)
            {
                enemyHealth.OnHealthChanged -= OnHealthChanged;
            }

            if (enemyController != null)
            {
                enemyController.OnIntentChanged -= UpdateIntentUI;
            }
        }

        private void OnHealthChanged(int current, int max) => UpdateEnemyName();

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
            {
                // Long names shrink onto one line instead of wrapping into the intent below.
                if (!enemyNameText.enableAutoSizing)
                {
                    enemyNameText.fontSizeMax = enemyNameText.fontSize;
                    enemyNameText.fontSizeMin = Mathf.Min(18f, enemyNameText.fontSize);
                    enemyNameText.enableAutoSizing = true;
                    enemyNameText.textWrappingMode = TextWrappingModes.NoWrap;
                }
                enemyNameText.text = nameString;
            }

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

        /// <summary>
        /// Manually refresh all UI elements (useful for initialization or debugging)
        /// </summary>
        public void RefreshUI()
        {
            UpdateEnemyName();
            UpdateIntentUI();
        }
    }
}

