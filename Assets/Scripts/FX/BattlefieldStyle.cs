using UnityEngine;

namespace Aeternal.FX
{
    /// <summary>
    /// Look of the combat battlefield and the HUDs on its characters. Loaded by
    /// BattlefieldStyler from Resources/BattlefieldStyle; every value is a tuning knob.
    /// </summary>
    [CreateAssetMenu(menuName = "Aeternal/Battlefield Style")]
    public class BattlefieldStyle : ScriptableObject
    {
        public const string ResourcePath = "BattlefieldStyle";

        [Header("Cards outside combat")]
        [Tooltip("Card prefab used to show card faces on the map, at the merchant and in rewards.")]
        public GameObject cardPrefab;

        [Header("Background")]
        [Tooltip("Drawn behind the battlefield, scaled to cover the camera view. Empty keeps the plain camera colour.")]
        public Sprite backgroundSprite;
        [Tooltip("Multiplied into the background; darker keeps the characters readable.")]
        public Color backgroundTint = new Color(0.32f, 0.32f, 0.37f, 1f);
        [Tooltip("Darkens the screen edges so the eye goes to the middle of the battlefield.")]
        public Color vignetteColor = new Color(0f, 0f, 0f, 0.8f);

        [Header("Characters")]
        [Tooltip("Multiplier on the player and enemy sprite size. Tiles are 2 units apart and the " +
                 "characters are already taller than a tile, so much above 1.1 makes them overlap.")]
        public float characterScale = 1.1f;
        [Tooltip("Soft shadow under each character's feet.")]
        public Color shadowColor = new Color(0f, 0f, 0f, 0.6f);
        [Tooltip("Shadow width relative to the character's visible width.")]
        public float shadowWidth = 0.85f;
        [Tooltip("Shadow height in world units.")]
        public float shadowHeight = 0.28f;
        [Tooltip("Soft glow behind each character so it separates from the dark background.")]
        public Color backlightColor = new Color(1f, 0.78f, 0.5f, 0.28f);
        [Tooltip("Glow size relative to the character's visible size.")]
        public float backlightScale = 1.4f;

        [Header("Tiles")]
        public Color tileBorderColor = new Color(0.78f, 0.66f, 0.42f, 1f);
        public Color tileFillColor = new Color(0.16f, 0.17f, 0.21f, 0.92f);
        [Tooltip("Fill colour of the enemy's centre tile.")]
        public Color centerTileFillColor = new Color(0.26f, 0.14f, 0.14f, 0.92f);
        [Tooltip("Size of the fill relative to the tile; the rest shows as the border.")]
        [Range(0.5f, 1f)] public float tileFillScale = 0.86f;
        [Tooltip("Dark edge under each tile that makes it read as a raised block.")]
        public Color tileSideColor = new Color(0.07f, 0.06f, 0.05f, 1f);
        [Tooltip("Height of that edge, in world units.")]
        public float tileThickness = 0.14f;

        [Header("Attack telegraph")]
        public Color attackBorderColor = new Color(1f, 0.32f, 0.25f, 1f);
        public Color attackFillColor = new Color(0.34f, 0.07f, 0.07f, 0.95f);
        public Color attackGlowColor = new Color(1f, 0.2f, 0.12f, 0.65f);
        [Tooltip("Glow size relative to the tile.")]
        public float attackGlowScale = 1.35f;
        [Tooltip("Pulses per second of the glow on attacked tiles.")]
        public float attackPulseSpeed = 1.5f;
        [Tooltip("Badge on each attacked tile with the damage it will take.")]
        public Color damageBadgeColor = new Color(0.72f, 0.09f, 0.07f, 1f);
        public Color damageBadgeRimColor = new Color(1f, 0.8f, 0.35f, 1f);
        [Tooltip("Badge diameter relative to the tile width.")]
        public float damageBadgeSize = 0.46f;
        [Tooltip("Border of the tiles the player can step to when standing on an attacked tile.")]
        public Color safeBorderColor = new Color(0.5f, 0.9f, 0.5f, 1f);

