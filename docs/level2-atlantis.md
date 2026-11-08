# Level 2: Atlantis (the sunken city)

Based on: World Building Assignments (Poseidon: squids, octopus, drowned corpses, a half-human,
half-octopus boss, Atlantis), Initial Card Ideas (Poseidon deck: terrain and water cage), GDD §5
("some special enemies will create terrain that blocks certain areas").
Status: **first version, numbers to be checked in playtests.** Built on branch `feat/level-2-atlantis`.

---

## 1. Where it sits in the run

```
Zeusonia → Zeus → Blessing of Zeus → Atlantis → Poseidon → Blessing of Poseidon → To be continued
```

- `Zeusonia.nextMap` now points to Atlantis (it pointed to the Divine Trial test region; that asset is
  kept, just no longer in the chain).
- Atlantis `keepsProgress`: deck, health, gold, Aether, Shift levels and the awakened Zeus deck carry
  over. **Shift is available throughout Level 2** (Zeus deck), the first level where it is.
- Story: the ruler falls from the storm into the sea; Atlantis is his own sunken harbour capital.
  The opening narration also teaches where the Shift control is.

## 2. Map

Branching map, same floor plan as Zeusonia (default `floorPlan`), merchant guaranteed before the boss.

| Node | Content |
|---|---|
| Enemy | Reef Lurker (16 HP / 3 water / three squares), Drowned Sailor (20 HP / 4 / one square) |
| Elite | Drowned Captain (34 HP / 4 / one square, **raises coral walls every 2 rounds**), Tidecaller (30 HP / 5 water / three squares) |
| Event | Drowned Shrine: heal 8 / remove a card / pick 1 of 3 Tide Pearls |
| Rest | heal 10 |
| Treasure | a card for the starting deck (deck-choice rewards: Tide Pearls come from fights) |
| Boss | **Poseidon** (72 HP / 4 water / three squares): **Tidal Wave** every 3rd round (every square but one, ×2 damage, warned the round before as "THE TIDE RISES"), coral walls every 2 rounds |

Gold: 16–24 per fight, 34–44 per elite (Zeusonia: 14–22 / 30–40).

## 3. Coral walls (terrain)

- A wall stands on one of the four squares for **2 rounds** (number shown on the tile), then crumbles.
- You can't stand on a wall or move onto it. A one-square move stops in front of it; a move to the
  far side is a leap over the enemy and only needs an open landing square.
- Enemies raise a wall on a square **next to you**, never on yours, and never on the last square an
  attack leaves safe. A boss's charged attack always leaves an open square safe.
- Rules live in `BattlefieldTerrain`; when walls rise: `EncounterData.terrainEvery / terrainRounds`.

## 4. Poseidon's deck (sealed in Atlantis, awakens after the boss)

Sealed cards are **Tide Pearls** (`MapData.shardName`). Card list from Initial Card Ideas, AP as there:

