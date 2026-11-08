using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Aeternal.Combat;
using Aeternal.Stats;
using Aeternal.Status;
using Aeternal.Enemies;

namespace Aeternal.FX
{
    /// <summary>
    /// Combat "juice" layer: floating damage/heal/status numbers, hit flashes, camera shake,
    /// enemy attack lunges, turn banners, deck-shift flashes, and the hit effects (ImpactFX). Purely additive — it only
    /// LISTENS to existing gameplay events, never changes game logic.
    /// Bootstraps itself on game start (no scene wiring) and binds whenever a scene with a
    /// CombatManager loads.
    /// </summary>
    public class CombatFXManager : MonoBehaviour
    {
        public static CombatFXManager Instance { get; private set; }

        // ---- Tunables ----
        const float FLOAT_RISE = 70f;
        const float FLOAT_TIME = 0.9f;
        const float SHAKE_TIME = 0.25f;
        const float SHAKE_MAG = 0.14f;

        static readonly Color DAMAGE_COLOR = new Color(1f, 0.32f, 0.28f);
        static readonly Color DOT_COLOR = new Color(0.85f, 0.3f, 0.75f);
        static readonly Color HEAL_COLOR = new Color(0.4f, 1f, 0.45f);
        static readonly Color SHIELD_COLOR = new Color(0.4f, 0.85f, 1f);
        static readonly Color STATUS_COLOR = new Color(1f, 0.75f, 0.3f);
        static readonly Color MISS_COLOR = new Color(0.8f, 0.8f, 0.8f);

        private Canvas fxCanvas;
        private ImpactFX impact;
        private TextMeshProUGUI banner;

        // Bound combat refs
        private CombatManager combat;
        private HealthComponent playerHealth;
        private HealthComponent enemyHealth;
        private StatusManager playerStatus;
        private StatusManager enemyStatus;
        private EnemyController enemyController;
        private Transform playerVisual;
        private SpriteRenderer playerSprite;
        private SpriteRenderer enemySprite;
        private Color playerBaseColor = Color.white, enemyBaseColor = Color.white;
        private int lastPlayerShield, lastEnemyShield;
        private bool suppressNextShiftBanner;

        private Coroutine shakeRoutine;
        private Vector3 shakeBasePos;
        private Coroutine bannerRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("CombatFXManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CombatFXManager>();
        }

        private void Awake()
        {
            BuildCanvas();
            impact = gameObject.AddComponent<ImpactFX>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Bind();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unbind();
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m) => Bind();

        // ------------------------------------------------------------------
        // Binding to the combat scene
        // ------------------------------------------------------------------
        private void Bind()
        {
            Unbind();
            combat = FindFirstObjectByType<CombatManager>();
            bool inCombat = combat != null;
            if (fxCanvas != null) fxCanvas.enabled = inCombat;
            if (!inCombat) return;

            playerHealth = combat.playerHealth;
            enemyController = combat.enemy;
            enemyHealth = enemyController != null ? enemyController.GetComponent<HealthComponent>() : null;
            playerStatus = playerHealth != null ? playerHealth.GetComponent<StatusManager>() : null;
            enemyStatus = enemyController != null ? enemyController.GetComponent<StatusManager>() : null;

            var vis = combat.playerVisualPositioner;
            playerVisual = vis != null && vis.playerObject != null ? vis.playerObject.transform
                          : (playerHealth != null ? playerHealth.transform : null);
            playerSprite = playerVisual != null ? playerVisual.GetComponentInChildren<SpriteRenderer>() : null;
            enemySprite = enemyController != null ? enemyController.GetComponentInChildren<SpriteRenderer>() : null;
            if (playerSprite != null) playerBaseColor = playerSprite.color;
            if (enemySprite != null) enemyBaseColor = enemySprite.color;
            lastPlayerShield = playerHealth != null ? playerHealth.GetTotalShield() : 0;
            lastEnemyShield = enemyHealth != null ? enemyHealth.GetTotalShield() : 0;

            if (playerHealth != null)
            {
                playerHealth.OnDamageTaken += OnPlayerDamaged;
                playerHealth.OnHealed += OnPlayerHealed;
                playerHealth.OnDodged += OnPlayerDodged;
                playerHealth.OnShieldChanged += OnPlayerShieldChanged;
                playerHealth.OnDeathLocal += OnPlayerDied;
            }
            if (enemyHealth != null)
            {
                enemyHealth.OnDamageTaken += OnEnemyDamaged;
                enemyHealth.OnHealed += OnEnemyHealed;
                enemyHealth.OnShieldChanged += OnEnemyShieldChanged;
                enemyHealth.OnDeathLocal += OnEnemyDied;
            }
            if (playerStatus != null) playerStatus.OnStatusApplied += OnPlayerStatusApplied;
            if (enemyStatus != null) enemyStatus.OnStatusApplied += OnEnemyStatusApplied;
            if (enemyController != null) enemyController.OnAttackExecuted += OnEnemyAttackExecuted;

            if (combat.turnManager != null)
            {
                combat.turnManager.OnPlayerPhaseStart += OnPlayerPhase;
                combat.turnManager.OnEnemyPhaseStart += OnEnemyPhase;
            }

            suppressNextShiftBanner = true; // combat init fires OnDeckShifted once
            combat.OnDeckShifted += OnDeckShifted;
        }

