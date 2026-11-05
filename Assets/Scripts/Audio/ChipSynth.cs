using System;
using UnityEngine;

namespace Aeternal.Audio
{
    /// <summary>The game's sound effects. Each is synthesized once by ChipSynth.</summary>
    public enum Sfx
    {
        Click, Card, Hit, Hurt, Shield, Dodge, Heal, Move, Miss,
        PlayerTurn, EnemyTurn, Shift, Coin, Victory, Defeat
    }

    /// <summary>The music loops: one per kind of screen.</summary>
    public enum MusicTrack { None, Menu, Map, Battle }

    /// <summary>
    /// Placeholder 8-bit audio generated in code (square, triangle and noise voices), so the
    /// game has sound before real assets exist (brief: 8-bit music and sound, build order 7).
    /// Swap in real clips by giving GameAudio's clip overrides; nothing else changes.
    /// Pure functions of their arguments: the same track always renders the same samples.
    /// </summary>
    public static class ChipSynth
    {
        public const int SampleRate = 22050;

        // ------------------------------------------------------------------
        // Sound effects
        // ------------------------------------------------------------------

        /// <summary>Renders one sound effect. Main thread only (creates an AudioClip).</summary>
        public static AudioClip Effect(Sfx sfx)
        {
            var rng = new System.Random((int)sfx + 1);
            float[] data;
            switch (sfx)
            {
                case Sfx.Click:
                    data = Render(0.05f, t => Square(880f * t) * Decay(t, 0.05f) * 0.22f);
                    break;
                case Sfx.Card:
                    data = Render(0.14f, Filtered(rng, t => Mathf.Lerp(0.6f, 0.08f, t / 0.14f), t => Decay(t, 0.14f) * 0.35f));
                    break;
                case Sfx.Hit:
                    data = Render(0.2f, t =>
                        (Noise(rng) * Decay(t, 0.06f) * 0.5f + Square(Sweep(t, 220f, 70f, 0.2f), 0.5f) * 0.35f) * Decay(t, 0.2f));
                    break;
                case Sfx.Hurt:
                    data = Render(0.28f, t =>
                        (Square(Sweep(t, 160f, 45f, 0.28f), 0.25f) * 0.45f + Noise(rng) * Decay(t, 0.1f) * 0.35f) * Decay(t, 0.28f));
                    break;
                case Sfx.Shield:
                    data = Render(0.22f, t =>
                        (Square(Sweep(t, 440f, 880f, 0.12f), 0.125f) * 0.2f + Triangle(Sweep(t, 880f, 1320f, 0.12f)) * 0.25f) * Decay(t, 0.22f));
                    break;
                case Sfx.Dodge:
                    data = Render(0.2f, Filtered(rng, t => Mathf.Lerp(0.05f, 0.7f, t / 0.2f), t => Mathf.Sin(Mathf.PI * t / 0.2f) * 0.3f));
                    break;
                case Sfx.Heal:
                    data = Arpeggio(new[] { 72, 76, 79, 84 }, 0.07f, 0.3f, triangle: true);
                    break;
                case Sfx.Move:
                    data = Render(0.08f, t => Triangle(Sweep(t, 320f, 200f, 0.08f)) * Decay(t, 0.08f) * 0.3f);
                    break;
                case Sfx.Miss:
                    data = Render(0.22f, Filtered(rng, t => Mathf.Lerp(0.3f, 0.05f, t / 0.22f), t => Mathf.Sin(Mathf.PI * t / 0.22f) * 0.22f));
                    break;
                case Sfx.PlayerTurn:
                    data = Render(0.35f, t => (Triangle(660f * t) * 0.25f + Triangle(990f * t) * 0.12f) * Decay(t, 0.35f));
                    break;
                case Sfx.EnemyTurn:
                    data = Render(0.35f, t => (Square(110f * t, 0.5f) * 0.12f + Square(116.5f * t, 0.5f) * 0.12f) * Decay(t, 0.35f));
                    break;
                case Sfx.Shift:
                    data = Arpeggio(new[] { 67, 71, 74, 79, 83, 86 }, 0.06f, 0.25f, triangle: true, tail: 0.3f);
                    break;
                case Sfx.Coin:
                    data = Arpeggio(new[] { 83, 88 }, 0.06f, 0.2f, triangle: false, tail: 0.1f);
                    break;
                case Sfx.Victory:
                    data = Arpeggio(new[] { 67, 72, 76, 79, 84 }, 0.11f, 0.3f, triangle: false, tail: 0.5f);
                    break;
                case Sfx.Defeat:
                    data = Arpeggio(new[] { 64, 63, 62, 61 }, 0.26f, 0.3f, triangle: true, tail: 0.4f);
                    break;
                default:
                    data = new float[1];
                    break;
            }
            return ToClip("Sfx " + sfx, data);
        }

