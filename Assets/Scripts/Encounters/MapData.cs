using System.Collections.Generic;
using UnityEngine;
using Aeternal.Decks;
using Aeternal.Cards;

namespace Aeternal.Encounters
{
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
    }
}
