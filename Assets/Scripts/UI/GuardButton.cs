using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Aeternal.Combat;

namespace Aeternal.UI
{
    /// <summary>
    /// The Guard button: spend Action Points for a small shield (CombatManager.Guard). Built at
    /// runtime as a copy of the End Turn button placed just above it, so it matches the
    /// battle UI; needs no scene wiring. Greyed out when Guard can't be used.
    /// </summary>
    public class GuardButton : MonoBehaviour
    {
        const float Gap = 12f;

        CombatManager combat;
        Button button;
        TMP_Text label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachIfCombat();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachIfCombat();

        static void AttachIfCombat()
        {
            var combat = FindFirstObjectByType<CombatManager>();
            var endTurn = FindFirstObjectByType<EndTurnButton>();
            if (combat == null || endTurn == null || FindFirstObjectByType<GuardButton>() != null) return;

            var copy = Instantiate(endTurn.gameObject, endTurn.transform.parent);
            copy.name = "Guard Button";
            DestroyImmediate(copy.GetComponent<EndTurnButton>());
            var guard = copy.AddComponent<GuardButton>();
            guard.combat = combat;
            guard.button = copy.GetComponent<Button>();
            guard.button.onClick = new Button.ButtonClickedEvent(); // drop End Turn's listeners
            guard.button.onClick.AddListener(() => guard.combat.Guard());

            var source = (RectTransform)endTurn.transform;
            var rect = (RectTransform)copy.transform;
            rect.anchoredPosition = source.anchoredPosition + new Vector2(0f, source.rect.height + Gap);
            guard.label = copy.GetComponentInChildren<TMP_Text>();
            if (guard.label != null)
            {
                guard.label.text = $"Guard ({combat.guardKey})\n<size=70%>{combat.guardAPCost} AP: +{combat.guardShield} Shield</size>";
                guard.label.fontSizeMax = guard.label.fontSize;
                guard.label.enableAutoSizing = true; // two lines in a button sized for one
            }
        }

        void Update()
        {
            if (combat != null) button.interactable = combat.CanGuard;
        }
    }
}
