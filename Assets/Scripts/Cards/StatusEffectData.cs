using UnityEngine;

namespace Aeternal.Cards
{
    /// <summary>
    /// Defines a status effect that can be applied to entities.
    /// Contains the status ID and behavior information.
    /// </summary>
    [System.Serializable]
    public class StatusEffectData
    {
        [Header("Status Identification")]
        [Tooltip("Unique identifier for this status effect (e.g., 'bleeding', 'dodge', 'blackout')")]
        public string statusId;

        [Header("Behavior")]
        [Tooltip("The type of behavior this status effect has")]
        public StatusEffectBehavior behavior;

        [Tooltip("Additional data for the behavior (e.g., damage amount for bleeding)")]
        public int behaviorValue;
    }

    /// <summary>
    /// Defines what a status effect does when active
    /// </summary>
    public enum StatusEffectBehavior
    {
        None,           // No special behavior
        Bleeding,       // Lose health equal to behaviorValue at round end
        Dodge,          // Dodge the next attack
        Blackout,       // Unable to act for a turn
        // Add more behaviors as needed
    }
}

