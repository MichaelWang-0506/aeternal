using UnityEngine;
using System.Linq;
using Aeternal.Enemies;

namespace Aeternal.UI
{
    /// <summary>
    /// Visualizes enemy attack intent by highlighting the position squares that will be hit.
    /// Attach this to a GameObject and assign the 4 square GameObjects (Up, Right, Down, Left).
    /// </summary>
    public class EnemyIntentVisualizer : MonoBehaviour
    {
        [Header("Enemy Reference")]
        [Tooltip("The enemy GameObject with EnemyController")]
        public GameObject enemyObject;

        [Header("Position Squares")]
        [Tooltip("Square for Up direction (index 0)")]
        public GameObject upSquare;
        [Tooltip("Square for Right direction (index 1)")]
        public GameObject rightSquare;
        [Tooltip("Square for Down direction (index 2)")]
        public GameObject downSquare;
        [Tooltip("Square for Left direction (index 3)")]
        public GameObject leftSquare;

        [Header("Visual Settings")]
        [Tooltip("Color to apply to squares in the attack intent")]
        public Color attackIntentColor = new Color(1f, 0.3f, 0.3f, 1f); // Red tint
        [Tooltip("Default color for squares not in attack intent")]
        public Color defaultColor = Color.white;

        private EnemyController enemyController;
        private SpriteRenderer[] squareRenderers = new SpriteRenderer[4];
        private Color[] originalColors = new Color[4];

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Initialize()
        {
            if (enemyObject == null)
            {
                Debug.LogWarning("[EnemyIntentVisualizer] Enemy object not assigned!");
                return;
            }

            enemyController = enemyObject.GetComponent<EnemyController>();
            if (enemyController == null)
            {
                Debug.LogWarning($"[EnemyIntentVisualizer] Enemy object {enemyObject.name} doesn't have an EnemyController!");
                return;
            }

            // Get SpriteRenderer components from squares and reset them to default first
            // This ensures we store the correct original colors even if intent was already set
            GameObject[] squares = { upSquare, rightSquare, downSquare, leftSquare };
            for (int i = 0; i < 4; i++)
            {
                if (squares[i] != null)
                {
                    squareRenderers[i] = squares[i].GetComponent<SpriteRenderer>();
                    if (squareRenderers[i] != null)
                    {
                        // Reset to default color first to ensure we have the correct original
                        squareRenderers[i].color = defaultColor;
                        originalColors[i] = defaultColor;
                    }
                    else
                    {
                        Debug.LogWarning($"[EnemyIntentVisualizer] Square {i} ({squares[i].name}) doesn't have a SpriteRenderer component!");
                    }
                }
            }

            // Subscribe to enemy intent changes
            enemyController.OnIntentChanged += UpdateSquareColors;

            // Initial update - this will apply the correct colors based on current intent
            UpdateSquareColors();
        }

        private void UnsubscribeFromEvents()
        {
            if (enemyController != null)
            {
                enemyController.OnIntentChanged -= UpdateSquareColors;
            }
        }

        private void UpdateSquareColors()
        {
            if (enemyController == null) return;

            var attackDirections = enemyController.GetAttackDirections();

            // Update each square's color based on whether it's in the attack intent
            for (int i = 0; i < 4; i++)
            {
                if (squareRenderers[i] != null)
                {
                    if (attackDirections.Contains(i))
                    {
                        // Square is in attack intent - apply attack color
                        squareRenderers[i].color = attackIntentColor;
                    }
                    else
                    {
                        // Square is not in attack intent - restore default color
                        squareRenderers[i].color = originalColors[i];
                    }
                }
            }
        }

        /// <summary>
        /// Manually refresh the square colors (useful for initialization or debugging)
        /// </summary>
        public void RefreshColors()
        {
            UpdateSquareColors();
        }
    }
}

