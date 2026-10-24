using UnityEngine;
using UnityEngine.UI;
using Aeternal.Combat;

namespace Aeternal.UI
{
    /// <summary>
    /// Button component that ends the player's turn when clicked
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class EndTurnButton : MonoBehaviour
    {
        /// <summary>
        /// Optional hook run when the button is clicked, before the turn ends. Returning true
        /// consumes the click and the turn does not end. Null outside the tutorial, which
        /// uses it to hold the end of turn until the player presses Next.
        /// </summary>
        public static System.Func<bool> EndTurnInterceptor;

        [Header("References")]
        [Tooltip("Reference to the TurnManager. If null, will try to find it in the scene.")]
        public TurnManager turnManager;

        [Tooltip("Reference to CombatManager to check game state. If null, will try to find it.")]
        public CombatManager combatManager;

        private Button button;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClicked);
            }

            // Get or add CanvasGroup for disabling interaction
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void Start()
        {
            if (turnManager == null)
            {
                turnManager = FindFirstObjectByType<TurnManager>();
            }

            if (combatManager == null)
            {
                combatManager = FindFirstObjectByType<CombatManager>();
            }

            UpdateInteractability();
        }

        private void Update()
        {
            UpdateInteractability();
        }

        private void UpdateInteractability()
        {
            bool canInteract = true;

            // Check if game is over
            if (combatManager != null && combatManager.IsGameOver)
            {
                canInteract = false;
            }

            // Update button and canvas group
            if (button != null)
            {
                button.interactable = canInteract;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = canInteract;
                canvasGroup.alpha = canInteract ? 1f : 0.5f; // Dim when disabled
            }
        }

        private void OnButtonClicked()
        {
            // Check if game is over
            if (combatManager != null && combatManager.IsGameOver)
            {
                Debug.Log("[EndTurnButton] Cannot end turn - game is over");
                return;
            }

            if (EndTurnInterceptor != null && EndTurnInterceptor()) return;

            if (turnManager != null)
            {
                turnManager.EndPlayerPhase();
            }
            else
            {
                Debug.LogWarning("[EndTurnButton] TurnManager not found!");
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }
        }
    }
}