        /// <summary>Notes (MIDI numbers) one after another; the last one rings for tail seconds.</summary>
        static float[] Arpeggio(int[] notes, float step, float volume, bool triangle, float tail = 0.15f)
        {
            float length = step * (notes.Length - 1) + tail;
            return Render(length, t =>
            {
                int i = Mathf.Min(notes.Length - 1, (int)(t / step));
                float local = t - i * step;
                float span = i == notes.Length - 1 ? tail : step;
                float f = Midi(notes[i]);
                float v = triangle ? Triangle(f * t) : Square(f * t, 0.25f) * 0.6f;
                return v * Decay(local, span * 1.2f) * volume;
            });
        }

        // ------------------------------------------------------------------
        // Music
        // ------------------------------------------------------------------

        struct Song
        {
            public float bpm;
            public int key;          // MIDI note of the tonic
            public int[] chords;     // chord roots in semitones above the key, one per bar
            public bool[] minor;     // minor triad per chord
            public float lead, bass, drums;
            public int seed;
        }

        static Song Describe(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Menu:
                    // A minor, slow: i - VI - III - VII.
                    return new Song { bpm = 76f, key = 57, chords = new[] { 0, 8, 3, 10 }, minor = new[] { true, false, false, false },
                        lead = 0.07f, bass = 0.16f, drums = 0f, seed = 11 };
                case MusicTrack.Map:
                    // D minor, walking: i - IV - i - VII.
                    return new Song { bpm = 96f, key = 50, chords = new[] { 0, 5, 0, 10 }, minor = new[] { true, false, true, false },
                        lead = 0.07f, bass = 0.15f, drums = 0.05f, seed = 23 };
                default:
                    // E minor, driving: i - VI - VII - V.
                    return new Song { bpm = 138f, key = 52, chords = new[] { 0, 8, 10, 7 }, minor = new[] { true, false, false, false },
                        lead = 0.08f, bass = 0.17f, drums = 0.12f, seed = 37 };
            }
        }

