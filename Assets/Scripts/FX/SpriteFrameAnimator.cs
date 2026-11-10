using UnityEngine;

namespace Aeternal.FX
{
    /// <summary>
    /// Plays a SpriteFrameSet on a SpriteRenderer by swapping its sprite every frame of the game
    /// loop: the idle loops, and each action (move, attack, hit, guard) plays once and then
    /// returns to the idle. CharacterAnimator starts the actions from gameplay events.
    /// </summary>
    public class SpriteFrameAnimator : MonoBehaviour
    {
        /// <summary>The animations a frame set can hold.</summary>
        public enum State { Idle, Move, Attack, Hit, Guard }

        SpriteRenderer body;
        SpriteFrameSet set;
        Sprite[] frames;     // the animation playing now
        State state;
        int index;           // current frame in `frames`
        float timer;         // seconds spent on the current frame
        bool held;           // stopped on its current frame (death)
        bool baseFlip;       // the renderer's flip before an attack turned it

        /// <summary>The state playing now.</summary>
        public State Current => state;

        /// <summary>Adds an animator to the renderer's object and starts the idle loop.</summary>
        public static SpriteFrameAnimator Attach(SpriteRenderer body, SpriteFrameSet set)
        {
            var anim = body.gameObject.AddComponent<SpriteFrameAnimator>();
            anim.body = body;
            anim.set = set;
            anim.baseFlip = body.flipX;
            anim.Play(State.Idle);
            return anim;
        }

        /// <summary>
        /// Starts an animation from its first frame. faceRight: for an attack, mirror the art so
        /// the blow goes to the right. A state with no frames falls back to the idle.
        /// </summary>
        public void Play(State next, bool faceRight = false)
        {
            if (held) return;
            var list = FramesFor(next);
            if (list == null || list.Length == 0)
            {
                next = State.Idle;
                list = set.idle;
            }
            state = next;
            frames = list;
            index = 0;
            timer = 0f;
            // Only an attack turns the art; every other state shows it the usual way round.
            body.flipX = next == State.Attack && set.artFacesLeft == faceRight ? !baseFlip : baseFlip;
            body.sprite = frames[0];
        }

        /// <summary>Shows the last frame of an animation and stops there (used when the character dies).</summary>
        public void HoldLastFrame(State pose)
        {
            Play(pose);
            body.sprite = frames[frames.Length - 1];
            held = true;
        }

        Sprite[] FramesFor(State s)
        {
            switch (s)
            {
                case State.Move: return set.move;
                case State.Attack: return set.attack;
                case State.Hit: return set.hit;
                case State.Guard: return set.guard;
                default: return set.idle;
            }
        }

        void Update()
        {
            if (held || frames == null || frames.Length == 0) return;

            float fps = state == State.Idle ? set.idleFramesPerSecond : set.actionFramesPerSecond;
            timer += Time.deltaTime;
            float frameTime = 1f / Mathf.Max(1f, fps);
            while (timer >= frameTime)
            {
                timer -= frameTime;
                index++;
                if (index < frames.Length) continue;
                // End of the animation: the idle loops, an action hands back to the idle.
                if (state == State.Idle) index = 0;
                else { Play(State.Idle); return; }
            }
            body.sprite = frames[index];
        }
    }
}
