using UnityEngine;

namespace Aeternal.Stats
{
    [CreateAssetMenu(menuName = "Aeternal/Stats/PlayerStats")]
    public class PlayerStats : ScriptableObject
    {
        [Header("Health")]
        public int maxHealth = 100;
        public bool allowOverheal = false;

        [Header("Resources")]
        public int maxAP = 3;
        public int maxDF = 2;

        [Header("Misc")]
        public int baseDefense = 0; // reserved for future flat damage reduction
    }
}
