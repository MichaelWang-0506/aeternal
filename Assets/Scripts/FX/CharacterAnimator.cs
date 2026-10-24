using System.Collections;
using UnityEngine;
using Aeternal.Cards;
using Aeternal.Decks;
using Aeternal.Enemies;
using Aeternal.Player;
using Aeternal.Stats;

namespace Aeternal.FX
{
    /// <summary>
    /// Procedural animation for a combat character drawn as a single sprite: an idle breath and
    /// sway, plus one-shot reactions (hit, attack, hop, brace, death) driven by gameplay events.
    /// It only touches the sprite's scale and rotation, so it layers on top of the position
    /// tweens (movement slide, enemy lunge) and colour flashes the other systems use.
    /// Created by BattlefieldStyler; stands in until the art has real animation frames.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        const float IdleBreath = 0.022f;   // scale change of a breath
        const float IdleSpeed = 0.5f;      // breaths per second
        const float IdleSway = 1.2f;       // degrees
        const float DizzySway = 7f;        // degrees
        const float DizzySpeed = 2.4f;     // radians per second

        /// <summary>Set while stunned (by StatusVisuals): the idle becomes a slow reel.</summary>
        public bool Dizzy { get; set; }

        Vector3 baseScale;
        Quaternion baseRotation;
        float phase;

        // One-shot offsets, layered over the idle.
        Vector2 squash = Vector2.one;
        float tilt;
        bool dead;
        Coroutine current;

        // Event sources, kept to unsubscribe.
        HealthComponent health;
        PlayerPositioningComponent positioning;
        DeckManager deck;
        EnemyController enemy;
        Transform opponent;

        /// <summary>Animates the player: hit, death, hop on moving, attack/brace on playing cards.</summary>
        public static CharacterAnimator ForPlayer(SpriteRenderer body, HealthComponent health,
            PlayerPositioningComponent positioning, DeckManager deck, Transform opponent)
        {
            var anim = Attach(body, health);
            anim.positioning = positioning;
            anim.deck = deck;
            anim.opponent = opponent;
            if (positioning != null) positioning.OnFacingChanged += anim.OnMoved;
            if (deck != null) deck.OnCardRemovedFromHand += anim.OnCardPlayed;
            return anim;
        }

        /// <summary>Animates an enemy: hit, death, and a strike when it attacks.</summary>
        public static CharacterAnimator ForEnemy(SpriteRenderer body, HealthComponent health, EnemyController enemy)
        {
            var anim = Attach(body, health);
            anim.enemy = enemy;
            if (enemy != null) enemy.OnAttackExecuted += anim.OnEnemyAttacked;
            return anim;
        }

        static CharacterAnimator Attach(SpriteRenderer body, HealthComponent health)
        {
            var anim = body.gameObject.AddComponent<CharacterAnimator>();
            anim.health = health;
            if (health != null)
            {
                health.OnDamageTaken += anim.OnDamaged;
                health.OnDeathLocal += anim.OnDied;
            }
            return anim;
        }

        void Awake()
        {
            baseScale = transform.localScale;
            baseRotation = transform.localRotation;
            phase = Random.value * Mathf.PI * 2f;
        }

        void OnDestroy()
        {
            if (health != null)
            {
                health.OnDamageTaken -= OnDamaged;
                health.OnDeathLocal -= OnDied;
            }
            if (positioning != null) positioning.OnFacingChanged -= OnMoved;
            if (deck != null) deck.OnCardRemovedFromHand -= OnCardPlayed;
            if (enemy != null) enemy.OnAttackExecuted -= OnEnemyAttacked;
        }

