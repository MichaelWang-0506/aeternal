# Game Brief: Aeternal

<!-- Rewritten 2026-11-04 from the team's GDD, Final Project Documentation and Mechanics
     Progress Check 2 (Desktop/aeternal/Development Documents/, outside the repo), merged with
     the earlier draft (README.md, docs/dev-handoff.md, docs/level1-zeusonia.md).
     This brief is authoritative where it and the GDD differ: see "Amendments to the GDD".
     Lines marked ⚠ are inferred, not confirmed. -->

**One-sentence pitch:** A Greek-myth roguelike deck-builder where a fallen tyrant, trapped in Chronus's time loop for mocking Death, retakes his shattered empire region by region, dodging telegraphed attacks around the enemy and Shifting between the element decks of the gods he defeats.

## Core loop
- Read the enemy's intent (which of the four squares around it is hit, and for how much) → spend Action Points on movement cards to step aside, attack cards to strike, or Guard (1 AP for a small shield) → end turn.
- Win a fight → gold, Aether, and 1 of 3 cards (or a sealed Storm Shard for the region god's deck).
- Climb the region's branching map (fights, elites, events, rest, treasure, merchant) to its god.
- Beat the god → its blessing awakens that god's deck → Shift between decks in battle (Z, costs Divine Focus); upgrade each deck's Shift effect with Aether on the Shift page (Space).

## Player goal & fail state — what "working" looks like
- Goal: clear a region by beating its god (Level 1: Zeus, 60 HP, a Thunderbolt every 3 rounds) while building the deck you will Shift into next; the long goal is the right to challenge Thanatos.
- Fail: health carries over between fights; 0 HP ends the run, and the loop starts again.

## MVP — what must exist to be the game
- Positional combat around a central enemy: intents, AP, Guard, Bleed / Stun / Dodge — built
- Divine Shift between element decks with Divine Focus, Shift-in / Shift-out effects — built
- Shift upgrades: Aether from fights and the merchant, Shift page (Space) — built 2026-11-04, untested
- Prologue "The Fall" with the scripted Thanatos death + tutorial — built
- Level 1 Zeusonia: branching map, Storm Shards, Zeus boss + blessing, merchant guaranteed before the boss — built, partly tested
- Gold, merchant Hermus (cards, shard, Aether, card removal), 1-of-3 rewards — built, partly tested
- Save / Continue, pause menu (save, settings, controls, give up) — built, partly tested

## Out of scope — not building this (for now)
- Elemental reactions between decks (Final Doc's Genshin-style idea) until a second god deck exists
- Terrain-blocking enemies (GDD §5) until Level 2
- Multiplayer, platforms other than PC (GDD §10)

## Build order
1. Playtest and commit the current batch (merchant, Shift upgrades, Guard, save, pause menu)
2. Automated tests: rewards, merchant gold and Aether, map connectivity and the guaranteed merchant, Shift bonus, save round-trip (`/test-setup` first)
3. Shift page, GDD remainder: reorder the Shift sequence (drag), deck-pair bonuses (e.g. Zeus → original deck: extra damage or stun for a few rounds)
4. Divine Focus recharge rules (GDD §9: does not refill each round, only under certain conditions)
5. Level 2: the next god's region, deck and blessing; then terrain-blocking enemies
6. Chronus's bonus chart (GDD §5, "the roguelike portion"): bonuses between attempts, unlocked with currency from enemies
7. Art without printed card text; 8-bit music and sound (placeholder chiptune music and effects generated in code since 2026-11-04: Assets/Scripts/Audio; swap in real tracks)

## Amendments to the GDD (decided since it was written)
- Shop: GDD "open the shop anytime" → merchant Hermus is a map node. Every path converges on one merchant on the floor right before each boss; the rest stop moves to the elite floor below it.
- Regions: GDD "3 enemies & 1 boss, an event after every two" → a 5-floor branching map with an event floor midway (docs/level1-zeusonia.md).
- Shift upgrade "materials" → **Aether**: 1 per fight, 2 per elite, 3 per boss, 30 gold at the merchant; each Shift level adds +2 to the deck's damage, shield and heal Shift effects (costs 2 / 3 / 4). Kept per deck, so a sealed god deck can be upgraded in advance.
- GDD §9 "use an action point to create a shield" → **Guard**: 1 AP for 2 shield until the end of the round (X).
- Growth stays flat, not exponential (pillar 2): no stat multipliers; growth comes from options, AP and Shift effects (Final Doc).

---
**Design pillars (GDD §4):** progressive growth in utility and interactions; tight, non-exponential balance; strategy over RNG; thematic decks with matching playstyles; a fall from the peak and a climb back, with redemption.

**Who it's for / what they feel:** ⚠ Slay the Spire and Inscryption players (the Final Doc's inspirations) who like positional puzzles: an arrogant ruler humbled, then the tension of reading an attack and stepping out of the way just in time.

**Art & audio direction:** 2D pixel art with one atmospheric colour per region (grey steampunk, yellow thunder, red fire); 8-bit music, AI-generated, and public-domain sound effects (GDD §7, §8, §10).

**Reference game:** Slay the Spire: its branching map, 1-of-3 card rewards and merchant. Aeternal adds positioning around the enemy and Divine Shift between god decks.
