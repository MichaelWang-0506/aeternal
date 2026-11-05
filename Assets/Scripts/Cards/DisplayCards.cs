using System;
using UnityEngine;

/// <summary>
/// Lays the cards in a row out side by side, centred, squeezing them together when they do
/// not fit. Cards ease to their slot rather than jumping, so a card placed somewhere else
/// first (the draw pile, its old slot) glides into place. Hidden cards (scale 0) are left
/// where they are until they show.
/// </summary>
public class DisplayCards : MonoBehaviour
{
    UIConfig uiConfig;
    public RectTransform rectTransform;
    [Tooltip("How quickly cards ease into their slot (higher is faster); 0 snaps them.")]
    public float followSpeed = 12f;
    void Awake()
    {
        uiConfig = FindAnyObjectByType<UIConfig>();
    }
    void Start()
    {
        UpdateUI(true);
    }

    
    void Update()
    {
        UpdateUI();
    }
    

    void UpdateUI(bool snap = false)
    {
        // Cards being removed are deactivated first; they no longer take a slot.
        int cardCount = 0;
        for (int i = 0; i < rectTransform.childCount; i++)
            if (rectTransform.GetChild(i).gameObject.activeSelf) cardCount++;

        if (cardCount == 0) return;

        float width = rectTransform.rect.width;
        float totalCardsWidth = uiConfig.cardWidth * cardCount;
        float maxPossibleSpacing = (width - totalCardsWidth) / Mathf.Max(1, cardCount - 1);
        float spacing = Mathf.Min(uiConfig.maxCardSpacing, maxPossibleSpacing);

        // Center cards by shifting half width to the left
        float totalWidth = totalCardsWidth + spacing * (cardCount - 1);
        float startX = -totalWidth / 2f + uiConfig.cardWidth / 2f;

        float follow = snap || followSpeed <= 0f ? 1f : 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        int slot = 0;
        for (int i = 0; i < rectTransform.childCount; i++)
        {
            RectTransform cardRect = rectTransform.GetChild(i) as RectTransform;
            if (cardRect == null || !cardRect.gameObject.activeSelf) continue;
            var target = new Vector2(startX + slot * (uiConfig.cardWidth + spacing), 0);
            slot++;
            if (cardRect.localScale.x <= 0f) continue;
            cardRect.anchoredPosition = (cardRect.anchoredPosition - target).sqrMagnitude < 0.25f
                ? target : Vector2.Lerp(cardRect.anchoredPosition, target, follow);
        }

    }
}
