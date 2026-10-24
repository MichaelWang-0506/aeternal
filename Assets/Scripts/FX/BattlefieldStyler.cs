using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Aeternal.Combat;
using Aeternal.Core;
using Aeternal.Enemies;
using Aeternal.Stats;
using Aeternal.Status;
using Aeternal.UI;

namespace Aeternal.FX
{
    /// <summary>
    /// Restyles any scene with a CombatManager (combat and the tutorial) at runtime, from the
    /// BattlefieldStyle asset: a darkened, vignetted backdrop; larger characters with a ground
    /// shadow and backlight; raised, bordered tiles with a pulsing glow on the tiles the enemy
    /// will attack; and a CharacterHUD on the player and the enemy. Health, shield, statuses
    /// and intent move onto those HUDs, so the matching lines of the old corner stats panels
    /// are hidden (the enemy name stays), and AP and DF become rows of icons (ResourcePips).
    /// Purely visual: it never changes game state, and needs no scene wiring.
    /// </summary>
    public class BattlefieldStyler : MonoBehaviour
    {
        const string CenterTileName = "Center Square Background";
        const string OldInnerTileName = "Block Square";

        // Draw order, back to front. The characters themselves are at 2 (set in the scenes).
        const int BackgroundOrder = -100;
        const int VignetteOrder = -90;
        const int TileGlowOrder = -8;
        const int TileSideOrder = -7;
        const int TileBorderOrder = -6;
        const int TileFillOrder = -5;
        const int ShadowOrder = -2;

        BattlefieldStyle style;
        EnemyController enemy;
        // Direction tiles in EnemyController order (0=Up, 1=Right, 2=Down, 3=Left).
        readonly SpriteRenderer[] tileBorders = new SpriteRenderer[4];
        readonly SpriteRenderer[] tileFills = new SpriteRenderer[4];
        readonly SpriteRenderer[] tileGlows = new SpriteRenderer[4];
        readonly bool[] attacked = new bool[4];
        // Per character: its sprite, and the shadow and backlight that follow it.
        readonly System.Collections.Generic.List<(SpriteRenderer body, SpriteRenderer shadow, SpriteRenderer light)> characters =
            new System.Collections.Generic.List<(SpriteRenderer, SpriteRenderer, SpriteRenderer)>();
        readonly System.Collections.Generic.Dictionary<SpriteRenderer, Color> auras =
            new System.Collections.Generic.Dictionary<SpriteRenderer, Color>();
        readonly System.Collections.Generic.Dictionary<SpriteRenderer, Vector3> baseLightScale =
            new System.Collections.Generic.Dictionary<SpriteRenderer, Vector3>();
        bool ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachIfCombat();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachIfCombat();

        static void AttachIfCombat()
        {
            if (FindFirstObjectByType<CombatManager>() == null) return;
            if (FindFirstObjectByType<BattlefieldStyler>() != null) return;
            // Lives in the combat scene, so it goes away with it.
            new GameObject("Battlefield Styler").AddComponent<BattlefieldStyler>();
        }