        void LateUpdate()
        {
            float breath = 0f, sway = 0f;
            if (!dead)
            {
                float t = Time.time * IdleSpeed * Mathf.PI * 2f + phase;
                breath = Mathf.Sin(t) * IdleBreath;
                sway = Mathf.Sin(t * 0.5f) * IdleSway;
                // Stunned: reel from side to side instead of standing steady.
                if (Dizzy) sway = Mathf.Sin(Time.time * DizzySpeed) * DizzySway;
            }
            // Breathing in stretches up and narrows slightly, so the volume reads as constant.
            transform.localScale = new Vector3(
                baseScale.x * (1f - breath * 0.5f) * squash.x,
                baseScale.y * (1f + breath) * squash.y,
                baseScale.z);
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, sway + tilt);
        }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        void OnDamaged(DamageInfo info)
        {
            if (info != null && info.amount > 0) Play(Hit());
        }

        void OnDied() => Play(Die(), force: true);

        void OnMoved(int facing) => Play(Hop());

        void OnCardPlayed(CardData card)
        {
            if (card == null || card.effects == null) return;
            bool attacks = false, supports = false;
            foreach (var effect in card.effects)
            {
                if (effect.effectType == EffectType.Damage) attacks = true;
                if (effect.effectType == EffectType.AddShield || effect.effectType == EffectType.Heal) supports = true;
            }
            if (attacks) Play(Strike(DirectionTo(opponent)));
            else if (supports) Play(Brace());
        }

        void OnEnemyAttacked(bool hit)
        {
            // Lean the way the attack went (the lunge itself is CombatFXManager's).
            float dir = 0f;
            foreach (int d in enemy.GetAttackDirections())
            {
                if (d == 1) dir += 1f;
                if (d == 3) dir -= 1f;
            }
            Play(Strike(Mathf.Sign(dir)));
        }

        float DirectionTo(Transform target)
        {
            if (target == null) return 1f;
            float dx = target.position.x - transform.position.x;
            return Mathf.Abs(dx) < 0.01f ? 0f : Mathf.Sign(dx);
        }

        // ------------------------------------------------------------------
        // One-shots
        // ------------------------------------------------------------------

        void Play(IEnumerator routine, bool force = false)
        {
            if (dead && !force) return;
            if (current != null) StopCoroutine(current);
            squash = Vector2.one;
            tilt = 0f;
            current = StartCoroutine(routine);
        }

        /// <summary>Squashed by the blow, recoiling away, then springing back.</summary>
        IEnumerator Hit()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                float e = (1f - t) * (1f - t);
                squash = new Vector2(1f + 0.14f * e, 1f - 0.14f * e);
                tilt = 7f * e;
                yield return null;
            }
            Done();
        }

        /// <summary>Wind up away from the target, then lunge toward it. dir: -1 left, 1 right, 0 straight.</summary>
        IEnumerator Strike(float dir)
        {
            float lean = dir == 0f ? 0f : -dir; // rotating clockwise leans right
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.42f)
            {
                if (t < 0.35f)
                {
                    float k = t / 0.35f;                         // anticipation
                    squash = new Vector2(1f - 0.05f * k, 1f - 0.07f * k);
                    tilt = -lean * 7f * k;
                }
                else
                {
                    float k = (t - 0.35f) / 0.65f;               // strike, then settle
                    float hit = Mathf.Sin(Mathf.Min(1f, k * 2f) * Mathf.PI * 0.5f) * (1f - k);
                    squash = new Vector2(1f + 0.12f * hit, 1f + 0.04f * hit);
                    tilt = lean * 11f * hit;
                }
                yield return null;
            }
            Done();
        }

        /// <summary>A springy stretch while sliding to a new square.</summary>
        IEnumerator Hop()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.26f)
            {
                float s = Mathf.Sin(t * Mathf.PI);
                squash = new Vector2(1f - 0.07f * s, 1f + 0.13f * s);
                yield return null;
            }
            Done();
        }

        /// <summary>A swell when shielding or healing.</summary>
        IEnumerator Brace()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.32f)
            {
                float s = Mathf.Sin(t * Mathf.PI) * 0.09f;
                squash = new Vector2(1f + s, 1f + s);
                yield return null;
            }
            Done();
        }

        /// <summary>Topples to the side and stays down.</summary>
        IEnumerator Die()
        {
            dead = true;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
            {
                float e = 1f - (1f - t) * (1f - t) * (1f - t);
                tilt = 72f * e;
                squash = new Vector2(1f, 1f - 0.12f * e);
                yield return null;
            }
            tilt = 72f;
            squash = new Vector2(1f, 0.88f);
            current = null;
        }

        void Done()
        {
            squash = Vector2.one;
            tilt = 0f;
            current = null;
        }
    }
}
