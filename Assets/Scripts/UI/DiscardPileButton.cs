using UnityEngine;
using UnityEngine.UI;
using Aeternal.Combat;
using Aeternal.Decks;
using Aeternal.Cards;
using TMPro;

namespace Aeternal.UI
{
    /// <summary>
    /// The discard pile button: shows how many cards are in it and opens or closes its list.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class DiscardPileButton : MonoBehaviour
    {
        [Header("References")]

        public DeckManager deckManager;

        private Button button;
        
        public GameObject discardPileDisplayPanel;

        public TMP_Text text;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClicked);
            }

            // Subscribe to deck manager events
            if (deckManager != null)
            {
                deckManager.OnCardAddedToHand += OnCardAddedToHand;
                deckManager.OnCardRemovedFromHand += OnCardRemovedFromHand;
                deckManager.OnDrawPileChanged += UpdateCount; // the reshuffle empties the discard pile
            }
        }

        private void UpdateCount()
        {
            if (text != null && deckManager != null) text.text = "" + deckManager.discardPile.Count;
        }

        private void Start()
        {

            if (deckManager == null)
            {
                deckManager = FindFirstObjectByType<DeckManager>();
            }
            
        }

        public void OnCardAddedToHand(CardData card)
        {
            text.text = "" + deckManager.discardPile.Count;
        }

        public void OnCardRemovedFromHand(CardData card)
        {
            text.text = "" + deckManager.discardPile.Count;
            
        }



        private void OnButtonClicked()
        {
            discardPileDisplayPanel.SetActive(!discardPileDisplayPanel.activeInHierarchy);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }
            if (deckManager != null)
            {
                deckManager.OnCardAddedToHand -= OnCardAddedToHand;
                deckManager.OnCardRemovedFromHand -= OnCardRemovedFromHand;
                deckManager.OnDrawPileChanged -= UpdateCount;
            }
        }
    }
}