        [Header("Character HUD")]
        [Tooltip("Width of the HUD under each character, in world units. Tiles are 2 units apart.")]
        public float hudWidth = 1.5f;
        [Tooltip("Size of the HUD's text and bars; width stays hudWidth.")]
        public float hudScale = 1.5f;
        [Tooltip("Dark outline around HUD text so it reads over any background (0 = none).")]
        [Range(0f, 0.5f)] public float hudTextOutline = 0.25f;
        [Tooltip("Gap between the sprite and its HUD / intent marker, in world units.")]
        public float hudGap = 0.05f;
        public int hudSortingOrder = 10;
        public string playerLabel = "YOU";
        public Color playerBarColor = new Color(0.36f, 0.82f, 0.4f, 1f);
        public Color enemyBarColor = new Color(0.9f, 0.3f, 0.26f, 1f);
        public Color shieldBarColor = new Color(0.42f, 0.7f, 1f, 1f);
        public Color barBackColor = new Color(0f, 0f, 0f, 0.75f);
        public Color intentColor = new Color(1f, 0.55f, 0.35f, 1f);
        [Tooltip("Colour of the ELITE tag before an elite enemy's name.")]
        public Color eliteLabelColor = new Color(1f, 0.8f, 0.3f, 1f);
        [Tooltip("Pulsing aura behind elite enemies, so they read as a bigger threat at a glance.")]
        public Color eliteAuraColor = new Color(1f, 0.75f, 0.25f, 0.5f);

        [Header("Resources (bottom-left panel)")]
        [Tooltip("Action Point icon. Empty uses a generated copper gear.")]
        public Sprite apIcon;
        public Color apColor = new Color(1f, 0.72f, 0.36f, 1f);
        public Color apEmptyColor = new Color(0.3f, 0.25f, 0.2f, 0.75f);
        [Tooltip("Divine Focus icon. Empty uses a generated crystal.")]
        public Sprite dfIcon;
        public Color dfColor = new Color(0.55f, 0.85f, 1f, 1f);
        public Color dfEmptyColor = new Color(0.2f, 0.25f, 0.35f, 0.75f);
        [Tooltip("Icon size in UI units; shrinks automatically if the maximum doesn't fit.")]
        public float resourceIconSize = 40f;

        [Header("Layout (keeps the scene UI from overlapping the battlefield)")]
        [Tooltip("Show the enemy's intent above its head. Off: it is shown under the enemy's name " +
                 "in the top-right corner instead (tall enemies leave no room above their head).")]
        public bool intentAboveHead = false;
        [Tooltip("Where the intent line sits relative to the enemy name in the top-right corner.")]
        public Vector2 intentOffsetFromName = new Vector2(0f, -70f);
        [Tooltip("Active-deck label, relative to the centre of the bottom-left resource panel.")]
        public Vector2 deckLabelPosition = new Vector2(0f, 160f);
        [Tooltip("Shift button, relative to the centre of the bottom-left resource panel.")]
        public Vector2 shiftButtonPosition = new Vector2(0f, 105f);
        [Tooltip("Offset between the stacked cards of the draw pile; small, so only their edges show.")]
        public Vector2 drawPileOffset = new Vector2(5f, 5f);

        [Header("Battle UI (matches the main menu)")]
        public Color panelColor = new Color(0.07f, 0.06f, 0.08f, 0.96f);
        [Tooltip("Gold edge around panels and buttons.")]
        public Color panelEdgeColor = new Color(1f, 0.8f, 0.35f, 0.45f);
        [Tooltip("Tray behind the cards in hand.")]
        public Color handTrayColor = new Color(0.12f, 0.1f, 0.11f, 0.9f);
        public Color buttonColor = new Color(0.1f, 0.09f, 0.11f, 0.92f);
        [Tooltip("End Turn: the button the player presses most.")]
        public Color primaryButtonColor = new Color(0.55f, 0.36f, 0.12f, 0.95f);
        public Color buttonTextColor = new Color(1f, 0.94f, 0.82f, 1f);
        [Tooltip("Font size of the combat log on the left; the log prefab's is too big to read past.")]
        public float logFontSize = 22f;
        public Color logColor = new Color(0.85f, 0.8f, 0.7f, 0.8f);

