using UnityEngine;

namespace Aeternal.FX
{
    /// <summary>
    /// Small sprites generated at runtime for battlefield depth (soft shadows, vignette,
    /// tile shading) and placeholder icons (AP gear, DF crystal), so they need no art assets.
    /// Each is one world unit across and cached. Shapes are white/grey; tint them where used.
    /// </summary>
    public static class ProceduralSprites
    {
        static Sprite softEllipse;
        static Sprite vignette;
        static Sprite verticalGradient;
        static Sprite gear;
        static Sprite crystal;
        static Sprite drop;
        static Sprite star;
        static Sprite disc;
        static Sprite coral, coralTips, cageRing;

        // Coral on a blocked square, 16 x 16 pixels: '#' branch, 'o' branch tip.
        static readonly string[] CoralPattern =
        {
            "................",
            "...o.......o....",
            "...#..o...o#....",
            "..o#..#...#.....",
            "...##.#..##..o..",
            "....#.##.#...#..",
            ".o..###..#..##..",
            ".#...#...####...",
            ".##..#..##.#....",
            "..####.##..#..o.",
            "....#####.##.##.",
            ".....######.##..",
            "......#######...",
            ".....#########..",
            "....###########.",
            "................",
        };

        /// <summary>Coral branches (white; tint them), point-filtered pixel art.</summary>
        public static Sprite Coral() => coral != null ? coral : coral = Pattern(CoralPattern, '#');

        /// <summary>The tips of the coral's branches, to tint in a second colour over Coral().</summary>
        public static Sprite CoralTips() => coralTips != null ? coralTips : coralTips = Pattern(CoralPattern, 'o');

        /// <summary>A soft ring, for the water cage around a caged enemy.</summary>
        public static Sprite CageRing()
        {
            if (cageRing != null) return cageRing;
            const int size = 96;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.1f);
                a = Mathf.Max(a, Mathf.Clamp01(0.86f - r) * 0.25f); // a faint fill of water inside
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return cageRing = ToSprite(tex);
        }

