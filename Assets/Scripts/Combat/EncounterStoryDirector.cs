using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Aeternal.Core;
using Aeternal.Encounters;
using Aeternal.Enemies;
using Aeternal.FX;
using Aeternal.Stats;
using Aeternal.Status;
using Aeternal.UI;

namespace Aeternal.Combat
{
    /// <summary>
    /// Runs an encounter's story beats in combat, from its EncounterData: the region's opening
    /// narration (MapData, first fight of a run only), the intro dialogue before the first
    /// turn, a boss's charged attack, an enemy raising walls (terrainEvery) and, for a
    /// scripted-death fight, the death itself:
    ///  - the enemy can't drop below 1 HP;
    ///  - on scriptedDeathRound, or once the enemy is down to scriptedDeathHealthFraction, it
    ///    telegraphs a blow on all four squares and its line is shown;
    ///  - it enrages: a banner, a red aura and an UNSTOPPABLE badge say it can't be stopped,
    ///    and from then on statuses put on it are shrugged off and the player's Dodge fails;
    ///  - that enemy phase kills the player whatever they did (dodge, stun, shields);
    ///  - instead of the defeat panel, the epilogue and ending title play, then the run ends.
    /// Attaches itself to combat scenes that need it; needs no scene wiring.
    /// </summary>
    public class EncounterStoryDirector : MonoBehaviour
    {
        static readonly int[] AllSquares = { 0, 1, 2, 3 };
        const float StrikeDelay = 0.4f;
        const float DeathPause = 1.5f;
        const float RageBannerSeconds = 2.6f;
        const float RageBannerLead = 1.1f;   // banner first, then the dialogue

        Color rageColor = Color.red;

        EncounterData data;
        CombatManager combat;
        EnemyController enemy;
        HealthComponent enemyHealth;
        HealthComponent playerHealth;
        TurnManager turnManager;
        StoryOverlay overlay;

        // Round 1 has already started by the time this binds.
        int round = 1;
        bool deathArmed;
        bool deathShown;
        bool ending;

        // Charged attack: the enemy's normal damage, restored after the charged blow.
        int normalDamage;
        bool chargeReleased;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachIfNeeded();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachIfNeeded();

        static void AttachIfNeeded()
        {
            if (FindFirstObjectByType<CombatManager>() == null) return;
            if (FindFirstObjectByType<EncounterStoryDirector>() != null) return;
            var encounter = CurrentEncounter();
            if (encounter == null) return;
            bool hasStory = encounter.scriptedDeath || encounter.chargedAttackEvery > 0 || encounter.terrainEvery > 0 || HandlesVictory
                            || HasLines(encounter.introDialogue) || HasLines(OpeningNarrative());
            if (!hasStory) return;
            new GameObject("Encounter Story").AddComponent<EncounterStoryDirector>();
        }

        /// <summary>
        /// True for a region's boss whose defeat grants the god's blessing: the director plays
        /// it and advances the run, instead of the usual victory panel.
        /// </summary>
        public static bool HandlesVictory
        {
            get
            {
                var run = RunManager.Instance;
                return run != null && run.HasActiveRun && run.IsCurrentEncounterBoss
                       && run.CurrentMap.blessingDeck != null && !run.BlessingReceived;
            }
        }

        static bool HasLines(System.Collections.Generic.List<DialogueLine> lines) => lines != null && lines.Count > 0;

        /// <summary>The region's opening narration, if this is the run's first fight.</summary>
        static System.Collections.Generic.List<DialogueLine> OpeningNarrative()
        {
            var run = RunManager.Instance;
            // On a branching map, the map screen shows it before the first choice.
            if (run == null || !run.HasActiveRun || run.Map != null || run.ClearedCount > 0 || run.CurrentNodeIndex != 0) return null;
            return run.CurrentMap != null ? run.CurrentMap.openingNarrative : null;
        }

        static EncounterData CurrentEncounter()
        {
            var run = RunManager.Instance;
            return run != null && run.HasActiveRun ? run.CurrentEncounter : null;
        }

        /// <summary>True while the current fight is one the player is meant to lose to the story.</summary>
        public static bool IsScriptedDeathFight
        {
            get
            {
                var encounter = CurrentEncounter();
                return encounter != null && encounter.scriptedDeath;
            }
        }

