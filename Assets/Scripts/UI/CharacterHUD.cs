using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Aeternal.Cards;
using Aeternal.Enemies;
using Aeternal.FX;
using Aeternal.Stats;
using Aeternal.Status;

namespace Aeternal.UI
{
    /// <summary>
    /// World-space HUD on a combat character: name, status effects and a health bar (with
    /// shield) under the sprite, and for an enemy its attack intent above its head. Follows the
    /// sprite's bounds and polls its targets each frame, so it needs no event wiring.
    /// Created by BattlefieldStyler.
    /// </summary>
    public class CharacterHUD : MonoBehaviour
    {
        const float PixelsPerUnit = 100f;
        static readonly string[] Arrows = { "↑", "→", "↓", "←" };

        SpriteRenderer sprite;
        HealthComponent health;
        StatusManager status;
        EnemyController enemy;
        BattlefieldStyle style;

        const float TrailDelay = 0.35f;     // seconds the lost health lingers
        const float TrailSpeed = 1.2f;      // bar widths per second it then drains
        const float IntentBob = 3f;         // canvas units
        const float IntentPopDuration = 0.3f;

        RectTransform bottom;
        RectTransform hpTrail;
        RectTransform hpFill;
        RectTransform shieldFill;
        RectTransform intent;
        TMP_Text hpText;
        RectTransform badgeRow;
        readonly Dictionary<string, string> lastBadgeValues = new Dictionary<string, string>();
        readonly List<(RectTransform rect, float t)> pops = new List<(RectTransform, float)>();
        string extraBadge;
        Color extraBadgeColor;
        const float BadgePopDuration = 0.35f;

        static readonly Dictionary<HealthComponent, CharacterHUD> huds = new Dictionary<HealthComponent, CharacterHUD>();
        TMP_Text intentText;

        // Canvas units per world unit (smaller when hudScale enlarges the HUD).
        float unitsToCanvas = PixelsPerUnit;

        // Opaque extent of the current sprite, in its local space.
        Sprite cachedSprite;
        bool hasLocalExtent;
        Vector2 localMin, localMax;

        int lastHp = -1, lastMax = -1, lastShield = -1;
        string lastStatus, lastIntent;

        // Damage trail: the bar's previous length, draining to the new one after a pause.
        float hpRatio, trailRatio;
        float trailWait;
        float intentPop;

