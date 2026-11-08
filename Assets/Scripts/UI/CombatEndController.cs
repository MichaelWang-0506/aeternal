using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Combat;
using Aeternal.Core;
using Aeternal.Cards;

namespace Aeternal.UI
{
    /// <summary>
    /// Presents the end-of-combat resolution that combat previously lacked entirely:
    /// a victory panel (with a card-reward pick when a run is active) or a defeat panel,
    /// and the buttons that advance the run / return to the menu.
    ///
    /// Wire the panel + button references in the Inspector. All references are optional and
    /// null-checked, so a partially-wired setup still runs without throwing.
    /// </summary>
    public class CombatEndController : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("If null, found automatically in the scene.")]
        public CombatManager combatManager;

        [Header("Panels (hidden until combat ends)")]
        public GameObject victoryPanel;
        public GameObject defeatPanel;
        public GameObject rewardPanel;
        [Tooltip("Shown instead of the reward panel when the region BOSS is defeated (run complete).")]
        public GameObject regionClearPanel;
        [Tooltip("Continue button on the region-clear panel.")]
        public Button regionClearContinueButton;

        [Header("Reward UI (parallel arrays; index i = one card choice)")]
        public Button[] rewardButtons;
        public TextMeshProUGUI[] rewardLabels;
        [Tooltip("How many card choices to offer (clamped to rewardButtons length and pool size).")]
        public int rewardChoiceCount = 3;
        [Tooltip("Optional 'skip reward' button.")]
        public Button skipRewardButton;

        [Header("Simple buttons")]
        [Tooltip("Victory 'continue' button, used when there is no reward step (e.g. standalone play).")]
        public Button victoryContinueButton;
        [Tooltip("Defeat 'return to menu' button.")]
        public Button defeatContinueButton;

        [Header("Fallback scene (standalone, no active run)")]
        public string fallbackSceneName = "MainMenu";

        private void Start()
        {
            if (combatManager == null)
                combatManager = FindFirstObjectByType<CombatManager>();

            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            if (rewardPanel != null) rewardPanel.SetActive(false);
            if (regionClearPanel != null) regionClearPanel.SetActive(false);

            if (regionClearContinueButton != null)
                regionClearContinueButton.onClick.AddListener(Advance);
            if (skipRewardButton != null)
                skipRewardButton.onClick.AddListener(Advance);
            if (victoryContinueButton != null)
                victoryContinueButton.onClick.AddListener(Advance);
            if (defeatContinueButton != null)
                defeatContinueButton.onClick.AddListener(OnDefeatContinue);

            if (combatManager != null)
                combatManager.OnCombatEnded += HandleCombatEnded;
        }

        private void OnDestroy()
        {
            if (combatManager != null)
                combatManager.OnCombatEnded -= HandleCombatEnded;
        }

        private void HandleCombatEnded(bool playerWon)
        {
            // Losing a story fight isn't a defeat: EncounterStoryDirector plays the epilogue instead.
            if (!playerWon && EncounterStoryDirector.IsScriptedDeathFight) return;

            if (playerWon) ShowVictory();
            else ShowDefeat();
        }

        private void ShowVictory()
        {
            var run = RunManager.Instance;

            // Beating a god: EncounterStoryDirector plays the blessing, then advances the run.
            if (EncounterStoryDirector.HandlesVictory) return;

            // Beating the region boss completes the run — celebrate instead of offering a
            // card reward the player can never use.
            if (run != null && run.HasActiveRun && run.IsCurrentEncounterBoss && regionClearPanel != null)
            {
                regionClearPanel.SetActive(true);
                return;
            }

            var pool = (run != null && run.HasActiveRun && run.CurrentMap != null)
                ? run.CurrentMap.cardRewardPool
                : null;

            // In a run: gold, a short victory beat, then the cards to choose from as card faces.
            if (pool != null)
            {
                var map = run.CurrentMap;
                var range = run.IsCurrentEncounterElite ? map.eliteGoldReward : map.goldReward;
                int gold = Random.Range(range.x, range.y + 1);
                run.AddGold(gold);
                if (map.deckChoiceRewards)
                {
                    ShowDeckChoiceReward(run, gold);
                    return;
                }
                var choices = BuildChoices(pool, rewardChoiceCount)
                    .Select(c => new RewardScreen.Choice { card = c.card, isShard = c.isShard }).ToList();
                string shardDeck = map.blessingDeck != null ? map.blessingDeck.deckId : "";
                RewardScreen.Show(gold, run.LastAetherEarned, choices, shardDeck, c => ChooseReward(c.card, c.isShard), Advance);
                return;
            }

            bool canReward = rewardPanel != null && rewardButtons != null && rewardButtons.Length > 0
                             && pool != null && pool.Count > 0;

            if (canReward)
            {
                PopulateReward(pool);
                rewardPanel.SetActive(true);
            }
            else if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }
            else
            {
                // No UI wired at all — just advance so the loop still progresses.
                Advance();
            }
        }

        private void ShowDefeat()
        {
            if (defeatPanel != null) defeatPanel.SetActive(true);
        }

        /// <summary>
        /// The reward choices. While a god's deck is sealed, the last choice is a Storm Shard
        /// for it: power now (a card for the run deck) or power later (a card for the god's
        /// deck). An elite, worth the risk, offers two.
        /// </summary>
        private static List<(CardData card, bool isShard)> BuildChoices(List<CardData> pool, int slots)
        {
            var run = RunManager.Instance;
            var shards = new List<CardData>();
            if (run != null && run.CollectsStormShards)
            {
                int shardCount = run.IsCurrentEncounterElite ? 2 : 1;
                shards = run.CurrentMap.stormShardPool.Where(c => c != null).Distinct()
                    .OrderBy(_ => Random.value).Take(Mathf.Min(shardCount, slots)).ToList();
            }

            var choices = pool.Where(c => c != null).Distinct().OrderBy(_ => Random.value)
                .Take(Mathf.Max(0, slots - shards.Count))
                .Select(c => (card: c, isShard: false)).ToList();
            foreach (var shard in shards) choices.Add((shard, true));
            return choices;
        }

        private void PopulateReward(List<CardData> pool)
        {
            var run = RunManager.Instance;
            var choices = BuildChoices(pool, Mathf.Min(rewardChoiceCount, rewardButtons.Length));

            for (int i = 0; i < rewardButtons.Length; i++)
            {
                var btn = rewardButtons[i];
                if (btn == null) continue;

                if (i < choices.Count)
                {
                    var (card, isShard) = choices[i];
                    btn.gameObject.SetActive(true);
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => ChooseReward(card, isShard));

                    if (rewardLabels != null && i < rewardLabels.Length && rewardLabels[i] != null)
                        rewardLabels[i].text = RewardLabel(card, isShard, run);
                }
                else
                {
                    btn.gameObject.SetActive(false);
                }
            }
        }

        private static string RewardLabel(CardData card, bool isShard, RunManager run)
        {
            string label = string.IsNullOrEmpty(card.id) ? card.name : card.id;
            string text = string.IsNullOrEmpty(card.description) ? label : $"{label}\n<size=70%>{card.description}</size>";
            if (!isShard) return text;
            string deck = run != null && run.CurrentMap.blessingDeck != null ? run.CurrentMap.blessingDeck.deckId : "god's";
            string shard = run != null && run.CurrentMap != null ? run.CurrentMap.ShardName : "Storm Shard";
            return $"<color=#FFD24D><size=60%>{shard.ToUpperInvariant()}</size></color>\n{text}\n" +
                   $"<size=55%><color=#FFD24D>Sealed in the {deck} deck until its blessing</color></size>";
        }

        /// <summary>
        /// Deck-choice reward: the player picks one of their decks and takes 1 of 3 from that
        /// deck's pool, then 1 of the sealed god deck's cards (one more to choose from after an
        /// elite). Either pick can be skipped.
        /// </summary>
        private void ShowDeckChoiceReward(RunManager run, int gold)
        {
            var map = run.CurrentMap;
            var offers = new List<RewardScreen.DeckOffer>();
            foreach (var deck in run.OwnedDecks())
            {
                var cards = map.RewardPoolFor(deck).Where(c => c != null).Distinct()
                    .OrderBy(_ => Random.value).Take(Mathf.Max(1, rewardChoiceCount)).ToList();
                offers.Add(new RewardScreen.DeckOffer
                {
                    name = run.OwnedDeckName(deck), size = run.CardsIn(deck).Count, cards = cards, deck = deck,
                });
            }
            var sealedCards = new List<CardData>();
            if (run.CollectsStormShards)
            {
                int count = Mathf.Max(1, map.sealedChoices) + (run.IsCurrentEncounterElite ? 1 : 0);
                sealedCards = map.stormShardPool.Where(c => c != null).Distinct().OrderBy(_ => Random.value).Take(count).ToList();
            }
            string sealedDeck = map.blessingDeck != null ? map.blessingDeck.deckId : "";
            RewardScreen.ShowDeckChoice(gold, run.LastAetherEarned, offers, sealedCards, sealedDeck,
                (offer, card) => run.AddCardTo(offer.deck as Aeternal.Decks.DeckData, card),
                card => run.AddStormShard(card),
                Advance);
        }

        private void ChooseReward(CardData card, bool isShard)
        {
            var run = RunManager.Instance;
            if (isShard) run?.AddStormShard(card);
            else run?.AddCardToDeck(card);
            Advance();
        }

        private void Advance()
        {
            var run = RunManager.Instance;
            if (run != null && run.HasActiveRun)
            {
                // GDD: a special event after every two enemies, before moving on.
                if (run.RegionEventDue && !eventShown)
                {
                    eventShown = true;
                    if (rewardPanel != null) rewardPanel.SetActive(false);
                    if (victoryPanel != null) victoryPanel.SetActive(false);
                    int maxHealth = combatManager != null && combatManager.playerHealth != null
                        ? combatManager.playerHealth.GetMaxHealth() : 0;
                    RegionEventPanel.Show(run, maxHealth, run.AdvanceAfterVictory);
                    return;
                }
                run.AdvanceAfterVictory();
            }
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(fallbackSceneName);
        }

        private bool eventShown;

        private void OnDefeatContinue()
        {
            var run = RunManager.Instance;
            if (run != null && run.HasActiveRun)
                run.ReportDefeat();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(fallbackSceneName);
        }
    }
}
