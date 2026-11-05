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
using Aeternal.Core;
using Aeternal.Combat;
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
        const float TimeLimit = 600f;   // seconds of real time before the run is abandoned
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

            bool firstFight = true, sawShop = false, sawEvent = false;
            for (int step = 0; step < 8; step++)
            {
                run = RunManager.Instance;
                var view = MapView();
                if (view == null) { Fail("map screen missing between nodes"); yield break; }
                var options = run.AvailableNodes();
                var node = Prefer(options);
                if (node == null || node.type == MapNodeType.Boss)
                {
                    Note("Reached the boss floor; stopping before the boss.");
                    break;
                }
                Note($"Floor {node.floor + 1}: options {string.Join(", ", options.Select(o => o.type))}; taking {node.type}");
                Call(view, "Choose", node);

                if (node.IsFight)
                {
                    yield return Fight(firstFight, node.type);
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
            yield return Shot("map-before-boss");

            yield return SaveAndContinue();
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

        static RunMapNode Prefer(List<RunMapNode> options)
        {
            // Elite before Event: the elite fight is the one with its own look to check.
            MapNodeType[] order = { MapNodeType.Shop, MapNodeType.Elite, MapNodeType.Event, MapNodeType.Enemy,
                                    MapNodeType.Treasure, MapNodeType.Rest, MapNodeType.Boss };
            foreach (var type in order)
            {
                var hit = options.FirstOrDefault(n => n != null && n.type == type);
                if (hit != null) return hit;
            }
            return null;
        }

        IEnumerator Fight(bool first, MapNodeType type)
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

                yield return PlayUntilReshuffle(combat);
            }

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
            if (first || type == MapNodeType.Elite) yield return Shot($"reward-{type.ToString().ToLowerInvariant()}");
            var cardButton = reward.GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable && Label(b) != "Skip");
            Check(cardButton != null, "the reward offers a card to pick");
            int deckBefore = RunManager.Instance.RunDeck.Count, shardsBefore = RunManager.Instance.StormShards.Count;
            if (cardButton != null) cardButton.onClick.Invoke(); else Click("Skip");
            yield return Until(() => MapView() != null, 20f, "map returns after the reward");
            var run = RunManager.Instance;
            if (cardButton != null)
                Check(run.RunDeck.Count + run.StormShards.Count == deckBefore + shardsBefore + 1,
                      "the picked reward is added (deck or Storm Shards)");
            Note($"After the fight: gold {run.Gold}, Aether {run.Aether}, deck {run.RunDeck.Count}, shards {run.StormShards.Count}, HP {run.CarriedHealth}");
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
            bool tookShot = false;

            for (int turn = 1; turn <= FirstFightTurns && !combat.IsGameOver; turn++)
            {
                foreach (var ui in HandCards())
                {
                    if (combat.IsGameOver) break;
                    if (ui == null || ui.card == null) continue;
                    if (ui.card.costAP > combat.playerResources.currentAP) continue;
                    ui.OnClickPlayCard();
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
                button.onClick.Invoke();
                yield return Wait(4f);
                if (dm.drawPile.Count == 0 && dm.discardPile.Count > 0) emptyWithDiscard = true;
                if (reshuffled > 0 && !tookShot)
                {
                    tookShot = true;
                    yield return Shot("after-reshuffle");
                }
            }
            dm.OnDiscardReshuffled -= onReshuffle;

            if (reshuffled > 0)
                Pass($"the discard pile was shuffled back in when the draw pile ran out ({reshuffled} cards)");
            else if (combat.IsGameOver)
                Note("The first fight ended before the draw pile ran out; the reshuffle was not seen.");
            else
                Fail($"the draw pile never ran out in {FirstFightTurns} turns; reshuffle not seen");
            Check(!emptyWithDiscard, "the draw pile is never empty while the discard pile has cards");
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
            Check(Click("Leave"), "leave the merchant");
        }

        IEnumerator RegionEvent()
        {
            RegionEventPanel panel = null;
            yield return Until(() => (panel = FindFirstObjectByType<RegionEventPanel>()) != null, 10f, "event opens");
            if (panel == null) yield break;
            yield return Wait(1.2f);
            yield return Shot("event");
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
