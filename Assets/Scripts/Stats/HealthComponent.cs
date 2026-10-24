using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Aeternal.Events;
using Aeternal.Cards;

namespace Aeternal.Stats
{

    public class DamageInfo
    {
        public int amount;
        public DamageType type;
        public GameObject source;
        public bool isCritical;
        // True for damage-over-time / status ticks (e.g. Bleeding). Such damage ignores Dodge,
        // which is meant to negate incoming attacks, not passive status damage.
        public bool isDamageOverTime;

        public DamageInfo(int amount, DamageType type = DamageType.Physical, GameObject source = null, bool isCritical = false, bool isDamageOverTime = false)
        {
            this.amount = amount;
            this.type = type;
            this.source = source;
            this.isCritical = isCritical;
            this.isDamageOverTime = isDamageOverTime;
        }
    }

    public class HealthComponent : MonoBehaviour
    {
        [Header("Stats")]
        public PlayerStats statsRef;

        protected int currentHealth;
        public int getHealth => currentHealth;
        protected List<ShieldInstance> shields = new List<ShieldInstance>();

        public bool invulnerable = false;
        public bool canDie = true;

        public event Action<int, int> OnHealthChanged; // (current, max)
        public event Action<int> OnShieldChanged; // total shield
        public event Action<DamageInfo> OnDamageTaken;
        public event Action<int> OnHealed; // actual amount healed
        public event Action OnDodged; // an attack was negated by Dodge
        public event Action OnDeathLocal;

        private void Awake()
        {
            if (statsRef != null)
            {
                Initialize(statsRef);
            }
        }

        public void Initialize(PlayerStats stats)
        {
            statsRef = stats;
            currentHealth = Mathf.Clamp(currentHealth, 0, statsRef?.maxHealth ?? currentHealth);
            if (currentHealth == 0)
                currentHealth = statsRef != null ? statsRef.maxHealth : currentHealth;
            RaiseHealthChanged();
            RaiseShieldChanged();
        }

        public int GetTotalShield()
        {
            return shields.Sum(s => s.amount);
        }

        public int GetMaxHealth() => statsRef != null ? statsRef.maxHealth : 0;

        /// <summary>
        /// Force current health to a specific value (used to carry HP between fights in a run).
        /// A value &lt;= 0 leaves health at full (the freshly-initialized max).
        /// </summary>
        public void SetCurrentHealth(int value)
        {
            if (value <= 0) return;
            int max = GetMaxHealth();
            currentHealth = max > 0 ? Mathf.Clamp(value, 1, max) : Mathf.Max(1, value);
            RaiseHealthChanged();
        }

        public int TakeDamage(DamageInfo info)
        {
            if (invulnerable) return 0;
            var status = GetComponent<Aeternal.Status.StatusManager>();
            if (status != null && !info.isDamageOverTime && status.ConsumeDodge())
            {
                EventBus.RaiseLog("Attack dodged!");
                Aeternal.UI.ActionLog.Instance?.AddMessage("Attack dodged!");
                OnDodged?.Invoke();
                return 0; // no damage taken
            }


            int original = info.amount;
            int remaining = info.amount;

            // True damage bypasses shields
            if (info.type != DamageType.True)
            {
                for (int i = 0; i < shields.Count && remaining > 0; i++)
                {
                    ShieldInstance s = shields[i];
                    int used = Mathf.Min(s.amount, remaining);
                    s.amount -= used;
                    remaining -= used;
                }

                // Remove exhausted shield instances
                shields.RemoveAll(s => s.amount <= 0);
                RaiseShieldChanged();
            }

            int damageToHealth = remaining;
            currentHealth -= damageToHealth;
            currentHealth = Mathf.Max(0, currentHealth);

            var taken = new DamageInfo(original - remaining + damageToHealth, info.type, info.source, info.isCritical);
            OnDamageTaken?.Invoke(taken);
            EventBus.RaiseLog($"Damage taken: {damageToHealth} (type {info.type})");

            RaiseHealthChanged();

            if (currentHealth <= 0 && canDie)
            {
                Die();
            }
            Debug.Log($"[HealthComponent] {gameObject.name} took {remaining} damage. HP now {currentHealth}/{GetMaxHealth()}");


            return damageToHealth;
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            if (statsRef == null) return;

            int before = currentHealth;
            if (statsRef.allowOverheal)
            {
                currentHealth += amount;
            }
            else
            {
                currentHealth = Mathf.Min(statsRef.maxHealth, currentHealth + amount);
            }

            RaiseHealthChanged();
            EventBus.RaiseLog($"Healed: {amount}");
            int actual = currentHealth - before;
            if (actual > 0) OnHealed?.Invoke(actual);
        }

        public void AddShield(ShieldInstance shield)
        {
            if (shield == null) return;
            shields.Add(shield);
            RaiseShieldChanged();
            EventBus.RaiseLog($"Shield added: {shield.amount} ({shield.tag})");
        }

        public void AddShield(int amount, int durationRounds = 0, string tag = null)
        {
            AddShield(new ShieldInstance(Guid.NewGuid().ToString(), amount, durationRounds, tag));
        }

        public void OnRoundEnd()
        {
            GetComponent<Aeternal.Status.StatusManager>()?.OnRoundEnd();

            // decrement shield durations and remove expired
            bool removed = false;
            foreach (var s in shields.ToArray())
            {
                if (s.roundsRemaining > 0)
                {
                    s.roundsRemaining--;
                    if (s.roundsRemaining <= 0)
                    {
                        shields.Remove(s);
                        removed = true;
                    }
                }
            }

            if (removed)
            {
                RaiseShieldChanged();
            }
        }

        public void Kill()
        {
            currentHealth = 0;
            RaiseHealthChanged();
            Die();
        }

        private void Die()
        {
            EventBus.RaiseLog("Actor died");
            OnDeathLocal?.Invoke();
            EventBus.RaiseDeath();
        }

        private void RaiseHealthChanged()
        {
            int maxHealth = GetMaxHealth();
            Debug.Log($"[HealthComponent] {gameObject.name} - Health changed: {currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            EventBus.RaiseHealthChanged(currentHealth, maxHealth);
        }

        private void RaiseShieldChanged()
        {
            int totalShield = GetTotalShield();
            Debug.Log($"[HealthComponent] {gameObject.name} - Shield changed: {totalShield}");
            EventBus.RaiseShieldChanged(totalShield);
            OnShieldChanged?.Invoke(totalShield);
        }
    }
}