        IEnumerator Start()
        {
            // Let CombatManager apply the encounter (enemy sprite and stats) first.
            yield return null;

            var combat = FindFirstObjectByType<CombatManager>();
            if (combat == null) yield break;
            style = BattlefieldStyle.Load();
            enemy = combat.enemy;

            var playerHealth = combat.playerHealth;
            var vis = combat.playerVisualPositioner;
            Transform playerVisual = vis != null && vis.playerObject != null ? vis.playerObject.transform
                : playerHealth != null ? playerHealth.transform : null;

            BuildBackground();
            ScaleCharacter(playerVisual);
            ScaleCharacter(enemy != null ? enemy.transform : null);
            StyleTiles();
            HideOldStatLines();
            ShowResourceIcons(combat.playerResources);
            ArrangeSceneUI();

            if (playerHealth != null && playerVisual != null)
            {
                CharacterHUD.Create(transform, playerVisual.GetComponentInChildren<SpriteRenderer>(), playerHealth,
                    playerHealth.GetComponent<StatusManager>(), null, style.playerLabel, style.playerBarColor, style);
            }
            if (enemy != null)
            {
                // Same fallbacks as the enemy name in the corner panel.
                string label = !string.IsNullOrEmpty(enemy.displayName) ? enemy.displayName
                    : !string.IsNullOrEmpty(enemy.enemyId) ? enemy.enemyId : enemy.name;
                label = label.ToUpperInvariant();
                var encounter = RunManager.Instance != null && RunManager.Instance.HasActiveRun
                    ? RunManager.Instance.CurrentEncounter : null;
                if (encounter != null && encounter.isElite)
                    label = $"<color=#{ColorUtility.ToHtmlStringRGB(style.eliteLabelColor)}>ELITE</color> {label}";
                CharacterHUD.Create(transform, enemy.GetComponentInChildren<SpriteRenderer>(), enemy.GetComponent<HealthComponent>(),
                    enemy.GetComponent<StatusManager>(), enemy, label, style.enemyBarColor, style);
            }

            // After the HUDs: they look the body sprite up with GetComponentInChildren.
            var playerBody = playerVisual != null ? playerVisual.GetComponentInChildren<SpriteRenderer>() : null;
            var enemyBody = enemy != null ? enemy.GetComponentInChildren<SpriteRenderer>() : null;
            AddDepth(playerBody);
            AddDepth(enemyBody);

            // After scaling, so the animators keep the enlarged size as their rest pose.
            if (playerBody != null && playerHealth != null)
            {
                var playerAnim = CharacterAnimator.ForPlayer(playerBody, playerHealth,
                    playerHealth.GetComponent<Aeternal.Player.PlayerPositioningComponent>(),
                    combat.deckManager, enemyBody != null ? enemyBody.transform : null);
                StatusVisuals.Attach(transform, playerBody, playerHealth.GetComponent<StatusManager>(), playerAnim, style);
            }
            if (enemyBody != null)
            {
                var enemyAnim = CharacterAnimator.ForEnemy(enemyBody, enemy.GetComponent<HealthComponent>(), enemy);
                StatusVisuals.Attach(transform, enemyBody, enemy.GetComponent<StatusManager>(), enemyAnim, style);
            }
            ready = true;
        }

