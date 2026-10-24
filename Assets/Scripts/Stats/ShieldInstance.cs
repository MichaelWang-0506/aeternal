using System;

namespace Aeternal.Stats
{
    [Serializable]
    public class ShieldInstance
    {
        public string id;
        public int amount;
        public int roundsRemaining; // 0 = persistent until consumed
        public string tag; // optional (e.g., "tempShield", "aegis")

        public ShieldInstance(string id, int amount, int rounds = 0, string tag = null)
        {
            this.id = id;
            this.amount = amount;
            this.roundsRemaining = rounds;
            this.tag = tag;
        }
    }
}
