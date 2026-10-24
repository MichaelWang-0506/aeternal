using System;
using UnityEngine;
using Aeternal.Stats;

namespace Aeternal.Cards
{
    /// <summary>
    /// Defines a single effect that can be applied by a card.
    /// </summary>
    [Serializable]
    public class EffectPayload
    {
        [Header("Effect Type")]
        [Tooltip("The type of effect this is")]
        public EffectType effectType;

        [Header("Effect Parameters")]
        [Tooltip("Magnitude of the effect (damage amount, heal amount, card draw count, movement squares, etc.)")]
        public int magnitude;

        [Tooltip("Duration in rounds (mainly for status effects and shields)")]
        public int durationRounds;

        [Header("Targeting")]
        [Tooltip("Target of the effect (Enemy or Self)")]
        public EffectTarget target = EffectTarget.Enemy;

        [Header("Damage Settings")]
        [Tooltip("Damage type (only used if effectType is Damage)")]
        public DamageType damageType = DamageType.Physical;

        [Header("Status Effect Settings")]
        [Tooltip("Status effect data (only used if effectType is ApplyStatus)")]
        public StatusEffectData statusEffectData;
    }

    /// <summary>
    /// Defines the target of an effect
    /// </summary>
    public enum EffectTarget
    {
        Self,
        Enemy,
        None
    }
}
