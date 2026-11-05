using System.Collections;
using UnityEngine;
using Aeternal.Cards;

namespace Aeternal.FX
{
    /// <summary>
    /// Pixel-style hit effects drawn in the world over the characters: a slash arc and sparks
    /// for physical hits, a bolt from the sky for lightning, a ring for shields, rising plus
    /// signs for heals, drops for Bleed and a burst on death; plus a short hit-stop on big
    /// hits. Purely visual. Lives on the CombatFXManager, which calls it from combat events;
    /// colours and thresholds come from BattlefieldStyle.
    /// </summary>
    public class ImpactFX : MonoBehaviour
    {
        // Above the characters (2) and their HUDs (10), below the damage badges (20).
        const int FxOrder = 15;
        const float Gravity = 9f;           // world units / s², for falling sparks and drops

        BattlefieldStyle style;
        bool hitStopping;

        static Sprite pixel, arc, ring, plus;

        void Awake() => style = BattlefieldStyle.Load();

        /// <summary>
        /// A hit landing on a character. <paramref name="from"/> is where the blow came from
        /// (the attacker), so the slash sweeps away from it.
        /// </summary>
        public void Hit(SpriteRenderer target, Vector3 from, DamageType type, int amount)
        {
            if (target == null) return;
            Vector3 at = target.bounds.center;
            if (type == DamageType.Lightning) StartCoroutine(Bolt(at, target.sortingLayerID));
            else StartCoroutine(Slash(at, from, target.sortingLayerID));
            Color sparks = type == DamageType.Lightning ? style.lightningColor : style.sparkColor;
            Burst(at, sparks, 7 + Mathf.Min(amount, 8), 2.5f, 4.5f, 0.35f, target.sortingLayerID);
            if (amount >= style.bigHitDamage) HitStop();
        }

        /// <summary>True when the hit is big enough to warrant the stronger shake.</summary>
        public bool IsBigHit(int amount) => amount >= style.bigHitDamage;

        /// <summary>Camera shake strength for a big hit.</summary>
        public float BigHitShake => style.bigHitShake;

        /// <summary>A shield going up: a ring swelling out from the body.</summary>
        public void Shield(SpriteRenderer target)
        {
            if (target == null) return;
            StartCoroutine(Ring(target.bounds.center, style.shieldFxColor, target.sortingLayerID));
        }

        /// <summary>Healing: green plus signs drifting up.</summary>
        public void Heal(SpriteRenderer target)
        {
            if (target == null) return;
            var b = target.bounds;
            for (int i = 0; i < 7; i++)
            {
                var start = new Vector3(b.center.x + Random.Range(-0.45f, 0.45f), b.min.y + Random.Range(0.2f, 1f), 0f);
                StartCoroutine(Rise(start, style.healFxColor, i * 0.06f, target.sortingLayerID));
            }
        }

