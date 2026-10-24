using UnityEngine;
using Aeternal.Core;

namespace Aeternal.Map
{
    /// <summary>
    /// World-space (sprite) map node. Attach to an enemy/boss sprite that has a Collider2D.
    /// Clicking it enters that encounter, if it is the next fightable node.
    /// Cleared nodes and locked (future) nodes ignore clicks.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MapNode2D : MonoBehaviour
    {
        [Tooltip("Index into MapData.encounters this node represents.")]
        public int encounterIndex = 0;

        [Tooltip("Optional: dim the sprite when this node is cleared or locked.")]
        public bool dimWhenNotAvailable = true;

        private SpriteRenderer sr;
        private Color baseColor = Color.white;

        private void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
        }

        private void OnEnable()
        {
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (!dimWhenNotAvailable || sr == null) return;
            var run = RunManager.Instance;
            bool available = run != null && run.HasActiveRun && run.IsEncounterAvailable(encounterIndex);
            bool cleared = run != null && run.HasActiveRun && run.IsEncounterCleared(encounterIndex);

            if (cleared) sr.color = baseColor * new Color(0.4f, 0.4f, 0.4f, 1f); // beaten: darkened
            else if (available) sr.color = baseColor;                            // next: full
            else sr.color = baseColor * new Color(0.7f, 0.7f, 0.7f, 1f);        // locked: slightly dim
        }

        private void OnMouseDown()
        {
            var run = RunManager.Instance;
            if (run == null || !run.HasActiveRun)
            {
                Debug.LogWarning("[MapNode2D] No active run. Start one from the main menu.");
                return;
            }

            if (!run.IsEncounterAvailable(encounterIndex))
            {
                Debug.Log($"[MapNode2D] Node {encounterIndex} not available yet (cleared {run.ClearedCount}).");
                return;
            }

            run.SelectEncounter(encounterIndex);
            run.LoadCombat();
        }
    }
}
