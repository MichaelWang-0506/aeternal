#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Aeternal.Cards;
using Aeternal.Core;
using Aeternal.Encounters;
using Aeternal.Combat;
using Aeternal.FX;
using Aeternal.Map;
using Aeternal.Stats;
using Aeternal.UI;

namespace Aeternal.DevTools
{
    /// <summary>
    /// Automated smoke playthrough with screenshots. Does nothing unless the player is launched
    /// with "--playtest &lt;absolute output folder&gt;". It clicks through a new run in Zeusonia (main
    /// menu, map, Shift page, fights, reward, event, merchant, pause menu, save and Continue),
    /// saves a numbered screenshot at each screen, and writes report.md with the checks it ran
    /// and every error the game logged. Compiled only in the Editor and development builds.
    /// </summary>
    public class PlaytestDriver : MonoBehaviour
    {
        const float TimeLimit = 900f;   // seconds of real time before the run is abandoned
        const int FirstFightTurns = 8;

        string outDir;
        int shotIndex;
        bool autoAdvance;
        float nextAdvance;
        readonly StringBuilder log = new StringBuilder();
        readonly List<string> checks = new List<string>();
        readonly List<string> errors = new List<string>();
        int failures;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var dir = Arg("--playtest");
            if (string.IsNullOrEmpty(dir)) return;
            Application.runInBackground = true; // keep playing when the window isn't focused
            var go = new GameObject(nameof(PlaytestDriver));
            DontDestroyOnLoad(go);
            go.AddComponent<PlaytestDriver>().outDir = dir;
        }