        /// <summary>A Bleed tick: drops falling from the body.</summary>
        public void Bleed(SpriteRenderer target)
        {
            if (target == null) return;
            var b = target.bounds;
            for (int i = 0; i < 4; i++)
            {
                var start = new Vector3(b.center.x + Random.Range(-0.35f, 0.35f), b.center.y + Random.Range(-0.1f, 0.5f), 0f);
                var v = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.4f, 1.4f));
                StartCoroutine(Particle(start, v, ProceduralSprites.Drop(), 0.16f, style.bleedFxColor, 0.6f, true, target.sortingLayerID));
            }
        }

        /// <summary>A character dying: a burst of its own colour and a ring.</summary>
        public void Death(SpriteRenderer target)
        {
            if (target == null) return;
            Vector3 at = target.bounds.center;
            Color tint = target.color.a > 0f ? target.color : Color.white;
            Burst(at, Color.Lerp(tint, Color.white, 0.3f), 26, 2f, 6f, 0.7f, target.sortingLayerID);
            StartCoroutine(Ring(at, new Color(1f, 1f, 1f, 0.8f), target.sortingLayerID));
        }

        /// <summary>
        /// Freezes the action for a moment so a big hit lands. Skipped while paused, and it
        /// only restores the speed it changed, so it never unpauses the game.
        /// </summary>
        public void HitStop()
        {
            if (hitStopping || Time.timeScale != 1f) return;
            StartCoroutine(HitStopRoutine());
        }

        IEnumerator HitStopRoutine()
        {
            hitStopping = true;
            const float frozen = 0.05f;
            Time.timeScale = frozen;
            yield return new WaitForSecondsRealtime(style.hitStopSeconds);
            if (Time.timeScale == frozen) Time.timeScale = 1f;
            hitStopping = false;
        }

        // ------------------------------------------------------------------
        // Effects
        // ------------------------------------------------------------------
        IEnumerator Slash(Vector3 at, Vector3 from, int layer)
        {
            var sr = Make("Slash", Arc(), style.slashColor, layer);
            Vector3 dir = at - from;
            float angle = dir.sqrMagnitude > 0.01f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 0f;
            // A diagonal cut across the line of the blow, flipped at random.
            float tilt = Random.value < 0.5f ? 35f : -35f;
            sr.transform.position = at;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angle + tilt);
            if (tilt < 0f) sr.flipY = true;

            const float time = 0.2f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                sr.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 2.1f, 1f - (1f - k) * (1f - k));
                sr.color = Fade(style.slashColor, 1f - k * k);
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        IEnumerator Bolt(Vector3 at, int layer)
        {
            // Jagged segments from above the screen down to the target, with a soft glow.
            const int segments = 7;
            var root = new GameObject("Bolt").transform;
            var parts = new SpriteRenderer[segments * 2];
            Vector3 top = at + new Vector3(Random.Range(-1f, 1f), 7f, 0f);
            Vector3 prev = top;
            for (int i = 1; i <= segments; i++)
            {
                float k = (float)i / segments;
                Vector3 next = Vector3.Lerp(top, at, k);
                if (i < segments) next.x += Random.Range(-0.45f, 0.45f);
                parts[(i - 1) * 2] = Segment(root, prev, next, 0.26f, Fade(style.lightningColor, 0.5f), layer, FxOrder);
                parts[(i - 1) * 2 + 1] = Segment(root, prev, next, 0.09f, Color.white, layer, FxOrder + 1);
                prev = next;
            }
            var flash = Make("Bolt Flash", ProceduralSprites.SoftEllipse(), style.lightningColor, layer);
            flash.transform.SetParent(root, false);
            flash.transform.position = at;
            flash.transform.localScale = Vector3.one * 2.4f;

            // On, off, on again, then fade: the flicker reads as electricity.
            yield return Wait(0.06f);
            root.gameObject.SetActive(false);
            yield return Wait(0.04f);
            root.gameObject.SetActive(true);
            const float fade = 0.22f;
            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                float a = 1f - t / fade;
                for (int i = 0; i < parts.Length; i++)
                    parts[i].color = Fade(i % 2 == 0 ? style.lightningColor : Color.white, (i % 2 == 0 ? 0.5f : 1f) * a);
                flash.color = Fade(style.lightningColor, 0.8f * a);
                yield return null;
            }
            Destroy(root.gameObject);
        }

        IEnumerator Ring(Vector3 at, Color color, int layer)
        {
            var sr = Make("Ring", Ring(), color, layer);
            sr.transform.position = at;
            const float time = 0.4f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                sr.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.4f, 1f - (1f - k) * (1f - k));
                sr.color = Fade(color, 1f - k);
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        IEnumerator Rise(Vector3 start, Color color, float delay, int layer)
        {
            if (delay > 0f) yield return Wait(delay);
            var sr = Make("Heal Mote", Plus(), color, layer);
            sr.transform.localScale = Vector3.one * 0.32f;
            const float time = 0.8f;
            float phase = Random.value * 6f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                sr.transform.position = start + new Vector3(Mathf.Sin(phase + k * 9f) * 0.08f, k * 1.1f, 0f);
                sr.color = Fade(color, k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f);
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        void Burst(Vector3 at, Color color, int count, float minSpeed, float maxSpeed, float life, int layer)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(minSpeed, maxSpeed);
                v.y += 1.5f;
                StartCoroutine(Particle(at, v, Pixel(), Random.Range(0.08f, 0.16f), color, life * Random.Range(0.7f, 1.2f), true, layer));
            }
        }

        IEnumerator Particle(Vector3 start, Vector2 velocity, Sprite sprite, float size, Color color, float life, bool falls, int layer)
        {
            var sr = Make("Spark", sprite, color, layer);
            sr.transform.localScale = Vector3.one * size;
            Vector3 pos = start;
            for (float t = 0f; t < life; t += Time.deltaTime)
            {
                if (falls) velocity.y -= Gravity * Time.deltaTime;
                pos += (Vector3)velocity * Time.deltaTime;
                sr.transform.position = pos;
                sr.color = Fade(color, 1f - (t / life) * (t / life));
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(a));

        static SpriteRenderer Make(string name, Sprite sprite, Color color, int layer, int order = FxOrder)
        {
            var sr = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order;
            return sr;
        }

        static SpriteRenderer Segment(Transform parent, Vector3 a, Vector3 b, float width, Color color, int layer, int order)
        {
            var sr = Make("Bolt Segment", Pixel(), color, layer, order);
            sr.transform.SetParent(parent, false);
            Vector3 d = b - a;
            sr.transform.position = (a + b) * 0.5f;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(d.magnitude + width * 0.5f, width, 1f);
            return sr;
        }

        // ------------------------------------------------------------------
        // Pixel sprites (point-filtered, so they match the pixel art). One world unit across.
        // ------------------------------------------------------------------
        static Sprite Pixel()
        {
            if (pixel != null) return pixel;
            var tex = NewTexture(2);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return pixel = ToSprite(tex);
        }

        /// <summary>A crescent opening to the left, thick in the middle, tapering to points.</summary>
        static Sprite Arc()
        {
            if (arc != null) return arc;
            const int size = 32;
            var tex = NewTexture(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float outer = Mathf.Sqrt(dx * dx + dy * dy);
                float ix = dx + 0.4f;                                     // inner circle shifted left
                float inner = Mathf.Sqrt(ix * ix + dy * dy);
                bool on = outer < 0.95f && inner > 1.05f;             // tapers to points at the tips
                // White along the outer cutting edge, fading toward the inside of the blade.
                float edge = Mathf.InverseLerp(1.05f, 1.35f, inner);
                tex.SetPixel(x, y, on ? new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, edge)) : Color.clear);
            }
            tex.Apply();
            return arc = ToSprite(tex);
        }

        static Sprite Ring()
        {
            if (ring != null) return ring;
            const int size = 24;
            var tex = NewTexture(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, r < 0.95f && r > 0.78f ? Color.white : Color.clear);
            }
            tex.Apply();
            return ring = ToSprite(tex);
        }

        static Sprite Plus()
        {
            if (plus != null) return plus;
            const int size = 5;
            var tex = NewTexture(size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, x == 2 || y == 2 ? Color.white : Color.clear);
            tex.Apply();
            return plus = ToSprite(tex);
        }

        static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
            };
        }

        static Sprite ToSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
    }
}
