using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Aeternal.Combat;
using Aeternal.Core;
using Aeternal.Decks;
using Aeternal.Enemies;
using Aeternal.Player;
using Aeternal.Stats;

namespace Aeternal.Audio
{
    /// <summary>
    /// Music and sound effects. Spawns itself on game start and lives across scenes; needs no
    /// scene wiring. It is the game's audio event system: it listens to the existing game events
    /// (damage, shield, dodge, moves, cards leaving the hand, turns, Shift, combat end, gold) and
    /// to clicks on any interactable UI Selectable, so no gameplay or UI script plays a sound
    /// itself. Music follows the scene (menu, map, battle) and crossfades.
    /// Volume is the settings' master volume (AudioListener.volume) times the levels below.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const float MusicLevel = 0.7f;
        const float SfxLevel = 0.8f;
        const float CrossfadeSeconds = 1.2f;
        // The same effect retriggered faster than this (the hand discarding at end of turn) plays once.
        const float RetriggerSeconds = 0.06f;

        static GameAudio instance;

        readonly Dictionary<Sfx, AudioClip> effects = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        readonly Dictionary<MusicTrack, AudioClip> music = new Dictionary<MusicTrack, AudioClip>();
        readonly Dictionary<MusicTrack, Task<float[]>> rendering = new Dictionary<MusicTrack, Task<float[]>>();
        AudioSource sfxSource;
        AudioSource[] musicSources;
        int activeMusic;
        MusicTrack wantedTrack = MusicTrack.None;
        MusicTrack playingTrack = MusicTrack.None;
        float fade = 1f;

        // Combat bindings.
        CombatManager combat;
        HealthComponent playerHealth, enemyHealth;
        EnemyController enemy;
        PlayerPositioningComponent playerPos;
        DeckManager deck;
        int lastGold = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("Game Audio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameAudio>();
        }

        /// <summary>Plays a sound effect (for code with no event to listen to).</summary>
        public static void Play(Sfx sfx)
        {
            if (instance != null) instance.PlayEffect(sfx);
        }

