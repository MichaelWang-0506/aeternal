using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Combat;

namespace Aeternal.UI
{
    /// <summary>
    /// Small combat HUD for the Shifting mechanic: shows the active deck name and a button
    /// (plus the Z hotkey handled by CombatManager) to shift to the next deck. Hides itself
    /// when the current combat only has one deck (no shifting available).
    /// </summary>
    public class ShiftHUD : MonoBehaviour
    {
        public CombatManager combat;
        public TMP_Text deckLabel;
        public Button shiftButton;
        [Tooltip("Optional: hide the whole HUD when shifting isn't available.")]
        public GameObject root;

        private void Start()
        {
            if (combat == null) combat = FindFirstObjectByType<CombatManager>();

            if (shiftButton != null)
                shiftButton.onClick.AddListener(OnShiftClicked);

            if (combat != null)
                combat.OnDeckShifted += UpdateLabel;

            UpdateLabel(combat != null ? combat.ActiveDeckName : "");
            ApplyAvailability();
        }

        private void OnDestroy()
        {
            if (combat != null)
                combat.OnDeckShifted -= UpdateLabel;
        }

        private void OnShiftClicked()
        {
            if (combat != null) combat.ShiftNext();
        }

        private void UpdateLabel(string deckName)
        {
            if (deckLabel != null)
                deckLabel.text = string.IsNullOrEmpty(deckName) ? "" : $"Deck: {deckName}  (Z to Shift)";
            ApplyAvailability();
        }

        private void ApplyAvailability()
        {
            bool canShift = combat != null && combat.CanShift;
            if (root != null) root.SetActive(canShift);
            else if (shiftButton != null) shiftButton.gameObject.SetActive(canShift);
        }
    }
}
