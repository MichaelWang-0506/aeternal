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

        private void PopulateReward(List<CardData> pool)
        {
            // Pick distinct random cards from the pool.
            var choices = pool.Where(c => c != null).Distinct().OrderBy(_ => Random.value).ToList();
            int count = Mathf.Min(rewardChoiceCount, rewardButtons.Length, choices.Count);

            for (int i = 0; i < rewardButtons.Length; i++)
            {
                var btn = rewardButtons[i];
                if (btn == null) continue;

                if (i < count)
                {
                    var card = choices[i];
                    btn.gameObject.SetActive(true);
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => ChooseReward(card));

                    if (rewardLabels != null && i < rewardLabels.Length && rewardLabels[i] != null)
                    {
                        string label = string.IsNullOrEmpty(card.id) ? card.name : card.id;
                        rewardLabels[i].text = string.IsNullOrEmpty(card.description)
                            ? label
                            : $"{label}\n<size=70%>{card.description}</size>";
                    }
                }
                else
                {
                    btn.gameObject.SetActive(false);
                }
            }
        }

        private void ChooseReward(CardData card)
        {
            RunManager.Instance?.AddCardToDeck(card);
            Advance();
        }

        private void Advance()
        {
            var run = RunManager.Instance;
            if (run != null && run.HasActiveRun)
                run.AdvanceAfterVictory();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(fallbackSceneName);
        }

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