        /// <summary>
        /// Builds a HUD for one character. Pass an enemy to also show its intent.
        /// </summary>
        public static CharacterHUD Create(Transform parent, SpriteRenderer sprite, HealthComponent health,
            StatusManager status, EnemyController enemy, string label, Color barColor, BattlefieldStyle style)
        {
            var go = new GameObject(label + " HUD", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<CharacterHUD>();
            hud.sprite = sprite;
            hud.health = health;
            hud.status = status;
            hud.enemy = enemy;
            hud.style = style;
            hud.Build(label, barColor);
            if (health != null) huds[health] = hud;
            return hud;
        }

        void Build(string label, Color barColor)
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = style.hudSortingOrder;
            // hudScale enlarges text and bars; the canvas is narrowed to match so the HUD's
            // world width stays hudWidth.
            unitsToCanvas = PixelsPerUnit / Mathf.Max(0.01f, style.hudScale);
            float width = style.hudWidth * unitsToCanvas;
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(width, 10f);
            root.localScale = Vector3.one / unitsToCanvas;

            // Under the sprite: name and statuses on one row, the health bar below. Kept short:
            // tiles are close together, and this has to fit between neighbouring characters.
            bottom = MakeRect("Bottom", root, new Vector2(0.5f, 1f), new Vector2(width, 28f));
            var nameText = MakeText("Name", bottom, label, 12f, TextAlignmentOptions.Left, Color.white);
            nameText.fontStyle = FontStyles.Bold;
            // Long names ("ELITE BANDIT CHAMPION") shrink to fit rather than spill over.
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 7f;
            nameText.fontSizeMax = 12f;
            AddTextOutline(nameText);
            Stretch(nameText.rectTransform, 0f, 14f);
            // Status badges sit in a row just above the name, over the character's feet.
            var row = new GameObject("Status Badges", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            badgeRow = (RectTransform)row.transform;
            badgeRow.SetParent(bottom, false);
            badgeRow.anchorMin = badgeRow.anchorMax = new Vector2(0f, 1f);
            badgeRow.pivot = new Vector2(0f, 0f);
            badgeRow.anchoredPosition = new Vector2(0f, 2f);
            badgeRow.sizeDelta = new Vector2(width, 16f);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.LowerLeft;
            rowLayout.spacing = 3f;
            rowLayout.childControlWidth = rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;

            var back = MakeRect("Bar", bottom, new Vector2(0.5f, 1f), Vector2.zero);
            Stretch(back, -15f, 13f);
            back.gameObject.AddComponent<Image>().color = style.barBackColor;
            var edge = back.gameObject.AddComponent<UnityEngine.UI.Outline>();
            edge.effectColor = Color.black;
            edge.effectDistance = new Vector2(1f, -1f);
            // Behind the health fill: shows the health just lost, then drains away.
            hpTrail = MakeFill("Damage Trail", back, new Color(1f, 0.92f, 0.7f, 0.85f));
            hpFill = MakeFill("Health", back, barColor);
            shieldFill = MakeFill("Shield", back, style.shieldBarColor);
            hpText = MakeText("Value", back, "", 11f, TextAlignmentOptions.Center, Color.white);
            AddTextOutline(hpText);
            hpText.fontStyle = FontStyles.Bold;
            var valueRect = hpText.rectTransform;
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = valueRect.offsetMax = Vector2.zero;

            // Above an enemy's head: what it will attack, and for how much (if the style wants
            // it there; otherwise the corner panel shows it).
            if (enemy != null && style.intentAboveHead)
            {
                intent = MakeRect("Intent", root, new Vector2(0.5f, 0f), new Vector2(width * 0.8f, 22f));
                intent.gameObject.AddComponent<Image>().color = style.barBackColor;
                intentText = MakeText("Text", intent, "", 15f, TextAlignmentOptions.Center, style.intentColor);
                intentText.fontStyle = FontStyles.Bold;
                AddTextOutline(intentText);
                var textRect = intentText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            }
        }

        void OnDestroy()
        {
            if (health != null && huds.TryGetValue(health, out var hud) && hud == this) huds.Remove(health);
        }

        void LateUpdate()
        {
            if (sprite == null || health == null) return;

            var bounds = VisibleBounds();
            transform.position = new Vector3(bounds.center.x, bounds.center.y, 0f);
            bottom.anchoredPosition = new Vector2(0f, (bounds.min.y - bounds.center.y - style.hudGap) * unitsToCanvas);

            RefreshHealth();
            DrainTrail();
            RefreshStatus();
            AnimateBadgePops();
            if (intent != null)
            {
                // Floats gently above the head, and pops when the intent changes.
                float bob = Mathf.Sin(Time.time * 2.4f) * IntentBob;
                intent.anchoredPosition = new Vector2(0f, (bounds.max.y - bounds.center.y + style.hudGap) * unitsToCanvas + bob);
                RefreshIntent();
                intentPop = Mathf.Max(0f, intentPop - Time.deltaTime / IntentPopDuration);
                float pop = 1f + 0.25f * intentPop * intentPop;
                intent.localScale = new Vector3(pop, pop, 1f);
            }
        }

        void DrainTrail()
        {
            if (trailRatio <= hpRatio) return;
            if (trailWait > 0f)
            {
                trailWait -= Time.deltaTime;
                return;
            }
            trailRatio = Mathf.Max(hpRatio, trailRatio - TrailSpeed * Time.deltaTime);
            hpTrail.anchorMax = new Vector2(trailRatio, 1f);
        }

        /// <summary>
        /// World bounds of the sprite's opaque part. The character art has transparent margins,
        /// so the renderer bounds would float the HUD well away from the body; a Tight sprite
        /// mesh hugs the opaque pixels, so its vertices give the visible extent.
        /// </summary>
        Bounds VisibleBounds()
        {
            if (sprite.sprite != cachedSprite)
            {
                cachedSprite = sprite.sprite;
                hasLocalExtent = OpaqueExtent(cachedSprite, out localMin, out localMax);
            }
            if (!hasLocalExtent) return sprite.bounds;

            var min = localMin;
            var max = localMax;
            if (sprite.flipX) { min.x = -localMax.x; max.x = -localMin.x; }
            if (sprite.flipY) { min.y = -localMax.y; max.y = -localMin.y; }
            // Position and scale only: the CharacterAnimator sways and topples the sprite, and
            // the HUD should stay put rather than swing with it.
            var t = sprite.transform;
            var scale = t.lossyScale;
            var origin = t.position;
            var bounds = new Bounds(origin + Vector3.Scale(min, scale), Vector3.zero);
            bounds.Encapsulate(origin + Vector3.Scale(max, scale));
            return bounds;
        }

        /// <summary>
        /// The opaque part of a sprite, in its local space, from its (Tight) mesh vertices.
        /// False if the sprite has no mesh, in which case use the renderer bounds instead.
        /// </summary>
        public static bool OpaqueExtent(Sprite s, out Vector2 min, out Vector2 max)
        {
            min = max = Vector2.zero;
            var vertices = s != null ? s.vertices : null;
            if (vertices == null || vertices.Length == 0) return false;
            min = max = vertices[0];
            foreach (var v in vertices)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            return true;
        }

        void RefreshHealth()
        {
            int hp = health.getHealth;
            int max = health.GetMaxHealth();
            int shield = health.GetTotalShield();
            if (hp == lastHp && max == lastMax && shield == lastShield) return;
            bool first = lastHp < 0;
            lastHp = hp; lastMax = max; lastShield = shield;

            float ratio = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;
            if (!first && ratio < hpRatio) trailWait = TrailDelay;   // took damage: let it linger
            else trailRatio = ratio;                                  // healed or first frame: no trail
            hpRatio = ratio;
            hpTrail.anchorMax = new Vector2(trailRatio, 1f);

            float shieldEnd = max > 0 ? Mathf.Clamp01(hpRatio + (float)shield / max) : hpRatio;
            hpFill.anchorMax = new Vector2(hpRatio, 1f);
            shieldFill.anchorMin = new Vector2(hpRatio, 0f);
            shieldFill.anchorMax = new Vector2(shieldEnd, 1f);
            hpText.text = shield > 0 ? $"{hp}/{max}  +{shield}" : $"{hp}/{max}";
        }

        /// <summary>One badge in the status row.</summary>
        struct Badge
        {
            public string key;      // what it is, e.g. "bleed"
            public Sprite icon;     // null for a text-only badge
            public Color color;
            public string value;    // big number (stacks for Bleed, rounds otherwise) or the text
            public string detail;   // small trailing text (rounds left for Bleed)
        }

        /// <summary>
        /// Adds a text-only badge (e.g. "UNSTOPPABLE") after the status badges; null removes it.
        /// </summary>
        public void SetExtraBadge(string text, Color color)
        {
            extraBadge = text;
            extraBadgeColor = color;
            lastStatus = null;
        }

        /// <summary>The HUD showing this character's health, if there is one.</summary>
        public static CharacterHUD For(HealthComponent health)
        {
            return health != null && huds.TryGetValue(health, out var hud) ? hud : null;
        }

        void RefreshStatus()
        {
            var badges = CurrentBadges();
            var sb = new StringBuilder();
            foreach (var b in badges) sb.Append(b.key).Append(b.value).Append('/').Append(b.detail).Append(';');
            string signature = sb.ToString();
            if (signature == lastStatus) return;
            lastStatus = signature;

            foreach (Transform child in badgeRow) Destroy(child.gameObject);
            pops.Clear();
            var seen = new Dictionary<string, string>();
            foreach (var b in badges)
            {
                var rect = MakeBadge(b);
                // Pop the badges that are new or whose number changed.
                if (!lastBadgeValues.TryGetValue(b.key, out var before) || before != b.value) pops.Add((rect, 1f));
                seen[b.key] = b.value;
            }
            lastBadgeValues.Clear();
            foreach (var kv in seen) lastBadgeValues[kv.Key] = kv.Value;
        }

        List<Badge> CurrentBadges()
        {
            var list = new List<Badge>();
            if (status != null)
            {
                foreach (var s in status.activeStatuses)
                {
                    if (s.remainingRounds <= 0) continue;
                    var behavior = s.behaviorData != null ? s.behaviorData.behavior : StatusEffectBehavior.None;
                    switch (behavior)
                    {
                        case StatusEffectBehavior.Bleeding:
                            list.Add(new Badge { key = "bleed", icon = ProceduralSprites.Drop(), color = style.bleedColor,
                                value = s.stacks.ToString(), detail = $"{s.remainingRounds}r" });
                            break;
                        case StatusEffectBehavior.Blackout:
                            list.Add(new Badge { key = "stun", icon = ProceduralSprites.Star(), color = style.stunColor,
                                value = s.remainingRounds.ToString(), detail = "STUN" });
                            break;
                        case StatusEffectBehavior.Dodge:
                            list.Add(new Badge { key = "dodge", icon = ProceduralSprites.Crystal(), color = style.dodgeColor,
                                value = s.remainingRounds.ToString(), detail = "DODGE" });
                            break;
                        default:
                            string id = !string.IsNullOrEmpty(s.id) ? s.id : behavior.ToString();
                            list.Add(new Badge { key = id, color = style.otherStatusColor,
                                value = id, detail = $"{s.remainingRounds}r" });
                            break;
                    }
                }
            }
            if (!string.IsNullOrEmpty(extraBadge))
                list.Add(new Badge { key = "extra", color = extraBadgeColor, value = extraBadge, detail = "" });
            return list;
        }

        RectTransform MakeBadge(Badge b)
        {
            var go = new GameObject(b.key, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            var rect = (RectTransform)go.transform;
            rect.SetParent(badgeRow, false);
            go.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.05f, 0.88f);
            var edge = go.AddComponent<UnityEngine.UI.Outline>();
            edge.effectColor = b.color;
            edge.effectDistance = new Vector2(1f, -1f);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(2, 3, 1, 1);
            layout.spacing = 1f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (b.icon != null)
            {
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(rect, false);
                var image = icon.GetComponent<Image>();
                image.sprite = b.icon;
                image.color = b.color;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var size = icon.AddComponent<LayoutElement>();
                size.preferredWidth = size.preferredHeight = 12f;
            }
            string detail = string.IsNullOrEmpty(b.detail) ? "" : $"<size=60%> {b.detail}</size>";
            var text = MakeText("Value", rect, $"{b.value}{detail}", 11f, TextAlignmentOptions.MidlineLeft, b.color);
            text.fontStyle = FontStyles.Bold;
            AddTextOutline(text);
            return rect;
        }

        void AnimateBadgePops()
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (rect, t) = pops[i];
                if (rect == null) { pops.RemoveAt(i); continue; }
                t = Mathf.Max(0f, t - Time.deltaTime / BadgePopDuration);
                float s = 1f + 0.5f * t * t;
                rect.localScale = new Vector3(s, s, 1f);
                if (t <= 0f) pops.RemoveAt(i);
                else pops[i] = (rect, t);
            }
        }

