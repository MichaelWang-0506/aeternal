using UnityEngine;

namespace Aeternal.FX
{
    /// <summary>
    /// A character's frame-by-frame animations: one list of sprites per state, played in order
    /// by SpriteFrameAnimator. The hero's set (Scriptable Objects/Hero Frames) is generated from
    /// protagonist.png by tools/content/gen_hero_frames.py.
    /// </summary>
    [CreateAssetMenu(menuName = "Aeternal/Sprite Frame Set")]
    public class SpriteFrameSet : ScriptableObject
    {
        [Tooltip("Speed of the looping idle (breathing, cape stirring).")]
        public float idleFramesPerSecond = 5f;
        [Tooltip("Speed of the one-shot actions: move, attack, hit, guard.")]
        public float actionFramesPerSecond = 18f;
        [Tooltip("The art punches toward the left of the image; flipped when the target is on the right.")]
        public bool artFacesLeft = true;

        [Tooltip("Loops while nothing else plays.")]
        public Sprite[] idle;
        [Tooltip("A hop to another square.")]
        public Sprite[] move;
        [Tooltip("Playing an attack card.")]
        public Sprite[] attack;
        [Tooltip("Taking damage.")]
        public Sprite[] hit;
        [Tooltip("Playing a shield or heal card.")]
        public Sprite[] guard;

        /// <summary>True when the set has at least an idle loop to fall back to.</summary>
        public bool IsUsable => idle != null && idle.Length > 0;
    }
}
