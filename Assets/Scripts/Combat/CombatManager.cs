using System;
using System.Collections.Generic;
using UnityEngine;
using Aeternal.Stats;
using Aeternal.Resources;
using Aeternal.Decks;
using Aeternal.Cards;
using Aeternal.Enemies;
using Aeternal.Events;
using Aeternal.UI;
using Aeternal.Core;

namespace Aeternal.Combat
{
    public class CombatManager : MonoBehaviour
    {
        /// <summary>Fired once when combat resolves. Arg is true if the player won.</summary>
        public event Action<bool> OnCombatEnded;

        /// <summary>Player's health at the moment combat ended (valid after OnCombatEnded).</summary>
        public int PlayerRemainingHealth => playerHealth != null ? playerHealth.getHealth : 0;

        [Header("Shifting")]
        [Tooltip("Divine Focus spent per deck shift.")]
        public int shiftCost = 1;
        [Tooltip("Hotkey to shift to the next deck.")]
        public KeyCode shiftKey = KeyCode.Z;

        [Header("Guard (GDD: spend an action point to create a shield)")]
        [Tooltip("Action Points a Guard costs.")]
        public int guardAPCost = 1;
        [Tooltip("Shield a Guard gives.")]
        public int guardShield = 2;
        [Tooltip("Rounds the Guard shield lasts (1: through the enemy's next attack).")]
        public int guardRounds = 1;
        [Tooltip("Hotkey to Guard.")]
        public KeyCode guardKey = KeyCode.X;

        /// <summary>One swappable element deck available during combat.</summary>
        [System.Serializable]
        public class DeckSlot
        {
            public string name;
            public List<CardData> cards;
            public List<EffectPayload> onShiftIn;
            public List<EffectPayload> onShiftOut;
            /// <summary>Shift upgrade bonus added to damage, shield and heal Shift effects.</summary>
            public int shiftBonus;
        }

        private readonly List<DeckSlot> shiftSlots = new List<DeckSlot>();
        private int activeShiftIndex = 0;

        /// <summary>Fired after a successful shift, with the new active deck's name.</summary>
        public event Action<string> OnDeckShifted;
        public string ActiveDeckName =>
            (shiftSlots.Count > 0 && activeShiftIndex >= 0 && activeShiftIndex < shiftSlots.Count)
                ? shiftSlots[activeShiftIndex].name : "";
        public bool CanShift => shiftSlots.Count > 1;

        [Header("Scene References")]
        public HealthComponent playerHealth;
        public ResourceComponent playerResources;
        public DeckManager deckManager;
        public CardResolver cardResolver;
        public TurnManager turnManager;
        public CardHandDisplay handDisplay;

        public EnemyController enemy;

        [Header("Game State")]
        [Tooltip("Reference to PlayerVisualPositioner to update position at start")]
        public Aeternal.Player.PlayerVisualPositioner playerVisualPositioner;

        // Game state tracking
        private bool isGameOver = false;
        private bool isPlayerDead = false;
        private bool isEnemyDead = false;

        public bool IsGameOver => isGameOver;
        public bool IsPlayerDead => isPlayerDead;
        public bool IsEnemyDead => isEnemyDead;

        private void Start()
        {
            InitializeCombat();
        }

