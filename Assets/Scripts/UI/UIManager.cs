using UnityEngine;
using Aeternal.Events;
using Aeternal.Stats;
using Aeternal.Resources;

namespace Aeternal.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Reference to the PlayerStatsUI component that displays health, AP, DF, and shield")]
        public PlayerStatsUI playerStatsUI;

        [Header("Player References")]
        [Tooltip("Reference to the player's HealthComponent. If set, will subscribe to its local events instead of global EventBus.")]
        public HealthComponent playerHealth;
        [Tooltip("Reference to the player's ResourceComponent. If set, will subscribe to its local events instead of global EventBus.")]
        public ResourceComponent playerResources;

        [Header("Debug Options")]
        [Tooltip("If enabled, will also log UI updates to console for debugging")]
        public bool enableDebugLogs = false;

        private void OnEnable()
        {
            // Subscribe to player-specific events if references are provided
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += UpdateHealthUI;
                playerHealth.OnShieldChanged += UpdateShieldUI;
            }
            else
            {
                // Fallback to global EventBus if player reference not set
            EventBus.OnHealthChanged += UpdateHealthUI;
                EventBus.OnShieldChanged += UpdateShieldUI;
            }

            if (playerResources != null)
            {
                playerResources.OnAPChanged += UpdateAPUI;
                playerResources.OnDFChanged += UpdateDFUI;
            }
            else
            {
                // Fallback to global EventBus if player reference not set
            EventBus.OnAPChanged += UpdateAPUI;
            EventBus.OnDFChanged += UpdateDFUI;
            }

            // Always subscribe to log events (global)
            EventBus.OnLog += LogToConsole;
        }

        private void OnDisable()
        {
            // Unsubscribe from player-specific events
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= UpdateHealthUI;
                playerHealth.OnShieldChanged -= UpdateShieldUI;
            }
            else
        {
            EventBus.OnHealthChanged -= UpdateHealthUI;
                EventBus.OnShieldChanged -= UpdateShieldUI;
            }

            if (playerResources != null)
            {
                playerResources.OnAPChanged -= UpdateAPUI;
                playerResources.OnDFChanged -= UpdateDFUI;
            }
            else
            {
            EventBus.OnAPChanged -= UpdateAPUI;
            EventBus.OnDFChanged -= UpdateDFUI;
            }

            EventBus.OnLog -= LogToConsole;
        }

        void UpdateHealthUI(int cur, int max)
        {
            if (enableDebugLogs)
            Debug.Log($"[UI] Health: {cur}/{max}");

            if (playerStatsUI != null)
                playerStatsUI.UpdateHealth(cur, max);
        }

        void UpdateAPUI(int cur, int max)
        {
            if (enableDebugLogs)
            Debug.Log($"[UI] AP: {cur}/{max}");

            if (playerStatsUI != null)
                playerStatsUI.UpdateAP(cur, max);
        }

        void UpdateDFUI(int cur, int max)
        {
            if (enableDebugLogs)
            Debug.Log($"[UI] DF: {cur}/{max}");

            if (playerStatsUI != null)
                playerStatsUI.UpdateDF(cur, max);
        }

        void UpdateShieldUI(int totalShield)
        {
            if (enableDebugLogs)
                Debug.Log($"[UI] Shield: {totalShield}");

            if (playerStatsUI != null)
                playerStatsUI.UpdateShield(totalShield);
        }

        void LogToConsole(string msg)
        {
            Debug.Log($"[LOG] {msg}");
        }
    }
}