        static Sprite Pattern(string[] rows, char on)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = NewTexture(w, h);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, h - 1 - y, rows[y][x] == on ? Color.white : Color.clear);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }

        /// <summary>Crisp disc, slightly lit from the top; tint it for map nodes.</summary>
        public static Sprite Disc()
        {
            if (disc != null) return disc;
            const int size = 128;
            const float aa = 2f / size;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((0.98f - r) / aa);
                float v = 0.82f + 0.18f * dy;
                if (r > 0.86f) v *= 1.15f;                                   // a lighter rim
                tex.SetPixel(x, y, new Color(Mathf.Min(1f, v), Mathf.Min(1f, v), Mathf.Min(1f, v), a));
            }
            tex.Apply();
            return disc = ToSprite(tex);
        }

        /// <summary>Shaded teardrop, point up; tint it red for Bleed.</summary>
        public static Sprite Drop()
        {
            if (drop != null) return drop;
            const int size = 64;
            const float aa = 2f / size;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                // Round bottom (circle at y=-0.3), narrowing to a point at the top.
                float r = 0.55f;
                float cy = -0.3f;
                float inside;
                if (dy <= cy) inside = r - Mathf.Sqrt(dx * dx + (dy - cy) * (dy - cy));
                else
                {
                    float half = r * Mathf.Clamp01(1f - (dy - cy) / (0.95f - cy));
                    inside = half - Mathf.Abs(dx);
                }
                float a = Mathf.Clamp01(inside / aa);
                float v = 0.75f + 0.25f * (-dx) - 0.15f * Mathf.Max(0f, -dy);   // lit from the upper left
                if (dx < -0.15f && dx > -0.32f && dy < 0.05f && dy > -0.45f) v = 1f;  // highlight
                tex.SetPixel(x, y, new Color(v, v, v, a));
            }
            tex.Apply();
            return drop = ToSprite(tex);
        }

        /// <summary>Five-pointed star; tint it yellow for Stun.</summary>
        public static Sprite Star()
        {
            if (star != null) return star;
            const int size = 64;
            const float aa = 2f / size;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // Radius of the star's edge at this angle: alternates between tip and notch.
                float ang = Mathf.Atan2(dx, dy);                         // 0 = straight up
                float seg = Mathf.Repeat(ang / (2f * Mathf.PI) * 5f, 1f);
                float k = Mathf.Abs(seg - 0.5f) * 2f;                    // 1 at a tip, 0 at a notch
                float edge = Mathf.Lerp(0.4f, 0.97f, k * k);               // squared: concave sides, sharp tips
                float a = Mathf.Clamp01((edge - r) / aa);
                float v = 0.8f + 0.2f * dy;
                tex.SetPixel(x, y, new Color(v, v, v, a));
            }
            tex.Apply();
            return star = ToSprite(tex);
        }

        /// <summary>
        /// Shaded grey cog (lit from the top, with a hub hole and a groove); tint it for the
        /// Action Point icon.
        /// </summary>
        public static Sprite Gear()
        {
            if (gear != null) return gear;
            const int size = 64;
            const int teeth = 8;
            const float aa = 2f / size;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float turn = Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI) * teeth, 1f);
                bool tooth = Mathf.Abs(turn - 0.5f) < 0.24f;
                float outer = tooth ? 0.96f : 0.76f;

                float a = Mathf.Clamp01((outer - r) / aa) * Mathf.Clamp01((r - 0.26f) / aa);
                float v = 0.78f + 0.22f * dy;                                   // lit from the top
                if (r > 0.48f && r < 0.56f) v *= 0.62f;                         // groove
                if (r > outer - 0.08f) v *= 0.85f;                              // darker rim
                tex.SetPixel(x, y, new Color(v, v, v, a));
            }
            tex.Apply();
            return gear = ToSprite(tex);
        }

        /// <summary>
        /// Faceted diamond crystal with a soft halo; tint it for the Divine Focus icon.
        /// </summary>
        public static Sprite Crystal()
        {
            if (crystal != null) return crystal;
            const int size = 64;
            const float aa = 2f / size;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                // Diamond: 1 on its edge, below 1 inside.
                float d = Mathf.Abs(dx) / 0.56f + Mathf.Abs(dy) / 0.92f;
                float body = Mathf.Clamp01((1f - d) / aa);

                // Four facets: left brighter than right, top brighter than bottom.
                float v = (dx < 0f ? 1f : 0.72f) * (dy > 0f ? 1f : 0.82f);
                if (d < 0.35f) v = Mathf.Min(1f, v + 0.25f);                    // inner shine

                float halo = Mathf.Clamp01(1f - (d - 1f) / 0.6f);
                halo = halo * halo * 0.45f;
                float alpha = Mathf.Max(body, halo);
                float shade = body > 0f ? Mathf.Lerp(1f, v, body) : 1f;
                tex.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
            tex.Apply();
            return crystal = ToSprite(tex);
        }

        /// <summary>White disc fading to transparent at the edge; tint it for a ground shadow.</summary>
        public static Sprite SoftEllipse()
        {
            if (softEllipse != null) return softEllipse;
            const int size = 64;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            return softEllipse = ToSprite(tex);
        }

        /// <summary>Transparent centre, opaque white edges; tint it to darken the screen edges.</summary>
        public static Sprite Vignette()
        {
            if (vignette != null) return vignette;
            const int size = 128;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return vignette = ToSprite(tex);
        }

        /// <summary>White at the top shading to grey at the bottom; tint it for a lit tile face.</summary>
        public static Sprite VerticalGradient()
        {
            if (verticalGradient != null) return verticalGradient;
            const int size = 32;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
            {
                float v = Mathf.Lerp(0.62f, 1f, (y + 0.5f) / size);
                for (int x = 0; x < size; x++) tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
            tex.Apply();
            return verticalGradient = ToSprite(tex);
        }

        static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        static Sprite ToSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
    }
}
