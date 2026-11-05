using System.Collections.Generic;
using UnityEngine;
using Aeternal.Core;
using Aeternal.Decks;
using Aeternal.Cards;

namespace Aeternal.Encounters
{
    /// <summary>
    /// How one floor of a branching map is rolled: how many nodes, the odds of each node type,
    /// and up to two types the floor must contain at least once.
    /// </summary>
    [System.Serializable]
    public class MapFloorRule
    {
        public int minNodes = 2;
        public int maxNodes = 3;
        public float enemyWeight;
        public float eliteWeight;
        public float eventWeight;
        public float restWeight;
        public float treasureWeight;
        public float shopWeight;
        public MapNodeType guaranteed = MapNodeType.None;
        [Tooltip("A second type this floor must contain (e.g. a merchant beside the rest before the boss).")]
        public MapNodeType alsoGuaranteed = MapNodeType.None;

        public MapNodeType Pick(System.Random rng)
        {
            float total = enemyWeight + eliteWeight + eventWeight + restWeight + treasureWeight + shopWeight;
            if (total <= 0f) return MapNodeType.Enemy;
            double roll = rng.NextDouble() * total;
            if ((roll -= enemyWeight) < 0) return MapNodeType.Enemy;
            if ((roll -= eliteWeight) < 0) return MapNodeType.Elite;
            if ((roll -= eventWeight) < 0) return MapNodeType.Event;
            if ((roll -= restWeight) < 0) return MapNodeType.Rest;
            if ((roll -= treasureWeight) < 0) return MapNodeType.Treasure;
            return MapNodeType.Shop;
        }

        static MapFloorRule Rule(float enemy, float elite, float ev, float rest, float treasure, float shop,
            MapNodeType guaranteed = MapNodeType.None, MapNodeType alsoGuaranteed = MapNodeType.None) =>
            new MapFloorRule { enemyWeight = enemy, eliteWeight = elite, eventWeight = ev, restWeight = rest,
                               treasureWeight = treasure, shopWeight = shop, guaranteed = guaranteed,
                               alsoGuaranteed = alsoGuaranteed };

        /// <summary>A floor of exactly one node of this type: every path passes through it.</summary>
        static MapFloorRule Single(MapNodeType type) =>
            new MapFloorRule { minNodes = 1, maxNodes = 1, guaranteed = type };

        /// <summary>
        /// Five floors before the boss: fights to start, an event midway (GDD: after every two
        /// enemies), a floor of elites and a rest, then every path converges on one merchant
        /// right before the boss, when there is gold to spend. More merchants may turn up on
        /// floors 2 and 3.
        /// </summary>
        public static List<MapFloorRule> DefaultPlan() => new List<MapFloorRule>
        {
            Rule(1f, 0f, 0f, 0f, 0f, 0f),
            Rule(0.45f, 0f, 0.25f, 0f, 0.15f, 0.15f),
            Rule(0.45f, 0f, 0.25f, 0.15f, 0f, 0.15f, MapNodeType.Event),
            Rule(0.3f, 0.5f, 0f, 0.2f, 0f, 0f, MapNodeType.Elite, MapNodeType.Rest),
            Single(MapNodeType.Shop),
        };
    }

    /// <summary>
    /// A between-fights event: the player picks one boon. Shown after the reward of the
    /// encounter at index afterEncounter (linear regions), or at an Event node of the map.
    /// </summary>
    [System.Serializable]
    public class RegionEvent
    {
        public bool enabled;
        [Tooltip("Index of the encounter after which the event happens (1 = after the second fight).")]
        public int afterEncounter = 1;
        public string title = "Storm Altar";
        [TextArea(2, 4)] public string text;
        [Tooltip("Health restored by the 'rest' choice.")]
        public int healAmount = 8;
        [Tooltip("How many Storm Shards the 'shard' choice lets the player pick from.")]
        public int shardChoices = 3;
    }

    /// <summary>
    /// Describes one region/map of a run: the ordered encounters the player fights,
    /// the deck they start the region with, and the pool of cards offered as rewards.
    /// Per the GDD each region is 3 enemies + 1 boss; this asset is data-driven so that
    /// count is just however many EncounterData entries you add (last one flagged isBoss).
    /// Create via: Assets > Create > Aeternal > Map.
    /// </summary>
    [CreateAssetMenu(menuName = "Aeternal/Map")]
    public class MapData : ScriptableObject
    {
        [Header("Identity")]
        public string regionName = "Steampunk City";