        /// <summary>
        /// Grounds a character: a soft shadow at its feet, and a soft glow behind it so the dark
        /// art separates from the dark background. (A recoloured copy of the sprite can't make a
        /// light outline: sprite colour multiplies the art, so it stays dark.)
        /// </summary>
        void AddDepth(SpriteRenderer body)
        {
            if (body == null) return;

            Vector2 min, max;
            if (!CharacterHUD.OpaqueExtent(body.sprite, out min, out max))
            {
                var b = body.sprite != null ? body.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
                min = b.min;
                max = b.max;
            }

            var shadow = new GameObject("Shadow", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            shadow.transform.SetParent(body.transform, false);
            shadow.sprite = ProceduralSprites.SoftEllipse();
            shadow.color = style.shadowColor;
            shadow.sortingLayerID = body.sortingLayerID;
            shadow.sortingOrder = ShadowOrder;
            // Centred on the feet; height is in world units, so undo the character's scale.
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(body.transform.lossyScale.y));
            shadow.transform.localPosition = new Vector3((min.x + max.x) * 0.5f, min.y, 0f);
            shadow.transform.localScale = new Vector3((max.x - min.x) * style.shadowWidth, style.shadowHeight / scaleY, 1f);

            var light = new GameObject("Backlight", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            light.transform.SetParent(body.transform, false);
            light.sprite = ProceduralSprites.SoftEllipse();
            light.sortingLayerID = body.sortingLayerID;
            light.sortingOrder = body.sortingOrder - 1;
            light.transform.localPosition = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
            light.transform.localScale = new Vector3((max.x - min.x) * style.backlightScale, (max.y - min.y) * style.backlightScale, 1f);

            baseLightScale[light] = light.transform.localScale;
            characters.Add((body, shadow, light));
            SyncDepth(body, shadow, light);
        }

        /// <summary>
        /// Replaces a character's backlight with a pulsing aura of this colour (null restores it).
        /// Used to mark a state the player should read at a glance, such as an enraged boss.
        /// </summary>
        public void SetAura(SpriteRenderer body, Color? color)
        {
            if (body == null) return;
            if (color.HasValue) auras[body] = color.Value;
            else auras.Remove(body);
        }

        /// <summary>Shows, hides and fades the shadow and backlight along with the body.</summary>
        void SyncDepth(SpriteRenderer body, SpriteRenderer shadow, SpriteRenderer light)
        {
            bool visible = body.enabled && body.gameObject.activeInHierarchy && body.color.a > 0.01f;
            shadow.enabled = visible;
            light.enabled = visible;
            if (!visible) return;
            var l = style.backlightColor;
            float size = 1f;
            if (auras.TryGetValue(body, out var aura))
            {
                // A pulsing, larger glow in the aura colour.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                l = aura;
                l.a *= Mathf.Lerp(0.55f, 1f, pulse);
                size = Mathf.Lerp(1.15f, 1.35f, pulse);
            }
            l.a *= body.color.a;
            light.color = l;
            light.transform.localScale = baseLightScale.TryGetValue(light, out var rest) ? rest * size : light.transform.localScale;
            var s = style.shadowColor;
            s.a *= body.color.a;
            shadow.color = s;
        }

        void Update()
        {
            if (!ready) return;

            foreach (var (body, shadow, light) in characters)
            {
                if (body != null && shadow != null && light != null) SyncDepth(body, shadow, light);
            }

            for (int i = 0; i < 4; i++) attacked[i] = false;
            if (enemy != null && enemy.IsAlive)
            {
                foreach (int d in enemy.GetAttackDirections())
                {
                    if (d >= 0 && d < 4) attacked[d] = true;
                }
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * style.attackPulseSpeed * Mathf.PI * 2f);
            for (int i = 0; i < 4; i++)
            {
                if (tileBorders[i] == null) continue;
                tileBorders[i].color = attacked[i] ? style.attackBorderColor : style.tileBorderColor;
                tileFills[i].color = attacked[i] ? style.attackFillColor : style.tileFillColor;
                tileGlows[i].enabled = attacked[i];
                if (attacked[i])
                {
                    var glow = style.attackGlowColor;
                    glow.a *= Mathf.Lerp(0.35f, 1f, pulse);
                    tileGlows[i].color = glow;
                }
            }
        }

        void BuildBackground()
        {
            var cam = Camera.main;
            if (style.backgroundSprite == null || cam == null || !cam.orthographic) return;

            var go = new GameObject("Battlefield Background", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = style.backgroundSprite;
            sr.color = style.backgroundTint;
            sr.sortingOrder = BackgroundOrder;

            // Cover the whole camera view, cropping whichever side overflows.
            float viewHeight = cam.orthographicSize * 2f;
            float viewWidth = viewHeight * cam.aspect;
            var size = style.backgroundSprite.bounds.size;
            float scale = Mathf.Max(viewWidth / size.x, viewHeight / size.y);
            var center = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            go.transform.position = center;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            // Darken the edges of the backdrop only; tiles and characters draw above it.
            var vignette = new GameObject("Battlefield Vignette", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            vignette.transform.SetParent(transform, false);
            vignette.sprite = ProceduralSprites.Vignette();
            vignette.color = style.vignetteColor;
            vignette.sortingOrder = VignetteOrder;
            vignette.transform.position = center;
            vignette.transform.localScale = new Vector3(viewWidth, viewHeight, 1f);
        }

        void ScaleCharacter(Transform character)
        {
            if (character != null) character.localScale *= style.characterScale;
        }

        void StyleTiles()
        {
            // The intent visualizer owns the four direction tiles; take them over. Its intent
            // event still fires while disabled, so give it the same colours to avoid a flicker.
            var visualizer = FindFirstObjectByType<EnemyIntentVisualizer>();
            if (visualizer != null)
            {
                visualizer.enabled = false;
                visualizer.defaultColor = style.tileBorderColor;
                visualizer.attackIntentColor = style.attackBorderColor;
                var tiles = new[] { visualizer.upSquare, visualizer.rightSquare, visualizer.downSquare, visualizer.leftSquare };
                for (int i = 0; i < 4; i++)
                {
                    var border = tiles[i] != null ? tiles[i].GetComponent<SpriteRenderer>() : null;
                    if (border == null) continue;
                    tileBorders[i] = border;
                    tileFills[i] = StyleTile(border, style.tileFillColor);
                    tileGlows[i] = AddLayer(border, "Tile Glow", border.sprite, style.attackGlowScale, TileGlowOrder, style.attackGlowColor);
                    tileGlows[i].enabled = false;
                }
            }

            var center = GameObject.Find(CenterTileName);
            var centerBorder = center != null ? center.GetComponent<SpriteRenderer>() : null;
            if (centerBorder != null) StyleTile(centerBorder, style.centerTileFillColor);
        }

        /// <summary>
        /// Turns a plain square into a raised, bordered tile: a dark side edge below it and a
        /// shaded face inside the border. Returns the face layer.
        /// </summary>
        SpriteRenderer StyleTile(SpriteRenderer border, Color fill)
        {
            border.color = style.tileBorderColor;
            border.sortingOrder = TileBorderOrder;
            // The scene's grey inner squares would sit on top of the new face.
            if (border.transform.parent != null)
            {
                foreach (Transform sibling in border.transform.parent)
                {
                    if (sibling.name == OldInnerTileName) sibling.gameObject.SetActive(false);
                }
            }

            var side = AddLayer(border, "Tile Side", border.sprite, 1f, TileSideOrder, style.tileSideColor);
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(border.transform.lossyScale.y));
            side.transform.localPosition = new Vector3(0f, -style.tileThickness / scaleY, 0f);

            // The gradient sprite is one unit across; match it to the tile sprite's size.
            var gradient = ProceduralSprites.VerticalGradient();
            float fit = border.sprite != null ? border.sprite.bounds.size.x / gradient.bounds.size.x : 1f;
            return AddLayer(border, "Tile Face", gradient, style.tileFillScale * fit, TileFillOrder, fill);
        }

        static SpriteRenderer AddLayer(SpriteRenderer tile, string name, Sprite sprite, float scale, int order, Color color)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(tile.transform, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = tile.sortingLayerID;
            sr.sortingOrder = order;
            sr.color = color;
            return sr;
        }

        static void HideOldStatLines()
        {
            foreach (var ui in FindObjectsByType<PlayerStatsUI>(FindObjectsSortMode.None))
            {
                Hide(ui.healthSlider);
                Hide(ui.healthText);
                Hide(ui.shieldText);
                HideWithGroup(ui.activeStatusEffectText, ui.transform);
            }
            foreach (var ui in FindObjectsByType<EnemyStatsUI>(FindObjectsSortMode.None))
            {
                Hide(ui.healthSlider);
                Hide(ui.healthText);
                Hide(ui.shieldText);
                HideWithGroup(ui.activeStatusEffectText, ui.transform);
            }
        }

        /// <summary>
        /// Moves the scene UI that collided with the battlefield or with each other:
        /// the enemy's intent goes under its name in the top-right corner (there is no room
        /// above a tall enemy's head), the Shift controls move into the resource panel next to
        /// the Divine Focus they spend, and the draw pile stacks tightly instead of fanning out.
        /// </summary>
        void ArrangeSceneUI()
        {
            foreach (var ui in FindObjectsByType<EnemyStatsUI>(FindObjectsSortMode.None))
            {
                if (ui.intentText == null) continue;
                ui.intentText.color = style.intentColor;
                ui.intentText.fontStyle = TMPro.FontStyles.Bold;
                if (ui.enemyNameText != null)
                {
                    // Directly under the name, in the space the hidden health lines left.
                    var nameRect = ui.enemyNameText.rectTransform;
                    var intent = ui.intentText.rectTransform;
                    intent.anchoredPosition = nameRect.anchoredPosition + style.intentOffsetFromName;
                }
            }

            var stats = FindFirstObjectByType<PlayerStatsUI>();
            foreach (var shift in FindObjectsByType<ShiftHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                MoveShiftControls(shift, stats != null ? stats.transform as RectTransform : null);

            foreach (var pile in FindObjectsByType<DeckDisplay>(FindObjectsSortMode.None))
            {
                pile.cardOffset = style.drawPileOffset;
                pile.RefreshDeckDisplay();
            }
        }

        void MoveShiftControls(ShiftHUD shift, RectTransform panel)
        {
            if (panel == null || shift.deckLabel == null || shift.shiftButton == null) return;

            // A container in the panel becomes the HUD's root, so ShiftHUD still hides both
            // controls when this fight has nothing to shift to.
            var row = new GameObject("Shift Controls", typeof(RectTransform));
            var rowRect = (RectTransform)row.transform;
            rowRect.SetParent(panel, false);
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = Vector2.zero;
            bool wasShown = shift.root == null || shift.root.activeSelf;
            shift.root = row;
            row.SetActive(wasShown);

            var label = shift.deckLabel.rectTransform;
            label.SetParent(rowRect, false);
            label.anchorMin = label.anchorMax = label.pivot = new Vector2(0.5f, 0.5f);
            label.anchoredPosition = style.deckLabelPosition;
            label.sizeDelta = new Vector2(480f, 40f);
            shift.deckLabel.enableAutoSizing = true;
            shift.deckLabel.fontSizeMin = 16f;
            shift.deckLabel.fontSizeMax = 28f;
            shift.deckLabel.alignment = TMPro.TextAlignmentOptions.Center;

            var button = (RectTransform)shift.shiftButton.transform;
            button.SetParent(rowRect, false);
            button.anchorMin = button.anchorMax = button.pivot = new Vector2(0.5f, 0.5f);
            button.anchoredPosition = style.shiftButtonPosition;
            button.sizeDelta = new Vector2(240f, 50f);
        }

        /// <summary>Replaces the AP and DF text lines with rows of icons, in the same place.</summary>
        void ShowResourceIcons(Aeternal.Resources.ResourceComponent resources)
        {
            if (resources == null) return;
            foreach (var ui in FindObjectsByType<PlayerStatsUI>(FindObjectsSortMode.None))
            {
                if (ui.apText != null)
                {
                    ResourcePips.Create(ui.apText.rectTransform, "AP",
                        style.apIcon != null ? style.apIcon : ProceduralSprites.Gear(),
                        style.apColor, style.apEmptyColor, style.resourceIconSize,
                        () => new Vector2Int(resources.currentAP, resources.maxAP));
                }
                if (ui.dfText != null)
                {
                    ResourcePips.Create(ui.dfText.rectTransform, "DF",
                        style.dfIcon != null ? style.dfIcon : ProceduralSprites.Crystal(),
                        style.dfColor, style.dfEmptyColor, style.resourceIconSize,
                        () => new Vector2Int(resources.currentDF, resources.maxDF));
                }
            }
        }

        static void Hide(Component c)
        {
            if (c != null) c.gameObject.SetActive(false);
        }

        /// <summary>Hides a line and, if it sits in its own group (with a label), that group too.</summary>
        static void HideWithGroup(Component c, Transform panel)
        {
            if (c == null) return;
            c.gameObject.SetActive(false);
            var parent = c.transform.parent;
            if (parent != null && parent != panel) parent.gameObject.SetActive(false);
        }
    }
}
