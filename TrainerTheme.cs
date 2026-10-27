using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    // Shared look for the HUD and the bind menu: palette, rounded rectangles (9-sliced via
    // GUIStyle.border) and an OS font. IMGUI's default skin is what made the old UI look
    // dated, so nothing here uses it.
    public static class TrainerTheme
    {
        public static readonly Color Panel        = new Color(0.067f, 0.075f, 0.094f, 0.93f);
        public static readonly Color Shadow       = new Color(0f,     0f,     0f,     0.35f);
        public static readonly Color Stroke       = new Color(1f,     1f,     1f,     0.09f);
        public static readonly Color Divider      = new Color(1f,     1f,     1f,     0.07f);
        public static readonly Color Surface      = new Color(1f,     1f,     1f,     0.05f);
        public static readonly Color SurfaceHover = new Color(1f,     1f,     1f,     0.09f);
        public static readonly Color Chip         = new Color(0.17f,  0.19f,  0.24f,  1f);
        public static readonly Color ChipHover    = new Color(0.22f,  0.25f,  0.31f,  1f);
        public static readonly Color Accent       = new Color(0.36f,  0.72f,  1f,     1f);
        public static readonly Color AccentSoft   = new Color(0.36f,  0.72f,  1f,     0.16f);
        public static readonly Color Fly          = new Color(0.70f,  0.58f,  1f,     1f);
        public static readonly Color FlySoft      = new Color(0.70f,  0.58f,  1f,     0.14f);
        public static readonly Color On           = new Color(0.38f,  0.88f,  0.58f,  1f);
        public static readonly Color OnSoft       = new Color(0.38f,  0.88f,  0.58f,  0.16f);
        public static readonly Color Ghost        = new Color(1f,     0.55f,  0.80f,  1f);
        public static readonly Color Warn         = new Color(1f,     0.75f,  0.33f,  1f);
        public static readonly Color WarnSoft     = new Color(1f,     0.75f,  0.33f,  0.18f);
        public static readonly Color Danger       = new Color(0.96f,  0.42f,  0.42f,  1f);
        public static readonly Color Text         = new Color(0.93f,  0.95f,  0.98f,  1f);
        public static readonly Color TextDim      = new Color(0.60f,  0.64f,  0.72f,  1f);
        public static readonly Color TextFaint    = new Color(0.43f,  0.47f,  0.54f,  1f);
        public static readonly Color Backdrop     = new Color(0f,     0f,     0f,     0.45f);

        private const int RADIUS_LARGE = 10;
        private const int RADIUS_SMALL = 5;

        // Runtime assets are HideAndDontSave so scene loads (UnloadUnusedAssets) don't free
        // them; Unity's == null still catches a destroyed one and they get rebuilt.
        private static Texture2D _texLarge;
        private static Texture2D _texSmall;
        private static GUIStyle _boxLarge;
        private static GUIStyle _boxSmall;

        private static Font _font;
        private static bool _fontFailed;

        // Segoe UI only exists on Windows. Under Proton/Wine, CreateDynamicFontFromOSFont with a
        // missing name does not throw: it returns a font with no glyphs and every label renders
        // blank. So use the first font that is really installed (the others are the usual ones on
        // Linux and in Proton prefixes) and fall back to Unity's built-in font if none is.
        private static readonly string[] PreferredFonts = { "Segoe UI", "Arial", "Liberation Sans", "DejaVu Sans", "Noto Sans" };

        public static Font Font
        {
            get
            {
                if (_font != null || _fontFailed) return _font;
                try
                {
                    var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                    var name = PreferredFonts.FirstOrDefault(installed.Contains);
                    if (name != null)
                    {
                        _font = Font.CreateDynamicFontFromOSFont(name, 16);
                        if (_font != null) _font.hideFlags = HideFlags.HideAndDontSave;
                        TrainerPlugin.Logger.LogInfo($"[UI] Using OS font '{name}'.");
                    }
                    else
                    {
                        TrainerPlugin.Logger.LogWarning("[UI] None of the preferred OS fonts is installed; using Unity's default.");
                    }
                }
                catch (Exception ex)
                {
                    TrainerPlugin.Logger.LogWarning($"[UI] OS font unavailable ({ex.Message}); using Unity's default.");
                }
                if (_font == null) _fontFailed = true;
                return _font;
            }
        }

        public static GUIStyle MakeLabel(int size, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var s = new GUIStyle();
            if (Font != null) s.font = Font;
            s.fontSize = Mathf.Max(1, size);
            s.fontStyle = style;
            s.alignment = align;
            s.clipping = TextClipping.Clip;
            s.wordWrap = false;
            s.normal.textColor = Text;
            return s;
        }

        // Draws a label with the given color without leaking it into the shared style.
        public static void Label(Rect r, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        // Rounded rect, large radius (panels) or small (chips, rows, buttons).
        public static void Fill(Rect r, Color c, bool small = false)
        {
            EnsureBoxes();
            Color old = GUI.color;
            GUI.color = c;
            GUI.Box(r, GUIContent.none, small ? _boxSmall : _boxLarge);
            GUI.color = old;
        }

        // Square-cornered fill (backdrop, dividers, accent bars).
        public static void FillRect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // Panel with a soft drop shadow and a 1px light stroke.
        public static void DrawPanel(Rect r, Color bg)
        {
            Fill(new Rect(r.x - 2f, r.y + 1f, r.width + 4f, r.height + 5f), Shadow);
            Fill(r, Stroke);
            Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), bg);
        }

        // Key/button "keycap": rounded chip with centered text.
        public static void DrawChip(Rect r, string text, GUIStyle style, Color bg, Color fg)
        {
            Fill(r, bg, true);
            Label(r, text, style, fg);
        }

        private static void EnsureBoxes()
        {
            if (_texLarge == null || _boxLarge == null)
            {
                _texLarge = MakeRounded(RADIUS_LARGE);
                _boxLarge = MakeBoxStyle(_texLarge, RADIUS_LARGE);
            }
            if (_texSmall == null || _boxSmall == null)
            {
                _texSmall = MakeRounded(RADIUS_SMALL);
                _boxSmall = MakeBoxStyle(_texSmall, RADIUS_SMALL);
            }
        }

        private static GUIStyle MakeBoxStyle(Texture2D tex, int radius)
        {
            var s = new GUIStyle();
            s.normal.background = tex;
            // The 4-int RectOffset constructor is stripped from the interop build.
            s.border = new RectOffset { left = radius, right = radius, top = radius, bottom = radius };
            return s;
        }

        // White rounded square with anti-aliased corners; tinted through GUI.color.
        private static Texture2D MakeRounded(int radius)
        {
            int size = radius * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float dx = px < radius ? radius - px : (px > size - radius ? px - (size - radius) : 0f);
                    float dy = py < radius ? radius - py : (py > size - radius ? py - (size - radius) : 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