        [Header("Starting Deck")]
        [Tooltip("The deck the player begins this region with. Its cards are copied into the mutable run deck.")]
        public DeckData startingDeck;

        [Header("Shifting")]
        [Tooltip("Additional element decks the player can SHIFT to mid-combat (beyond the starting deck). " +
                 "Leave empty to disable Shifting for this region.")]
        public List<DeckData> shiftDecks = new List<DeckData>();

        [Header("Encounters (in order; flag the last one isBoss)")]
        public List<EncounterData> encounters = new List<EncounterData>();

        [Header("Reward Pool")]
        [Tooltip("Cards that can be offered as a reward after each victory.")]
        public List<CardData> cardRewardPool = new List<CardData>();

        [Header("Flow")]
        [Tooltip("Scene to load when this region's boss is defeated (e.g. 'MainMenu' or the next region's map). Leave blank to return to MainMenu.")]
        public string onRegionClearedScene = "MainMenu";
        [Tooltip("Show the map scene between fights. Off: the run goes straight from one encounter " +
                 "to the next (after the card reward), in order.")]
        public bool showMap = true;

        [Tooltip("Region to continue into after this one's last fight, from its own starting deck. " +
                 "Leave empty to end the run at onRegionClearedScene.")]
        public MapData nextMap;

        [Header("Story")]
        [Tooltip("Full-screen narration before the region's first fight.")]
        public List<DialogueLine> openingNarrative = new List<DialogueLine>();

        [Header("Blessing (the region god's deck, earned by beating the boss)")]
        [Tooltip("Core of the god's deck. Sealed during the region: the player can't Shift to it " +
                 "until the boss is beaten. Leave empty for a region without a blessing.")]
        public DeckData blessingDeck;
        [Tooltip("Storm Shards: one of each card reward offers a card from here. Taking it sets " +
                 "the card aside for the sealed deck instead of the run deck.")]
        public List<CardData> stormShardPool = new List<CardData>();
        [Tooltip("Narration when the boss falls and the deck awakens.")]
        public List<DialogueLine> blessingNarrative = new List<DialogueLine>();
        public string blessingTitle = "Blessing Received";

        [Header("Event (GDD: a special event after every two enemies)")]
        public RegionEvent regionEvent = new RegionEvent();

        [Header("Branching map (Slay the Spire style)")]
        [Tooltip("Generate a branching map each run instead of fighting 'encounters' in order. " +
                 "Needs showMap on.")]
        public bool branchingMap = false;
        [Tooltip("Normal enemies, easiest first: early floors only draw from the start of the list.")]
        public List<EncounterData> normalEncounters = new List<EncounterData>();
        public List<EncounterData> eliteEncounters = new List<EncounterData>();
        public EncounterData bossEncounter;
        [Tooltip("One rule per floor below the boss.")]
        public List<MapFloorRule> floorPlan = MapFloorRule.DefaultPlan();
        [Tooltip("Health restored at a Rest node.")]
        public int restHeal = 10;

        [Header("Gold and the merchant")]
        [Tooltip("Gold for beating a normal enemy (random between x and y).")]
        public Vector2Int goldReward = new Vector2Int(14, 22);
        [Tooltip("Gold for beating an elite (random between x and y).")]
        public Vector2Int eliteGoldReward = new Vector2Int(30, 40);
        [Tooltip("Price of a card at the merchant (each varies by up to priceSpread either way).")]
        public int cardPrice = 40;
        public int shardPrice = 65;
        public int priceSpread = 6;
        [Tooltip("Price of removing a card from the deck at the merchant.")]
        public int removalPrice = 50;
        [Tooltip("Normal cards on sale (plus one Storm Shard while the god's deck is sealed).")]
        public int shopCardCount = 4;

        [Header("Aether (material for Shift upgrades)")]
        [Tooltip("Aether for beating a normal enemy.")]
        public int aetherReward = 1;
        [Tooltip("Aether for beating an elite.")]
        public int eliteAetherReward = 2;
        [Tooltip("Aether for beating the boss.")]
        public int bossAetherReward = 3;
        [Tooltip("Gold price of one Aether at the merchant.")]
        public int aetherPrice = 30;
        [Tooltip("Aether the merchant has for sale per visit.")]
        public int shopAetherStock = 2;

        [Header("Progress")]
        [Tooltip("When the run arrives here from the previous region, keep the deck, health, gold " +
                 "and awakened decks. Off: start over from this region's starting deck (the time loop).")]
        public bool keepsProgress = false;
    }
}
