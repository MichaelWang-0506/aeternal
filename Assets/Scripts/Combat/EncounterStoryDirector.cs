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
    /// turn and, for a scripted-death fight, the death itself:
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
            bool hasStory = encounter.scriptedDeath || HasLines(encounter.introDialogue) || HasLines(OpeningNarrative());
            if (!hasStory) return;
            new GameObject("Encounter Story").AddComponent<EncounterStoryDirector>();
        }

        static bool HasLines(System.Collections.Generic.List<DialogueLine> lines) => lines != null && lines.Count > 0;

        /// <summary>The region's opening narration, if this is the run's first fight.</summary>
        static System.Collections.Generic.List<DialogueLine> OpeningNarrative()
        {
            var run = RunManager.Instance;
            if (run == null || !run.HasActiveRun || run.ClearedCount > 0 || run.CurrentNodeIndex != 0) return null;
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
                turnManager.OnRoundStart += OnRoundStart;
                turnManager.OnEnemyPhaseStart += OnEnemyPhaseStart;
                combat.OnCombatEnded += OnCombatEnded;
            }

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
                turnManager.OnEnemyPhaseStart -= OnEnemyPhaseStart;
            }
            if (combat != null) combat.OnCombatEnded -= OnCombatEnded;
        }

        void OnRoundStart()
        {
            round++;
            // Runs after CombatManager has rolled this round's intent, so it can replace it.
            if (deathArmed) TelegraphDeath();
            else if (round >= data.scriptedDeathRound) ArmDeath();
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

        void OnCombatEnded(bool playerWon)
        {
            if (playerWon || deathShown) return;
            deathShown = true;
            StartCoroutine(PlayEnding());
        }

        IEnumerator PlayEnding()
        {
            yield return new WaitForSeconds(DeathPause);
            yield return overlay.PlayNarration(data.epilogue);
            if (!string.IsNullOrEmpty(data.endingTitle))
                yield return overlay.ShowTitle(data.endingTitle, "Click to return to the main menu");

            var run = RunManager.Instance;
            if (run != null && run.HasActiveRun) run.ReportDefeat();
            else SceneManager.LoadScene("MainMenu");
        }
    }
}