        private void Unbind()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamageTaken -= OnPlayerDamaged;
                playerHealth.OnHealed -= OnPlayerHealed;
                playerHealth.OnDodged -= OnPlayerDodged;
                playerHealth.OnShieldChanged -= OnPlayerShieldChanged;
                playerHealth.OnDeathLocal -= OnPlayerDied;
            }
            if (enemyHealth != null)
            {
                enemyHealth.OnDamageTaken -= OnEnemyDamaged;
                enemyHealth.OnHealed -= OnEnemyHealed;
                enemyHealth.OnShieldChanged -= OnEnemyShieldChanged;
                enemyHealth.OnDeathLocal -= OnEnemyDied;
            }
            if (playerStatus != null) playerStatus.OnStatusApplied -= OnPlayerStatusApplied;
            if (enemyStatus != null) enemyStatus.OnStatusApplied -= OnEnemyStatusApplied;
            if (enemyController != null) enemyController.OnAttackExecuted -= OnEnemyAttackExecuted;
            if (combat != null)
            {
                combat.OnDeckShifted -= OnDeckShifted;
                if (combat.turnManager != null)
                {
                    combat.turnManager.OnPlayerPhaseStart -= OnPlayerPhase;
                    combat.turnManager.OnEnemyPhaseStart -= OnEnemyPhase;
                }
            }
            combat = null; playerHealth = null; enemyHealth = null;
            playerStatus = null; enemyStatus = null; enemyController = null;
            playerVisual = null; playerSprite = null; enemySprite = null;
        }

        // ------------------------------------------------------------------
        // Event handlers
        // ------------------------------------------------------------------
        private void OnPlayerDamaged(DamageInfo info)
        {
            SpawnFloatAtWorld(PlayerPos(), info.isDamageOverTime ? $"-{info.amount} {DotName(info)}" : $"-{info.amount}",
                info.isDamageOverTime ? DOT_COLOR : DAMAGE_COLOR, 34);
            Flash(playerSprite, playerBaseColor);
            if (info.isDamageOverTime) { impact.Bleed(playerSprite); return; }
            impact.Hit(playerSprite, EnemyPos(), info.type, info.amount);
            Shake(impact.IsBigHit(info.amount) ? impact.BigHitShake : SHAKE_MAG);
        }

        private void OnEnemyDamaged(DamageInfo info)
        {
            SpawnFloatAtWorld(EnemyPos(), info.isDamageOverTime ? $"-{info.amount} {DotName(info)}" : $"-{info.amount}",
                info.isDamageOverTime ? DOT_COLOR : DAMAGE_COLOR, 38);
            Flash(enemySprite, enemyController != null ? enemyController.SpriteTint : enemyBaseColor);
            if (info.isDamageOverTime)
            {
                if (info.type == Aeternal.Cards.DamageType.Water) impact.Hit(enemySprite, EnemyPos() + Vector3.up, info.type, info.amount);
                else impact.Bleed(enemySprite);
                return;
            }
            impact.Hit(enemySprite, PlayerPos(), info.type, info.amount);
            if (impact.IsBigHit(info.amount)) Shake(impact.BigHitShake);
        }

        /// <summary>Label of damage over time: the water cage's is water, everything else is Bleed.</summary>
        private static string DotName(DamageInfo info) => info.type == Aeternal.Cards.DamageType.Water ? "Cage" : "Bleed";

        private void OnPlayerHealed(int amount)
        {
            SpawnFloatAtWorld(PlayerPos(), $"+{amount}", HEAL_COLOR, 32);
            impact.Heal(playerSprite);
        }

        private void OnEnemyHealed(int amount)
        {
            SpawnFloatAtWorld(EnemyPos(), $"+{amount}", HEAL_COLOR, 32);
            impact.Heal(enemySprite);
        }

        private void OnPlayerDied() => impact.Death(playerSprite);
        private void OnEnemyDied() => impact.Death(enemySprite);
        private void OnPlayerDodged() => SpawnFloatAtWorld(PlayerPos(), "DODGED!", SHIELD_COLOR, 36);

        private void OnPlayerShieldChanged(int total)
        {
            int diff = total - lastPlayerShield;
            lastPlayerShield = total;
            if (diff <= 0) return;
            SpawnFloatAtWorld(PlayerPos(), $"+{diff} Shield", SHIELD_COLOR, 30);
            impact.Shield(playerSprite);
        }

        private void OnEnemyShieldChanged(int total)
        {
            int diff = total - lastEnemyShield;
            lastEnemyShield = total;
            if (diff <= 0) return;
            SpawnFloatAtWorld(EnemyPos(), $"+{diff} Shield", SHIELD_COLOR, 30);
            impact.Shield(enemySprite);
        }

        private void OnPlayerStatusApplied(string text) => SpawnFloatAtWorld(PlayerPos(), text, STATUS_COLOR, 30);
        private void OnEnemyStatusApplied(string text) => SpawnFloatAtWorld(EnemyPos(), text, STATUS_COLOR, 30);

        private void OnEnemyAttackExecuted(bool hit)
        {
            Lunge();
            if (!hit) SpawnFloatAtWorld(PlayerPos(), "Missed!", MISS_COLOR, 30);
        }

        private void OnPlayerPhase() => ShowBanner("YOUR TURN", new Color(1f, 0.9f, 0.5f));
        private void OnEnemyPhase() => ShowBanner("ENEMY TURN", new Color(1f, 0.45f, 0.4f));

        private void OnDeckShifted(string deckName)
        {
            if (suppressNextShiftBanner) { suppressNextShiftBanner = false; return; }
            ShowBanner($"SHIFTED TO {deckName.ToUpperInvariant()}", new Color(0.75f, 0.6f, 1f));
            StartCoroutine(ScreenFlash(new Color(1f, 1f, 1f, 0.35f), 0.25f));
        }

        /// <summary>Called by CardUI when a card is played: a ghost of the card flies at the enemy.</summary>
        public void NotifyCardPlayed(Sprite artwork, Vector3 cardScreenPos)
        {
            if (fxCanvas == null) return;
            StartCoroutine(CardGhost(artwork, cardScreenPos));
        }

        // ------------------------------------------------------------------
        // Effects
        // ------------------------------------------------------------------
        /// <summary>Floating text rising from a world position (e.g. "IMMUNE" over an enemy).</summary>
        public void FloatAt(Vector3 worldPos, string text, Color color) => SpawnFloatAtWorld(worldPos, text, color, 32);

        private Vector3 PlayerPos() => playerVisual != null ? playerVisual.position : Vector3.zero;
        private Vector3 EnemyPos() => enemyController != null ? enemyController.transform.position : Vector3.zero;

        private void SpawnFloatAtWorld(Vector3 worldPos, string text, Color color, float fontSize)
        {
            if (fxCanvas == null || Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(worldPos);
            screen.x += Random.Range(-18f, 18f);
            screen.y += 30f;
            StartCoroutine(FloatText(screen, text, color, fontSize));
        }

        private IEnumerator FloatText(Vector3 screenPos, string text, Color color, float fontSize)
        {
            var go = new GameObject("FloatText", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(fxCanvas.transform, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = fontSize; t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center; t.color = color;
            t.raycastTarget = false;
            t.outlineWidth = 0.2f; t.outlineColor = new Color32(0, 0, 0, 220);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 60);
            rt.position = screenPos;

            float elapsed = 0f;
            Vector3 start = screenPos;
            while (elapsed < FLOAT_TIME && go != null)
            {
                elapsed += Time.deltaTime;
                float k = elapsed / FLOAT_TIME;
                rt.position = start + new Vector3(0, FLOAT_RISE * k, 0);
                t.alpha = 1f - Mathf.SmoothStep(0.2f, 1f, k);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        private void Flash(SpriteRenderer sr, Color baseColor)
        {
            if (sr != null) StartCoroutine(FlashRoutine(sr, baseColor));
        }

        private IEnumerator FlashRoutine(SpriteRenderer sr, Color baseColor)
        {
            sr.color = new Color(1f, 0.35f, 0.3f, 1f);
            yield return new WaitForSeconds(0.1f);
            if (sr != null) sr.color = baseColor;
        }

        private void Shake(float magnitude)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (shakeRoutine != null) { StopCoroutine(shakeRoutine); cam.transform.localPosition = shakeBasePos; }
            shakeRoutine = StartCoroutine(ShakeRoutine(cam, magnitude));
        }

        private IEnumerator ShakeRoutine(Camera cam, float magnitude)
        {
            shakeBasePos = cam.transform.localPosition;
            float elapsed = 0f;
            while (elapsed < SHAKE_TIME && cam != null)
            {
                elapsed += Time.deltaTime;
                float damper = 1f - elapsed / SHAKE_TIME;
                cam.transform.localPosition = shakeBasePos + (Vector3)(Random.insideUnitCircle * magnitude * damper);
                yield return null;
            }
            if (cam != null) cam.transform.localPosition = shakeBasePos;
            shakeRoutine = null;
        }

        // Squares around the enemy, by EnemyController direction index (0=Up, 1=Right, 2=Down, 3=Left).
        private static readonly Vector3[] AttackDirections = { Vector3.up, Vector3.right, Vector3.down, Vector3.left };
        private const float SquareSpacing = 2f;

        /// <summary>
        /// The enemy lunges toward the square(s) it telegraphed, not toward wherever the player
        /// moved to: a dodged attack visibly swings at the empty square. A multi-square attack
        /// lunges toward the middle of its squares; if they cancel out (e.g. up and down), it hops.
        /// </summary>
        private void Lunge()
        {
            if (enemyController == null) return;
            var enemy = enemyController.transform;

            Vector3 direction = Vector3.zero;
            foreach (int d in enemyController.GetAttackDirections())
            {
                if (d >= 0 && d < AttackDirections.Length) direction += AttackDirections[d];
            }
            Vector3 target = direction.sqrMagnitude > 0.01f
                ? enemy.position + direction.normalized * SquareSpacing
                : enemy.position + Vector3.up * (SquareSpacing * 0.5f);
            StartCoroutine(LungeRoutine(enemy, target));
        }

        private IEnumerator LungeRoutine(Transform enemy, Vector3 target)
        {
            Vector3 start = enemy.position;
            Vector3 mid = Vector3.Lerp(start, target, 0.35f);
            float t = 0f;
            while (t < 1f && enemy != null)
            {
                t += Time.deltaTime / 0.09f;
                enemy.position = Vector3.Lerp(start, mid, Mathf.Clamp01(t));
                yield return null;
            }
            t = 0f;
            while (t < 1f && enemy != null)
            {
                t += Time.deltaTime / 0.16f;
                enemy.position = Vector3.Lerp(mid, start, Mathf.Clamp01(t));
                yield return null;
            }
            if (enemy != null) enemy.position = start;
        }

        private void ShowBanner(string text, Color color)
        {
            if (banner == null) return;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(text, color));
        }

        private IEnumerator BannerRoutine(string text, Color color)
        {
            banner.text = text;
            banner.color = color;
            float t = 0f;
            while (t < 1f) { t += Time.deltaTime / 0.15f; banner.alpha = Mathf.Clamp01(t); yield return null; }
            yield return new WaitForSeconds(0.55f);
            t = 0f;
            while (t < 1f) { t += Time.deltaTime / 0.25f; banner.alpha = 1f - Mathf.Clamp01(t); yield return null; }
            banner.alpha = 0f;
            bannerRoutine = null;
        }

        private IEnumerator ScreenFlash(Color color, float duration)
        {
            if (fxCanvas == null) yield break;
            var go = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(fxCanvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            float t = 0f;
            while (t < 1f && go != null)
            {
                t += Time.deltaTime / duration;
                img.color = new Color(color.r, color.g, color.b, color.a * (1f - Mathf.Clamp01(t)));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        private IEnumerator CardGhost(Sprite artwork, Vector3 startScreen)
        {
            var go = new GameObject("CardGhost", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(fxCanvas.transform, false);
            var img = go.GetComponent<Image>();
            img.sprite = artwork;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 168);
            rt.position = startScreen;

            Vector3 targetScreen = Camera.main != null
                ? Camera.main.WorldToScreenPoint(EnemyPos())
                : startScreen + new Vector3(0, 250, 0);

            float t = 0f;
            while (t < 1f && go != null)
            {
                t += Time.deltaTime / 0.4f;
                float k = Mathf.Clamp01(t);
                float eased = 1f - (1f - k) * (1f - k);
                rt.position = Vector3.Lerp(startScreen, targetScreen, eased);
                rt.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
                img.color = new Color(1f, 1f, 1f, 1f - k * k);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ------------------------------------------------------------------
        // Canvas
        // ------------------------------------------------------------------
        private void BuildCanvas()
        {
            var cgo = new GameObject("CombatFXCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            fxCanvas = cgo.GetComponent<Canvas>();
            fxCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fxCanvas.sortingOrder = 200; // above all game UI

            var bgo = new GameObject("TurnBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
            bgo.transform.SetParent(cgo.transform, false);
            banner = bgo.GetComponent<TextMeshProUGUI>();
            banner.fontSize = 64; banner.fontStyle = FontStyles.Bold;
            banner.alignment = TextAlignmentOptions.Center;
            banner.raycastTarget = false;
            banner.outlineWidth = 0.22f; banner.outlineColor = new Color32(0, 0, 0, 230);
            banner.alpha = 0f;
            var brt = bgo.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.72f);
            brt.sizeDelta = new Vector2(1200, 100);
        }
    }
}
