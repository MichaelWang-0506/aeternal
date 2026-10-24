using System;
using UnityEngine;

namespace Aeternal.Events
{
    public static class EventBus
    {
        public static event Action<int,int> OnHealthChanged; 
        public static event Action<int> OnShieldChanged; 
        public static event Action OnDeath;
        public static event Action<int,int> OnAPChanged; 
        public static event Action<int,int> OnDFChanged; 
        public static event Action<string> OnLog; 

        public static void RaiseHealthChanged(int cur, int max) => OnHealthChanged?.Invoke(cur, max);
        public static void RaiseShieldChanged(int shield) => OnShieldChanged?.Invoke(shield);
        public static void RaiseDeath() => OnDeath?.Invoke();
        public static void RaiseAPChanged(int cur, int max) => OnAPChanged?.Invoke(cur, max);
        public static void RaiseDFChanged(int cur, int max) => OnDFChanged?.Invoke(cur, max);
        public static void RaiseLog(string msg) => OnLog?.Invoke(msg);
    }
}