        public void InitializeCombat()
        {
            if (playerHealth == null || playerResources == null || deckManager == null) return;

            EventBus.RaiseLog("Combat initialized");

            // Reset game state
            isGameOver = false;
            isPlayerDead = false;
            isEnemyDead = false;

            // Subscribe to death events
            if (playerHealth != null)
            {
                playerHealth.OnDeathLocal += OnPlayerDied;
            }

            if (enemy != null)
            {
                var enemyHealth = enemy.GetComponent<Aeternal.Stats.HealthComponent>();
                if (enemyHealth != null)
                {
                    enemyHealth.OnDeathLocal += OnEnemyDied;
                }
            }

            // subscribe to turn events
            turnManager.OnRoundStart += HandleRoundStart;
            turnManager.OnPlayerPhaseStart += HandlePlayerPhase;
            turnManager.OnEnemyPhaseStart += HandleEnemyPhase;
            turnManager.OnRoundEnd += HandleRoundEnd;
            if (deckManager != null) deckManager.OnDiscardReshuffled += HandleDiscardReshuffled;

            // If a run is active, RunManager drives the enemy, deck, and carried-over health.
            // Otherwise fall back to the scene's serialized enemy/deck so PlayGameScene stays
            // playable standalone for isolated testing.
            var run = RunManager.Instance;
            if (run != null && run.HasActiveRun)
            {
                if (enemy != null && run.CurrentEncounter != null)
                {
                    enemy.ApplyEncounter(run.CurrentEncounter);
                }

                deckManager.InitializeDeckFromCards(run.RunDeck);

                if (run.CarriedHealth > 0)
                {
                    playerHealth.SetCurrentHealth(run.CarriedHealth);
                }
                // So between-fight healing (rest, events) knows the cap.
                run.PlayerMaxHealth = playerHealth.GetMaxHealth();

                BuildShiftSlots(run);
            }
            else
            {
                deckManager.InitializeDeck(deckManager.activeDeck);
                BuildSingleSlot(deckManager.activeDeck);
            }

            // Update player visual position at game start
            if (playerVisualPositioner != null)
            {
                playerVisualPositioner.RefreshPosition();
            }
            else
            {
                // Try to find it if not assigned
                var pos = FindFirstObjectByType<Aeternal.Player.PlayerVisualPositioner>();
                if (pos != null)
                {
                    pos.RefreshPosition();
                }
            }
            
            // Note: the enemy's first intent is decided in HandleRoundStart (fired by the
            // combat loop's OnRoundStart on round 1). Deciding it here as well would roll the
            // intent twice and flicker the telegraph, so we let round start own it.
            turnManager.StartCombat();
        }

        private void HandleRoundStart()
        {
            EventBus.RaiseLog("Round Start");
            
            // Enemy decides intent at the start of the round (before player phase)
            // This allows the player to see what the enemy will do
            if (enemy != null && enemy.IsAlive && playerHealth != null)
            {
                enemy.DecideNextIntent(playerHealth.gameObject);
            }

            playerResources.RefillAP();
            if (playerResources.refillDFAtRoundStart) playerResources.RefillDF();
            handDisplay.RefreshHand();

            // Log round start
            Aeternal.UI.ActionLog.Instance?.AddMessage("--- Round Start ---");
        }

        private void HandlePlayerPhase()
        {
            EventBus.RaiseLog("Player Phase started");
            // Player plays cards manually; UI calls CardResolver.ResolvePlayAsync per card.
        }

        private void HandleEnemyPhase()
        {
            EventBus.RaiseLog("Enemy Phase started");
            if (enemy != null && enemy.IsAlive)
            {
                enemy.ExecuteIntent(playerHealth.gameObject);
            }
            else
            {
                Aeternal.UI.ActionLog.Instance?.AddMessage("Enemy phase - No enemy present");
            }
        }

        private void HandleRoundEnd()
        {
            EventBus.RaiseLog("Round ended");
    
            // Call OnRoundEnd for player
            playerHealth.OnRoundEnd();
    
            // Call OnRoundEnd for enemy (this triggers bleeding and other status effects)
            if (enemy != null)
            {
                var enemyHealth = enemy.GetComponent<Aeternal.Stats.HealthComponent>();
                if (enemyHealth != null)
                {
                    enemyHealth.OnRoundEnd();
                }
            }
        }

        // ------------------------------------------------------------------
        // Shifting (the signature mechanic)
        // ------------------------------------------------------------------
        private void Update()
        {
            if (!PauseMenu.IsPaused && Input.GetKeyDown(shiftKey))
                ShiftNext();
            if (!PauseMenu.IsPaused && Input.GetKeyDown(guardKey))
                Guard();
        }

        // ------------------------------------------------------------------
        // Guarding: any turn, spare Action Points can become a small shield
        // ------------------------------------------------------------------

        /// <summary>Whether Guard can be used now: the player's turn, with enough AP.</summary>
        /// <summary>
        /// Optional lock on Guard: while it returns true, Guard can't be used and its button is
        /// greyed out. Null outside the tutorial, which doesn't teach Guard and holds it locked.
        /// </summary>
        public static System.Func<bool> GuardLock;

        bool GuardLocked => GuardLock != null && GuardLock();

        public bool CanGuard =>
            !GuardLocked && !isGameOver && playerResources != null && playerHealth != null
            && (turnManager == null || turnManager.currentState == TurnState.PlayerPhase)
            && playerResources.currentAP >= guardAPCost;

