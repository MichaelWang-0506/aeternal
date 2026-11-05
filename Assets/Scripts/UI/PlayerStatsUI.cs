using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Aeternal.UI
{
    /// <summary>
    /// The player's Action Point and Divine Focus lines in the bottom-left panel. Health, shield
    /// and status effects are shown by the CharacterHUD under the player instead.
    /// </summary>
    public class PlayerStatsUI : MonoBehaviour
    {
        [Header("Action Points Display")]
        public TMP_Text apText;

        public Slider apSlider;

        [Header("Divine Focus Display")]
        public TMP_Text dfText;

        public Slider dfSlider;

        /// <summary>
        /// Updates the Action Points display with current and max values
        /// </summary>
        public void UpdateAP(int current, int max)
        {
            string apString = $"Action Points:  {current}/{max}";
            
            if (apText != null)
                apText.text = apString;
            

            
            if (apSlider != null)
            {
                apSlider.maxValue = max;
                apSlider.value = current;
            }
        }

        /// <summary>
        /// Updates the Divine Focus display with current and max values
        /// </summary>
        public void UpdateDF(int current, int max)
        {
            string dfString = $"Divine Focus:  {current}/{max}";
            
            if (dfText != null)
                dfText.text = dfString;

            
            if (dfSlider != null)
            {
                dfSlider.maxValue = max;
                dfSlider.value = current;
            }
        }
    }
}

