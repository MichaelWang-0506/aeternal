using System;
using UnityEngine;
using Aeternal.Events;
using Aeternal.Stats;

namespace Aeternal.Resources
{
    public class ResourceComponent : MonoBehaviour
    {
        [Header("Stats Reference")]
        [Tooltip("Reference to PlayerStats ScriptableObject. If set, maxAP and maxDF will be read from here.")]
        public PlayerStats statsRef;

        [Header("AP / DF")]
        [Tooltip("Max Action Points. Overridden by statsRef if assigned.")]
        public int maxAP = 3;
        public int currentAP = 3;
        [Tooltip("Max Divine Focus. Overridden by statsRef if assigned.")]
        public int maxDF = 2;
        public int currentDF = 2;

        [Header("Refill rules")]
        public bool refillAPToMaxAtRoundStart = true;
        public int APPerRound = 3; // if not refill to max, add this amount
        public bool refillDFAtRoundStart = false;
        public int DFPerRound = 0;

        public event Action<int,int> OnAPChanged;
        public event Action<int,int> OnDFChanged;

        private void Awake()
        {
            if (statsRef != null)
            {
                Initialize(statsRef);
            }
        }

        private void Start()
        {
            // sync initial event
            RaiseAPChanged();
            RaiseDFChanged();
        }

        public void Initialize(PlayerStats stats)
        {
            statsRef = stats;
            if (statsRef != null)
            {
                int oldMaxAP = maxAP;
                int oldMaxDF = maxDF;
                
                maxAP = statsRef.maxAP;
                maxDF = statsRef.maxDF;
                
                // If current values are at the old max (indicating full resources), set to new max
                // This handles the case where Inspector values don't match PlayerStats
                // Also set to max if current is 0 (fresh initialization)
                if (currentAP == oldMaxAP || currentAP == 0 || currentAP > maxAP)
                {
                    currentAP = maxAP;
                }
                else
                {
                    // Clamp to new max if it exceeds it
                    currentAP = Mathf.Clamp(currentAP, 0, maxAP);
                }
                
                if (currentDF == oldMaxDF || currentDF == 0 || currentDF > maxDF)
                {
                    currentDF = maxDF;
                }
                else
                {
                    // Clamp to new max if it exceeds it
                    currentDF = Mathf.Clamp(currentDF, 0, maxDF);
                }
            }
            else
            {
                // If no statsRef, just clamp to current max values
                currentAP = Mathf.Clamp(currentAP, 0, maxAP);
                currentDF = Mathf.Clamp(currentDF, 0, maxDF);
            }
            
            RaiseAPChanged();
            RaiseDFChanged();
        }

        public void Initialize(int maxAP, int maxDF, int startAP = -1, int startDF = -1)
        {
            this.maxAP = maxAP;
            this.maxDF = maxDF;
            currentAP = startAP >= 0 ? startAP : maxAP;
            currentDF = startDF >= 0 ? startDF : maxDF;
            RaiseAPChanged();
            RaiseDFChanged();
        }

        public bool TryConsumeAP(int amount)
        {
            if (amount <= 0) return true;
            if (currentAP >= amount)
            {
                currentAP -= amount;
                RaiseAPChanged();
                return true;
            }
            return false;
        }

        public void ModifyAP(int delta)
        {
            currentAP = Mathf.Clamp(currentAP + delta, 0, maxAP);
            RaiseAPChanged();
        }

        public void RefillAP()
        {
            if (refillAPToMaxAtRoundStart)
                currentAP = maxAP;
            else
                currentAP = Mathf.Clamp(currentAP + APPerRound, 0, maxAP);

            RaiseAPChanged();
        }

        public bool TryConsumeDF(int amount)
        {
            if (amount <= 0) return true;
            if (currentDF >= amount)
            {
                currentDF -= amount;
                RaiseDFChanged();
                return true;
            }

            return false;
        }

        public void ModifyDF(int delta)
        {
            currentDF = Mathf.Clamp(currentDF + delta, 0, maxDF);
            RaiseDFChanged();
        }

        public void RefillDF()
        {
            if (refillDFAtRoundStart)
                currentDF = maxDF;
            else
                currentDF = Mathf.Clamp(currentDF + DFPerRound, 0, maxDF);

            RaiseDFChanged();
        }

        private void RaiseAPChanged()
        {
            OnAPChanged?.Invoke(currentAP, maxAP);
            EventBus.RaiseAPChanged(currentAP, maxAP);
        }

        private void RaiseDFChanged()
        {
            OnDFChanged?.Invoke(currentDF, maxDF);
            EventBus.RaiseDFChanged(currentDF, maxDF);
        }
    }
}