        /// <summary>
        /// Renders a seamless loop of eight bars (the progression twice, the second time with a
        /// busier melody). Pure: safe to call off the main thread; turn it into a clip with ToClip.
        /// </summary>
        public static float[] RenderMusic(MusicTrack track)
        {
            var song = Describe(track);
            var rng = new System.Random(song.seed);
            float beat = 60f / song.bpm;
            float eighth = beat * 0.5f;
            int bars = song.chords.Length * 2;
            int eighthsPerBar = 8;
            int total = Mathf.CeilToInt(bars * 4 * beat * SampleRate);
            var data = new float[total];

            // Melody: one choice per eighth note, from the chord or the natural minor scale.
            int[] scale = { 0, 2, 3, 5, 7, 8, 10 };
            var melody = new int[bars * eighthsPerBar];
            int degree = 4;
            for (int i = 0; i < melody.Length; i++)
            {
                bool secondHalf = i >= melody.Length / 2;
                bool rest = rng.NextDouble() < (secondHalf ? 0.3 : 0.5) || (i % 2 == 1 && rng.NextDouble() < 0.5);
                degree = Mathf.Clamp(degree + rng.Next(-2, 3), 0, scale.Length * 2 - 1);
                melody[i] = rest ? -1 : song.key + 12 + scale[degree % scale.Length] + 12 * (degree / scale.Length);
            }

            for (int s = 0; s < total; s++)
            {
                float t = (float)s / SampleRate;
                int e = Mathf.Min(melody.Length - 1, (int)(t / eighth));
                int bar = e / eighthsPerBar;
                int chord = bar % song.chords.Length;
                int root = song.key + song.chords[chord];
                float inEighth = t - e * eighth;
                float sample = 0f;

                // Bass: triangle, root and fifth alternating on eighths, an octave down.
                int bassNote = root - 12 + (e % 2 == 1 ? 7 : 0);
                sample += Triangle(Midi(bassNote) * t) * song.bass * Decay(inEighth, eighth * 1.6f);

                // Arpeggio: chord tones on each eighth, soft square.
                int[] triad = { 0, song.minor[chord] ? 3 : 4, 7, 12 };
                int arpNote = root + triad[e % 4];
                sample += Square(Midi(arpNote) * t, 0.125f) * song.bass * 0.3f * Decay(inEighth, eighth);

                // Lead.
                if (melody[e] >= 0)
                    sample += Square(Midi(melody[e]) * t, 0.25f) * song.lead * Decay(inEighth, eighth * 1.8f);

                // Drums: kick on beats 1 and 3, snare on 2 and 4, hats on eighths.
                if (song.drums > 0f)
                {
                    int eighthInBar = e % eighthsPerBar;
                    if (eighthInBar == 0 || eighthInBar == 4)
                        sample += Mathf.Sin(2f * Mathf.PI * Sweep(inEighth, 140f, 45f, 0.08f)) * song.drums * 1.6f * Decay(inEighth, 0.12f);
                    if (eighthInBar == 2 || eighthInBar == 6)
                        sample += Noise(rng) * song.drums * Decay(inEighth, 0.1f);
                    sample += Noise(rng) * song.drums * 0.25f * Decay(inEighth, 0.025f);
                }

                data[s] = sample / (1f + Mathf.Abs(sample)); // soft clip
            }

            // Fade the last few milliseconds into the first so the loop point doesn't click.
            int fade = Mathf.Min(256, total / 2);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                data[total - fade + i] = Mathf.Lerp(data[total - fade + i], data[i], k);
            }
            return data;
        }

        /// <summary>Wraps rendered samples in a mono AudioClip. Main thread only.</summary>
        public static AudioClip ToClip(string clipName, float[] data)
        {
            var clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------------
        // Voices and envelopes
        // ------------------------------------------------------------------

        static float[] Render(float seconds, Func<float, float> voice)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(voice((float)i / SampleRate), -1f, 1f);
            return data;
        }

        /// <summary>Noise through a one-pole low-pass whose openness (0-1) changes over time.</summary>
        static Func<float, float> Filtered(System.Random rng, Func<float, float> openness, Func<float, float> envelope)
        {
            float state = 0f;
            return t =>
            {
                state += (Noise(rng) - state) * Mathf.Clamp01(openness(t));
                return state * envelope(t) * 2f;
            };
        }

        static float Square(float cycles, float duty = 0.5f) => (cycles - Mathf.Floor(cycles)) < duty ? 1f : -1f;

        static float Triangle(float cycles)
        {
            float p = cycles - Mathf.Floor(cycles);
            return 4f * Mathf.Abs(p - 0.5f) - 1f;
        }

        static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

        static float Decay(float t, float length) => t >= length ? 0f : (1f - t / length) * (1f - t / length);

        /// <summary>
        /// Phase (in cycles) of a tone gliding from one frequency to another over length
        /// seconds, then holding: the integral of the frequency, so the glide has no clicks.
        /// </summary>
        static float Sweep(float t, float from, float to, float length)
        {
            if (t <= length) return from * t + (to - from) * t * t / (2f * length);
            return from * length + (to - from) * length / 2f + to * (t - length);
        }

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);
    }
}
