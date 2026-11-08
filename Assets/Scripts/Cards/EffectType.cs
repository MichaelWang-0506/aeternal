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
        EraseAllEffects, // Remove all status effects from target
        RaiseTerrain,   // Raise terrain on a square (magnitude = rounds it lasts; terrainPlacement = where)
        ExtendStatus,   // Multiply the remaining rounds of the target's status with statusEffectData's behavior by magnitude
        ConsumeStatus   // Remove the target's status with statusEffectData's behavior; deal magnitude damage per remaining round
    }

    /// <summary>When an effect applies. Effects with an unmet condition are skipped.</summary>
    public enum EffectCondition
    {
        Always,
        TerrainBehindEnemy   // terrain stands on the square across the enemy from the player
    }

    /// <summary>Where a RaiseTerrain effect puts its terrain.</summary>
    public enum TerrainPlacement
    {
        BehindEnemy,     // the square across the enemy from the player
        PreviousSquare   // the square the player stood on when the card was played
    }
}
