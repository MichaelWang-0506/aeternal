using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Aeternal.Cards;
using Aeternal.Encounters;

namespace Aeternal.Core
{
    /// <summary>
    /// Persistent owner of a single run's state: the mutable deck, carried-over health,
    /// map progress, and which encounter combat should load next. Survives scene loads.
    ///
    /// Flow it drives:
    ///   MainMenu -> (StartRun) -> MapScene -> (SelectEncounter + LoadCombat) -> PlayGameScene
    ///     -> victory: (ReportVictory -> reward -> AdvanceNode -> MapScene)
    ///     -> boss victory: region cleared -> onRegionClearedScene
    ///     -> defeat: (ReportDefeat -> MainMenu)
    ///
    /// PlayGameScene remains playable standalone: if no run is active (HasActiveRun == false),
    /// CombatManager falls back to its scene-serialized enemy/deck for isolated testing.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        [Header("Combat scene name")]
        public string combatSceneName = "PlayGameScene";
        public string mapSceneName = "MapScene";
        public string mainMenuSceneName = "MainMenu";

        // ---- Run state ----
        public MapData CurrentMap { get; private set; }
        public List<CardData> RunDeck { get; private set; } = new List<CardData>();
        /// <summary>Health to start the next combat with. -1 means "full" (use the stats max).</summary>
        public int CarriedHealth { get; private set; } = -1;
        public int CurrentNodeIndex { get; private set; } = -1;
        /// <summary>How many encounters have been beaten this run. The next fightable node == this value.</summary>
        public int ClearedCount { get; private set; } = 0;
        public EncounterData CurrentEncounter { get; private set; }

        public bool HasActiveRun => CurrentMap != null;
        public bool IsCurrentEncounterBoss => CurrentEncounter != null && CurrentEncounter.isBoss;

        /// <summary>Additional decks (beyond the primary run deck) the player can Shift to in combat.</summary>
        public List<Aeternal.Decks.DeckData> ShiftDecks =>
            CurrentMap != null ? CurrentMap.shiftDecks : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ------------------------------------------------------------------
        // Run lifecycle
        // ------------------------------------------------------------------

        /// <summary>Begin a fresh run on the given map. Rebuilds the run deck from the map's starting deck.</summary>
        public void StartRun(MapData map)
        {
            CurrentMap = map;
            CurrentNodeIndex = -1;
            ClearedCount = 0;
            CurrentEncounter = null;
            CarriedHealth = -1;
            RebuildRunDeckFromStartingDeck();
        }

        private void RebuildRunDeckFromStartingDeck()
        {
            RunDeck.Clear();
            if (CurrentMap == null || CurrentMap.startingDeck == null) return;
            foreach (var entry in CurrentMap.startingDeck.defaultCards)
            {
                if (entry.card == null) continue;
                for (int i = 0; i < Mathf.Max(1, entry.count); i++)
                    RunDeck.Add(entry.card);
            }
        }

        /// <summary>Select which encounter combat will load. Index into CurrentMap.encounters.</summary>
        public void SelectEncounter(int index)
        {
            if (CurrentMap == null || index < 0 || index >= CurrentMap.encounters.Count)
            {
                Debug.LogWarning($"[RunManager] SelectEncounter({index}) out of range.");
                return;
            }
            CurrentNodeIndex = index;
            CurrentEncounter = CurrentMap.encounters[index];
        }

        /// <summary>Convenience: select the next unfought encounter in order.</summary>
        public void SelectNextEncounter()
        {
            SelectEncounter(ClearedCount);
        }

        /// <summary>
        /// Enter the region after StartRun: the map, or, for a region without one, its first fight.
        /// </summary>
        public void EnterRegion()
        {
            if (CurrentMap != null && !CurrentMap.showMap)
            {
                SelectNextEncounter();
                LoadCombat();
            }
            else
            {
                SceneManager.LoadScene(mapSceneName);
            }
        }

        /// <summary>Load the combat scene for the currently selected encounter.</summary>
        public void LoadCombat()
        {
            if (CurrentEncounter == null)
            {
                Debug.LogWarning("[RunManager] LoadCombat with no encounter selected.");
                return;
            }
            SceneManager.LoadScene(combatSceneName);
        }

        // ------------------------------------------------------------------
        // Combat outcome reporting (called by CombatManager / CombatEndController)
        // ------------------------------------------------------------------

        /// <summary>Record a victory. Stores the player's remaining health so it carries to the next fight.</summary>
        public void ReportVictory(int remainingHealth)
        {
            CarriedHealth = Mathf.Max(1, remainingHealth);
        }

        /// <summary>End the run after a defeat and return to the main menu.</summary>
        public void ReportDefeat()
        {
            ResetRun();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>Add a rewarded card to the run deck.</summary>
        public void AddCardToDeck(CardData card)
        {
            if (card != null) RunDeck.Add(card);
        }

        /// <summary>
        /// Advance past the just-cleared encounter. If it was the boss, the region is complete
        /// and we load onRegionClearedScene; otherwise we return to the map for the next node,
        /// or straight into the next fight when the region has no map.
        /// </summary>
        public void AdvanceAfterVictory()
        {
            // Mark the just-cleared node so the next map node becomes the fightable one.
            if (CurrentNodeIndex >= ClearedCount) ClearedCount = CurrentNodeIndex + 1;

            bool wasBoss = IsCurrentEncounterBoss;
            bool wasLast = CurrentMap != null && CurrentNodeIndex >= CurrentMap.encounters.Count - 1;

            if (wasBoss || wasLast)
            {
                string next = (CurrentMap != null && !string.IsNullOrEmpty(CurrentMap.onRegionClearedScene))
                    ? CurrentMap.onRegionClearedScene
                    : mainMenuSceneName;
                ResetRun();
                SceneManager.LoadScene(next);
            }
            else
            {
                EnterRegion();
            }
        }

        public void ResetRun()
        {
            CurrentMap = null;
            CurrentEncounter = null;
            CurrentNodeIndex = -1;
            ClearedCount = 0;
            CarriedHealth = -1;
            RunDeck.Clear();
        }

        /// <summary>Has the encounter at this index already been cleared?</summary>
        public bool IsEncounterCleared(int index) => index < ClearedCount;

        /// <summary>Is this the next fightable node? (Used by map nodes to gate clicks.)</summary>
        public bool IsEncounterAvailable(int index) => index == ClearedCount;
    }
}