| Card | AP | Rarity | Effect |
|---|---|---|---|
| Wave Push | 1 | C | 5 water damage; if terrain stands behind the enemy, stun 1 round |
| Water Cage | 2 | U | Cage the enemy 1 round |
| Hidden Wave | 1 | C | 5 water damage; terrain behind the enemy for 2 rounds |
| Ocean's Blessing | 1 | U | +2 AP |
| Ocean's Protection | 1 | U | 7 shield |
| Ocean's Flow | 1 | C | Draw 2 |
| Trident Strike | 1 | C | 5 water damage, leap to the far side if open |
| Ocean Block | 1 | C | Move clockwise once, terrain where you stood for 2 rounds |
| Suffocation | 3 | R | Double the rounds left on the cage |
| Breakout | 2 | R | End the cage: 6 water damage per round it had left (**the document's text breaks off here; this ending is ours**) |

- **Water cage**: when your terrain makes two terrains on the field (yours or an enemy's coral), they
  collapse into a cage around the enemy. Caged: **5 damage at round end, +2 for each round it has
  held** (5, 7, 9…). Enemy coral + your Hidden Wave is the intended counterplay.
- Core deck (6): Trident Strike, Wave Push, Hidden Wave ×2, Water Cage, Ocean's Flow. Shift-in: 4 shield.
- Tide Pearl pool: Ocean Block, Ocean's Protection, Ocean's Blessing, Suffocation, Breakout, Trident Strike.

## 5. Rewards: grow the deck you choose (both levels)

Design question: with more god decks, one mixed card pool dilutes every deck. Decided rule
(`MapData.deckChoiceRewards`, on for Zeusonia and Atlantis):

1. After a fight, **choose one of your decks** (starting deck, or any awakened god deck), then take
   **1 of 3 from that deck's pool** (or skip). With only one deck, step 1 is skipped.
2. Then **1 of 2 sealed cards** for the region's god deck (1 of 3 after an elite), or skip.
3. Treasure gives a normal card, not a sealed one.
4. Merchant, once per visit: **remove** a card from a deck you choose, or **replace** one with 1 of 3
   cards **of the same rarity** from that deck's pool (`replacePrice` 60 gold).

Why the guards: the free sealed card every fight doubles card income and removes the old
"power now or later" choice. Making it a skippable pick (no bloat) and dropping the treasure shard
keep the awakened deck at about **10–12 cards** (old rule: 6–12), so god decks don't outgrow the
fights. Shifting still costs Divine Focus, which bounds how much a strong god deck matters.

Pools: starting deck → `cardRewardPool`; awakened decks → `deckRewardPools` (by deck id); sealed deck →
`stormShardPool`. Rarity is a new `CardData.rarity` (Common default); Level 1's uncommon and rare
cards got a value: Steam Mist, Backflip Kick, Sacrifice, Thunder Spear, Thunder Armor, Combo Punch (U);
Smash, Disembowel, Extraction, Thunder's Rage (R).

## 6. Keeping Level 1 untouched

- Every Level 2 asset is new: `Assets/Scriptable Objects/Atlantis/`, `Assets/Images/Atlantis/`. Sprites
  and card art are recoloured **copies**; Atlantis's reward cards are copies of the Level 1 cards
  (asset "Atlantis Slash", shown as "Slash" via `CardData.title`), so retuning them leaves Level 1 alone.
- Code features are off unless data turns them on (terrain, cage, new effects, underwater ambience).
- Level 1 asset edits: `Zeusonia.asset` (`nextMap`, `deckChoiceRewards`) and one `rarity` line in each
  of the 10 cards above.
- The content is generated by `tools/content/gen_atlantis.py` (recolours + assets with stable GUIDs); regenerate rather than
  hand-edit if the list changes.

## 7. Tuning knobs

| Change | Where |
|---|---|
| Enemy HP | `Atlantis/Enemies/* Stats.asset` → `maxHealth` |
| Damage, AOE, coral frequency, Tidal Wave | `Atlantis/Enemies/* Encounter.asset` → `baseDamage`, `canAOE`, `terrainEvery`, `chargedAttackEvery` |
| Cage damage / growth | the cards' `statusEffectData.behaviorValue` / `growthPerRound` |
| Reward rule, sealed picks, Replace price | `Atlantis.asset` / `Zeusonia.asset` → `deckChoiceRewards`, `sealedChoices`, `replacePrice` |
| Pools | `cardRewardPool`, `deckRewardPools`, `stormShardPool` |
| Look | `Atlantis.asset` → `backgroundTint`, `ambience`; `BattlefieldStyle` → "Terrain", "Ambience" |

## 8. Open questions

1. Poseidon's art is Zeus recoloured (still a bolt in his hand); the GDD's half-octopus boss needs real art.
2. Poseidon's cards are only played after his blessing, i.e. from Level 3. They are tested by the
   automated playtest; players can't use them yet.
3. Balance of Level 2 is unplayed: 72 HP Poseidon + coral + Tidal Wave against a player who can Shift.
4. "Corrosion" for drowned corpses (World Building) is not in: they deal plain physical damage.
