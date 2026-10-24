# Aeternal

A roguelike deck-builder built in Unity. Fight your way across a region map with a deck of
cards, add new cards after every victory, and defeat the region's boss.

Combat is positional: the enemy stands in the center of a 3×3 grid and you stand on one of
the four squares around it. Every round the enemy telegraphs which square(s) it will attack —
step out of the way with movement cards, then strike back.

## Gameplay

- **Positioning and intents** — at the start of each round the enemy marks the square it will
  hit (some enemies hit three squares at once). Move with cards such as *Rotate*, *Backstab*,
  *Sword Swing* or *Teleport* to dodge.
- **Action Points (AP)** — spent to play cards; refilled every round.
- **Divine Focus (DF)** — spent to **Shift**: swap your active deck for another (for example the
  Zeus deck). Shifting can trigger shift-in / shift-out effects. DF does not refill on its own.
- **Draw, discard and recover** — played cards go to the discard pile; a few return to your
  deck each round, and cards like *Recycle* bring back more.
- **Status effects** — Bleed (damage at round end), Stun / Blackout (skip the next action),
  Dodge (cancel the next attack), and more.
- **Run structure** — Main Menu → Map → Combat → card reward → Map → … → boss. Health carries
  over between fights; losing ends the run.

The current content is the *Steampunk City* region: Goblin and Bandit encounters, ending with
the boss **Thanatos**.

## Controls

| Input | Action |
|---|---|
| Mouse | Play cards, End Turn, open the discard pile; hover a card to enlarge it |
| `Z` | Shift to the next deck (during your turn) |
| `R` | Restart the current scene |
| `Q` | Return to the main menu (quit from the main menu) |
| `F1` / `F2` | Dev cheats: instantly win / lose a fight (Editor and development builds only) |

**Tutorial:** `Space` / `Enter` / `→` next step, `←` previous step.

## Tutorial

`TutorialScene` (reached from the main menu) teaches the game on top of a live combat:

- A step bar with **Skip Tutorial**, **Back**, a progress counter and **Next**.
- Steps that teach an action (play a card, open and close the discard pile, end the turn)
  spotlight only that target and block everything else until the player does it.
- The action is **held, not performed**: the screen dims and Next is highlighted. Pressing
  Next hides the tutorial, performs the action so its animation plays out, and then shows the
  next step.

Tutorial text lives in the scene; flow, gates and highlighting live in
`Assets/Scripts/TutorialManager.cs`. Per-step options (`gate`, `restrictToTarget`) and the
look (`spotlightColor`, `nextHighlightColor`, `resultDelay`, …) are editable in the Inspector.

## Getting started

1. Install **Unity 6000.2.6f2** (Unity 6) via Unity Hub.
2. Clone the repo and open the project folder in Unity Hub (*Add → Add project from disk*).
   The first import rebuilds `Library/`, which takes a few minutes.
3. Open `Assets/Scenes/MainMenu.unity` and press **Play**.

`PlayGameScene` can also be played on its own: without an active run it falls back to the
enemy and deck serialized in the scene, which is handy for testing combat in isolation.

Scenes in build order: `MainMenu`, `TutorialScene`, `MapScene`, `PlayGameScene`.

## Project layout

```text
Assets/
├── Scenes/                 MainMenu, TutorialScene, MapScene, PlayGameScene
├── Scripts/
│   ├── Core/               GameManager, RunManager (run state across scenes)
│   ├── Combat/             CombatManager (combat flow, Shifting), TurnManager (round loop)
│   ├── Cards/              CardData, EffectPayload, CardResolver (applies card effects)
│   ├── Decks/              DeckData, DeckManager (draw / discard / recover)
│   ├── Enemies/            EnemyController (intents, attacks)
│   ├── Encounters/         EncounterData, MapData
│   ├── Map/                Map nodes and run start
│   ├── Player/             Grid positioning
│   ├── Stats/ Status/ Resources/   Health, shields, status effects, AP / DF
│   ├── UI/                 Hand, HUD, card hover preview, end-of-combat flow
│   ├── FX/                 Combat feedback (hit flashes, floating text, card ghosts)
│   ├── Debug/              Dev cheats
│   └── TutorialManager.cs  Tutorial flow (+ TutorialClickHole.cs)
├── Scriptable Objects/     Cards, decks, enemies, encounters, maps
├── Editor/                 One-shot setup automation (Aeternal menu)
├── Prefabs/                Card, action log entry
├── Images/                 Art
└── Design Documents/       GDD, level design, world building, character write-ups
```

## Content is data

Cards, decks, enemies, encounters and maps are ScriptableObjects under
`Assets/Scriptable Objects/`, so most content changes need no code:

- **Card** (`Aeternal/Card`) — AP cost, description, artwork and a list of effects
  (Damage, Heal, AddShield, ModifyAP, ModifyDF, DrawCards, RecycleCards, Move, ApplyStatus,
  EraseAllEffects).
- **Deck** — a list of cards with counts, plus shift-in / shift-out effects.
- **Encounter** — enemy stats, damage, damage type, AOE, sprite, boss flag.
- **Map** — region name, starting deck, shift decks, encounter order, card reward pool.

## Editor tools

The **Aeternal** menu holds idempotent setup scripts that built parts of the project and can
be re-run safely:

- *Stage 2 → Run Full Setup* — run loop: encounter and map assets, end-of-combat UI, map node,
  New Run button.
- *Stage 3-4 → Run Full Setup* — Zeus deck and Shifting, Bandit enemy, Shift HUD.
- *Polish → Run Full Setup* — live card text overlays, Region Cleared panel.

## Built with

Unity 6 (6000.2.6f2) · Universal Render Pipeline (2D) · TextMesh Pro · C#