        void Awake()
        {
            overlay = StoryOverlay.Create(transform);
            // Black from the first frame, so the fight isn't glimpsed before the narration.
            if (HasLines(OpeningNarrative())) overlay.CoverScreen();
        }

        IEnumerator Start()
        {
            // Let CombatManager apply the encounter first.
            yield return null;

            data = CurrentEncounter();
            combat = FindFirstObjectByType<CombatManager>();
            if (data == null || combat == null) yield break;
            enemy = combat.enemy;
            enemyHealth = enemy != null ? enemy.GetComponent<HealthComponent>() : null;
            playerHealth = combat.playerHealth;
            turnManager = combat.turnManager;

            if (data.scriptedDeath && enemyHealth != null && turnManager != null)
            {
                enemyHealth.canDie = false;
                enemyHealth.OnHealthChanged += OnEnemyHealthChanged;
                turnManager.OnEnemyPhaseStart += OnEnemyPhaseStart;
            }
            if (turnManager != null && (data.scriptedDeath || data.chargedAttackEvery > 0 || data.terrainEvery > 0))
            {
                turnManager.OnRoundStart += OnRoundStart;
                turnManager.OnRoundEnd += OnRoundEnd;
            }
            combat.OnCombatEnded += OnCombatEnded;

            var opening = OpeningNarrative();
            if (HasLines(opening))
            {
                yield return overlay.PlayNarration(opening);
                yield return overlay.FadeBackToGame();
            }
            yield return overlay.PlayDialogue(data.introDialogue);

            // The fight may already be due (a scriptedDeathRound of 1).
            if (data.scriptedDeath && round >= data.scriptedDeathRound) ArmDeath();
        }

        void OnDestroy()
        {
            if (enemyHealth != null) enemyHealth.OnHealthChanged -= OnEnemyHealthChanged;
            if (turnManager != null)
            {
                turnManager.OnRoundStart -= OnRoundStart;
                turnManager.OnRoundEnd -= OnRoundEnd;
                turnManager.OnEnemyPhaseStart -= OnEnemyPhaseStart;
            }
            if (combat != null) combat.OnCombatEnded -= OnCombatEnded;
        }

        void OnRoundStart()
        {
            round++;
            // Runs after CombatManager has rolled this round's intent, so it can replace it.
            if (data.scriptedDeath)
            {
                if (deathArmed) TelegraphDeath();
                else if (round >= data.scriptedDeathRound) ArmDeath();
            }
            if (data.chargedAttackEvery > 0 && !deathArmed) ChargedAttackRound();
            if (data.terrainEvery > 0 && !deathArmed && round % data.terrainEvery == 0) RaiseWall();
        }

        /// <summary>
        /// The enemy raises a wall on a square next to the player, to cut off an escape. Never on
        /// the player's square, and never on the last square this round's attack leaves safe.
        /// </summary>
        void RaiseWall()
        {
            var terrain = combat != null ? combat.Terrain : null;
            var positioning = playerHealth != null ? playerHealth.GetComponent<Aeternal.Player.PlayerPositioningComponent>() : null;
            if (terrain == null || positioning == null || enemy == null || !enemy.IsAlive) return;
            int standing = positioning.facingIndex;
            var attacked = enemy.GetAttackDirections();

            int safeOpen = 0;
            for (int d = 0; d < 4; d++)
                if (d != standing && !attacked.Contains(d) && !terrain.IsBlocked(d)) safeOpen++;

            var candidates = new System.Collections.Generic.List<int>();
            foreach (int d in new[] { (standing + 1) % 4, (standing + 3) % 4 })
            {
                if (terrain.IsBlocked(d)) continue;
                bool safe = !attacked.Contains(d);
                if (safe && safeOpen <= 1) continue; // it would leave nowhere safe to go
                candidates.Add(d);
            }
            if (candidates.Count == 0) return;
            int square = candidates[Random.Range(0, candidates.Count)];
            if (!terrain.Raise(square, Mathf.Max(1, data.terrainRounds), false, standing)) return;

            var style = BattlefieldStyle.Load();
            StartCoroutine(overlay.ShowWarning(data.terrainName, data.terrainWarning, style.terrainColor, 1.8f));
            string[] names = { "Up", "Right", "Down", "Left" };
            ActionLog.Instance?.AddMessage($"{data.displayName} raised a {data.terrainName.ToLowerInvariant()} {names[square]}");
        }