        /// <summary>
        /// Spends guardAPCost AP for guardShield shield lasting guardRounds rounds. Weaker than a
        /// shield card on purpose: it turns leftover AP into some protection.
        /// </summary>
        public bool Guard()
        {
            if (!CanGuard)
            {
                if (!isGameOver && !GuardLocked) Aeternal.UI.ActionLog.Instance?.AddMessage($"Guard needs {guardAPCost} AP on your turn");
                return false;
            }
            if (!playerResources.TryConsumeAP(guardAPCost)) return false;
            playerHealth.AddShield(guardShield, guardRounds, "guard");
            string msg = $"Guard: +{guardShield} shield";
            EventBus.RaiseLog(msg);
            Aeternal.UI.ActionLog.Instance?.AddMessage(msg);
            return true;
        }

        private void BuildShiftSlots(RunManager run)
        {
            shiftSlots.Clear();
            activeShiftIndex = 0;

            var startDeck = run.CurrentMap != null ? run.CurrentMap.startingDeck : null;
            shiftSlots.Add(new DeckSlot
            {
                name = (startDeck != null && !string.IsNullOrEmpty(startDeck.deckId)) ? startDeck.deckId : "Deck 1",
                cards = new List<CardData>(run.RunDeck),
                onShiftIn = startDeck != null ? startDeck.onShiftIn : null,
                onShiftOut = startDeck != null ? startDeck.onShiftOut : null,
                shiftBonus = run.ShiftBonus(startDeck),
            });

            if (run.ShiftDecks != null)
            {
                foreach (var d in run.ShiftDecks)
                {
                    if (d == null) continue;
                    shiftSlots.Add(new DeckSlot
                    {
                        name = !string.IsNullOrEmpty(d.deckId) ? d.deckId : d.name,
                        cards = ExpandDeck(d),
                        onShiftIn = d.onShiftIn,
                        onShiftOut = d.onShiftOut,
                        shiftBonus = run.ShiftBonus(d),
                    });
                }
            }

            OnDeckShifted?.Invoke(ActiveDeckName);
        }

        private void BuildSingleSlot(DeckData deck)
        {
            shiftSlots.Clear();
            activeShiftIndex = 0;
            shiftSlots.Add(new DeckSlot
            {
                name = (deck != null && !string.IsNullOrEmpty(deck.deckId)) ? deck.deckId : "Deck",
                cards = deck != null ? ExpandDeck(deck) : new List<CardData>(),
                onShiftIn = deck != null ? deck.onShiftIn : null,
                onShiftOut = deck != null ? deck.onShiftOut : null,
            });
            OnDeckShifted?.Invoke(ActiveDeckName);
        }

        private List<CardData> ExpandDeck(DeckData deck)
        {
            var list = new List<CardData>();
            if (deck == null) return list;
            foreach (var e in deck.defaultCards)
            {
                if (e.card == null) continue;
                for (int i = 0; i < Mathf.Max(1, e.count); i++) list.Add(e.card);
            }
            return list;
        }

        /// <summary>Shift to the next deck in the cycle (bound to the shift hotkey and UI button).</summary>
        public void ShiftNext()
        {
            if (shiftSlots.Count < 2) return;
            ShiftToDeck((activeShiftIndex + 1) % shiftSlots.Count);
        }

        /// <summary>
        /// Shift the active deck. Costs Divine Focus, only allowed on the player's turn, and
        /// runs the current deck's shift-out effects then the new deck's shift-in effects.
        /// </summary>
        public bool ShiftToDeck(int index)
        {
            if (isGameOver) return false;
            if (shiftSlots.Count < 2)
            {
                Aeternal.UI.ActionLog.Instance?.AddMessage("No other deck to shift to");
                return false;
            }
            if (index < 0 || index >= shiftSlots.Count || index == activeShiftIndex) return false;

            if (turnManager != null && turnManager.currentState != TurnState.PlayerPhase)
            {
                Aeternal.UI.ActionLog.Instance?.AddMessage("Can only shift during your turn");
                return false;
            }

            if (!playerResources.TryConsumeDF(shiftCost))
            {
                Aeternal.UI.ActionLog.Instance?.AddMessage("Not enough Divine Focus to shift");
                return false;
            }

            // Shift-out of the current deck, swap piles, shift-in to the new deck.
            ApplyShiftEffects(shiftSlots[activeShiftIndex].onShiftOut, "shift-out", shiftSlots[activeShiftIndex].shiftBonus);
            activeShiftIndex = index;
            var slot = shiftSlots[index];
            deckManager.InitializeDeckFromCards(slot.cards);
            ApplyShiftEffects(slot.onShiftIn, "shift-in", slot.shiftBonus);
            if (handDisplay != null) handDisplay.RefreshHand();

            string msg = $"Shifted to {slot.name} deck";
            EventBus.RaiseLog(msg);
            Aeternal.UI.ActionLog.Instance?.AddMessage(msg);
            OnDeckShifted?.Invoke(slot.name);
            return true;
        }

