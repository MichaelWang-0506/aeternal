# Aeternal

A roguelike deck-builder built in Unity, set in a world of Greek gods. A mortal ruler blessed by
Chronus, the god of Time, mocks Death itself — and pays for it. Fight with a deck of cards, add
new cards after every victory, and face Thanatos.

Combat is positional: the enemy stands in the center of a 3×3 grid and you stand on one of
the four squares around it. Every round the enemy telegraphs which square(s) it will attack —
step out of the way with movement cards, then strike back.

## Gameplay

- **Positioning and intents** — at the start of each round the enemy marks the square(s) it will
  hit; they glow red, and its intent (direction and damage) is shown under its name in the
  top-right corner. Move with cards such as *Rotate*, *Backstab*, *Sword Swing* or *Teleport*
  to dodge.
- **Action Points (AP)** — the copper gears; spent to play cards, refilled every round.
- **Divine Focus (DF)** — the blue crystals; spent to **Shift**: swap your active deck for
  another (for example the Zeus deck). Shifting can trigger shift-in / shift-out effects. DF
  does not refill on its own.
- **Draw, discard and recover** — played cards go to the discard pile; a few return to your
  deck each round, and cards like *Recycle* bring back more.
- **Status effects** — Bleed (damage at round end, stacks), Stun / Blackout (skip the next
  action), Dodge (cancel the next attack). Shown as badges under each character, with the
  number of stacks or rounds, and on the character itself (blood drips, circling stars).
- **Run structure** — New Game → opening narrative → fight → card reward → next fight → … .
  Health carries over between fights; losing ends the run.

## Level 1: The Fall

The current content follows the prologue in the GDD and the Level Design Document: the
empire's tenth-year feast, and the night the ruler mocked Death.

1. **Opening narrative** — the birth of Time and Death, and the mortal Chronus blessed.
2. **Goblin** — a feast-day challenger.
3. **Bandit Champion** (elite) — tougher, and attacks three squares at once.
4. **Thanatos** — a scripted death. He can't be brought below 1 HP. On round 3 (or at half
   health) he enrages — *Death's Reach*: unstoppable, immune to Stun, ignoring Dodge — and
   strikes every square. The epilogue tells how Chronus bound the ruler's soul in a time loop.
   *To be continued.*

The fights follow one another directly; the map scene is kept but not shown for this region
(`MapData.showMap`).

## Controls

| Input | Action |
|---|---|
| Mouse | Play cards, End Turn, open the discard pile; hover a card to enlarge it |
| `Z` | Shift to the next deck (during your turn; also the Shift button in the bottom-left panel) |
| Click / `Space` / `Enter` | Advance dialogue and narration |
| `Esc` | Close Settings / Credits on the main menu |
| `R` | Restart the current scene |
| `Q` | Return to the main menu (quit from the main menu) |
| `F1` / `F2` | Dev cheats: instantly win / lose a fight (Editor and development builds only) |

**Tutorial:** `Space` / `Enter` / `→` next step, `←` previous step.

## Main menu

**New Game**, **Continue** (disabled until there is a save system), **Settings** (volume,
fullscreen, whether New Game plays the tutorial), **Credits** and **Quit**. Settings are saved
on the machine (PlayerPrefs).

## Tutorial

`TutorialScene` plays automatically the **first time** the player starts a New Game; finishing
or skipping it goes straight into the run. Turn *Tutorial on New Game* back on in Settings to
see it again. It teaches the game on top of a live combat:

- A step bar with **Skip Tutorial**, **Back**, a progress counter and **Next**.
- Steps that teach an action (play a card, open and close the discard pile, end the turn)
  spotlight only that target and block everything else until the player does it.
- The action is **held, not performed**: the screen dims and Next is highlighted. Pressing
  Next hides the tutorial, performs the action so its animation plays out, and then shows the
  next step.

Tutorial text lives in the scene; flow, gates and highlighting live in
`Assets/Scripts/TutorialManager.cs`. Per-step options (`gate`, `restrictToTarget`) and the
look (`spotlightColor`, `nextHighlightColor`, `resultDelay`, …) are editable in the Inspector.

## Battlefield presentation

Combat scenes are restyled at runtime by `BattlefieldStyler`, so the scenes themselves stay
simple and every look is tunable in one asset, **`Assets/Resources/BattlefieldStyle.asset`**:

- A darkened, vignetted backdrop; raised tiles with a pulsing red glow on attacked squares.
- Characters with a ground shadow and backlight, and a HUD under each one: name, health bar
  with shield and a damage trail, status badges.
