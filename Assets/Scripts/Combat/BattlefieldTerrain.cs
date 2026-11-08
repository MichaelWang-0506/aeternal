using System;
using UnityEngine;
using Aeternal.Cards;
using Aeternal.Status;

namespace Aeternal.Combat
{
    /// <summary>
    /// Terrain on the four squares around the enemy (GDD: "some special enemies will create
    /// terrain that blocks certain areas"; Poseidon's deck builds it too). A square with terrain
    /// can't be stood on or moved onto. Terrain stands for a number of rounds, then crumbles.
    /// When terrain the player raised makes two terrains on the field, they collapse into a
    /// water cage around the enemy (a Caged status). Created by CombatManager; one per fight.
    /// </summary>
    public class BattlefieldTerrain : MonoBehaviour
    {
        public const int Squares = 4;

        /// <summary>The terrain of the current fight, or null outside combat.</summary>
        public static BattlefieldTerrain Current { get; private set; }

        readonly int[] rounds = new int[Squares];      // 0: open
        readonly bool[] byPlayer = new bool[Squares];

        /// <summary>Fired when terrain rises or falls (also when a cage forms).</summary>
        public event Action OnChanged;
        /// <summary>Fired when terrain collapses into a cage around the enemy (cage rounds).</summary>
        public event Action<int> OnCageFormed;

        void Awake() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public bool IsBlocked(int square) => square >= 0 && square < Squares && rounds[square] > 0;

        /// <summary>Rounds the terrain on this square still stands (0: open).</summary>
        public int RoundsLeft(int square) => square >= 0 && square < Squares ? rounds[square] : 0;

        public bool RaisedByPlayer(int square) => IsBlocked(square) && byPlayer[square];

        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Squares; i++) if (rounds[i] > 0) n++;
                return n;
            }
        }

        /// <summary>
        /// Raises terrain on a square for some rounds. Fails on a square that already has
        /// terrain, or on the one the player stands on.
        /// </summary>
        public bool Raise(int square, int forRounds, bool raisedByPlayer, int playerSquare)
        {
            if (square < 0 || square >= Squares || square == playerSquare || rounds[square] > 0 || forRounds <= 0) return false;
            rounds[square] = forRounds;
            byPlayer[square] = raisedByPlayer;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// After the player raises terrain: two terrains on the field collapse into a water cage
        /// around the enemy, adding cageRounds to its Caged status. Returns whether a cage formed.
        /// </summary>
        public bool TryFormCage(StatusManager enemyStatus, StatusEffectData cage, int cageRounds)
        {
            if (enemyStatus == null || cage == null || cage.behavior != StatusEffectBehavior.Caged || Count < 2) return false;
            ClearAll();
            enemyStatus.AddStatus(string.IsNullOrEmpty(cage.statusId) ? "Caged" : cage.statusId, 1, Mathf.Max(1, cageRounds), cage);
            OnCageFormed?.Invoke(cageRounds);
            return true;
        }

        public void ClearAll()
        {
            bool any = false;
            for (int i = 0; i < Squares; i++)
            {
                if (rounds[i] > 0) any = true;
                rounds[i] = 0;
                byPlayer[i] = false;
            }
            if (any) OnChanged?.Invoke();
        }

        /// <summary>Called at round end: every terrain stands one round less.</summary>
        public void Tick()
        {
            bool changed = false;
            for (int i = 0; i < Squares; i++)
            {
                if (rounds[i] <= 0) continue;
                rounds[i]--;
                changed = true;
                if (rounds[i] == 0) byPlayer[i] = false;
            }
            if (changed) OnChanged?.Invoke();
        }

        /// <summary>
        /// Where a move of the given squares (positive clockwise) from a square ends. A move
        /// to the far side is a leap over the enemy: only its landing square matters. Shorter
        /// moves stop in front of terrain. blocked is true if terrain cut the move short.
        /// </summary>
        public int MoveTarget(int from, int steps, out bool blocked)
        {
            blocked = false;
            if (steps == 0) return from;
            if (Mathf.Abs(steps) == 2)
            {
                int across = (from + 2) % Squares;
                if (IsBlocked(across)) { blocked = true; return from; }
                return across;
            }
            int at = from;
            int dir = steps > 0 ? 1 : -1;
            for (int i = 0; i < Mathf.Abs(steps); i++)
            {
                int next = (at + dir + Squares) % Squares;
                if (IsBlocked(next)) { blocked = true; break; }
                at = next;
            }
            return at;
        }
    }
}