        void OnRoundEnd()
        {
            // The charged attack is spent; back to normal blows.
            if (chargeReleased && enemy != null)
            {
                enemy.baseDamage = normalDamage;
                chargeReleased = false;
            }
        }

        /// <summary>
        /// Boss mechanic: every chargedAttackEvery rounds, a blow on every square but one, for
        /// extra damage. The round before, a warning and a CHARGING badge. The safe square is
        /// never the one the player stands on, so they must move. A stunned boss loses the blow.
        /// </summary>
        void ChargedAttackRound()
        {
            int every = data.chargedAttackEvery;
            var hud = CharacterHUD.For(enemyHealth);
            var style = BattlefieldStyle.Load();

            if (round % every == 0)
            {
                var positioning = playerHealth != null ? playerHealth.GetComponent<Aeternal.Player.PlayerPositioningComponent>() : null;
                int standing = positioning != null ? positioning.facingIndex : 0;
                // The safe square is one the player can stand on: not theirs, and free of terrain.
                var terrain = combat != null ? combat.Terrain : null;
                var open = new System.Collections.Generic.List<int>();
                for (int d = 1; d < 4; d++)
                {
                    int square = (standing + d) % 4;
                    if (terrain == null || !terrain.IsBlocked(square)) open.Add(square);
                }
                int safe = open.Count > 0 ? open[Random.Range(0, open.Count)] : (standing + Random.Range(1, 4)) % 4;
                var hit = new System.Collections.Generic.List<int>();
                for (int d = 0; d < 4; d++) if (d != safe) hit.Add(d);

                normalDamage = enemy.baseDamage;
                enemy.baseDamage = Mathf.RoundToInt(normalDamage * data.chargedAttackMultiplier);
                enemy.SetIntentAOE(hit);
                chargeReleased = true;
                if (hud != null) hud.SetExtraBadge(null, Color.clear);
                StartCoroutine(overlay.ShowWarning(data.chargedAttackName, data.chargeRelease, style.stunColor, 2.2f));
            }
            else if ((round + 1) % every == 0)
            {
                if (hud != null) hud.SetExtraBadge("CHARGING", style.stunColor);
                StartCoroutine(overlay.ShowWarning(string.IsNullOrEmpty(data.chargeTitle) ? "STORM GATHERING" : data.chargeTitle,
                    data.chargeWarning, style.stunColor, 2.2f));
            }
        }

        void OnCombatEnded(bool playerWon)
        {
            if (playerWon)
            {
                if (HandlesVictory && !ending) { ending = true; StartCoroutine(PlayBlessing()); }
                return;
            }
            if (data.scriptedDeath && !deathShown)
            {
                deathShown = true;
                StartCoroutine(PlayEnding());
            }
        }

        /// <summary>
        /// The god falls and blesses the player: its deck awakens with every Storm Shard
        /// collected, and Shifting to it is unlocked. Then on to the next region.
        /// </summary>
        IEnumerator PlayBlessing()
        {
            var run = RunManager.Instance;
            var map = run.CurrentMap;
            yield return new WaitForSeconds(DeathPause);

            int shards = run.StormShards.Count;
            var deck = run.ReceiveBlessing();
            var lines = new System.Collections.Generic.List<DialogueLine>(map.blessingNarrative);
            if (deck != null)
            {
                int cards = 0;
                foreach (var entry in deck.defaultCards) cards += Mathf.Max(1, entry.count);
                lines.Add(new DialogueLine
                {
                    text = $"The {deck.deckId} deck awakens: {cards} cards, {shards} of them {map.ShardName}s you gathered. " +
                           (run.UnlockedDecks.Count > 1
                               ? $"Divine Shift can now reach the {deck.deckId} deck too."
                               : "Divine Shift is unlocked: spend Divine Focus to change decks mid-battle.")
                });
            }
            yield return overlay.PlayNarration(lines);
            yield return overlay.ShowTitle(map.blessingTitle,
                map.nextMap != null ? "Click to continue" : "To be continued. Click to return to the main menu");
            run.AdvanceAfterVictory();
        }

        void OnEnemyHealthChanged(int current, int max)
        {
            // Death can't be killed here; hold it at 1 so its bar never reads empty.
            if (current <= 0)
            {
                enemyHealth.SetCurrentHealth(1);
                return;
            }
            if (!deathArmed && max > 0 && current <= max * data.scriptedDeathHealthFraction) ArmDeath();
        }