- AP and DF as rows of icons; the enemy's intent under its name.
- **Procedural animation** (`CharacterAnimator`, `StatusVisuals`) — idle breathing, hit,
  attack, hop, brace, death; bleed drips and stun stars. These stand in until the art has real
  animation frames; see `docs/art-requirements.md` for the art list and generation prompts.
- Placeholder icons (gear, crystal, blood drop, star) are generated in code
  (`ProceduralSprites`); assign real sprites in `BattlefieldStyle` to replace them.

Story beats (opening narration, intro dialogue, scripted death, epilogue) are run by
`EncounterStoryDirector` from the encounter and map data, and drawn by `StoryOverlay`.

## Getting started

1. Install **Unity 6000.2.6f2** (Unity 6) via Unity Hub.
2. Clone the repo and open the project folder in Unity Hub (*Add → Add project from disk*).
   The first import rebuilds `Library/`, which takes a few minutes.
3. Open `Assets/Scenes/MainMenu.unity`, press **Play**, then **New Game**.

`PlayGameScene` can also be played on its own: without an active run it falls back to the
enemy and deck serialized in the scene, which is handy for testing combat in isolation.

Scenes in build order: `MainMenu`, `TutorialScene`, `MapScene`, `PlayGameScene`.

## Project layout

```text
Assets/
├── Scenes/                 MainMenu, TutorialScene, MapScene, PlayGameScene
├── Scripts/
│   ├── Core/               GameManager, RunManager (run state across scenes)
│   ├── Combat/             CombatManager (combat flow, Shifting), TurnManager (round loop),
│   │                       EncounterStoryDirector (narration, dialogue, scripted death)
│   ├── Cards/              CardData, EffectPayload, CardResolver (applies card effects)
│   ├── Decks/              DeckData, DeckManager (draw / discard / recover)
│   ├── Enemies/            EnemyController (intents, attacks)
│   ├── Encounters/         EncounterData, MapData
│   ├── Map/                Map nodes, RunStarter (New Game, first-time tutorial)
│   ├── Player/             Grid positioning
│   ├── Stats/ Status/ Resources/   Health, shields, status effects, AP / DF
│   ├── UI/                 Main menu, hand, character HUD, resource icons, story overlay,
│   │                       card hover preview, end-of-combat flow
│   ├── FX/                 BattlefieldStyler + BattlefieldStyle, CharacterAnimator,
│   │                       StatusVisuals, ProceduralSprites, SceneFader, CombatFXManager
│   ├── Debug/              Dev cheats
│   └── TutorialManager.cs  Tutorial flow (+ TutorialClickHole.cs)
├── Resources/              BattlefieldStyle.asset (look of combat)
├── Scriptable Objects/     Cards, decks, enemies, encounters, maps
├── Editor/                 One-shot setup automation (Aeternal menu)
├── Prefabs/                Card, action log entry
├── Images/                 Art
└── Design Documents/       GDD, level design, world building, character write-ups
docs/
└── art-requirements.md     Art needed for real animation, with AI prompts
```

## Content is data

Cards, decks, enemies, encounters and maps are ScriptableObjects under
`Assets/Scriptable Objects/`, so most content changes need no code:

- **Card** (`Aeternal/Card`) — AP cost, description, artwork and a list of effects
  (Damage, Heal, AddShield, ModifyAP, ModifyDF, DrawCards, RecycleCards, Move, ApplyStatus,
  EraseAllEffects).
- **Deck** — a list of cards with counts, plus shift-in / shift-out effects.
- **Encounter** — enemy stats, damage, damage type, AOE, sprite, boss / elite flags, intro
  dialogue, and for a story fight the scripted death: trigger round and health fraction,
  rage banner and badge, dialogue, epilogue and ending title.
- **Map** — region name, starting deck, shift decks, encounter order, card reward pool,
  whether to show the map between fights, and the opening narrative.

## Editor tools

The **Aeternal** menu holds idempotent setup scripts that built parts of the project and can
be re-run safely:

- *Stage 2 → Run Full Setup* — run loop: encounter and map assets, end-of-combat UI, map node,
  New Run button.
- *Stage 3-4 → Run Full Setup* — Zeus deck and Shifting, Bandit enemy, Shift HUD.
- *Polish → Run Full Setup* — live card text overlays, Region Cleared panel.

Re-running *Polish* resets the size, position and colour of the card prefab's text strips,
undoing the later layout fix (opaque cost and description strips placed over the printed text)
— re-apply it in `Card.prefab` afterwards.

## Built with

Unity 6 (6000.2.6f2) · Universal Render Pipeline (2D) · TextMesh Pro · C#
