using System.Collections.Generic;
using UnityEngine;
using Aeternal.Cards;
using Aeternal.Stats;
using Aeternal.Events;

namespace Aeternal.Status
{
    public class StatusManager : MonoBehaviour
    {
        [System.Serializable]
        public class StatusInstance
        {
            public string id;
            public int stacks;
            public int remainingRounds;
            public StatusEffectData behaviorData; // Store behavior information

            public StatusInstance(string id, int stacks, int rounds, StatusEffectData behaviorData = null)
            {
                this.id = id;
                this.stacks = stacks;
                this.remainingRounds = rounds;
                this.behaviorData = behaviorData;
            }
        }

        public List<StatusInstance> activeStatuses = new List<StatusInstance>();

        // Events for UI updates
        public System.Action OnStatusChanged;
        /// <summary>Fired when a status is (re)applied, with a short popup text like "+2 Bleed".</summary>
        public System.Action<string> OnStatusApplied;

        // Legacy methods using ID (kept for backward compatibility)
        public bool HasStatus(string id)
        {
            return activeStatuses.Exists(s => s.id != null && s.id.Equals(id, System.StringComparison.OrdinalIgnoreCase) && s.remainingRounds > 0);
        }

        public StatusInstance GetStatus(string id)
        {
            return activeStatuses.Find(s => s.id != null && s.id.Equals(id, System.StringComparison.OrdinalIgnoreCase) && s.remainingRounds > 0);
        }

        // New methods using behavior enum (preferred)
        public bool HasStatusBehavior(StatusEffectBehavior behavior)
        {
            return activeStatuses.Exists(s => s.behaviorData != null && 
                                             s.behaviorData.behavior == behavior && 
                                             s.remainingRounds > 0);
        }

        public StatusInstance GetStatusByBehavior(StatusEffectBehavior behavior)
        {
            return activeStatuses.Find(s => s.behaviorData != null && 
                                            s.behaviorData.behavior == behavior && 
                                            s.remainingRounds > 0);
        }

        public void AddStatus(string id, int stacks = 1, int duration = 1, StatusEffectData behaviorData = null)
        {
            // If behaviorData is provided, try to find existing status by behavior first
            // This allows stacking statuses with the same behavior even if IDs differ
            StatusInstance existing = null;
            
            if (behaviorData != null && behaviorData.behavior != StatusEffectBehavior.None)
            {
                existing = activeStatuses.Find(s => s.behaviorData != null && 
                                                   s.behaviorData.behavior == behaviorData.behavior);
            }
            
            // Fallback to ID-based lookup if no behavior match found
            if (existing == null && !string.IsNullOrEmpty(id))
            {
                existing = activeStatuses.Find(s => s.id != null && 
                                                   s.id.Equals(id, System.StringComparison.OrdinalIgnoreCase));
            }

            if (existing != null)
            {
                // Re-applying a status ADDS its stacks (layers accumulate, e.g. Bleeding 2 + Bleeding 1
                // = Bleeding 3) and keeps the longer of the two durations (a short re-apply must never
                // cut an existing status short).
                existing.stacks += Mathf.Max(0, stacks);
                existing.remainingRounds = Mathf.Max(existing.remainingRounds, duration);
                // Only adopt new behavior data if the existing instance has none — otherwise a
                // different card's behaviorValue would silently rewrite this status's damage.
                if (behaviorData != null && existing.behaviorData == null)
                {
                    existing.behaviorData = behaviorData;
                }
            }
            else
            {
                activeStatuses.Add(new StatusInstance(id ?? "", stacks, duration, behaviorData));
            }

            OnStatusChanged?.Invoke();
            OnStatusApplied?.Invoke(BuildApplyPopupText(id, stacks, behaviorData));
        }

        private static string BuildApplyPopupText(string id, int stacks, StatusEffectData behaviorData)
        {
            var behavior = behaviorData != null ? behaviorData.behavior : StatusEffectBehavior.None;
            switch (behavior)
            {
                case StatusEffectBehavior.Bleeding: return $"+{Mathf.Max(1, stacks)} Bleed";
                case StatusEffectBehavior.Blackout: return "STUNNED!";
                case StatusEffectBehavior.Dodge: return "Dodge ready";
                default: return $"+{(string.IsNullOrEmpty(id) ? "Status" : id)}";
            }
        }

