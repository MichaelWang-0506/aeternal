using UnityEngine;
using UnityEngine.UI;
using Aeternal.Core;

namespace Aeternal.Map
{
    /// <summary>
    /// Put this on a map node (a UI Button or any collider-with-click). Clicking it selects
    /// the matching encounter in the run and loads combat. Makes the previously-inert MapScene
    /// an actual node the player can enter.
    ///
    /// Two modes:
    ///  - useNextEncounter = true: always enters the next unfought encounter (simplest linear map).
    ///  - useNextEncounter = false: enters the encounter at 'encounterIndex'.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MapNodeButton : MonoBehaviour
    {
        [Tooltip("If true, ignore encounterIndex and enter the next unfought node in order.")]
        public bool useNextEncounter = true;

        [Tooltip("Which encounter this node maps to (used only when useNextEncounter is false).")]
        public int encounterIndex = 0;

        [Tooltip("Optional: disable this node once its encounter has been cleared.")]
        public bool disableWhenCleared = true;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(Enter);
                button.onClick.AddListener(Enter);
            }
            RefreshInteractable();
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(Enter);
        }

        private void RefreshInteractable()
        {
            if (button == null) return;
            var run = RunManager.Instance;
            if (run == null || !run.HasActiveRun) return;

            if (disableWhenCleared && !useNextEncounter && run.IsEncounterCleared(encounterIndex))
                button.interactable = false;
        }

        public void Enter()
        {
            var run = RunManager.Instance;
            if (run == null || !run.HasActiveRun)
            {
                Debug.LogWarning("[MapNodeButton] No active run. Start a run from the main menu first.");
                return;
            }

            if (useNextEncounter) run.SelectNextEncounter();
            else run.SelectEncounter(encounterIndex);

            run.LoadCombat();
        }
    }
}
