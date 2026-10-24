using UnityEngine;

namespace Aeternal.Player
{
    /// <summary>
    /// Moves the player visual to the correct position based on their facing direction.
    /// Uses transform references for each of the four directions.
    /// </summary>
    public class PlayerVisualPositioner : MonoBehaviour
    {
        [Header("Position Transforms")]
        [Tooltip("Transform for Up position (0)")]
        public Transform upPosition;
        
        [Tooltip("Transform for Right position (1)")]
        public Transform rightPosition;
        
        [Tooltip("Transform for Down position (2)")]
        public Transform downPosition;
        
        [Tooltip("Transform for Left position (3)")]
        public Transform leftPosition;

        [Header("References")]
        [Tooltip("Player GameObject to move. If null, uses this GameObject.")]
        public GameObject playerObject;

        [Tooltip("PlayerPositioningComponent to track. If null, will try to find it.")]
        public PlayerPositioningComponent positioningComponent;

        private Transform[] positionTransforms;

        private void Awake()
        {
            // Set up position array
            positionTransforms = new Transform[4]
            {
                upPosition,      // 0 = Up
                rightPosition,   // 1 = Right
                downPosition,    // 2 = Down
                leftPosition     // 3 = Left
            };

            // If playerObject not set, use this GameObject
            if (playerObject == null)
            {
                playerObject = gameObject;
            }

            // Find positioning component if not set
            if (positioningComponent == null)
            {
                positioningComponent = playerObject.GetComponent<PlayerPositioningComponent>();
            }
        }

        private void Start()
        {
            // Subscribe to facing changes
            if (positioningComponent != null)
            {
                positioningComponent.OnFacingChanged += OnFacingChanged;
                // Update position immediately based on current facing
                UpdatePosition(positioningComponent.facingIndex);
            }
            else
            {
                Debug.LogWarning("[PlayerVisualPositioner] PlayerPositioningComponent not found!");
            }
        }

        private void OnEnable()
        {
            // Ensure position is updated when component is enabled (e.g., at game start)
            if (positioningComponent != null)
            {
                UpdatePosition(positioningComponent.facingIndex);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (positioningComponent != null)
            {
                positioningComponent.OnFacingChanged -= OnFacingChanged;
            }
        }

        [Header("Animation")]
        [Tooltip("Seconds to slide between squares (0 = instant).")]
        public float moveDuration = 0.22f;

        private Coroutine moveRoutine;

        private void OnFacingChanged(int newFacingIndex)
        {
            UpdatePosition(newFacingIndex, animate: true);
        }

        private void UpdatePosition(int facingIndex, bool animate = false)
        {
            if (facingIndex < 0 || facingIndex >= positionTransforms.Length)
            {
                Debug.LogWarning($"[PlayerVisualPositioner] Invalid facingIndex: {facingIndex}");
                return;
            }

            Transform targetTransform = positionTransforms[facingIndex];
            if (targetTransform == null)
            {
                Debug.LogWarning($"[PlayerVisualPositioner] Position transform for direction {facingIndex} is not assigned!");
                return;
            }

            if (moveRoutine != null) StopCoroutine(moveRoutine);

            if (animate && moveDuration > 0f && gameObject.activeInHierarchy)
            {
                moveRoutine = StartCoroutine(SlideTo(targetTransform.position));
            }
            else
            {
                // Instant snap (initialization / disabled object)
                playerObject.transform.position = targetTransform.position;
            }
        }

        private System.Collections.IEnumerator SlideTo(Vector3 target)
        {
            Vector3 start = playerObject.transform.position;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / moveDuration;
                // Ease-out so the slide feels snappy but settles softly
                float eased = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
                playerObject.transform.position = Vector3.LerpUnclamped(start, target, eased);
                yield return null;
            }
            playerObject.transform.position = target;
            moveRoutine = null;
        }

        /// <summary>
        /// Manually update position (useful for initialization or external calls)
        /// </summary>
        public void RefreshPosition()
        {
            if (positioningComponent != null)
            {
                UpdatePosition(positioningComponent.facingIndex);
            }
        }
    }
}

