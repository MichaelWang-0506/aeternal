using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Aeternal.Cards;
using Aeternal.Stats;
using Aeternal.Events;

namespace Aeternal.Enemies
{
    [RequireComponent(typeof(HealthComponent))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Enemy Stats")]
        public string enemyId = "enemy_default";
        [Tooltip("Human-readable name shown in the UI (falls back to enemyId if blank).")]
        public string displayName = "";
        public int baseDamage = 8;
        public DamageType damageType = DamageType.Physical;
        
        [Header("Attack Directions")]
        public bool canAOE = false;
        [Tooltip("Single direction attack (0=Up,1=Right,2=Down,3=Left). Used if directionIndices is empty.")]
        public int directionIndex = 0; // direction of next attack (0=Up,1=Right,2=Down,3=Left)
        
        [Tooltip("Multiple directions for AOE attacks. If set, this takes precedence over directionIndex.")]
        public List<int> directionIndices = new List<int>();
        
        public int turnsBetweenAttacks = 1;
        private int turnCooldown = 0;

        [Header("Refs")]
        public HealthComponent health;

        public event Action OnIntentChanged;
        /// <summary>Fired when the enemy executes its attack. Arg is true if it hit the player.</summary>
        public event Action<bool> OnAttackExecuted;
        public bool IsAlive => health != null && health.getHealth > 0;

        /// <summary>
        /// Gets all active attack directions. Returns directionIndices if set, otherwise returns single directionIndex.
        /// </summary>
        public List<int> GetAttackDirections()
        {
            if (directionIndices != null && directionIndices.Count > 0)
            {
                return new List<int>(directionIndices);
            }
            return new List<int> { directionIndex };
        }

        /// <summary>
        /// Checks if this is an AOE attack (multiple directions)
        /// </summary>
        public bool IsAOEAttack()
        {
            return directionIndices != null && directionIndices.Count > 0;
        }

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
        }

        /// <summary>
        /// Configure this enemy from an EncounterData asset at combat start. Lets one scene
        /// host any enemy in a run without per-enemy scenes.
        /// </summary>
        public void ApplyEncounter(Aeternal.Encounters.EncounterData data)
        {
            if (data == null) return;

            enemyId = string.IsNullOrEmpty(data.encounterId) ? enemyId : data.encounterId;
            displayName = data.displayName;
            baseDamage = data.baseDamage;
            damageType = data.damageType;
            canAOE = data.canAOE;
            turnsBetweenAttacks = Mathf.Max(1, data.turnsBetweenAttacks);

            if (health == null) health = GetComponent<HealthComponent>();
            if (data.stats != null && health != null)
            {
                health.Initialize(data.stats);
            }

            if (!string.IsNullOrEmpty(data.displayName))
                gameObject.name = data.displayName;

            if (data.sprite != null)
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.sprite = data.sprite;
            }
        }

        public void SetIntent(int direction, int turnsDelay = 1)
        {
            directionIndex = direction;
            directionIndices.Clear(); // Clear AOE when setting single direction
            turnCooldown = turnsDelay;
            OnIntentChanged?.Invoke();
        }

        public void SetIntentAOE(List<int> directions, int turnsDelay = 1)
        {
            if (directions == null || directions.Count == 0)
            {
                Debug.LogWarning("[EnemyController] SetIntentAOE called with empty directions list. Use SetIntent for single direction.");
                return;
            }

            directionIndices = new List<int>(directions);
            // Keep directionIndex for backward compatibility (use first direction)
            if (directions.Count > 0)
                directionIndex = directions[0];
            turnCooldown = turnsDelay;
            OnIntentChanged?.Invoke();
        }

        public void SetIntentAOE(int[] directions, int turnsDelay = 1)
        {
            SetIntentAOE(directions?.ToList(), turnsDelay);
        }

        /// <summary>
        /// Decides the next attack intent based on player position
        /// Randomly chooses between:
        /// 1. Target the player square (single direction)
        /// 2. Target the player square and the 2 surrounding squares (AOE)
        /// </summary>
        public void DecideNextIntent(GameObject player = null)
        {
            if (player != null)
            {
                var playerPos = player.GetComponent<Aeternal.Player.PlayerPositioningComponent>();
                if (playerPos != null)
                {
                    int playerDirection = playerPos.facingIndex;
                    
                    // Randomly choose between single target and AOE (player + 2 surrounding squares)
                    bool useAOE = UnityEngine.Random.Range(0, 2) == 0; // 50% chance
                    if (!canAOE) useAOE = false;
                    
                    if (useAOE)
                    {
                        // Target player square and 2 surrounding squares
                        // Surrounding squares: counter-clockwise, player position, clockwise
                        int leftSquare = (playerDirection - 1 + 4) % 4;  // Counter-clockwise
                        int rightSquare = (playerDirection + 1) % 4;      // Clockwise
                        
                        List<int> aoeDirections = new List<int> { leftSquare, playerDirection, rightSquare };
                        SetIntentAOE(aoeDirections);
                    }
                    else
                    {
                        // Target only the player's current position
                        SetIntent(playerDirection);
                    }
                    return;
                }
            }

            // Fallback: simple pattern rotate clockwise if player not found
            directionIndex = (directionIndex + 1) % 4;
            directionIndices.Clear(); // Clear AOE, use single direction
            OnIntentChanged?.Invoke();
        }

        public void ExecuteIntent(GameObject player)
        {
            if (!IsAlive) return;

            // Check if enemy can act (not affected by Blackout)
            var statusManager = GetComponent<Aeternal.Status.StatusManager>();
            if (statusManager != null && !statusManager.CanAct())
            {
                EventBus.RaiseLog("Enemy is affected by Blackout and cannot act");
                Aeternal.UI.ActionLog.Instance?.AddMessage("Enemy is affected by Blackout and cannot act");
                return;
            }

            var pPos = player.GetComponent<Player.PlayerPositioningComponent>();
            if (pPos == null) return;

            var attackDirections = GetAttackDirections();
            bool hit = attackDirections.Contains(pPos.facingIndex);

            string[] directionNames = { "Up", "Right", "Down", "Left" };
            string directionStr = IsAOEAttack() 
                ? string.Join(", ", attackDirections.Select(d => directionNames[d]))
                : directionNames[directionIndex];

            if (hit)
            {
                var hc = player.GetComponent<HealthComponent>();
                if (hc != null)
                {
                    int healthBefore = hc.getHealth;
                var dmg = new DamageInfo(baseDamage, damageType);
                    hc.TakeDamage(dmg);
                    int healthAfter = hc.getHealth;
                    int actualDamage = healthBefore - healthAfter;

                    string msg = $"Enemy attacked {directionStr} - Hit player for {actualDamage} {damageType} damage";
                    EventBus.RaiseLog(msg);
                    Aeternal.UI.ActionLog.Instance?.AddMessage(msg);
                }
                OnAttackExecuted?.Invoke(true);
            }
            else
            {
                string msg = $"Enemy attacked {directionStr} - Missed";
                EventBus.RaiseLog(msg);
                Aeternal.UI.ActionLog.Instance?.AddMessage(msg);
                OnAttackExecuted?.Invoke(false);
            }

            // Note: Intent for next round is decided at round start in CombatManager
            // so the player can see it before their turn
        }
    }
}
