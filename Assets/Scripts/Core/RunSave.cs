using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Aeternal.Cards;
using Aeternal.Decks;
using Aeternal.Encounters;

namespace Aeternal.Core
{
    /// <summary>
    /// A run as written to disk. Assets (maps, decks, cards, encounters) are stored by asset
    /// name and looked up again through a <see cref="RunCatalog"/> when the run is loaded.
    /// </summary>
    [Serializable]
    public class RunSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string mapName;
        public string mapSceneName;
        /// <summary>Saved as a fight began: continuing restarts that fight.</summary>
        public bool inCombat;

        public List<string> runDeck = new List<string>();
        public List<string> stormShards = new List<string>();
        public List<SavedDeck> unlockedDecks = new List<SavedDeck>();
        public bool blessingReceived;

        public int carriedHealth = -1;
        public int playerMaxHealth;
        public int gold;
        public int aether;
        public List<SavedShiftLevel> shiftLevels = new List<SavedShiftLevel>();

        public int clearedCount;
        public int currentNodeIndex = -1;
        public string currentEncounter;
        public bool openingShown;

        public bool hasMap;
        public int mapFloorCount;
        public List<SavedNode> mapNodes = new List<SavedNode>();
        public int currentMapNodeId = -1;
    }

    /// <summary>A deck awakened by a blessing: its source deck and the cards it holds now.</summary>
    [Serializable]
    public class SavedDeck
    {
        public string source;
        public List<SavedCardEntry> cards = new List<SavedCardEntry>();
    }

    /// <summary>A deck's Shift upgrade level, by deck id.</summary>
    [Serializable]
    public class SavedShiftLevel
    {
        public string deckId;
        public int level;
    }

    [Serializable]
    public class SavedCardEntry
    {
        public string card;
        public int count;
    }

    [Serializable]
    public class SavedNode
    {
        public int id;
        public int floor;
        public int order;
        public int type;
        public string encounter;
        public List<int> next = new List<int>();
        public bool visited;
    }

    /// <summary>
    /// Every asset a run can refer to, found by walking the region chain from the first map
    /// (MapData.nextMap): its maps, decks, cards and encounters, keyed by asset name.
    /// </summary>
    public class RunCatalog
    {
        readonly Dictionary<string, MapData> maps = new Dictionary<string, MapData>();
        readonly Dictionary<string, DeckData> decks = new Dictionary<string, DeckData>();
        readonly Dictionary<string, CardData> cards = new Dictionary<string, CardData>();
        readonly Dictionary<string, EncounterData> encounters = new Dictionary<string, EncounterData>();

        /// <summary>Catalog of every region reachable from firstMap.</summary>
        public static RunCatalog Build(MapData firstMap)
        {
            var catalog = new RunCatalog();
            for (var map = firstMap; map != null && !catalog.maps.ContainsKey(map.name); map = map.nextMap)
                catalog.AddMap(map);
            return catalog;
        }

        public MapData Map(string assetName) => Find(maps, assetName);
        public DeckData Deck(string assetName) => Find(decks, assetName);
        public CardData Card(string assetName) => Find(cards, assetName);
        public EncounterData Encounter(string assetName) => Find(encounters, assetName);

        void AddMap(MapData map)
        {
            maps[map.name] = map;
            AddDeck(map.startingDeck);
            AddDeck(map.blessingDeck);
            if (map.shiftDecks != null) foreach (var deck in map.shiftDecks) AddDeck(deck);
            AddCards(map.cardRewardPool);
            AddCards(map.stormShardPool);
            AddEncounters(map.encounters);
            AddEncounters(map.normalEncounters);
            AddEncounters(map.eliteEncounters);
            AddEncounter(map.bossEncounter);
        }

        void AddDeck(DeckData deck)
        {
            if (deck == null) return;
            Add(decks, deck);
            if (deck.defaultCards == null) return;
            foreach (var entry in deck.defaultCards) AddCard(entry.card);
        }

        void AddCards(List<CardData> list)
        {
            if (list != null) foreach (var card in list) AddCard(card);
        }

        void AddCard(CardData card)
        {
            if (card != null) Add(cards, card);
        }

        void AddEncounters(List<EncounterData> list)
        {
            if (list != null) foreach (var encounter in list) AddEncounter(encounter);
        }

        void AddEncounter(EncounterData encounter)
        {
            if (encounter != null) Add(encounters, encounter);
        }

        static void Add<T>(Dictionary<string, T> table, T asset) where T : UnityEngine.Object
        {
            if (table.TryGetValue(asset.name, out var existing))
            {
                if (existing != asset)
                    Debug.LogWarning($"[RunCatalog] Two {typeof(T).Name} assets are named '{asset.name}'; saves can only tell one apart.");
                return;
            }
            table[asset.name] = asset;
        }

        static T Find<T>(Dictionary<string, T> table, string assetName) where T : class
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            if (table.TryGetValue(assetName, out var asset)) return asset;
            Debug.LogWarning($"[RunCatalog] Saved {typeof(T).Name} '{assetName}' no longer exists; skipped.");
            return null;
        }
    }

    /// <summary>
    /// The save file: one run, as JSON in Application.persistentDataPath. Written through a
    /// temporary file so a crash mid-write leaves the previous save intact.
    /// </summary>
    public static class RunSave
    {
        const string FileName = "run.json";

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Whether there is a saved run to continue.</summary>
        public static bool Exists => File.Exists(FilePath);

        public static void Write(RunSaveData data)
        {
            try
            {
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RunSave] Couldn't save the run: {e.Message}");
            }
        }

        /// <summary>The saved run, or null if there is none or it can't be read.</summary>
        public static RunSaveData Read()
        {
            if (!Exists) return null;
            try
            {
                var data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(FilePath));
                if (data == null || data.version != RunSaveData.CurrentVersion || string.IsNullOrEmpty(data.mapName))
                    return null;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RunSave] Couldn't read the saved run: {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RunSave] Couldn't delete the saved run: {e.Message}");
            }
        }
    }
}