        /// <summary>
        /// The enemy enrages: telegraphs the killing blow on every square, shows that it can't
        /// be stopped (banner, aura, badge, intent note), then plays its line.
        /// </summary>
        void ArmDeath()
        {
            if (deathArmed) return;
            deathArmed = true;
            TelegraphDeath();
            StartCoroutine(Enrage());
        }

        IEnumerator Enrage()
        {
            var style = BattlefieldStyle.Load();
            rageColor = style.rageColor;

            var styler = FindFirstObjectByType<BattlefieldStyler>();
            if (styler != null && enemy != null) styler.SetAura(enemy.GetComponentInChildren<SpriteRenderer>(), rageColor);
            var hud = CharacterHUD.For(enemyHealth);
            if (hud != null) hud.SetExtraBadge(data.rageBadge, rageColor);

            StartCoroutine(overlay.ShowWarning(data.rageTitle, data.rageSubtitle, rageColor, RageBannerSeconds));
            yield return new WaitForSeconds(RageBannerLead);
            yield return overlay.PlayDialogue(data.scriptedDeathDialogue);
        }

        /// <summary>
        /// While enraged, the rules the banner states hold: any status put on the enemy (a stun,
        /// a bleed) is shrugged off, and the player's Dodge is stripped before it can trigger.
        /// </summary>
        void Update()
        {
            if (!deathArmed || deathShown) return;

            var enemyStatus = enemy != null ? enemy.GetComponent<StatusManager>() : null;
            if (enemyStatus != null && enemyStatus.activeStatuses.Count > 0)
            {
                enemyStatus.RemoveAllStatuses();
                CombatFXManager.Instance?.FloatAt(enemy.transform.position, "IMMUNE", rageColor);
            }

            var playerStatus = playerHealth != null ? playerHealth.GetComponent<StatusManager>() : null;
            if (playerStatus != null && playerStatus.HasStatusBehavior(Aeternal.Cards.StatusEffectBehavior.Dodge))
            {
                playerStatus.RemoveStatusByBehavior(Aeternal.Cards.StatusEffectBehavior.Dodge);
                CombatFXManager.Instance?.FloatAt(playerHealth.transform.position, "Dodge has no effect", new Color(0.75f, 0.75f, 0.75f));
            }
        }

        void LateUpdate()
        {
            if (!deathArmed || deathShown || string.IsNullOrEmpty(data.rageIntentNote)) return;
            // The corner intent line is rewritten whenever the intent changes; keep the note on it.
            foreach (var ui in FindObjectsByType<EnemyStatsUI>(FindObjectsSortMode.None))
            {
                if (ui.intentText == null || ui.intentText.text.Contains(data.rageIntentNote)) continue;
                ui.intentText.text += $"\n<size=60%>{data.rageIntentNote}</size>";
            }
        }

        void TelegraphDeath()
        {
            if (enemy == null) return;
            // Nothing stops this blow: clear a stun or bleed so the enemy reads as untouched.
            var status = enemy.GetComponent<StatusManager>();
            if (status != null) status.RemoveAllStatuses();
            enemy.baseDamage = data.scriptedDeathDamage;
            enemy.SetIntentAOE(AllSquares);
        }

        void OnEnemyPhaseStart()
        {
            if (deathArmed) StartCoroutine(MakeSureTheBlowLands());
        }

        IEnumerator MakeSureTheBlowLands()
        {
            // Let the attack (and its lunge) play first; it usually kills on its own.
            yield return new WaitForSeconds(StrikeDelay);
            if (playerHealth != null && playerHealth.getHealth > 0) playerHealth.Kill();
        }

        /// <summary>
        /// After the scripted death: the epilogue and its title, then the next region (the time
        /// loop begins) if this one has a nextMap, otherwise back to the main menu.
        /// </summary>
        IEnumerator PlayEnding()
        {
            var run = RunManager.Instance;
            bool continues = run != null && run.HasActiveRun && run.CurrentMap.nextMap != null;

            yield return new WaitForSeconds(DeathPause);
            yield return overlay.PlayNarration(data.epilogue);
            if (!string.IsNullOrEmpty(data.endingTitle))
                yield return overlay.ShowTitle(data.endingTitle,
                    continues ? "Click to continue" : "Click to return to the main menu");

            if (continues) run.ContinueToNextRegion();
            else if (run != null && run.HasActiveRun) run.ReportDefeat();
            else SceneManager.LoadScene("MainMenu");
        }
    }
}
