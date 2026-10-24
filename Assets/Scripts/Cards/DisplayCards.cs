using System;
using UnityEngine;

public class DisplayCards : MonoBehaviour
{
    UIConfig uiConfig;
    public RectTransform rectTransform;
    void Awake()
    {
        uiConfig = FindAnyObjectByType<UIConfig>();
    }
    void Start()
    {
        UpdateUI();
    }

    
    void Update()
    {
        UpdateUI();
    }
    

    void UpdateUI()
    {
        int cardCount = rectTransform.childCount;

        if (cardCount == 0) return;

        float width = rectTransform.rect.width;
        float totalCardsWidth = uiConfig.cardWidth * cardCount;
        float maxPossibleSpacing = (width - totalCardsWidth) / Mathf.Max(1, cardCount - 1);
        float spacing = Mathf.Min(uiConfig.maxCardSpacing, maxPossibleSpacing);

        // Center cards by shifting half width to the left
        float totalWidth = totalCardsWidth + spacing * (cardCount - 1);
        float startX = -totalWidth / 2f + uiConfig.cardWidth / 2f;

        for (int i = 0; i < cardCount; i++)
        {
            RectTransform cardRect = rectTransform.GetChild(i) as RectTransform;
            float x = startX + i * (uiConfig.cardWidth + spacing);
            cardRect.anchoredPosition = new Vector2(x, 0);
            // Ensure right side cards render above left side cards
            cardRect.SetSiblingIndex(i);
        }

    }
}