        static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }

        void Awake() => Application.logMessageReceived += OnLog;
        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string first = stackTrace?.Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "";
            errors.Add($"[{SceneManager.GetActiveScene().name}] {type}: {message} ({first.Trim()})");
        }

        void Update()
        {
            // Story text waits for a click; press "continue" every 1.5 s while allowed.
            if (autoAdvance && Time.unscaledTime >= nextAdvance)
            {
                StoryOverlay.PlaytestAdvance = true;
                nextAdvance = Time.unscaledTime + 1.5f;
            }
            if (!finished && Time.realtimeSinceStartup > TimeLimit)
            {
                Fail($"time limit of {TimeLimit:0} s reached; run abandoned");
                Finish();
            }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            Note($"Playtest started {DateTime.Now:yyyy-MM-dd HH:mm}, {Screen.width}x{Screen.height}");
            yield return Play();
            Finish();
        }

        // ------------------------------------------------------------------
        // The run
        // ------------------------------------------------------------------

        IEnumerator Play()
        {
            yield return Until(() => FindFirstObjectByType<MainMenuController>() != null, 15f, "main menu loads");
            yield return Wait(1.5f);
            yield return Shot("main-menu");
            Check(!RunStarter.CanContinue, "fresh profile: Continue has no save to load");

            Check(Click("New Game"), "click New Game");
            yield return Wait(0.6f);
            yield return Shot("new-game-choices");
            yield return Tutorial();
            Check(Click("Skip Prologue"), "click Skip Prologue");

            autoAdvance = true;
            yield return Until(() => MapView() != null, 20f, "region map loads");
            yield return Wait(2.5f);
            yield return Shot("opening-narration");
            yield return Until(() => MapView() != null && !MapView().Busy, 60f, "opening narration finishes");
            var run = RunManager.Instance;
            Check(run != null && run.HasActiveRun, "a run is active");
            if (run == null) yield break;
            Note($"Region {run.CurrentMap.regionName}: HP {run.CarriedHealth}, gold {run.Gold}, Aether {run.Aether}, deck {run.RunDeck.Count}");
            yield return Wait(0.5f);
            yield return Shot("map-start");

            Call(MapView(), "ShowShift");
            yield return Wait(0.8f);
            Check(FindFirstObjectByType<ShiftPanel>() != null, "Space opens the Shift page");
            yield return Shot("shift-page");
            Click("Close");
            yield return Wait(0.5f);

            yield return ClimbRegion(true);
            yield return Shot("map-before-boss");

            yield return SaveAndContinue();

            // Level 2: beat Zeus, take his blessing, on to Atlantis.
            yield return BossFight("Zeus");
            yield return Until(() => MapView() != null && RunManager.Instance != null && RunManager.Instance.HasActiveRun
                                     && RunManager.Instance.CurrentMap.regionName == "Atlantis", 90f, "after Zeus's blessing, Atlantis loads");
            run = RunManager.Instance;
            if (run == null || !run.HasActiveRun || run.CurrentMap.regionName != "Atlantis") yield break;
            yield return Wait(2.5f);
            yield return Shot("atlantis-narration");
            yield return Until(() => MapView() != null && !MapView().Busy, 60f, "Atlantis opening narration finishes");
            Check(run.UnlockedDecks.Any(d => d.deckId == "Zeus"), "the Zeus deck is awakened in Atlantis");
            Check(run.RunDeck.Count > 0, $"the deck carries over into Atlantis ({run.RunDeck.Count} cards)");
            Note($"Region {run.CurrentMap.regionName}: {State(run)}, Zeus deck {run.CardsIn(run.UnlockedDecks.First()).Count} cards");
            yield return Wait(0.5f);
            yield return Shot("atlantis-map");

            Call(MapView(), "ShowDeck");
            yield return Wait(0.8f);
            var deckView = FindFirstObjectByType<CardGridPanel>();
            var titles = deckView != null ? deckView.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToList() : new List<string>();
            Check(titles.Any(t => t.StartsWith("Zeus deck")) && titles.Any(t => t.StartsWith("Steampunk deck")),
                  "View Deck shows the starting deck and the awakened Zeus deck");
            yield return Shot("atlantis-view-deck");
            Click("Close");
            yield return Wait(0.5f);

            yield return ClimbRegion(false);
            yield return Shot("atlantis-before-boss");
            yield return BossFight("Poseidon");
            yield return Until(() => FindFirstObjectByType<MainMenuController>() != null, 90f,
                               "after Poseidon's blessing, the run ends on the main menu (to be continued)");
        }

        /// <summary>
        /// Climbs the region's map to the boss floor, preferring the merchant, then elites. In
        /// Level 1 the first fight runs the long checks; in Atlantis the first fight tests
        /// Poseidon's cards and the elite or boss tests the coral walls.
        /// </summary>
        IEnumerator ClimbRegion(bool level1)
        {
            bool firstFight = true, sawShop = false, sawEvent = false;
            for (int step = 0; step < 8; step++)
            {
                var run = RunManager.Instance;
                var view = MapView();
                if (view == null) { Fail("map screen missing between nodes"); yield break; }
                var options = run.AvailableNodes();
                // In Atlantis the event comes first: it checks offering a card from the Zeus deck.
                var node = Prefer(options, eventFirst: !level1);
                if (node == null || node.type == MapNodeType.Boss)
                {
                    Note("Reached the boss floor.");
                    break;
                }
                Note($"Floor {node.floor + 1}: options {string.Join(", ", options.Select(o => o.type))}; taking {node.type}");
                Call(view, "Choose", node);

                if (node.IsFight)
                {
                    yield return Fight(level1 && firstFight, node.type, !level1 && firstFight);
                    if (failures > 0 && RunManager.Instance == null) yield break;
                    firstFight = false;
                }
                else if (node.type == MapNodeType.Shop)
                {
                    yield return Shop();
                    sawShop = true;
                }
                else if (node.type == MapNodeType.Event)
                {
                    yield return RegionEvent();
                    sawEvent = true;
                }
                else
                {
                    yield return Wait(1.5f);
                    yield return Shot(node.type.ToString().ToLowerInvariant());
                }
                yield return Until(() => MapView() != null && !MapView().Busy, 30f, $"back on the map after {node.type}");
                yield return Wait(0.5f);
            }
            Check(sawShop, "the merchant was reached before the boss");
            if (!sawEvent) Note("No event node was taken on this map.");
        }

        /// <summary>
        /// The region's boss: the fight, a charged attack and coral walls if it has them, then
        /// the kill and the blessing narration (auto-advanced).
        /// </summary>
        IEnumerator BossFight(string boss)
        {
            var run = RunManager.Instance;
            var node = run != null ? run.AvailableNodes().FirstOrDefault(n => n.type == MapNodeType.Boss) : null;
            Check(node != null, $"the {boss} node is reachable");
            if (node == null) yield break;
            Call(MapView(), "Choose", node);
            CombatManager combat = null;
            yield return Until(() => (combat = FindFirstObjectByType<CombatManager>()) != null
                                     && combat.deckManager != null && combat.deckManager.hand.Count > 0, 30f, $"{boss} fight starts");
            if (combat == null) yield break;
            yield return Wait(9f); // intro dialogue, auto-advanced
            Check(combat.enemy != null && combat.enemy.name == boss, $"the boss is {boss} (got {combat.enemy?.name})");
            yield return Shot($"fight-{boss.ToLowerInvariant()}");

            var encounter = run.CurrentEncounter;
            if (encounter != null && encounter.chargedAttackEvery > 0)
                yield return PlayUntilCharged(combat, encounter);

            combat.enemy.GetComponent<HealthComponent>().Kill();
            yield return Wait(3f);
            yield return Shot($"blessing-{boss.ToLowerInvariant()}");
        }

        /// <summary>
        /// Ends turns until the boss telegraphs its charged attack (every square but one): checks
        /// that the safe square is open ground and that coral, if any, never covers the player.
        /// </summary>
        IEnumerator PlayUntilCharged(CombatManager combat, EncounterData encounter)
        {
            var pos = combat.playerHealth.GetComponent<Aeternal.Player.PlayerPositioningComponent>();
            bool sawCharge = false, sawWall = false;
            for (int turn = 0; turn < encounter.chargedAttackEvery + 1 && !combat.IsGameOver; turn++)
            {
                // The charged blow is the one hitting harder than the boss's usual attacks.
                if (combat.enemy.baseDamage > encounter.baseDamage)
                {
                    sawCharge = true;
                    int safe = Enumerable.Range(0, 4).First(d => !combat.enemy.GetAttackDirections().Contains(d));
                    Check(!combat.Terrain.IsBlocked(safe) && safe != pos.facingIndex,
                          $"{encounter.chargedAttackName}: the safe square is open and not the one you stand on");
                    yield return Wait(2.4f); // let the banner show
                    yield return Shot($"{encounter.chargedAttackName.ToLowerInvariant().Replace(' ', '-')}");
                    break;
                }
                if (combat.Terrain.Count > 0 && !sawWall)
                {
                    sawWall = true;
                    Check(!combat.Terrain.IsBlocked(pos.facingIndex), "a coral wall never stands on the player's square");
                }
                combat.playerHealth.Heal(99);
                yield return EndTurn(combat);
            }
            Check(sawCharge, $"{encounter.displayName} telegraphs {encounter.chargedAttackName} by round {encounter.chargedAttackEvery}");
            if (encounter.terrainEvery > 0) Check(sawWall || combat.Terrain.Count > 0, $"{encounter.displayName} raises coral walls");
        }

        IEnumerator EndTurn(CombatManager combat)
        {
            var endTurn = FindFirstObjectByType<EndTurnButton>();
            var button = endTurn != null ? endTurn.GetComponent<Button>() : null;
            if (button == null) { Fail("End Turn button not found"); yield break; }
            button.onClick.Invoke();
            yield return Wait(3.5f);
        }

        /// <summary>
        /// Poseidon's cards on the battlefield, resolved straight through the card resolver: terrain
        /// behind the enemy, a stun that needs it, a blocked leap, two terrains collapsing into a
        /// water cage, the cage doubled and broken. The deck is still sealed in Atlantis, so this is
        /// the only place they are played.
        /// </summary>
        IEnumerator PoseidonCards(CombatManager combat)
        {
            var run = RunManager.Instance;
            var cards = run.CurrentMap.blessingDeck.defaultCards.Select(e => e.card)
                .Concat(run.CurrentMap.stormShardPool).Where(c => c != null).Distinct().ToList();
            CardData Get(string name) => cards.FirstOrDefault(c => c.name == name);
            var enemy = combat.enemy;
            var enemyHealth = enemy.GetComponent<HealthComponent>();
            var enemyStatus = enemy.GetComponent<Aeternal.Status.StatusManager>();
            var pos = combat.playerHealth.GetComponent<Aeternal.Player.PlayerPositioningComponent>();
            var terrain = combat.Terrain;
            enemyHealth.SetCurrentHealth(enemyHealth.GetMaxHealth());

            void Play(string name)
            {
                var card = Get(name);
                if (card == null) { Fail($"Poseidon card {name} not found"); return; }
                var ctx = new CardContext { caster = combat.playerHealth.gameObject, enemy = enemy.gameObject, startSquare = pos.facingIndex };
                foreach (var effect in card.effects) combat.cardResolver.ResolveEffect(effect, ctx, combat.deckManager);
            }

            int behind = (pos.facingIndex + 2) % 4;
            int hp = enemyHealth.getHealth;
            Play("Hidden Wave");
            Check(terrain.IsBlocked(behind) && enemyHealth.getHealth == hp - 5, "Hidden Wave: 5 damage and terrain behind the enemy");
            yield return Wait(0.6f);
            yield return Shot("poseidon-terrain");

            Play("Wave Push");
            Check(enemyStatus.HasStatusBehavior(StatusEffectBehavior.Blackout), "Wave Push stuns the enemy when terrain stands behind it");
            enemyStatus.RemoveAllStatuses();

            int at = pos.facingIndex;
            Play("Trident Strike");
            Check(pos.facingIndex == at, "terrain on the far side blocks Trident Strike's leap");

            Play("Ocean Block");
            Check(pos.facingIndex == (at + 1) % 4, "Ocean Block moves you clockwise");
            Check(terrain.Count == 0 && enemyStatus.HasStatusBehavior(StatusEffectBehavior.Caged),
                  "two terrains collapse into a water cage around the enemy");

            Play("Suffocation");
            var cage = enemyStatus.GetStatusByBehavior(StatusEffectBehavior.Caged);
            Check(cage != null && cage.remainingRounds == 2, $"Suffocation doubles the cage (rounds: {cage?.remainingRounds})");
            yield return Wait(0.6f);
            yield return Shot("water-cage");

            enemyHealth.SetCurrentHealth(enemyHealth.GetMaxHealth());
            hp = enemyHealth.getHealth;
            Play("Breakout");
            Check(!enemyStatus.HasStatusBehavior(StatusEffectBehavior.Caged) && enemyHealth.getHealth == hp - 12,
                  $"Breakout ends the cage for 6 damage per round left ({hp - enemyHealth.getHealth})");

            // A cage left to run: 5 damage at the first round end.
            enemyHealth.SetCurrentHealth(enemyHealth.GetMaxHealth());
            Play("Water Cage");
            hp = enemyHealth.getHealth;
            combat.playerHealth.Heal(99);
            yield return EndTurn(combat);
            Check(enemyHealth.getHealth <= hp - 5, $"the water cage deals 5 damage at round end ({hp - enemyHealth.getHealth})");
            enemyStatus.RemoveAllStatuses();
            terrain.ClearAll();
        }

        static RegionMapView MapView() => FindFirstObjectByType<RegionMapView>();

        /// <summary>
        /// Opens the tutorial's first step and checks that Guard, which the tutorial doesn't
        /// teach, is greyed out and locked; then goes back to the New Game choices.
        /// </summary>
        IEnumerator Tutorial()
        {
            Check(Click("Play Tutorial"), "click Play Tutorial");
            yield return Until(() => FindFirstObjectByType<TutorialManager>() != null
                && FindFirstObjectByType<GuardButton>() != null, 20f, "tutorial loads");
            yield return Wait(2f);
            yield return Shot("tutorial-first-step");
            var guard = FindFirstObjectByType<GuardButton>();
            var combat = FindFirstObjectByType<CombatManager>();
            Check(guard != null && !guard.GetComponent<Button>().interactable, "tutorial: the Guard button is greyed out");
            Check(combat != null && !combat.CanGuard && !combat.Guard(), "tutorial: Guard can't be used (button or X)");

            SceneManager.LoadScene("MainMenu");
            yield return Until(() => FindFirstObjectByType<MainMenuController>() != null, 15f, "back to the main menu");
            yield return Wait(1f);
            Check(Click("New Game"), "click New Game again");
            yield return Wait(0.6f);
        }

        static RunMapNode Prefer(List<RunMapNode> options, bool eventFirst = false)
        {
            // Elite before Event: the elite fight is the one with its own look to check.
            MapNodeType[] order = eventFirst
                ? new[] { MapNodeType.Shop, MapNodeType.Event, MapNodeType.Elite, MapNodeType.Enemy,
                          MapNodeType.Treasure, MapNodeType.Rest, MapNodeType.Boss }
                : new[] { MapNodeType.Shop, MapNodeType.Elite, MapNodeType.Event, MapNodeType.Enemy,
                          MapNodeType.Treasure, MapNodeType.Rest, MapNodeType.Boss };
            foreach (var type in order)
            {
                var hit = options.FirstOrDefault(n => n != null && n.type == type);
                if (hit != null) return hit;
            }
            return null;
        }

        IEnumerator Fight(bool first, MapNodeType type, bool testPoseidon = false)
        {
            CombatManager combat = null;
            yield return Until(() => (combat = FindFirstObjectByType<CombatManager>()) != null
                                     && combat.deckManager != null && combat.deckManager.hand.Count > 0, 30f,
                               $"{type} fight starts with a hand");
            if (combat == null) yield break;
            yield return Wait(4f); // intro dialogue, auto-advanced
            var dm = combat.deckManager;
            Note($"Fight vs {combat.enemy?.name}: hand {dm.hand.Count}, draw {dm.drawPile.Count}, discard {dm.discardPile.Count}");
            yield return Shot($"fight-{type.ToString().ToLowerInvariant()}");

            if (first)
            {
                Call(FindFirstObjectByType<DeckDisplay>(), "ShowContents");
                yield return Wait(0.8f);
                var grid = FindFirstObjectByType<CardGridPanel>();
                Check(grid != null, "clicking the draw pile opens its contents");
                yield return Shot("draw-pile-contents");
                Click("Close");
                yield return Wait(0.5f);

                var pause = FindFirstObjectByType<PauseMenu>();
                Check(pause != null, "pause menu exists in the fight");
                if (pause != null)
                {
                    Call(pause, "SetPaused", true);
                    yield return Wait(0.6f);
                    Check(PauseMenu.IsPaused && Time.timeScale == 0f, "Esc pauses the game");
                    yield return Shot("pause-in-fight");
                    Click("Controls");
                    yield return Wait(0.4f);
                    yield return Shot("pause-controls");
                    Click("Back");
                    Click("Resume");
                    yield return Wait(0.4f);
                    Check(!PauseMenu.IsPaused && Time.timeScale == 1f, "Resume unpauses");
                }

                int apBefore = combat.playerResources.currentAP;
                bool guarded = combat.Guard();
                Check(guarded && combat.playerResources.currentAP == apBefore - 1, "Guard spends 1 AP");
                yield return Wait(0.8f);
                yield return Shot("after-guard");
                yield return HitEffects(combat);

                yield return PlayUntilReshuffle(combat);
            }

            if (testPoseidon && combat != null && !combat.IsGameOver)
            {
                Check(combat.CanShift, "Shift is available in Atlantis (the Zeus deck)");
                var resources = combat.playerResources;
                int df = resources.currentDF;
                combat.ShiftNext();
                yield return Wait(1f);
                Check(combat.ActiveDeckName.Contains("Zeus") || resources.currentDF < df, $"Shifting to the Zeus deck works (active: {combat.ActiveDeckName})");
                yield return Shot("atlantis-shifted");
                yield return PoseidonCards(combat);
            }

            var current = RunManager.Instance != null ? RunManager.Instance.CurrentEncounter : null;
            if (current != null && current.terrainEvery > 0 && combat != null && !combat.IsGameOver)
                yield return CoralWalls(combat, current);

            if (combat == null || combat.IsGameOver)
            {
                if (combat != null && combat.PlayerRemainingHealth <= 0) Fail("the player died in the fight");
            }
            else
            {
                combat.enemy.GetComponent<HealthComponent>().Kill();
            }

            RewardScreen reward = null;
            yield return Until(() => (reward = FindFirstObjectByType<RewardScreen>()) != null, 15f, "reward screen appears");
            if (reward == null) yield break;
            yield return Wait(2.5f); // gold, then the cards are dealt
            var run = RunManager.Instance;
            int cardsBefore = OwnedCards(run);
            bool deckChoice = run.CurrentMap.deckChoiceRewards;
            int picks = 0;
            // Each step of the reward: a deck to grow, a card for it, a sealed card. Take the first offer.
            for (int stage = 0; stage < 4 && reward != null; stage++)
            {
                if (first || type == MapNodeType.Elite || stage == 0 && !deckChoice)
                    yield return Shot($"reward-{type.ToString().ToLowerInvariant()}{(deckChoice ? "-" + (stage + 1) : "")}");
                var button = reward.GetComponentsInChildren<Button>()
                    .FirstOrDefault(b => b.interactable && Label(b) != "Skip" && Label(b) != "Back");
                if (stage == 0) Check(button != null, "the reward offers a card to pick");
                if (button == null) { Click("Skip"); break; }
                bool isDeck = button.name.StartsWith("<b>"); // deck buttons are named by their label
                Note($"Reward step {stage + 1}: {Label(button).Replace('\n', ' ')}");
                button.onClick.Invoke();
                if (!isDeck) picks++;
                yield return Wait(1.4f);
                reward = FindFirstObjectByType<RewardScreen>();
            }
            yield return Until(() => MapView() != null, 20f, "map returns after the reward");
            run = RunManager.Instance;
            if (picks > 0)
                Check(OwnedCards(run) == cardsBefore + picks,
                      $"each picked reward is added to its deck ({picks} picked: {cardsBefore} -> {OwnedCards(run)} cards)");
            Note($"After the fight: gold {run.Gold}, Aether {run.Aether}, deck {run.RunDeck.Count}, shards {run.StormShards.Count}, HP {run.CarriedHealth}");
        }

        /// <summary>
        /// A lightning strike on the enemy, captured mid-flash (the first fight's deck has no
        /// Thunder cards yet), and the hit-stop it triggers handing the game speed back.
        /// </summary>
        IEnumerator HitEffects(CombatManager combat)
        {
            var fx = CombatFXManager.Instance != null ? CombatFXManager.Instance.GetComponent<ImpactFX>() : null;
            var enemySprite = combat.enemy != null ? combat.enemy.GetComponentInChildren<SpriteRenderer>() : null;
            Check(fx != null && enemySprite != null, "hit effects are ready in the fight");
            if (fx == null || enemySprite == null) yield break;
            fx.Hit(enemySprite, combat.playerHealth.transform.position, Aeternal.Cards.DamageType.Lightning, 6);
            yield return Wait(0.03f);
            yield return Shot("hit-lightning");
            yield return Wait(0.6f);
            Check(Time.timeScale == 1f, "the big-hit freeze hands the game speed back");
        }

        /// <summary>
        /// Plays every card it can each turn and ends the turn, until the draw pile runs out and
        /// the discard pile is shuffled back in. Heals the player each turn so the test survives.
        /// </summary>
        IEnumerator PlayUntilReshuffle(CombatManager combat)
        {
            var dm = combat.deckManager;
            int reshuffled = -1;
            Action<int> onReshuffle = n => reshuffled = n;
            dm.OnDiscardReshuffled += onReshuffle;
            bool emptyWithDiscard = false;
            bool tookShot = false, tookHitShot = false, tookDrawShot = false;
            bool drew = false;
            Action<Aeternal.Cards.CardData> onDrawn = _ => drew = true;
            dm.OnCardAddedToHand += onDrawn;

            for (int turn = 1; turn <= FirstFightTurns && !combat.IsGameOver; turn++)
            {
                foreach (var ui in HandCards())
                {
                    if (combat.IsGameOver) break;
                    if (ui == null || ui.card == null) continue;
                    if (ui.card.costAP > combat.playerResources.currentAP) continue;
                    bool attacks = ui.card.effects.Any(e => e.effectType == Aeternal.Cards.EffectType.Damage);
                    ui.OnClickPlayCard();
                    if (attacks && !tookHitShot)
                    {
                        tookHitShot = true;
                        yield return Wait(0.06f);
                        yield return Shot("hit-physical");
                    }
                    yield return Wait(0.9f);
                    if (dm.drawPile.Count == 0 && dm.discardPile.Count > 0) emptyWithDiscard = true;
                    if (reshuffled > 0 && !tookShot)
                    {
                        tookShot = true;
                        yield return Shot("after-reshuffle");
                    }
                }
                Note($"Turn {turn}: draw {dm.drawPile.Count}, discard {dm.discardPile.Count}, hand {dm.hand.Count}, enemy HP {combat.enemy?.GetComponent<HealthComponent>()?.getHealth}");
                if (reshuffled > 0) break;
                if (combat.IsGameOver) break;

                combat.playerHealth.Heal(99);
                combat.enemy?.GetComponent<HealthComponent>()?.Heal(99); // keep the fight going until the pile runs out
                var endTurn = FindFirstObjectByType<EndTurnButton>();
                var button = endTurn != null ? endTurn.GetComponent<Button>() : null;
                if (button == null) { Fail("End Turn button not found"); break; }
                drew = false;
                int kept = dm.hand.Count;
                button.onClick.Invoke();
                if (!tookDrawShot)
                {
                    // Catch the new round's cards in flight from the draw pile.
                    yield return Until(() => drew, 6f, "a new round deals cards into the hand");
                    tookDrawShot = true;
                    yield return Wait(0.15f);
                    // Being dealt: still hidden on the pile, or tilted on the way in. Kept cards are neither.
                    int dealing = HandCards().Count(c => c.transform.localScale.x <= 0f
                                                          || Mathf.Abs(Mathf.DeltaAngle(0f, c.transform.localEulerAngles.z)) > 0.5f);
                    int drawn = dm.hand.Count - kept;
                    Check(dealing == drawn, $"only the newly drawn cards are dealt from the pile ({dealing} animating, {drawn} drawn, {kept} kept)");
                    yield return Shot("cards-drawing");
                }
                yield return Wait(4f);
                if (dm.drawPile.Count == 0 && dm.discardPile.Count > 0) emptyWithDiscard = true;
                if (reshuffled > 0 && !tookShot)
                {
                    tookShot = true;
                    yield return Shot("after-reshuffle");
                }
            }
            dm.OnDiscardReshuffled -= onReshuffle;
            dm.OnCardAddedToHand -= onDrawn;

            if (reshuffled > 0)
                Pass($"the discard pile was shuffled back in when the draw pile ran out ({reshuffled} cards)");
            else if (combat.IsGameOver)
                Note("The first fight ended before the draw pile ran out; the reshuffle was not seen.");
            else
                Fail($"the draw pile never ran out in {FirstFightTurns} turns; reshuffle not seen");
            Check(!emptyWithDiscard, "the draw pile is never empty while the discard pile has cards");
        }

        /// <summary>Cards the player owns: every owned deck plus the sealed ones.</summary>
        static int OwnedCards(RunManager run) =>
            run.OwnedDecks().Sum(d => run.CardsIn(d).Count) + run.StormShards.Count;

        /// <summary>
        /// A coral-raising enemy: ends turns until a wall stands, then checks it is never under the
        /// player and that it blocks a move onto it.
        /// </summary>
        IEnumerator CoralWalls(CombatManager combat, EncounterData encounter)
        {
            var pos = combat.playerHealth.GetComponent<Aeternal.Player.PlayerPositioningComponent>();
            var terrain = combat.Terrain;
            for (int turn = 0; turn < encounter.terrainEvery + 1 && terrain.Count == 0 && !combat.IsGameOver; turn++)
            {
                combat.playerHealth.Heal(99);
                combat.enemy.GetComponent<HealthComponent>().Heal(99);
                yield return EndTurn(combat);
            }
            Check(terrain.Count > 0, $"{encounter.displayName} raises a coral wall by round {encounter.terrainEvery}");
            if (terrain.Count == 0) yield break;
            yield return Wait(1.6f);
            yield return Shot("coral-wall");
            Check(!terrain.IsBlocked(pos.facingIndex), "the coral wall is not on the player's square");
            int wall = Enumerable.Range(0, 4).First(terrain.IsBlocked);
            int steps = wall == (pos.facingIndex + 1) % 4 ? 1 : wall == (pos.facingIndex + 3) % 4 ? -1 : 2;
            int from = pos.facingIndex;
            combat.cardResolver.ResolveEffect(new EffectPayload { effectType = EffectType.Move, magnitude = steps, target = EffectTarget.Self },
                new CardContext { caster = combat.playerHealth.gameObject, enemy = combat.enemy.gameObject }, combat.deckManager);
            Check(pos.facingIndex == from, "you can't move onto a coral wall");
        }

        static List<CardUI> HandCards() =>
            FindObjectsByType<CardUI>(FindObjectsSortMode.None)
                .Where(c => !c.displayOnly && c.gameObject.activeInHierarchy && c.resolver != null)
                .Where(c => { var hand = c.GetComponentInParent<CardHandDisplay>(); return hand != null && !hand.isDiscardPileDisplay; })
                .ToList();

        IEnumerator Shop()
        {
            ShopPanel shop = null;
            yield return Until(() => (shop = FindFirstObjectByType<ShopPanel>()) != null, 10f, "merchant opens");
            if (shop == null) yield break;
            yield return Wait(0.8f);
            yield return Shot("merchant");
            var run = RunManager.Instance;
            int gold = run.Gold, aether = run.Aether, price = run.CurrentMap.aetherPrice;
            Call(shop, "BuyAether");
            yield return Wait(0.5f);
            if (gold >= price)
                Check(run.Gold == gold - price && run.Aether == aether + 1, $"buying Aether: -{price} gold, +1 Aether");
            else
                Check(run.Gold == gold && run.Aether == aether, "buying Aether without enough gold does nothing");
            yield return Shot("merchant-after-buy");

            if (run.CurrentMap.deckChoiceRewards)
            {
                int replacePrice = run.CurrentMap.replacePrice, owned = OwnedCards(run);
                if (run.Gold < replacePrice) Note($"Replace not tried: {run.Gold} gold, needs {replacePrice}");
                else
                {
                    gold = run.Gold;
                    Call(shop, "OpenReplace");
                    yield return Wait(0.8f);
                    var grid = FindFirstObjectByType<CardGridPanel>();
                    var pick = grid != null ? grid.GetComponentsInChildren<Button>()
                        .FirstOrDefault(b => b.interactable && !Label(b).Contains("no swap") && Label(b) != "Cancel") : null;
                    Check(pick != null, "Replace lists the cards of your decks");
                    if (pick != null)
                    {
                        Note($"Replacing: {Label(pick).Replace('\n', ' ')}");
                        pick.onClick.Invoke();
                        yield return Wait(0.8f);
                        yield return Shot("merchant-replace-offers");
                        grid = FindFirstObjectByType<CardGridPanel>();
                        var offer = grid != null ? grid.GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable && Label(b) != "Cancel") : null;
                        Check(offer != null, "Replace offers same-rarity cards");
                        if (offer != null) offer.onClick.Invoke();
                        yield return Wait(0.6f);
                        Check(run.Gold == gold - replacePrice && OwnedCards(run) == owned,
                              $"Replace swaps one card for {replacePrice} gold (gold {gold} -> {run.Gold}, cards {owned} -> {OwnedCards(run)})");
                    }
                }
            }
            Check(Click("Leave"), "leave the merchant");
        }

        IEnumerator RegionEvent()
        {
            RegionEventPanel panel = null;
            yield return Until(() => (panel = FindFirstObjectByType<RegionEventPanel>()) != null, 10f, "event opens");
            if (panel == null) yield break;
            yield return Wait(1.2f);
            yield return Shot("event");
            var run = RunManager.Instance;
            var zeus = run.UnlockedDecks.FirstOrDefault();
            if (zeus != null)
            {
                // With a god deck awakened: offer one of its cards, to check the event reaches every deck.
                int zeusBefore = run.CardsIn(zeus).Count, startBefore = run.RunDeck.Count;
                Check(Click("Offer a card"), "the event offers to remove a card");
                yield return Wait(1f);
                var offerGrid = FindFirstObjectByType<CardGridPanel>();
                var onlyZeus = run.CardsIn(zeus).Where(c => !run.RunDeck.Contains(c)).Select(c => c.DisplayName.ToUpperInvariant()).ToList();
                // A card face's first text is its cost, so look for the name in any of its texts.
                var zeusCard = offerGrid != null ? offerGrid.GetComponentsInChildren<Button>()
                    .FirstOrDefault(b => b.interactable && b.GetComponentsInChildren<TMP_Text>()
                        .Any(t => onlyZeus.Contains(Regex.Replace(t.text, "<.*?>", "").Trim().ToUpperInvariant()))) : null;
                Check(zeusCard != null, "the event lists the Zeus deck's cards");
                yield return Shot("event-offer-any-deck");
                // Picking closes the grid and destroys the button, so remember the pick in a plain bool.
                bool pickedZeus = zeusCard != null;
                if (pickedZeus) zeusCard.onClick.Invoke();
                else
                {
                    // Back to the event's choices, and take one, so the event ends either way.
                    Click("Cancel");
                    yield return Wait(0.6f);
                    var fallback = panel != null ? panel.GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable) : null;
                    if (fallback != null) fallback.onClick.Invoke();
                }
                yield return Wait(1f);
                if (pickedZeus)
                    Check(run.CardsIn(zeus).Count == zeusBefore - 1 && run.RunDeck.Count == startBefore,
                          $"offering a Zeus card removes it from the Zeus deck ({zeusBefore} -> {run.CardsIn(zeus).Count}), not the starting deck");
                yield return Shot("event-result");
                if (MapView() != null && MapView().Busy) Click("Continue");
                yield break;
            }
            var choice = panel.GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable);
            Check(choice != null, "the event has a choice");
            if (choice == null) yield break;
            Note($"Event choice: {Label(choice).Replace('\n', ' ')}");
            choice.onClick.Invoke();
            yield return Wait(1.2f);
            var grid = FindFirstObjectByType<CardGridPanel>();
            if (grid != null)
            {
                yield return Shot("event-pick-card");
                var pick = grid.GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable && Label(b) != "Cancel" && Label(b) != "Close");
                if (pick != null) pick.onClick.Invoke(); else Click("Cancel");
                yield return Wait(1f);
            }
            yield return Shot("event-result");
            // Events may finish with a "Continue" button.
            if (MapView() != null && MapView().Busy) Click("Continue");
        }

        IEnumerator SaveAndContinue()
        {
            var run = RunManager.Instance;
            string before = State(run);
            var pause = FindFirstObjectByType<PauseMenu>();
            if (pause == null) { Fail("pause menu missing on the map"); yield break; }
            Call(pause, "SetPaused", true);
            yield return Wait(0.6f);
            yield return Shot("pause-on-map");
            Call(pause, "ToMainMenu");
            yield return Until(() => FindFirstObjectByType<MainMenuController>() != null, 15f, "main menu after Main Menu");
            yield return Wait(1.2f);
            Check(RunStarter.CanContinue, "a save exists after leaving to the main menu");
            yield return Shot("main-menu-with-save");
            Check(Click("Continue"), "click Continue");
            yield return Until(() => MapView() != null && RunManager.Instance != null && RunManager.Instance.HasActiveRun, 20f, "Continue loads the map");
            yield return Wait(1.5f);
            string after = State(RunManager.Instance);
            Check(before == after, $"Continue restores the run (before: {before}; after: {after})");
            yield return Shot("continued-map");
        }

        static string State(RunManager run) => run == null ? "no run" :
            $"{run.CurrentMap?.regionName} node {run.CurrentMapNodeId}, HP {run.CarriedHealth}, gold {run.Gold}, Aether {run.Aether}, deck {run.RunDeck.Count}, shards {run.StormShards.Count}";

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        IEnumerator Shot(string what)
        {
            yield return null;
            shotIndex++;
            string file = $"{shotIndex:00}-{what}.png";
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, file));
            for (int i = 0; i < 3; i++) yield return null;
            Note($"Screenshot {file}");
        }

        static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        IEnumerator Until(Func<bool> condition, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < end)
            {
                bool ok;
                try { ok = condition(); } catch { ok = false; }
                if (ok) { Pass(what); yield break; }
                yield return null;
            }
            Fail($"{what} (timed out after {timeout:0} s)");
        }

        /// <summary>Clicks the first active, interactable button whose label starts with text.</summary>
        static bool Click(string text)
        {
            var button = FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.isActiveAndEnabled && b.interactable && Label(b).StartsWith(text));
            if (button == null) return false;
            button.onClick.Invoke();
            return true;
        }

        static string Label(Button button)
        {
            var text = button.GetComponentInChildren<TMP_Text>();
            return text == null ? "" : Regex.Replace(text.text, "<.*?>", "").Trim();
        }

        void Call(object target, string method, params object[] args)
        {
            if (target == null) { Fail($"{method}: target not found"); return; }
            var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) { Fail($"{target.GetType().Name}.{method} not found"); return; }
            info.Invoke(target, args);
        }

        void Check(bool ok, string what)
        {
            if (ok) Pass(what); else Fail(what);
        }

        void Pass(string what) => checks.Add($"- PASS: {what}");

        void Fail(string what)
        {
            failures++;
            checks.Add($"- **FAIL**: {what}");
            Debug.LogWarning($"[Playtest] FAIL: {what}");
        }

        void Note(string text)
        {
            log.AppendLine($"- {Time.realtimeSinceStartup:0.0}s {text}");
            Debug.Log($"[Playtest] {text}");
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            var report = new StringBuilder();
            report.AppendLine("# Playtest report");
            report.AppendLine();
            report.AppendLine($"{checks.Count(c => c.Contains("PASS"))} passed, {failures} failed, {errors.Count} errors logged.");
            report.AppendLine();
            report.AppendLine("## Checks");
            foreach (var c in checks) report.AppendLine(c);
            report.AppendLine();
            report.AppendLine("## Errors logged by the game");
            if (errors.Count == 0) report.AppendLine("- none");
            foreach (var e in errors.Distinct().Take(40)) report.AppendLine($"- {e}");
            report.AppendLine();
            report.AppendLine("## Log");
            report.Append(log);
            File.WriteAllText(Path.Combine(outDir, "report.md"), report.ToString());
            StartCoroutine(QuitSoon());
        }

        static IEnumerator QuitSoon()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }
    }
}
#endif
