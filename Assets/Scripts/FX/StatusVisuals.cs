using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Aeternal.Cards;
using Aeternal.Status;
using Aeternal.UI;

namespace Aeternal.FX
{
    /// <summary>
    /// Shows a character's status effects on the character itself: Bleed drips blood, faster
    /// the more stacks it has; Stun spins stars around the head and makes the character reel;
    /// a water cage closes a pulsing ring of water around the body.
    /// Effects are placed in world space from the sprite's visible extent, so the character's
    /// own sway and topple don't drag them around. Created by BattlefieldStyler.
    /// </summary>
    public class StatusVisuals : MonoBehaviour
    {
        const int StarCount = 3;
        const float StarSize = 0.22f;
        const float StarSpeed = 3.2f;            // radians per second
        const float DripSize = 0.13f;
        const float DripFall = 0.55f;            // world units
        const float DripDuration = 0.65f;
        const float SlowestDripInterval = 0.7f;  // at 1 stack; divided by the stack count
        const float FastestDripInterval = 0.12f;

        SpriteRenderer body;
        StatusManager status;
        CharacterAnimator animator;
        BattlefieldStyle style;
        Vector2 localMin, localMax;
        bool hasExtent;
        Sprite extentSprite;

        readonly List<SpriteRenderer> stars = new List<SpriteRenderer>();
        SpriteRenderer cage;
        float dripTimer;

        public static StatusVisuals Attach(Transform parent, SpriteRenderer body, StatusManager status,
            CharacterAnimator animator, BattlefieldStyle style)
        {
            var go = new GameObject(body.name + " Status FX");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<StatusVisuals>();
            fx.body = body;
            fx.status = status;
            fx.animator = animator;
            fx.style = style;
            for (int i = 0; i < StarCount; i++)
            {
                var star = new GameObject("Stun Star", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                star.transform.SetParent(go.transform, false);
                star.sprite = ProceduralSprites.Star();
                star.color = style.stunColor;
                star.sortingLayerID = body.sortingLayerID;
                star.transform.localScale = new Vector3(StarSize, StarSize, 1f);
                star.enabled = false;
                fx.stars.Add(star);
            }
            fx.cage = new GameObject("Water Cage", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            fx.cage.transform.SetParent(go.transform, false);
            fx.cage.sprite = ProceduralSprites.CageRing();
            fx.cage.color = style.cageColor;
            fx.cage.sortingLayerID = body.sortingLayerID;
            fx.cage.sortingOrder = body.sortingOrder + 2;
            fx.cage.enabled = false;
            return fx;
        }

        void Update()
        {
            if (body == null || status == null) return;

            int bleedStacks = 0;
            bool stunned = false, caged = false;
            foreach (var s in status.activeStatuses)
            {
                if (s.remainingRounds <= 0 || s.behaviorData == null) continue;
                if (s.behaviorData.behavior == StatusEffectBehavior.Bleeding) bleedStacks += Mathf.Max(1, s.stacks);
                if (s.behaviorData.behavior == StatusEffectBehavior.Blackout) stunned = true;
                if (s.behaviorData.behavior == StatusEffectBehavior.Caged) caged = true;
            }
            bool visible = body.enabled && body.gameObject.activeInHierarchy;
            if (!visible) { bleedStacks = 0; stunned = false; caged = false; }

            if (animator != null) animator.Dizzy = stunned;
            var (min, max) = VisibleExtent();
            UpdateStars(stunned, min, max);
            UpdateDrips(bleedStacks, min, max);
            UpdateCage(caged, min, max);
        }

        void UpdateCage(bool caged, Vector3 min, Vector3 max)
        {
            cage.enabled = caged;
            if (!caged) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            cage.transform.position = (min + max) * 0.5f;
            float size = Mathf.Max(max.x - min.x, max.y - min.y) * Mathf.Lerp(1.05f, 1.15f, pulse);
            cage.transform.localScale = new Vector3(size, size, 1f);
            var c = style.cageColor;
            c.a *= Mathf.Lerp(0.6f, 1f, pulse);
            cage.color = c;
        }

        /// <summary>World-space corners of the sprite's opaque part (position and scale only).</summary>
        (Vector3 min, Vector3 max) VisibleExtent()
        {
            if (body.sprite != extentSprite)
            {
                extentSprite = body.sprite;
                hasExtent = CharacterHUD.OpaqueExtent(extentSprite, out localMin, out localMax);
            }
            if (!hasExtent) return (body.bounds.min, body.bounds.max);
            var t = body.transform;
            return (t.position + Vector3.Scale(localMin, t.lossyScale), t.position + Vector3.Scale(localMax, t.lossyScale));
        }

        void UpdateStars(bool stunned, Vector3 min, Vector3 max)
        {
            float rx = (max.x - min.x) * 0.32f;
            float ry = rx * 0.3f;
            var head = new Vector3((min.x + max.x) * 0.5f, max.y - (max.y - min.y) * 0.06f, 0f);
            for (int i = 0; i < stars.Count; i++)
            {
                var star = stars[i];
                star.enabled = stunned;
                if (!stunned) continue;
                float a = Time.time * StarSpeed + i * Mathf.PI * 2f / stars.Count;
                star.transform.position = head + new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f);
                star.transform.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                // In front of the head on the near half of the orbit, behind it on the far half.
                star.sortingOrder = Mathf.Sin(a) < 0f ? body.sortingOrder + 1 : body.sortingOrder - 1;
            }
        }

        void UpdateDrips(int stacks, Vector3 min, Vector3 max)
        {
            if (stacks <= 0)
            {
                dripTimer = 0f;
                return;
            }
            dripTimer -= Time.deltaTime;
            if (dripTimer > 0f) return;
            dripTimer = Mathf.Max(FastestDripInterval, SlowestDripInterval / stacks);

            // From somewhere on the body's middle, not its edges.
            var from = new Vector3(
                Mathf.Lerp(min.x, max.x, Random.Range(0.3f, 0.7f)),
                Mathf.Lerp(min.y, max.y, Random.Range(0.35f, 0.7f)),
                0f);
            StartCoroutine(Drip(from));
        }

        IEnumerator Drip(Vector3 from)
        {
            var drop = new GameObject("Blood Drop", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            drop.transform.SetParent(transform, false);
            drop.transform.position = from;
            drop.transform.localScale = new Vector3(DripSize * 0.75f, DripSize, 1f);
            drop.sprite = ProceduralSprites.Drop();
            drop.sortingLayerID = body.sortingLayerID;
            drop.sortingOrder = body.sortingOrder + 1;
            var color = style.bleedColor;
            for (float t = 0f; t < 1f; t += Time.deltaTime / DripDuration)
            {
                if (drop == null) yield break;
                drop.transform.position = from + Vector3.down * (DripFall * t * t);   // accelerating fall
                color.a = 1f - t * t;
                drop.color = color;
                yield return null;
            }
            if (drop != null) Destroy(drop.gameObject);
        }
    }
}
