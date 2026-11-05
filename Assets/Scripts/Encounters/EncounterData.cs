using System.Collections.Generic;
using UnityEngine;
using Aeternal.Stats;
using Aeternal.Cards;

namespace Aeternal.Encounters
{
    /// <summary>One line of story dialogue or narration. A blank speaker is narration.</summary>
    [System.Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea(2, 4)] public string text;
    }

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
        [Tooltip("Marks a tougher-than-normal enemy; shown as ELITE on its HUD.")]
        public bool isElite = false;

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
        [Tooltip("Multiplied into the sprite, so one piece of art can serve several enemies (white = as drawn).")]
        public Color spriteTint = Color.white;
        [Tooltip("Sprite size relative to the scene's enemy; elites and bosses read as bigger threats.")]
        public float spriteScale = 1f;

        [Header("Charged attack (boss mechanic)")]
        [Tooltip("Every Nth round the enemy unleashes a charged attack (0 = never). It is announced " +
                 "the round before, then hits every square but one safe square, for extra damage.")]
        public int chargedAttackEvery = 0;
        [Tooltip("Damage of the charged attack, as a multiple of baseDamage.")]
        public float chargedAttackMultiplier = 2f;
        public string chargedAttackName = "THUNDERBOLT";
        [Tooltip("Banner the round before the charged attack.")]
        public string chargeWarning = "The storm gathers. Next round: find the safe square!";
        [Tooltip("Banner when the charged attack is telegraphed.")]
        public string chargeRelease = "Only one square is safe.";

        [Header("Story (optional)")]
        [Tooltip("Shown before the first turn.")]
        public List<DialogueLine> introDialogue = new List<DialogueLine>();

        [Header("Scripted death (story fight the player cannot win)")]
        [Tooltip("The enemy cannot drop below 1 HP, and kills the player with an unavoidable attack " +
                 "once triggered. Run by ScriptedDeathDirector; replaces the defeat panel with the epilogue.")]
        public bool scriptedDeath = false;
        [Tooltip("Round on which the killing blow is telegraphed (1 = first round).")]
        public int scriptedDeathRound = 3;
        [Tooltip("...or earlier, once the enemy's health falls to this fraction of its max.")]
        [Range(0f, 1f)] public float scriptedDeathHealthFraction = 0.5f;
        [Tooltip("Damage shown on the killing blow's intent. The blow kills regardless.")]
        public int scriptedDeathDamage = 999;
        [Tooltip("Banner shown when the killing blow is telegraphed (the enemy enrages).")]
        public string rageTitle = "DEATH'S REACH";
        public string rageSubtitle = "Unstoppable: immune to Stun. Ignores Dodge.";
        [Tooltip("Badge on the enemy's HUD while enraged.")]
        public string rageBadge = "UNSTOPPABLE";
        [Tooltip("Added under the enemy's intent while enraged.")]
        public string rageIntentNote = "UNDODGEABLE  ·  UNSTOPPABLE";
        [Tooltip("Shown when the killing blow is telegraphed.")]
        public List<DialogueLine> scriptedDeathDialogue = new List<DialogueLine>();
        [Tooltip("Narration after the player falls.")]
        public List<DialogueLine> epilogue = new List<DialogueLine>();
        [Tooltip("Title card after the epilogue, before returning to the main menu.")]
        public string endingTitle = "To be continued";
    }
}