        public void RemoveStatus(string id)
        {
            bool removed = activeStatuses.RemoveAll(s => s.id != null && s.id.Equals(id, System.StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) OnStatusChanged?.Invoke();
        }

        public void RemoveStatusByBehavior(StatusEffectBehavior behavior)
        {
            bool removed = activeStatuses.RemoveAll(s => s.behaviorData != null && s.behaviorData.behavior == behavior) > 0;
            if (removed) OnStatusChanged?.Invoke();
        }

        public void RemoveAllStatuses()
        {
            if (activeStatuses.Count > 0)
            {
                activeStatuses.Clear();
                OnStatusChanged?.Invoke();
            }
        }

        /// <summary>
        /// Checks if the entity can act this turn (not affected by Blackout)
        /// </summary>
        public bool CanAct()
        {
            var blackout = GetStatusByBehavior(StatusEffectBehavior.Blackout);
            return blackout == null || blackout.remainingRounds <= 0;
        }

        /// <summary>
        /// Checks if the entity has Dodge status and consumes it if present
        /// </summary>
        public bool ConsumeDodge()
        {
            var dodge = GetStatusByBehavior(StatusEffectBehavior.Dodge);
            if (dodge != null && dodge.remainingRounds > 0)
            {
                activeStatuses.Remove(dodge);
                return true;
            }
            return false;
        }

        public void OnRoundEnd()
        {
            var hc = GetComponent<HealthComponent>();
            
            foreach (var s in activeStatuses.ToArray())
            {
                // Handle status behaviors
                if (s.behaviorData != null)
                {
                    switch (s.behaviorData.behavior)
                    {
                        case StatusEffectBehavior.Bleeding:
                            // Apply bleeding damage 
                            if (hc != null && s.behaviorData.behaviorValue > 0)
                            {
                                int bleedDamage = s.behaviorData.behaviorValue * s.stacks;
                                var dmg = new DamageInfo(bleedDamage, DamageType.True, null, false, isDamageOverTime: true);
                                hc.TakeDamage(dmg);
                                EventBus.RaiseLog($"{gameObject.name} took {bleedDamage} bleeding damage");
                                Aeternal.UI.ActionLog.Instance?.AddMessage($"{gameObject.name} took {bleedDamage} bleeding damage");
                            }
                            break;
                        // Other behaviors are handled elsewhere (Dodge in TakeDamage, Blackout in CanAct)
                    }
                }

                // Decrement duration
                s.remainingRounds--;
                if (s.remainingRounds <= 0)
                {
                    activeStatuses.Remove(s);
                }
            }
            
            OnStatusChanged?.Invoke();
        }

        /// <summary>
        /// Gets a formatted string of all active status effects
        /// </summary>
        public string GetStatusEffectsString()
        {
            if (activeStatuses == null || activeStatuses.Count == 0)
            {
                return "No Status Effects";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool first = true;
            for (int i = 0; i < activeStatuses.Count; i++)
            {
                var status = activeStatuses[i];
                if (status.remainingRounds <= 0) continue;

                if (!first) sb.Append("\n");
                first = false;
                sb.Append(DescribeStatus(status));
            }

            return sb.ToString();
        }

        /// <summary>
        /// One human-readable line for a status instance, spelling out exactly what it will do
        /// (e.g. "Bleeding x3 - 3 dmg at round end (2 rounds)").
        /// </summary>
        public static string DescribeStatus(StatusInstance status)
        {
            int rounds = status.remainingRounds;
            string roundsStr = $"({rounds} round{(rounds > 1 ? "s" : "")})";
            var behavior = status.behaviorData != null ? status.behaviorData.behavior : StatusEffectBehavior.None;

            switch (behavior)
            {
                case StatusEffectBehavior.Bleeding:
                {
                    int perRound = Mathf.Max(1, status.behaviorData.behaviorValue) * Mathf.Max(1, status.stacks);
                    return $"Bleeding x{status.stacks} - {perRound} dmg at round end {roundsStr}";
                }
                case StatusEffectBehavior.Dodge:
                    return $"Dodge - negates the next attack {roundsStr}";
                case StatusEffectBehavior.Blackout:
                    return $"Stunned - cannot act {roundsStr}";
                default:
                {
                    string name = !string.IsNullOrEmpty(status.id) ? status.id : behavior.ToString();
                    string stacksStr = status.stacks > 1 ? $" x{status.stacks}" : "";
                    return $"{name}{stacksStr} {roundsStr}";
                }
            }
        }
    }
}