        void Awake()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            musicSources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.volume = 0f;
                musicSources[i] = source;
            }
            // Render all music in the background now, so a scene change never waits for it.
            foreach (MusicTrack track in System.Enum.GetValues(typeof(MusicTrack)))
            {
                if (track == MusicTrack.None) continue;
                var t = track;
                rendering[track] = Task.Run(() => ChipSynth.RenderMusic(t));
            }
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unbind();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Bind();
            if (combat != null) wantedTrack = MusicTrack.Battle;
            else if (scene.name.Contains("Map")) wantedTrack = MusicTrack.Map;
            else wantedTrack = MusicTrack.Menu;
        }

        void Update()
        {
            UpdateMusic();
            if (Input.GetMouseButtonDown(0) && PointerOnInteractable()) PlayEffect(Sfx.Click);

            var run = RunManager.Instance;
            int gold = run != null && run.HasActiveRun ? run.Gold : -1;
            if (lastGold >= 0 && gold >= 0 && gold != lastGold) PlayEffect(Sfx.Coin);
            lastGold = gold;
        }

        void PlayEffect(Sfx sfx)
        {
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(sfx, out float last) && now - last < RetriggerSeconds) return;
            lastPlayed[sfx] = now;
            if (!effects.TryGetValue(sfx, out var clip))
            {
                clip = ChipSynth.Effect(sfx);
                effects[sfx] = clip;
            }
            sfxSource.PlayOneShot(clip, SfxLevel);
        }

        // ------------------------------------------------------------------
        // Music
        // ------------------------------------------------------------------

        void UpdateMusic()
        {
            if (wantedTrack != playingTrack)
            {
                var clip = wantedTrack == MusicTrack.None ? null : Clip(wantedTrack);
                if (wantedTrack != MusicTrack.None && clip == null) { /* still rendering */ }
                else
                {
                    activeMusic = 1 - activeMusic;
                    var next = musicSources[activeMusic];
                    next.clip = clip;
                    next.volume = 0f;
                    if (clip != null) next.Play();
                    playingTrack = wantedTrack;
                    fade = 0f;
                }
            }

            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / CrossfadeSeconds);
            musicSources[activeMusic].volume = fade * MusicLevel;
            var previous = musicSources[1 - activeMusic];
            previous.volume = (1f - fade) * MusicLevel;
            if (fade >= 1f && previous.isPlaying) previous.Stop();
        }

        AudioClip Clip(MusicTrack track)
        {
            if (music.TryGetValue(track, out var clip)) return clip;
            if (!rendering.TryGetValue(track, out var task) || !task.IsCompleted) return null;
            if (task.IsFaulted)
            {
                Debug.LogWarning($"[GameAudio] Could not render {track} music: {task.Exception?.GetBaseException().Message}");
                rendering.Remove(track);
                return null;
            }
            clip = ChipSynth.ToClip("Music " + track, task.Result);
            music[track] = clip;
            return clip;
        }

        // ------------------------------------------------------------------
        // Combat events
        // ------------------------------------------------------------------

        void Bind()
        {
            Unbind();
            combat = FindFirstObjectByType<CombatManager>();
            if (combat == null) return;
            playerHealth = combat.playerHealth;
            enemy = combat.enemy;
            enemyHealth = enemy != null ? enemy.GetComponent<HealthComponent>() : null;
            playerPos = playerHealth != null ? playerHealth.GetComponent<PlayerPositioningComponent>() : null;
            deck = combat.deckManager;
            lastShield = playerHealth != null ? playerHealth.GetTotalShield() : 0;

            if (playerHealth != null)
            {
                playerHealth.OnDamageTaken += OnPlayerDamaged;
                playerHealth.OnHealed += OnHealed;
                playerHealth.OnDodged += OnDodged;
                playerHealth.OnShieldChanged += OnPlayerShield;
            }
            if (enemyHealth != null)
            {
                enemyHealth.OnDamageTaken += OnEnemyDamaged;
                enemyHealth.OnHealed += OnHealed;
            }
            if (enemy != null) enemy.OnAttackExecuted += OnEnemyAttacked;
            if (playerPos != null) playerPos.OnFacingChanged += OnMoved;
            if (deck != null) deck.OnCardRemovedFromHand += OnCardPlayed;
            if (combat.turnManager != null)
            {
                combat.turnManager.OnPlayerPhaseStart += OnPlayerPhase;
                combat.turnManager.OnEnemyPhaseStart += OnEnemyPhase;
            }
            combat.OnDeckShifted += OnShifted;
            combat.OnCombatEnded += OnCombatEnded;
            ignoreNextShift = true; // combat init fires OnDeckShifted once
        }

        void Unbind()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamageTaken -= OnPlayerDamaged;
                playerHealth.OnHealed -= OnHealed;
                playerHealth.OnDodged -= OnDodged;
                playerHealth.OnShieldChanged -= OnPlayerShield;
            }
            if (enemyHealth != null)
            {
                enemyHealth.OnDamageTaken -= OnEnemyDamaged;
                enemyHealth.OnHealed -= OnHealed;
            }
            if (enemy != null) enemy.OnAttackExecuted -= OnEnemyAttacked;
            if (playerPos != null) playerPos.OnFacingChanged -= OnMoved;
            if (deck != null) deck.OnCardRemovedFromHand -= OnCardPlayed;
            if (combat != null)
            {
                if (combat.turnManager != null)
                {
                    combat.turnManager.OnPlayerPhaseStart -= OnPlayerPhase;
                    combat.turnManager.OnEnemyPhaseStart -= OnEnemyPhase;
                }
                combat.OnDeckShifted -= OnShifted;
                combat.OnCombatEnded -= OnCombatEnded;
            }
            combat = null; playerHealth = null; enemyHealth = null; enemy = null; playerPos = null; deck = null;
        }

        bool ignoreNextShift;
        int lastShield;

        void OnPlayerDamaged(DamageInfo info) => PlayEffect(Sfx.Hurt);
        void OnEnemyDamaged(DamageInfo info) => PlayEffect(Sfx.Hit);
        void OnHealed(int amount) { if (amount > 0) PlayEffect(Sfx.Heal); }
        void OnDodged() => PlayEffect(Sfx.Dodge);
        void OnMoved(int facing) => PlayEffect(Sfx.Move);
        void OnCardPlayed(Aeternal.Cards.CardData card) => PlayEffect(Sfx.Card);
        void OnPlayerPhase() => PlayEffect(Sfx.PlayerTurn);
        void OnEnemyPhase() => PlayEffect(Sfx.EnemyTurn);
        void OnEnemyAttacked(bool hit) { if (!hit) PlayEffect(Sfx.Miss); }

        void OnPlayerShield(int total)
        {
            if (total > lastShield) PlayEffect(Sfx.Shield);
            lastShield = total;
        }

        void OnShifted(string deckName)
        {
            if (ignoreNextShift) { ignoreNextShift = false; return; }
            PlayEffect(Sfx.Shift);
        }

        void OnCombatEnded(bool playerWon)
        {
            PlayEffect(playerWon ? Sfx.Victory : Sfx.Defeat);
            // Let the jingle ring out over silence; the next scene starts its own music.
            wantedTrack = MusicTrack.None;
        }

        // ------------------------------------------------------------------
        // UI clicks
        // ------------------------------------------------------------------

        static readonly List<RaycastResult> hits = new List<RaycastResult>();

        static bool PointerOnInteractable()
        {
            var events = EventSystem.current;
            if (events == null) return false;
            var pointer = new PointerEventData(events) { position = Input.mousePosition };
            hits.Clear();
            events.RaycastAll(pointer, hits);
            if (hits.Count == 0) return false;
            var selectable = hits[0].gameObject.GetComponentInParent<Selectable>();
            return selectable != null && selectable.IsInteractable();
        }
    }
}