        private void ApplyShiftEffects(List<EffectPayload> effects, string tag, int bonus)
        {
            if (effects == null || effects.Count == 0) return;
            var ctx = new CardContext
            {
                caster = playerHealth != null ? playerHealth.gameObject : null,
                enemy = enemy != null ? enemy.gameObject : null,
                explicitTarget = enemy != null ? enemy.gameObject : null,
            };
            foreach (var eff in effects)
            {
                string desc = cardResolver.ResolveEffect(WithShiftBonus(eff, bonus), ctx, deckManager);
                if (!string.IsNullOrEmpty(desc))
                    Aeternal.UI.ActionLog.Instance?.AddMessage($"({tag}) {desc}");
            }
        }

        /// <summary>
        /// The effect with the Shift upgrade bonus added, if it has an amount to raise (damage,
        /// shield, heal). The deck's own asset is never changed.
        /// </summary>
        public static EffectPayload WithShiftBonus(EffectPayload eff, int bonus)
        {
            if (eff == null || bonus == 0 || !IsShiftUpgradable(eff.effectType)) return eff;
            return new EffectPayload
            {
                effectType = eff.effectType,
                magnitude = eff.magnitude + bonus,
                durationRounds = eff.durationRounds,
                target = eff.target,
                damageType = eff.damageType,
                statusEffectData = eff.statusEffectData,
            };
        }

        /// <summary>Whether Shift upgrades raise this kind of effect.</summary>
        public static bool IsShiftUpgradable(EffectType type) =>
            type == EffectType.Damage || type == EffectType.AddShield || type == EffectType.Heal;

        private void OnPlayerDied()
        {
            isPlayerDead = true;
            CheckGameOver();
        }

        private void OnEnemyDied()
        {
            isEnemyDead = true;
            CheckGameOver();
        }

        private void CheckGameOver()
        {
            if (isGameOver) return; // Already handled

            isGameOver = true;

            // Stop the turn manager
            if (turnManager != null)
            {
                turnManager.StopCombat();
            }

            // Log game over message
            bool playerWon = isEnemyDead && !isPlayerDead;
            if (isPlayerDead)
            {
                string msg = "Game Over - Player Died";
                EventBus.RaiseLog(msg);
                Aeternal.UI.ActionLog.Instance?.AddMessage(msg);
            }
            else if (isEnemyDead)
            {
                string msg = "Victory - Enemy Defeated!";
                EventBus.RaiseLog(msg);
                Aeternal.UI.ActionLog.Instance?.AddMessage(msg);
            }

            // Record the outcome into the run (health carries to the next fight on a win).
            var run = RunManager.Instance;
            if (run != null && run.HasActiveRun && playerWon)
            {
                run.ReportVictory(PlayerRemainingHealth);
            }

            // Notify UI (CombatEndController) so it can show the victory/defeat + reward flow.
            OnCombatEnded?.Invoke(playerWon);
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (playerHealth != null)
            {
                playerHealth.OnDeathLocal -= OnPlayerDied;
            }

            if (enemy != null)
            {
                var enemyHealth = enemy.GetComponent<Aeternal.Stats.HealthComponent>();
                if (enemyHealth != null)
                {
                    enemyHealth.OnDeathLocal -= OnEnemyDied;
                }
            }

            if (turnManager != null)
            {
                turnManager.OnRoundStart -= HandleRoundStart;
                turnManager.OnPlayerPhaseStart -= HandlePlayerPhase;
                turnManager.OnEnemyPhaseStart -= HandleEnemyPhase;
                turnManager.OnRoundEnd -= HandleRoundEnd;
            }
            if (deckManager != null) deckManager.OnDiscardReshuffled -= HandleDiscardReshuffled;
        }

        private void HandleDiscardReshuffled(int count)
        {
            Aeternal.UI.ActionLog.Instance?.AddMessage($"Draw pile empty: shuffled {count} discarded card{(count > 1 ? "s" : "")} back in");
        }
    }
}
