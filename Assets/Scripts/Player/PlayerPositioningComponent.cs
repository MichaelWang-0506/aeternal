using UnityEngine;
using Aeternal.Events;

namespace Aeternal.Player
{
    public class PlayerPositioningComponent : MonoBehaviour
    {
        [Header("Position around enemy")]
        [Tooltip("0=Up, 1=Right, 2=Down, 3=Left")]
        public int facingIndex = 0; // four directions clockwise
        public Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0,1),   // Up
            new Vector2Int(1,0),   // Right
            new Vector2Int(0,-1),  // Down
            new Vector2Int(-1,0)   // Left
        };

        public event System.Action<int> OnFacingChanged;

        private int previousFacingIndex = -1; // Track previous value for Inspector changes

        public Vector2Int CurrentDirection => directions[facingIndex];

        /// <summary>
        /// Called when values are changed in the Inspector (both in editor and play mode)
        /// </summary>
        private void OnValidate()
        {
            // Clamp facingIndex to valid range
            if (facingIndex < 0 || facingIndex >= directions.Length)
            {
                facingIndex = Mathf.Clamp(facingIndex, 0, directions.Length - 1);
            }

            // If facingIndex changed and we're in play mode, trigger the event
            if (Application.isPlaying && facingIndex != previousFacingIndex)
            {
                OnFacingChanged?.Invoke(facingIndex);
                EventBus.RaiseLog($"Player moved to direction {facingIndex} (via Inspector)");
            }

            previousFacingIndex = facingIndex;
        }

        private void Awake()
        {
            // Initialize previous value
            previousFacingIndex = facingIndex;
        }

        public void SetPosition(Vector2Int dir)
        {
            // find the closest matching direction index
            for (int i = 0; i < directions.Length; i++)
            {
                if (directions[i] == dir)
                {
                    facingIndex = i;
                    previousFacingIndex = facingIndex; // Keep in sync
                    OnFacingChanged?.Invoke(facingIndex);
                    EventBus.RaiseLog($"Player moved to direction {facingIndex}");
                    return;
                }
            }
        }

        public void RotateClockwise()
        {
            facingIndex = (facingIndex + 1) % directions.Length;
            previousFacingIndex = facingIndex; // Keep in sync
            OnFacingChanged?.Invoke(facingIndex);
        }

        public void RotateCounterClockwise()
        {
            facingIndex = (facingIndex - 1 + directions.Length) % directions.Length;
            previousFacingIndex = facingIndex; // Keep in sync
            OnFacingChanged?.Invoke(facingIndex);
        }

        /// <summary>
        /// Sets the facing index directly and triggers the change event
        /// </summary>
        public void SetFacingIndex(int index)
        {
            if (index < 0 || index >= directions.Length)
            {
                Debug.LogWarning($"[PlayerPositioningComponent] Invalid facingIndex: {index}. Clamping to valid range.");
                index = Mathf.Clamp(index, 0, directions.Length - 1);
            }

            facingIndex = index;
            previousFacingIndex = facingIndex; // Keep in sync
            OnFacingChanged?.Invoke(facingIndex);
            EventBus.RaiseLog($"Player moved to direction {facingIndex}");
        }
    }
}
