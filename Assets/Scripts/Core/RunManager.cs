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
    ///     -> boss victory: region cleared -> the next region (MapData.nextMap) or onRegionClearedScene
    ///     -> defeat: (ReportDefeat -> MainMenu)
    /// It also holds what a region's god grants: Storm Shards set aside for a sealed deck, and
    /// the decks awakened by blessings, which are what the player can Shift to.
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

        /// <summary>
        /// Cards set aside for a god's deck that is still sealed (Storm Shards): unusable until
        /// the region's blessing, which adds them to that deck.
        /// </summary>
        public List<CardData> StormShards { get; private set; } = new List<CardData>();

        /// <summary>Decks awakened by blessings this run.</summary>
        public List<Aeternal.Decks.DeckData> UnlockedDecks { get; private set; } = new List<Aeternal.Decks.DeckData>();

        public bool BlessingReceived { get; private set; }

        /// <summary>
        /// Additional decks (beyond the primary run deck) the player can Shift to in combat:
        /// the map's own, plus any awakened by a blessing. None means Shifting is unavailable.
        /// </summary>
        public List<Aeternal.Decks.DeckData> ShiftDecks
        {
            get
            {
                var decks = new List<Aeternal.Decks.DeckData>();
                if (CurrentMap != null && CurrentMap.shiftDecks != null) decks.AddRange(CurrentMap.shiftDecks);
                decks.AddRange(UnlockedDecks);
                return decks;
            }
        }

        /// <summary>Whether this region seals a god's deck the player is collecting shards for.</summary>
        public bool CollectsStormShards =>
            CurrentMap != null && CurrentMap.blessingDeck != null && !BlessingReceived
            && CurrentMap.stormShardPool != null && CurrentMap.stormShardPool.Count > 0;

        /// <summary>
        /// Whether the region's event happens after the encounter just fought. Linear regions
        /// only; on a branching map, events are nodes the player picks.
        /// </summary>
        public bool RegionEventDue =>
            Map == null && CurrentMap != null && CurrentMap.regionEvent != null && CurrentMap.regionEvent.enabled
            && CurrentNodeIndex == CurrentMap.regionEvent.afterEncounter;

        // ---- Branching map ----

        /// <summary>This run's map of the region, or null for a region fought in order.</summary>
        public RunMap Map { get; private set; }

        /// <summary>The map node the player is at (-1: not on the map yet).</summary>
        public int CurrentMapNodeId { get; private set; } = -1;

        /// <summary>Whether the region's opening narration has been shown on the map.</summary>
        public bool OpeningShown { get; set; }

        /// <summary>The player's max health, as of the last fight (0 before any).</summary>
        public int PlayerMaxHealth { get; set; }

        public bool IsCurrentEncounterElite => CurrentEncounter != null && CurrentEncounter.isElite;

        /// <summary>The nodes the player can go to next.</summary>
        public List<RunMapNode> AvailableNodes()
        {
            var list = new List<RunMapNode>();
            if (Map == null) return list;
            var here = Map.Get(CurrentMapNodeId);
            if (here == null) list.AddRange(Map.Floor(0));
            else foreach (int id in here.next) list.Add(Map.Get(id));
            return list;
        }

        /// <summary>
        /// Move to a map node. A fight loads the combat scene; other nodes are resolved by the
        /// map screen. Returns false if the node can't be reached from here.
        /// </summary>
        public bool EnterMapNode(RunMapNode node)
        {
            if (node == null || !AvailableNodes().Contains(node)) return false;
            CurrentMapNodeId = node.id;
            node.visited = true;
            if (!node.IsFight) return true;
            CurrentNodeIndex = node.floor;
            CurrentEncounter = node.encounter;
            LoadCombat();
            return true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Back on the map (after a fight, or entering a region) is a save point.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == mapSceneName) Save();
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
            // Each region starts the loop over: earlier blessings and shards are gone.
            StormShards.Clear();
            UnlockedDecks.Clear();
            BlessingReceived = false;
            RebuildRunDeckFromStartingDeck();

            Map = map != null && map.branchingMap ? RunMap.Generate(map, new System.Random()) : null;
            CurrentMapNodeId = -1;
            OpeningShown = false;
            Gold = 0;
            Aether = 0;
            shiftLevels.Clear();
        }

        /// <summary>Set a card aside for the region's sealed deck.</summary>
        public void AddStormShard(CardData card)
        {
            if (card != null) StormShards.Add(card);
        }

        /// <summary>
        /// The region's god blesses the player: its deck awakens (the blessing deck plus every
        /// Storm Shard collected) and becomes available to Shift to. Returns the awakened deck.
        /// </summary>
        public Aeternal.Decks.DeckData ReceiveBlessing()
        {
            if (CurrentMap == null || CurrentMap.blessingDeck == null || BlessingReceived) return null;
            var source = CurrentMap.blessingDeck;
            var deck = ScriptableObject.CreateInstance<Aeternal.Decks.DeckData>();
            deck.name = source.name;
            deck.deckId = source.deckId;
            deck.description = source.description;
            deck.artwork = source.artwork;
            deck.defaultCards = new List<Aeternal.Decks.CardEntry>(source.defaultCards);
            foreach (var shard in StormShards)
                deck.defaultCards.Add(new Aeternal.Decks.CardEntry { card = shard, count = 1 });
            deck.onShiftIn = source.onShiftIn;
            deck.onShiftOut = source.onShiftOut;
            deck.CopyShiftUpgrades(source);

            StormShards.Clear();
            UnlockedDecks.Add(deck);
            BlessingReceived = true;
            return deck;
        }

        /// <summary>Remove one copy of a card from the run deck (e.g. at a region event).</summary>
        public void RemoveCardFromDeck(CardData card)
        {
            if (card != null) RunDeck.Remove(card);
        }

        /// <summary>Restore health between fights, up to maxHealth.</summary>
        public void HealBetweenFights(int amount, int maxHealth)
        {
            if (CarriedHealth < 0) return; // already full
            CarriedHealth = Mathf.Min(maxHealth, CarriedHealth + Mathf.Max(0, amount));
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
            Save(inCombat: true);
            SceneManager.LoadScene(combatSceneName);
        }

        // ------------------------------------------------------------------
        // Combat outcome reporting (called by CombatManager / CombatEndController)
        // ------------------------------------------------------------------

        /// <summary>Record a victory. Stores the player's remaining health so it carries to the next fight.</summary>
        public void ReportVictory(int remainingHealth)
        {
            CarriedHealth = Mathf.Max(1, remainingHealth);
            LastAetherEarned = 0;
            if (CurrentMap != null)
            {
                LastAetherEarned = IsCurrentEncounterBoss ? CurrentMap.bossAetherReward
                    : IsCurrentEncounterElite ? CurrentMap.eliteAetherReward
                    : CurrentMap.aetherReward;
                AddAether(LastAetherEarned);
            }
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
            // On a branching map, back to the map to pick the next node (unless that was the boss).
            if (Map != null && !IsCurrentEncounterBoss)
            {
                ClearedCount++;
                SceneManager.LoadScene(mapSceneName);
                return;
            }

            // Mark the just-cleared node so the next map node becomes the fightable one.
            if (CurrentNodeIndex >= ClearedCount) ClearedCount = CurrentNodeIndex + 1;

            bool wasBoss = IsCurrentEncounterBoss;
            bool wasLast = CurrentMap != null && CurrentNodeIndex >= CurrentMap.encounters.Count - 1;

            if ((wasBoss || wasLast) && CurrentMap.nextMap != null)
            {
                ContinueToNextRegion();
            }
            else if (wasBoss || wasLast)
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

        /// <summary>
        /// Start the next region (MapData.nextMap): from its own starting deck, or, if it
        /// keepsProgress, with the deck, health, gold and awakened decks the player has now.
        /// </summary>
        public void ContinueToNextRegion()
        {
            var next = CurrentMap != null ? CurrentMap.nextMap : null;
            if (next == null)
            {
                ReportDefeat();
                return;
            }

            if (next.keepsProgress)
            {
                var deck = new List<CardData>(RunDeck);
                var decks = new List<Aeternal.Decks.DeckData>(UnlockedDecks);
                var levels = new Dictionary<string, int>(shiftLevels);
                int health = CarriedHealth, gold = Gold, maxHealth = PlayerMaxHealth, aether = Aether;
                StartRun(next);
                RunDeck.Clear();
                RunDeck.AddRange(deck);
                UnlockedDecks.AddRange(decks);
                CarriedHealth = health;
                Gold = gold;
                PlayerMaxHealth = maxHealth;
                Aether = aether;
                foreach (var pair in levels) shiftLevels[pair.Key] = pair.Value;
            }
            else
            {
                StartRun(next);
            }
            EnterRegion();
        }

        // ---- Gold ----

        public int Gold { get; private set; }

        public void AddGold(int amount) => Gold += Mathf.Max(0, amount);

        /// <summary>Spends gold if there is enough; returns whether it was spent.</summary>
        public bool SpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        // ---- Aether and Shift upgrades ----
        // GDD: "upgrade their shifting effect using the materials they obtained after each fight".
        // Levels are kept per deck id, so a god's deck can be upgraded while still sealed.

        /// <summary>Material for Shift upgrades: earned from fights, sold by the merchant.</summary>
        public int Aether { get; private set; }

        /// <summary>Aether earned by the last victory (for the reward screen).</summary>
        public int LastAetherEarned { get; private set; }

        private readonly Dictionary<string, int> shiftLevels = new Dictionary<string, int>();

        public void AddAether(int amount) => Aether += Mathf.Max(0, amount);

        /// <summary>Buys one Aether for price gold; returns whether it was bought.</summary>
        public bool BuyAether(int price)
        {
            if (!SpendGold(price)) return false;
            Aether++;
            return true;
        }

        static string ShiftKey(Aeternal.Decks.DeckData deck) => string.IsNullOrEmpty(deck.deckId) ? deck.name : deck.deckId;

        /// <summary>The deck's Shift level (0: not upgraded).</summary>
        public int ShiftLevel(Aeternal.Decks.DeckData deck) =>
            deck != null && shiftLevels.TryGetValue(ShiftKey(deck), out int level) ? level : 0;

        /// <summary>Added to the deck's damage, shield and heal Shift effects.</summary>
        public int ShiftBonus(Aeternal.Decks.DeckData deck) => deck == null ? 0 : ShiftLevel(deck) * deck.shiftUpgradeBonus;

        /// <summary>Aether cost of the deck's next Shift level, or -1 at the top level.</summary>
        public int NextShiftUpgradeCost(Aeternal.Decks.DeckData deck)
        {
            int level = ShiftLevel(deck);
            return deck != null && level < deck.MaxShiftLevel ? deck.shiftUpgradeCosts[level] : -1;
        }

        /// <summary>Spends Aether to raise the deck's Shift level; returns whether it did.</summary>
        public bool TryUpgradeShift(Aeternal.Decks.DeckData deck)
        {
            int cost = NextShiftUpgradeCost(deck);
            if (cost < 0 || Aether < cost) return false;
            Aether -= cost;
            shiftLevels[ShiftKey(deck)] = ShiftLevel(deck) + 1;
            return true;
        }

        /// <summary>
        /// The decks whose Shift can be upgraded: the starting deck, those the player can Shift
        /// to, and the region's god deck while it is still sealed.
        /// </summary>
        public List<(Aeternal.Decks.DeckData deck, bool isSealed)> ShiftUpgradeDecks()
        {
            var list = new List<(Aeternal.Decks.DeckData, bool)>();
            var seen = new HashSet<string>();
            void Add(Aeternal.Decks.DeckData deck, bool isSealed)
            {
                if (deck != null && seen.Add(ShiftKey(deck))) list.Add((deck, isSealed));
            }
            if (CurrentMap == null) return list;
            Add(CurrentMap.startingDeck, false);
            foreach (var deck in ShiftDecks) Add(deck, false);
            if (CurrentMap.blessingDeck != null && !BlessingReceived) Add(CurrentMap.blessingDeck, true);
            return list;
        }

        /// <summary>The run is over (won, lost or abandoned): clears it and deletes its save.</summary>
        public void ResetRun()
        {
            RunSave.Delete();
            ResetRunState();
        }

        /// <summary>Clears the run from memory but keeps its save, for Continue.</summary>
        public void UnloadRun() => ResetRunState();

        private void ResetRunState()
        {
            CurrentMap = null;
            CurrentEncounter = null;
            CurrentNodeIndex = -1;
            ClearedCount = 0;
            CarriedHealth = -1;
            RunDeck.Clear();
            StormShards.Clear();
            UnlockedDecks.Clear();
            BlessingReceived = false;
            Map = null;
            CurrentMapNodeId = -1;
            OpeningShown = false;
            Gold = 0;
            Aether = 0;
            shiftLevels.Clear();
        }

        // ------------------------------------------------------------------
        // Saving
        // ------------------------------------------------------------------

        /// <summary>
        /// Writes the run to disk. Saved on the map and as each fight begins (inCombat), so
        /// continuing puts the player back on the map or restarts that fight.
        /// </summary>
        public void Save(bool inCombat = false)
        {
            if (HasActiveRun) RunSave.Write(Capture(inCombat));
        }

        /// <summary>The run as save data.</summary>
        public RunSaveData Capture(bool inCombat)
        {
            var data = new RunSaveData
            {
                mapName = CurrentMap != null ? CurrentMap.name : null,
                mapSceneName = mapSceneName,
                inCombat = inCombat,
                blessingReceived = BlessingReceived,
                carriedHealth = CarriedHealth,
                playerMaxHealth = PlayerMaxHealth,
                gold = Gold,
                aether = Aether,
                clearedCount = ClearedCount,
                currentNodeIndex = CurrentNodeIndex,
                currentEncounter = CurrentEncounter != null ? CurrentEncounter.name : null,
                openingShown = OpeningShown,
                hasMap = Map != null,
                currentMapNodeId = CurrentMapNodeId,
            };
            foreach (var card in RunDeck) if (card != null) data.runDeck.Add(card.name);
            foreach (var pair in shiftLevels) data.shiftLevels.Add(new SavedShiftLevel { deckId = pair.Key, level = pair.Value });
            foreach (var card in StormShards) if (card != null) data.stormShards.Add(card.name);
            foreach (var deck in UnlockedDecks)
            {
                var saved = new SavedDeck { source = deck.name };
                foreach (var entry in deck.defaultCards)
                    if (entry.card != null) saved.cards.Add(new SavedCardEntry { card = entry.card.name, count = entry.count });
                data.unlockedDecks.Add(saved);
            }
            if (Map != null)
            {
                data.mapFloorCount = Map.FloorCount;
                foreach (var node in Map.nodes)
                {
                    var saved = new SavedNode
                    {
                        id = node.id, floor = node.floor, order = node.order, type = (int)node.type,
                        encounter = node.encounter != null ? node.encounter.name : null, visited = node.visited,
                    };
                    saved.next.AddRange(node.next);
                    data.mapNodes.Add(saved);
                }
            }
            return data;
        }

        /// <summary>
        /// Replaces the current run with saved data. Returns false (and leaves no run) if the
        /// saved region no longer exists. Cards or encounters that no longer exist are skipped.
        /// </summary>
        public bool Restore(RunSaveData data, RunCatalog catalog)
        {
            ResetRunState();
            var map = data != null ? catalog.Map(data.mapName) : null;
            if (map == null) return false;

            CurrentMap = map;
            if (!string.IsNullOrEmpty(data.mapSceneName)) mapSceneName = data.mapSceneName;
            BlessingReceived = data.blessingReceived;
            CarriedHealth = data.carriedHealth;
            PlayerMaxHealth = data.playerMaxHealth;
            Gold = data.gold;
            Aether = data.aether;
            foreach (var saved in data.shiftLevels)
                if (!string.IsNullOrEmpty(saved.deckId)) shiftLevels[saved.deckId] = saved.level;
            ClearedCount = data.clearedCount;
            CurrentNodeIndex = data.currentNodeIndex;
            CurrentEncounter = catalog.Encounter(data.currentEncounter);
            OpeningShown = data.openingShown;

            foreach (var cardName in data.runDeck) { var card = catalog.Card(cardName); if (card != null) RunDeck.Add(card); }
            foreach (var cardName in data.stormShards) { var card = catalog.Card(cardName); if (card != null) StormShards.Add(card); }
            foreach (var saved in data.unlockedDecks)
            {
                var source = catalog.Deck(saved.source);
                if (source == null) continue;
                var deck = ScriptableObject.CreateInstance<Aeternal.Decks.DeckData>();
                deck.name = source.name;
                deck.deckId = source.deckId;
                deck.description = source.description;
                deck.artwork = source.artwork;
                deck.defaultCards = new List<Aeternal.Decks.CardEntry>();
                foreach (var entry in saved.cards)
                {
                    var card = catalog.Card(entry.card);
                    if (card != null) deck.defaultCards.Add(new Aeternal.Decks.CardEntry { card = card, count = entry.count });
                }
                deck.onShiftIn = source.onShiftIn;
                deck.onShiftOut = source.onShiftOut;
                deck.CopyShiftUpgrades(source);
                UnlockedDecks.Add(deck);
            }

            if (data.hasMap)
            {
                var nodes = new List<RunMapNode>();
                foreach (var saved in data.mapNodes)
                {
                    var node = new RunMapNode
                    {
                        id = saved.id, floor = saved.floor, order = saved.order, type = (MapNodeType)saved.type,
                        encounter = catalog.Encounter(saved.encounter), visited = saved.visited,
                    };
                    node.next.AddRange(saved.next);
                    nodes.Add(node);
                }
                Map = RunMap.Restore(data.mapFloorCount, nodes);
                CurrentMapNodeId = data.currentMapNodeId;
            }
            return true;
        }

        /// <summary>Has the encounter at this index already been cleared?</summary>
        public bool IsEncounterCleared(int index) => index < ClearedCount;

        /// <summary>Is this the next fightable node? (Used by map nodes to gate clicks.)</summary>
        public bool IsEncounterAvailable(int index) => index == ClearedCount;
    }
}
