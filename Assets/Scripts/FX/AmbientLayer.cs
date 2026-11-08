using UnityEngine;
using UnityEngine.UI;

namespace Aeternal.FX
{
    /// <summary>
    /// Brings a still background to life: smoke drifting across the city and embers rising
    /// from it (under the sea: murky haze and rising bubbles), and in stormy encounters dark clouds rolling along the top and lightning
    /// flashing now and then. Can also sway the background a little with the mouse. Sits just
    /// above the background art and below everything else; ignores clicks. Purely visual;
    /// every value comes from BattlefieldStyle ("Ambience").
    /// </summary>
    public class AmbientLayer : MonoBehaviour
    {
        struct Mote
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float age, life, size, maxAlpha, wobble;
        }

        BattlefieldStyle style;
        bool storm, underwater;
        RectTransform area;
        Mote[] smoke, embers, clouds;
        Image flash;
        float nextFlash, flashAge = -1f;

        Transform parallaxTarget;
        Vector3 parallaxBase;
        float parallaxAmount;
        Vector2 sway;

        /// <summary>
        /// Adds the layer to a UI background, right after <paramref name="art"/> among its
        /// siblings so it draws over the art and under whatever follows. The art sways with the
        /// mouse when <paramref name="parallax"/> is true.
        /// </summary>
        public static AmbientLayer AddToUI(RectTransform art, bool stormy, bool parallax, bool underwater = false)
        {
            if (art == null || art.parent == null) return null;
            var go = new GameObject("Ambience", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(art.parent, false);
            rect.SetSiblingIndex(art.GetSiblingIndex() + 1);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var layer = go.AddComponent<AmbientLayer>();
            layer.Init(stormy, underwater, parallax ? art : null, layer.style.parallaxPixels);
            return layer;
        }

        /// <summary>
        /// Adds the layer as the first child of <paramref name="background"/>: over its own
        /// image, under everything inside it.
        /// </summary>
        public static AmbientLayer AddInside(RectTransform background, bool stormy)
        {
            if (background == null) return null;
            var go = new GameObject("Ambience", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(background, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var layer = go.AddComponent<AmbientLayer>();
            layer.Init(stormy, false, null, 0f);
            return layer;
        }

        /// <summary>
        /// Adds the layer behind a world-space scene: a canvas on <paramref name="cam"/> drawn
        /// at <paramref name="sortingOrder"/> (between the background sprite and the rest).
        /// <paramref name="background"/> sways with the mouse when given.
        /// </summary>
        public static AmbientLayer AddToCamera(Camera cam, int sortingLayerId, int sortingOrder, Transform background, bool stormy,
            bool underwater = false)
        {
            if (cam == null) return null;
            var go = new GameObject("Ambience", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingLayerID = sortingLayerId;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var layer = go.AddComponent<AmbientLayer>();
            // Pixels to world units, so the backdrop sways as far as a UI one would.
            float worldPerPixel = cam.orthographic ? cam.orthographicSize * 2f / 1080f : 0.01f;
            layer.Init(stormy, underwater, background, layer.style.parallaxPixels * worldPerPixel);
            return layer;
        }

        void Awake()
        {
            style = BattlefieldStyle.Load();
            area = (RectTransform)transform;
        }

        void Init(bool stormy, bool sea, Transform swayTarget, float swayAmount)
        {
            storm = stormy;
            underwater = sea;
            if (underwater)
            {
                // First, so the haze and bubbles draw over it.
                var wash = new GameObject("Sea Wash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                wash.rectTransform.SetParent(area, false);
                wash.rectTransform.anchorMin = Vector2.zero; wash.rectTransform.anchorMax = Vector2.one;
                wash.rectTransform.offsetMin = wash.rectTransform.offsetMax = Vector2.zero;
                wash.color = style.underwaterWashColor;
                wash.raycastTarget = false;
            }
            smoke = Spawn(underwater ? "Haze" : "Smoke", style.smokeCount, ProceduralSprites.SoftEllipse());
            embers = underwater
                ? Spawn("Bubble", style.bubbleCount, ProceduralSprites.CageRing())
                : Spawn("Ember", style.emberCount, null);
            if (storm)
            {
                clouds = Spawn("Storm Cloud", style.stormCloudCount, ProceduralSprites.SoftEllipse());
                flash = new GameObject("Lightning Flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                flash.rectTransform.SetParent(area, false);
                flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one;
                flash.rectTransform.offsetMin = flash.rectTransform.offsetMax = Vector2.zero;
                flash.raycastTarget = false;
                flash.color = Color.clear;
                nextFlash = Random.Range(1f, style.stormFlashInterval.y);
            }

            parallaxTarget = swayTarget;
            parallaxAmount = swayAmount;
            if (parallaxTarget != null)
            {
                // Grow the art a little so swaying never shows its edge.
                parallaxBase = parallaxTarget is RectTransform r ? (Vector3)r.anchoredPosition : parallaxTarget.localPosition;
                parallaxTarget.localScale *= 1f + style.parallaxPixels * 2.5f / 1080f;
            }

            // Start mid-life so the screen is already full of smoke and embers.
            Seed(smoke, NewSmoke);
            Seed(embers, NewEmber);
            if (clouds != null) Seed(clouds, NewCloud);
        }

        void OnDestroy()
        {
            if (parallaxTarget == null) return;
            if (parallaxTarget is RectTransform r) r.anchoredPosition = parallaxBase;
            else parallaxTarget.localPosition = parallaxBase;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 size = area.rect.size;
            Step(smoke, NewSmoke, dt, size, false);
            Step(embers, NewEmber, dt, size, true);
            if (clouds != null) Step(clouds, NewCloud, dt, size, false);
            if (storm) Lightning(dt);
            Sway();
        }

        // ------------------------------------------------------------------
        // Motes: smoke puffs, embers, storm clouds
        // ------------------------------------------------------------------
        Mote[] Spawn(string name, int count, Sprite sprite)
        {
            var motes = new Mote[Mathf.Max(0, count)];
            for (int i = 0; i < motes.Length; i++)
            {
                var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(area, false);
                image.sprite = sprite;
                image.raycastTarget = false;
                motes[i] = new Mote { rect = image.rectTransform, image = image };
            }
            return motes;
        }

        delegate void Respawn(ref Mote mote, Vector2 area);

        void Seed(Mote[] motes, Respawn respawn)
        {
            Vector2 size = area.rect.size;
            if (size.x < 1f) size = new Vector2(1920f, 1080f);
            for (int i = 0; i < motes.Length; i++)
            {
                respawn(ref motes[i], size);
                motes[i].age = Random.Range(0f, motes[i].life);
                motes[i].rect.anchoredPosition += motes[i].velocity * motes[i].age;
            }
        }

        void Step(Mote[] motes, Respawn respawn, float dt, Vector2 size, bool twinkle)
        {
            for (int i = 0; i < motes.Length; i++)
            {
                ref var m = ref motes[i];
                m.age += dt;
                if (m.age >= m.life) { respawn(ref m, size); continue; }
                float k = m.age / m.life;
                var pos = m.rect.anchoredPosition + m.velocity * dt;
                if (m.wobble > 0f) pos.x += Mathf.Sin(m.age * 2.3f + i) * m.wobble * dt;
                m.rect.anchoredPosition = pos;
                float grow = Mathf.Lerp(1f, 1.6f, k);
                m.rect.sizeDelta = new Vector2(m.size * grow, m.size * grow * (twinkle ? 1f : 0.6f));
                float alpha = m.maxAlpha * Mathf.Sin(k * Mathf.PI);
                if (twinkle) alpha *= 0.6f + 0.4f * Mathf.Sin(m.age * 9f + i * 1.7f);
                var c = m.image.color; c.a = Mathf.Max(0f, alpha); m.image.color = c;
            }
        }

        // Positions are relative to the layer's centre, in canvas units.
        void NewSmoke(ref Mote m, Vector2 size)
        {
            m.age = 0f;
            m.life = Random.Range(14f, 24f);
            m.size = Random.Range(260f, 560f);
            var smokeColor = underwater ? style.underwaterHazeColor : style.smokeColor;
            m.maxAlpha = Random.Range(0.5f, 1f) * smokeColor.a;
            m.velocity = new Vector2(Random.Range(12f, 32f), Random.Range(3f, 12f));
            m.wobble = 0f;
            m.rect.anchoredPosition = new Vector2(Random.Range(-0.6f, 0.45f) * size.x, Random.Range(-0.45f, 0.25f) * size.y);
            m.image.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, 0f);
        }

        void NewEmber(ref Mote m, Vector2 size)
        {
            if (underwater) { NewBubble(ref m, size); return; }
            m.age = 0f;
            m.life = Random.Range(4f, 9f);
            m.size = Random.Range(3f, 6f);
            m.maxAlpha = Random.Range(0.5f, 1f) * style.emberColor.a;
            m.velocity = new Vector2(Random.Range(-6f, 14f), Random.Range(28f, 70f));
            m.wobble = Random.Range(10f, 30f);
            m.rect.anchoredPosition = new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.55f, -0.1f) * size.y);
            m.image.color = new Color(style.emberColor.r, style.emberColor.g, style.emberColor.b, 0f);
        }

        void NewBubble(ref Mote m, Vector2 size)
        {
            m.age = 0f;
            m.life = Random.Range(5f, 10f);
            m.size = Random.Range(8f, 22f);
            m.maxAlpha = Random.Range(0.5f, 1f) * style.bubbleColor.a;
            m.velocity = new Vector2(Random.Range(-4f, 4f), Random.Range(45f, 95f));
            m.wobble = Random.Range(15f, 35f);
            m.rect.anchoredPosition = new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.6f, -0.2f) * size.y);
            m.image.color = new Color(style.bubbleColor.r, style.bubbleColor.g, style.bubbleColor.b, 0f);
        }

        void NewCloud(ref Mote m, Vector2 size)
        {
            m.age = 0f;
            m.life = Random.Range(16f, 26f);
            m.size = Random.Range(420f, 820f);
            m.maxAlpha = Random.Range(0.6f, 1f) * style.stormCloudColor.a;
            m.velocity = new Vector2(Random.Range(18f, 40f), Random.Range(-2f, 2f));
            m.wobble = 0f;
            m.rect.anchoredPosition = new Vector2(Random.Range(-0.65f, 0.4f) * size.x, Random.Range(0.3f, 0.55f) * size.y);
            m.image.color = new Color(style.stormCloudColor.r, style.stormCloudColor.g, style.stormCloudColor.b, 0f);
        }

        // ------------------------------------------------------------------
        // Storm and sway
        // ------------------------------------------------------------------
        /// <summary>A double flash: a bright strike, a dark beat, a weaker after-flash.</summary>
        void Lightning(float dt)
        {
            if (flashAge < 0f)
            {
                nextFlash -= dt;
                if (nextFlash > 0f) return;
                flashAge = 0f;
                nextFlash = Random.Range(style.stormFlashInterval.x, style.stormFlashInterval.y);
            }
            flashAge += dt;
            float a;
            if (flashAge < 0.08f) a = 1f;
            else if (flashAge < 0.16f) a = 0.1f;
            else if (flashAge < 0.5f) a = 0.6f * (1f - (flashAge - 0.16f) / 0.34f);
            else { a = 0f; flashAge = -1f; }
            var c = style.stormFlashColor;
            flash.color = new Color(c.r, c.g, c.b, c.a * a);
        }

        /// <summary>True for a moment while lightning lights up the sky.</summary>
        public bool Flashing => flashAge >= 0f && flashAge < 0.16f;

        void Sway()
        {
            if (parallaxTarget == null || parallaxAmount <= 0f) return;
            Vector2 mouse = Input.mousePosition;
            Vector2 target = Screen.width > 0 && Screen.height > 0
                ? new Vector2(mouse.x / Screen.width - 0.5f, mouse.y / Screen.height - 0.5f) * -2f
                : Vector2.zero;
            target = Vector2.ClampMagnitude(target, 1f);
            sway = Vector2.Lerp(sway, target, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            Vector3 offset = sway * parallaxAmount;
            if (parallaxTarget is RectTransform r) r.anchoredPosition = (Vector2)(parallaxBase + offset);
            else parallaxTarget.localPosition = parallaxBase + offset;
        }
    }
}
