using UnityEngine;
using Aeternal.Stats;
using Aeternal.Cards;

namespace Aeternal.Encounters
{
    /// <summary>
    /// Data-driven description of a single combat encounter (one enemy).
    /// Applied to the scene's EnemyController + HealthComponent at combat start, so
    /// a single PlayGameScene can host many different enemies without per-enemy scenes.
    /// Create via: Assets > Create > Aeternal > Encounter.
    /// </summary>
    [CreateAssetMenu(menuName = "Aeternal/Encounter")]
    public class EncounterData : ScriptableObject
    {
        [Header("Identity")]
        public string encounterId = "enemy_default";
        public string displayName = "Enemy";
        [Tooltip("Marks this as a region boss (affects run flow: clearing it completes the region).")]
        public bool isBoss = false;

        [Header("Stats")]
        [Tooltip("PlayerStats ScriptableObject supplying this enemy's max health / defense (e.g. Goblin.asset, Thanatos.asset).")]
        public PlayerStats stats;

        [Header("Attack")]
        public int baseDamage = 8;
        public DamageType damageType = DamageType.Physical;
        [Tooltip("Whether this enemy may telegraph AOE (multi-square) attacks.")]
        public bool canAOE = false;
        public int turnsBetweenAttacks = 1;

        [Header("Presentation (optional)")]
        [Tooltip("Sprite to swap onto the enemy's SpriteRenderer, if assigned.")]
        public Sprite sprite;
    }
}