        [Header("Hit effects")]
        [Tooltip("Slash arc on a physical hit.")]
        public Color slashColor = new Color(1f, 0.93f, 0.78f, 1f);
        [Tooltip("Sparks thrown off a physical hit.")]
        public Color sparkColor = new Color(1f, 0.62f, 0.22f, 1f);
        [Tooltip("Bolt and sparks of a lightning hit (Thunder cards, storm enemies).")]
        public Color lightningColor = new Color(0.55f, 0.85f, 1f, 1f);
        public Color shieldFxColor = new Color(0.45f, 0.85f, 1f, 1f);
        public Color healFxColor = new Color(0.45f, 1f, 0.5f, 1f);
        public Color bleedFxColor = new Color(0.85f, 0.1f, 0.12f, 1f);
        [Tooltip("Splash of a water hit (Atlantis enemies, Poseidon's cards) and the water cage.")]
        public Color waterColor = new Color(0.35f, 0.75f, 1f, 1f);
        [Tooltip("A hit of at least this much damage freezes the action for a moment and shakes harder.")]
        public int bigHitDamage = 5;
        [Tooltip("Length of that freeze, in real seconds.")]
        public float hitStopSeconds = 0.07f;
        [Tooltip("Camera shake of a big hit, in world units (a normal hit on the player shakes 0.14).")]
        public float bigHitShake = 0.24f;

        [Header("Ambience (moving backgrounds)")]
        [Tooltip("Smoke drifting across the backdrop; alpha is the densest a puff gets.")]
        public Color smokeColor = new Color(0.55f, 0.5f, 0.45f, 0.11f);
        public int smokeCount = 10;
        [Tooltip("Embers rising from the city.")]
        public Color emberColor = new Color(1f, 0.62f, 0.25f, 0.9f);
        public int emberCount = 26;
        [Tooltip("Dark clouds along the top in stormy encounters (EncounterData.stormy).")]
        public Color stormCloudColor = new Color(0.1f, 0.11f, 0.18f, 0.6f);
        public int stormCloudCount = 7;
        [Tooltip("Colour of the sky lighting up in a storm; alpha is the brightest it gets.")]
        public Color stormFlashColor = new Color(0.75f, 0.85f, 1f, 0.3f);
        [Tooltip("Seconds between lightning flashes, at random between x and y.")]
        public Vector2 stormFlashInterval = new Vector2(5f, 11f);
        [Tooltip("Underwater regions (MapData.ambience): murky haze drifting across the backdrop.")]
        public Color underwaterHazeColor = new Color(0.2f, 0.45f, 0.5f, 0.12f);
        [Tooltip("Underwater regions: sea-green wash over the backdrop art, so any art reads as underwater.")]
        public Color underwaterWashColor = new Color(0.02f, 0.32f, 0.4f, 0.45f);
        [Tooltip("Underwater regions: bubbles rising from the sea floor.")]
        public Color bubbleColor = new Color(0.7f, 0.95f, 1f, 0.55f);
        public int bubbleCount = 22;
        [Tooltip("How far the backdrop sways with the mouse, in UI units (1080 tall). 0 = still.")]
        public float parallaxPixels = 14f;

        [Header("Terrain (coral walls, Poseidon's terrain)")]
        [Tooltip("Coral on a blocked square, its rounds-left number, and the banner when a wall rises.")]
        public Color terrainColor = new Color(0.35f, 0.85f, 0.75f, 1f);
        [Tooltip("Second coral colour, for the tips of the branches.")]
        public Color terrainTipColor = new Color(1f, 0.55f, 0.5f, 1f);
        [Tooltip("Fill of a blocked tile.")]
        public Color blockedTileColor = new Color(0.08f, 0.2f, 0.22f, 0.95f);
        [Tooltip("Water cage around a caged enemy.")]
        public Color cageColor = new Color(0.4f, 0.8f, 1f, 0.7f);

        [Header("Enraged boss")]
        [Tooltip("Aura, badge and banner colour of an enemy that can no longer be stopped.")]
        public Color rageColor = new Color(0.9f, 0.12f, 0.1f, 1f);

        [Header("Status labels")]
        public Color bleedColor = new Color(1f, 0.4f, 0.4f, 1f);
        public Color stunColor = new Color(1f, 0.85f, 0.3f, 1f);
        public Color dodgeColor = new Color(0.45f, 0.9f, 1f, 1f);
        public Color otherStatusColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        /// <summary>The project's style asset, or built-in defaults when it is missing.</summary>
        public static BattlefieldStyle Load()
        {
            var style = UnityEngine.Resources.Load<BattlefieldStyle>(ResourcePath);
            return style != null ? style : CreateInstance<BattlefieldStyle>();
        }
    }
}
