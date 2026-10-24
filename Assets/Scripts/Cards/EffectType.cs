namespace Aeternal.Cards
{
    /// <summary>
    /// Types of effects that can be applied by cards
    /// </summary>
    public enum EffectType
    {
        Damage,          // Deal damage to target
        Heal,           // Heal target
        AddShield,      // Add shield to target
        ModifyAP,       // Modify Action Points
        ModifyDF,       // Modify Divine Focus
        DrawCards,      // Draw cards (magnitude = number of cards)
        RecycleCards,   // Recycle cards from discard pile to end of draw pile (magnitude = number of cards)
        Move,           // Move player (magnitude = squares, can be negative for counterclockwise)
        ApplyStatus,    // Apply a status effect
        EraseAllEffects // Remove all status effects from target
    }
}