        void RefreshIntent()
        {
            var directions = enemy.IsAlive ? enemy.GetAttackDirections() : null;
            bool show = directions != null && directions.Count > 0;
            if (intent.gameObject.activeSelf != show) intent.gameObject.SetActive(show);
            if (!show) return;

            var sb = new StringBuilder();
            foreach (int d in directions)
            {
                if (d < 0 || d >= Arrows.Length) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Arrows[d]);
            }
            sb.Append($"  {enemy.baseDamage} dmg");
            string text = sb.ToString();
            if (text == lastIntent) return;
            if (lastIntent != null) intentPop = 1f;
            lastIntent = text;
            intentText.text = text;
        }

        /// <summary>Dark outline so the text reads over the busy background.</summary>
        void AddTextOutline(TMP_Text text)
        {
            if (style.hudTextOutline <= 0f) return;
            text.outlineWidth = style.hudTextOutline;
            text.outlineColor = new Color32(0, 0, 0, 255);
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 pivot, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Spans the parent's width as a row of the given height, top edge at y.</summary>
        static void Stretch(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, height);
        }

        static RectTransform MakeFill(string name, RectTransform bar, Color color)
        {
            var rect = MakeRect(name, bar, new Vector2(0f, 0.5f), Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        static TMP_Text MakeText(string name, Transform parent, string content, float size,
            TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
