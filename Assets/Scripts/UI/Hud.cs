using System.Collections.Generic;
using HonkAndLoad.Core;
using HonkAndLoad.Gameplay;
using HonkAndLoad.Level;
using UnityEngine;

namespace HonkAndLoad.UI
{
    /// <summary>
    /// Prototip arayüzü (IMGUI): giriş ekranı, oyun içi üst bar, kazanma / kaybetme panelleri.
    /// Aşama 3'te uGUI / UI Toolkit ile değiştirilecek.
    /// 1080 px genişliğe göre tasarlanır ve ekrana göre ölçeklenir.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        private const float DesignWidth = 1080f;
        private const string Version = "v0.1 prototip";

        private GameController _game;
        private float _scale = 1f;
        private bool _confirmReset;
        private GUIStyle _logo, _subtitle, _title, _label, _small, _button, _secondary, _panel;
        private GUIStyle _cardAdventure, _cardEndless, _cardTitle, _cardSub, _cardDesc, _badge, _bannerTitle, _bannerSub, _bigNumber;
        private Texture2D _barBack, _barFill, _bannerBack;

        // Modern ana menü
        private UiTextures _ui;
        private Texture2D _menuBg, _shadow, _circle, _iconFlag, _iconInfinity, _iconPlay, _crateTile;
        private GUIStyle _modeAdventure, _modeEndless, _modeTitle, _modeSub, _pillOn, _pillOff, _pillText, _link, _newBadge, _heroTitle, _heroTitleShadow;

        // Ortadaki duyuru ("Zorluk 3!", "Yeni Rekor!")
        private string _bannerText, _bannerSubText;
        private float _bannerStart = -10f;
        private const float BannerLife = 1.8f;
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        // Uçan yazılar ("x3 Kombo!", "Teslim!")
        private class PopupText
        {
            public string Text;
            public Vector3 World;
            public Color Color;
            public float Age;
            public float Delay;
        }

        private const float PopupLife = 1.1f;
        private readonly List<PopupText> _popups = new List<PopupText>();
        private GUIStyle _popup, _bubble, _hook, _cta, _smallLogo;
        private Texture2D _ringTex, _arrowTex, _dotTex;

        // Video modunda dokunma işareti
        private Vector3 _tapWorld;
        private float _tapTime = -10f;

        // Bu karede çizilen arayüz alanları (ekran koordinatı, y aşağıdan yukarı)
        private static readonly List<Rect> HudRects = new List<Rect>();

        public void Init(GameController game) => _game = game;

        /// <summary>Üst barın tasarım yüksekliği (1080 genişliğe göre).</summary>
        public const float TopBarHeight = 140f;

        /// <summary>Çentik / ön kamera nedeniyle ekranın üstünde kullanılamayan piksel.</summary>
        public static float SafeTopInsetPixels() =>
            Mathf.Max(0f, Screen.height - (Screen.safeArea.y + Screen.safeArea.height));

        /// <summary>Kameranın oyun alanını yerleştirmemesi gereken üst bölge (piksel).</summary>
        public static float TopReservedPixels()
        {
            float scale = Screen.width / DesignWidth;
            // Video modunda üstte açılış sorusu / logo için daha geniş boşluk
            float reserved = IsVideo ? 330f : TopBarHeight + 140f; // bar + zorluk çubuğu + "Raf dolmak üzere" yazısı
            return SafeTopInsetPixels() + reserved * scale;
        }

        /// <summary>Kamera sığdırma için: oyun video modunda mı?</summary>
        public static bool IsVideo;

        public static bool IsPointerOverHud(Vector2 screenPos)
        {
            foreach (Rect r in HudRects) if (r.Contains(screenPos)) return true;
            return false;
        }

        // ---------- Stiller ----------

        private void EnsureStyles()
        {
            if (_title != null) return;

            _logo = Text(130, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _subtitle = Text(44, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.9f));
            _title = Text(64, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _label = Text(44, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            _small = Text(32, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f));

            _button = ButtonStyle(new Color(0.15f, 0.65f, 0.35f), 46);
            _secondary = ButtonStyle(new Color(0.20f, 0.30f, 0.45f, 0.85f), 38);

            _panel = new GUIStyle(GUI.skin.box);
            _panel.normal.background = Tex(new Color(0f, 0f, 0f, 0.55f));

            // Ana menü mod kartları: iki mod eşit büyüklükte, farklı renkte
            _cardAdventure = ButtonStyle(new Color(0.15f, 0.65f, 0.35f), 40);
            _cardEndless = ButtonStyle(new Color(0.95f, 0.52f, 0.12f), 40);
            _cardTitle = Text(62, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _cardSub = Text(44, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _cardDesc = Text(32, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.9f));
            _badge = Text(34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _badge.normal.background = Tex(new Color(0.9f, 0.2f, 0.25f));
            _bannerTitle = Text(84, FontStyle.Bold, TextAnchor.MiddleCenter, Palette.Warning);
            _bannerSub = Text(40, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _bigNumber = Text(56, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            _barBack = Tex(new Color(0f, 0f, 0f, 0.35f));
            _barFill = Tex(new Color(0.95f, 0.52f, 0.12f));
            _bannerBack = Tex(new Color(0.05f, 0.08f, 0.15f, 0.8f));

            BuildMenuSkin();

            _popup = Text(58, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _bubble = Text(42, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.15f, 0.15f, 0.2f));
            _bubble.normal.background = Tex(new Color(1f, 1f, 1f, 0.95f));
            _bubble.padding = new RectOffset(30, 30, 20, 20);

            _hook = Text(86, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _dotTex = ShapeTex(64, (u, v) => u * u + v * v < 0.9f);
            _ringTex = ShapeTex(128, (u, v) => { float r = Mathf.Sqrt(u * u + v * v); return r < 0.95f && r > 0.72f; });
            _arrowTex = ShapeTex(64, (u, v) => (v < 0.9f && v > -0.1f && Mathf.Abs(u) < 0.22f)
                                              || (v <= -0.1f && v > -0.95f && Mathf.Abs(u) < (v + 0.95f) * 0.9f));
        }

        private void BuildMenuSkin()
        {
            _ui = new UiTextures();
            _menuBg = _ui.VerticalGradient(new Color(0.42f, 0.75f, 1f), new Color(0.24f, 0.36f, 0.86f));
            _shadow = _ui.SoftShadow();
            _circle = _ui.Circle();
            _iconFlag = _ui.Flag();
            _iconInfinity = _ui.Infinity();
            _iconPlay = _ui.Play();
            _crateTile = _ui.RoundedFlat(Color.white, 64, 14);

            var cardBorder = new RectOffset(44, 44, 44, 54);
            _modeAdventure = CardStyle(_ui.RoundedCard(new Color(0.33f, 0.86f, 0.47f), new Color(0.11f, 0.62f, 0.33f)),
                                       _ui.RoundedCard(new Color(0.22f, 0.72f, 0.38f), new Color(0.08f, 0.5f, 0.26f)), cardBorder);
            _modeEndless = CardStyle(_ui.RoundedCard(new Color(1f, 0.70f, 0.25f), new Color(0.96f, 0.42f, 0.10f)),
                                     _ui.RoundedCard(new Color(0.92f, 0.58f, 0.18f), new Color(0.84f, 0.34f, 0.06f)), cardBorder);

            _modeTitle = Text(66, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            _modeSub = Text(40, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.92f));
            _heroTitle = Text(132, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _heroTitleShadow = Text(132, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.08f, 0.12f, 0.4f, 0.45f));

            _pillOn = new GUIStyle { border = new RectOffset(48, 48, 48, 48) };
            _pillOn.normal.background = _ui.Pill(new Color(1f, 1f, 1f, 0.95f));
            _pillOn.active.background = _ui.Pill(new Color(0.85f, 0.88f, 0.95f, 0.95f));
            _pillOff = new GUIStyle { border = new RectOffset(48, 48, 48, 48) };
            _pillOff.normal.background = _ui.Pill(new Color(1f, 1f, 1f, 0.28f));
            _pillOff.active.background = _ui.Pill(new Color(1f, 1f, 1f, 0.4f));
            _pillText = Text(38, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);

            _link = Text(32, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f));
            _newBadge = Text(34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _newBadge.normal.background = _ui.Pill(new Color(0.93f, 0.2f, 0.32f));
            _newBadge.border = new RectOffset(48, 48, 48, 48);
        }

        private static GUIStyle CardStyle(Texture2D normal, Texture2D pressed, RectOffset border)
        {
            var s = new GUIStyle { border = border };
            s.normal.background = normal;
            s.hover.background = normal;
            s.active.background = pressed;
            return s;
        }

        private static GUIStyle Text(int size, FontStyle style, TextAnchor anchor, Color color)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, alignment = anchor, wordWrap = true };
            s.normal.textColor = color;
            return s;
        }

        private GUIStyle ButtonStyle(Color color, int size)
        {
            var s = new GUIStyle(GUI.skin.button) { fontSize = size, fontStyle = FontStyle.Bold };
            Texture2D tex = Tex(color);
            s.normal.background = tex;
            s.hover.background = tex;
            s.active.background = Tex(Color.Lerp(color, Color.black, 0.2f));
            s.normal.textColor = Color.white;
            s.hover.textColor = Color.white;
            s.active.textColor = Color.white;
            return s;
        }

        private Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            _textures.Add(t);
            return t;
        }

        /// <summary>Kenarları yumuşatılmış beyaz şekil dokusu (koordinatlar -1..1).</summary>
        private Texture2D ShapeTex(int size, System.Func<float, float, bool> inside)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = (x + 0.25f + 0.5f * sx) / size * 2f - 1f;
                            float v = (y + 0.25f + 0.5f * sy) / size * 2f - 1f;
                            if (inside(u, v)) hits++;
                        }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / 4));
                }
            t.SetPixels32(px);
            t.Apply();
            _textures.Add(t);
            return t;
        }

        // ---------- Uçan yazılar ----------

        /// <summary>Ekranın ortasında kısa duyuru.</summary>
        public void Banner(string title, string subtitle)
        {
            _bannerText = title;
            _bannerSubText = subtitle;
            _bannerStart = Time.time;
        }

        private void DrawBanner(float w, float h)
        {
            float age = Time.time - _bannerStart;
            if (age > BannerLife || string.IsNullOrEmpty(_bannerText)) return;
            float a = age < 0.15f ? age / 0.15f : age > BannerLife - 0.4f ? (BannerLife - age) / 0.4f : 1f;
            float pop = age < 0.2f ? Mathf.Lerp(0.7f, 1.08f, age / 0.2f) : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((age - 0.2f) / 0.2f));
            Rect r = new Rect(60, h * 0.36f, w - 120, 230);
            Matrix4x4 m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * pop, new Vector2((r.x + r.width / 2) * _scale, (r.y + r.height / 2) * _scale));
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(r, _bannerBack);
            GUI.Label(new Rect(r.x, r.y + 20, r.width, 120), _bannerText, _bannerTitle);
            GUI.Label(new Rect(r.x, r.y + 140, r.width, 60), _bannerSubText, _bannerSub);
            GUI.color = old;
            GUI.matrix = m;
        }

        public void Popup(string text, Vector3 world, Color color, float delay)
        {
            _popups.Add(new PopupText { Text = text, World = world, Color = color, Delay = delay });
        }

        private void Update()
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                PopupText p = _popups[i];
                if (p.Delay > 0f) { p.Delay -= Time.deltaTime; continue; }
                p.Age += Time.deltaTime;
                if (p.Age > PopupLife) _popups.RemoveAt(i);
            }
        }

        private void DrawPopups()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Color old = GUI.color;
            foreach (PopupText p in _popups)
            {
                if (p.Delay > 0f) continue;
                Vector3 sp = cam.WorldToScreenPoint(p.World);
                if (sp.z < 0f) continue;
                float k = p.Age / PopupLife;
                float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale - 60f - k * 120f;
                float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.1f, k / 0.15f) : 1f;
                var shadow = new Color(0f, 0f, 0f, 0.5f * (1f - k));
                Matrix4x4 m = GUI.matrix;
                GUIUtility.ScaleAroundPivot(Vector2.one * pop, new Vector2(x * _scale, y * _scale));
                GUI.color = shadow;
                GUI.Label(new Rect(x - 300 + 4, y - 40 + 4, 600, 80), p.Text, _popup);
                GUI.color = new Color(p.Color.r, p.Color.g, p.Color.b, 1f - k * k);
                GUI.Label(new Rect(x - 300, y - 40, 600, 80), p.Text, _popup);
                GUI.matrix = m;
            }
            GUI.color = old;
        }

        // ---------- Reklam videosu ----------

        public void ShowTap(Vector3 world)
        {
            _tapWorld = world;
            _tapTime = Time.time;
        }

        private void DrawVideo(float w, float h)
        {
            if (_cta == null)
            {
                _cta = new GUIStyle(_button) { fontSize = 76 };
                _smallLogo = Text(70, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            }
            DrawPopups();
            Color old = GUI.color;

            // Dokunma işareti: büzülen halka + nokta
            float age = Time.time - _tapTime;
            Camera cam = Camera.main;
            if (age < 0.45f && cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(_tapWorld);
                float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale;
                float k = age / 0.45f;
                float ring = Mathf.Lerp(230f, 120f, k);
                GUI.color = new Color(1f, 1f, 1f, 0.9f * (1f - k));
                GUI.DrawTexture(new Rect(x - ring / 2, y - ring / 2, ring, ring), _ringTex);
                GUI.color = new Color(1f, 1f, 1f, 0.75f * (1f - k * 0.6f));
                GUI.DrawTexture(new Rect(x - 45, y - 45, 90, 90), _dotTex);
            }

            // Açılış sorusu
            float t = _game.VideoTime;
            if (t < 3.2f)
            {
                float a = t < 0.25f ? t / 0.25f : t > 2.7f ? (3.2f - t) / 0.5f : 1f;
                float y = SafeTopInsetPixels() / _scale + 70f;
                ShadowLabel(new Rect(40, y, w - 80, 230), "Bu depoyu\nboşaltabilir misin?", _hook, a);
            }
            else if (_game.CurrentPhase == GameController.Phase.Playing || _game.PhaseTime < 1.9f)
            {
                // Soru kaybolunca üstte küçük oyun adı kalsın
                float a = Mathf.Clamp01((t - 3.2f) / 0.4f);
                float y = SafeTopInsetPixels() / _scale + 110f;
                ShadowLabel(new Rect(40, y, w - 80, 120), "Honk & Load!", _smallLogo, a);
            }

            // Kapanış ekranı
            if (_game.CurrentPhase == GameController.Phase.Won && _game.PhaseTime > 1.9f)
            {
                float k = Mathf.Clamp01((_game.PhaseTime - 1.9f) / 0.4f);
                GUI.color = new Color(0.06f, 0.1f, 0.2f, 0.82f * k);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, k);

                float y = h * 0.22f;
                GUI.Label(new Rect(0, y, w, 160), "Honk & Load!", _logo);
                GUI.Label(new Rect(60, y + 170, w - 120, 70), "Kolileri yükle, kamyonları yolla!", _subtitle);

                float boxSize = 70f, gap = 18f, rowWidth = 5 * boxSize + 4 * gap;
                for (int i = 0; i < 5; i++)
                {
                    float bounce = Mathf.Abs(Mathf.Sin(_game.PhaseTime * 5f + i * 0.6f)) * 20f;
                    Color c = Palette.Crate(i);
                    GUI.color = new Color(c.r, c.g, c.b, k);
                    GUI.DrawTexture(new Rect((w - rowWidth) / 2 + i * (boxSize + gap), y + 290 - bounce, boxSize, boxSize), Texture2D.whiteTexture);
                }

                float pulse = 1f + 0.05f * Mathf.Sin(_game.PhaseTime * 7f);
                float bw = (w - 240) * pulse, bh = 170 * pulse;
                GUI.color = new Color(1f, 1f, 1f, k);
                GUI.Label(new Rect((w - bw) / 2, h * 0.6f - bh / 2, bw, bh), "Ücretsiz Oyna!", _cta);
            }
            GUI.color = old;
        }

        private static void ShadowLabel(Rect r, string text, GUIStyle style, float alpha)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
            GUI.Label(new Rect(r.x + 5, r.y + 5, r.width, r.height), text, style);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        // ---------- İpucu ----------

        private void DrawHint(float w, float h)
        {
            if (!_game.HintVisible) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(_game.HintWorld);
            if (sp.z < 0f) return;
            float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale;
            float t = Time.unscaledTime;

            // Atan halka
            float pulse = 1f + 0.12f * Mathf.Sin(t * 6f);
            float size = 170f * pulse;
            Color old = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            GUI.DrawTexture(new Rect(x - size / 2, y - size / 2, size, size), _ringTex);

            // Yukarıdan inip kalkan ok
            float bob = Mathf.Abs(Mathf.Sin(t * 4f)) * 30f;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x - 40, y - size / 2 - 120 - bob, 80, 110), _arrowTex);
            GUI.color = old;

            // Öğretici açıklaması (yalnızca 1. bölümde)
            string text = _game.HintText;
            if (!string.IsNullOrEmpty(text))
            {
                // Alt kısımda: kamyonları ve dokunulacak koliyi kapatmasın
                float bottomInset = Screen.safeArea.y / _scale;
                Rect r = new Rect(60, h - bottomInset - 240, w - 120, 190);
                GUI.Label(r, text, _bubble);
            }
        }

        // ---------- Çizim ----------

        private void OnGUI()
        {
            if (_game == null) return;
            EnsureStyles();
            if (Event.current.type == EventType.Layout) HudRects.Clear();

            _scale = Screen.width / DesignWidth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float w = DesignWidth;
            float h = Screen.height / _scale;

            if (_game.CurrentPhase == GameController.Phase.Menu)
            {
                DrawMenu(w, h);
                return;
            }
            if (_game.State == null) return;

            if (_game.VideoMode)
            {
                DrawVideo(w, h);
                return;
            }

            float inset = SafeTopInsetPixels() / _scale;
            DrawTopBar(w, inset);
            DrawHint(w, h);
            DrawPopups();
            DrawBanner(w, h);
            // Kazanınca önce konvoy ve konfeti oynasın, panel biraz sonra gelsin
            if (_game.CurrentPhase == GameController.Phase.Won && _game.PhaseTime > 1.6f) DrawWin(w, h);
            else if (_game.CurrentPhase == GameController.Phase.Lost && _game.PhaseTime > 0.4f) DrawLose(w, h);
        }

        private void DrawMenu(float w, float h)
        {
            Track(new Rect(0, 0, w, h));
            Color old = GUI.color;
            float t = Time.time;

            // Arka plan: degrade + yavaşça süzülen soluk koliler
            GUI.DrawTexture(new Rect(0, 0, w, h), _menuBg);
            for (int i = 0; i < 9; i++)
            {
                float speed = 18f + (i % 3) * 9f;
                float size = 70f + (i * 37 % 60);
                float x = (i * 131 % 1000) + 20f;
                float y = h - ((t * speed + i * 260f) % (h + 300f)) + 150f;
                Color c = Palette.Crate(i);
                GUI.color = new Color(c.r, c.g, c.b, 0.22f);
                Matrix4x4 m = GUI.matrix;
                GUIUtility.RotateAroundPivot(t * (10f + i * 3f) + i * 40f, new Vector2((x + size / 2) * _scale, (y + size / 2) * _scale));
                GUI.DrawTexture(new Rect(x, y, size, size), _crateTile);
                GUI.matrix = m;
            }
            GUI.color = old;

            // Başlık
            float titleY = Mathf.Max(SafeTopInsetPixels() / _scale + 40f, h * 0.09f);
            GUI.Label(new Rect(0, titleY + 10, w, 170), "Honk & Load!", _heroTitleShadow);
            GUI.Label(new Rect(0, titleY, w, 170), "Honk & Load!", _heroTitle);
            GUI.Label(new Rect(60, titleY + 165, w - 120, 60), "Kolileri yükle, kamyonları yolla!", _subtitle);

            float boxSize = 58f, gap = 16f, rowWidth = 5 * boxSize + 4 * gap;
            for (int i = 0; i < 5; i++)
            {
                float bounce = Mathf.Abs(Mathf.Sin(t * 3f + i * 0.6f)) * 12f;
                GUI.color = Palette.Crate(i);
                GUI.DrawTexture(new Rect((w - rowWidth) / 2 + i * (boxSize + gap), titleY + 250 - bounce, boxSize, boxSize), _crateTile);
            }
            GUI.color = old;

            // Mod kartları: tam genişlik, alt alta, eşit önem
            float side = 70f, cardW = w - 2 * side, cardH = 250f, cardGap = 44f;
            float cy = Mathf.Max(titleY + 380f, h * 0.36f);

            if (ModeCard(new Rect(side, cy, cardW, cardH), _modeAdventure, _iconFlag, "MACERA",
                $"Bölüm {Progress.CurrentLevel}  ·  Devam et", new Color(0.11f, 0.62f, 0.33f), false))
            {
                _confirmReset = false;
                _game.Play();
            }

            int best = Progress.EndlessBest;
            if (ModeCard(new Rect(side, cy + cardH + cardGap, cardW, cardH), _modeEndless, _iconInfinity, "SONSUZ",
                best > 0 ? $"Rekor  {best}" : "Puan topla, rekor kır", new Color(0.96f, 0.42f, 0.10f), best == 0))
            {
                _confirmReset = false;
                _game.StartEndless();
            }

            // Ayarlar: yuvarlak hap düğmeler
            float py = cy + 2 * cardH + cardGap + 70f;
            float pw = (cardW - 30f) / 2f, ph = 104f;
            if (Pill(new Rect(side, py, pw, ph), Progress.SoundOn, Progress.SoundOn ? "Ses açık" : "Ses kapalı"))
            {
                Progress.SoundOn = !Progress.SoundOn;
                _game.ApplySettings();
            }
            if (Pill(new Rect(side + pw + 30f, py, pw, ph), Progress.HapticsOn, Progress.HapticsOn ? "Titreşim açık" : "Titreşim kapalı"))
            {
                Progress.HapticsOn = !Progress.HapticsOn;
                _game.ApplySettings();
            }

            if (Progress.CurrentLevel > 1)
            {
                string text = _confirmReset ? "Emin misin? Tekrar dokun" : "İlerlemeyi sıfırla";
                if (GUI.Button(new Rect(w / 2 - 250, py + ph + 40f, 500, 70), text, _link))
                {
                    if (_confirmReset) { Progress.ResetLevels(); _confirmReset = false; }
                    else _confirmReset = true;
                }
            }

            GUI.Label(new Rect(0, h - 80 - Screen.safeArea.y / _scale, w, 50), Version, _small);
        }

        /// <summary>Modern mod kartı: gölge, degrade, ikon dairesi, başlık, oynat düğmesi.</summary>
        private bool ModeCard(Rect r, GUIStyle style, Texture2D icon, string title, string subtitle, Color accent, bool isNew)
        {
            Color old = GUI.color;

            // Gölge
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(r.x - 14, r.y + 6, r.width + 28, r.height + 34), _shadow);

            bool clicked = GUI.Button(r, GUIContent.none, style);

            // Sol: ikon dairesi
            float ic = 160f;
            Rect circle = new Rect(r.x + 36, r.y + (r.height - 10 - ic) / 2f, ic, ic);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(circle, _circle);
            GUI.color = Color.white;
            float pad = 32f;
            GUI.DrawTexture(new Rect(circle.x + pad, circle.y + pad, ic - 2 * pad, ic - 2 * pad), icon);

            // Yazılar
            float tx = circle.xMax + 34f;
            GUI.Label(new Rect(tx, r.y + 42, r.width - (tx - r.x) - 150, 90), title, _modeTitle);
            GUI.Label(new Rect(tx, r.y + 128, r.width - (tx - r.x) - 150, 60), subtitle, _modeSub);

            // Sağ: beyaz daire içinde oynat üçgeni (hafif nabız)
            float pulse = 1f + 0.04f * Mathf.Sin(Time.time * 4f);
            float pc = 110f * pulse;
            Rect play = new Rect(r.xMax - 40 - pc, r.y + (r.height - 10 - pc) / 2f, pc, pc);
            GUI.color = Color.white;
            GUI.DrawTexture(play, _circle);
            GUI.color = accent;
            float ip = pc * 0.3f;
            GUI.DrawTexture(new Rect(play.x + ip + 4, play.y + ip, pc - 2 * ip, pc - 2 * ip), _iconPlay);

            // Yeni rozeti
            if (isNew)
            {
                float bp = 1f + 0.07f * Mathf.Sin(Time.time * 6f);
                float bw = 150f * bp, bh = 62f * bp;
                GUI.color = Color.white;
                GUI.Label(new Rect(r.xMax - bw - 10, r.y - bh / 2, bw, bh), "YENİ!", _newBadge);
            }

            GUI.color = old;
            return clicked;
        }

        private bool Pill(Rect r, bool on, string text)
        {
            bool clicked = GUI.Button(r, GUIContent.none, on ? _pillOn : _pillOff);
            Color old = GUI.color;
            _pillText.normal.textColor = on ? new Color(0.2f, 0.3f, 0.6f) : Color.white;
            GUI.Label(r, (on ? "● " : "○ ") + text, _pillText);
            GUI.color = old;
            return clicked;
        }

        private void DrawTopBar(float w, float inset)
        {
            // Bar çentiğin arkasına kadar uzanır; içerik çentiğin altından başlar
            Rect top = new Rect(0, 0, w, inset + TopBarHeight);
            GUI.Box(top, GUIContent.none, _panel);
            Track(top);

            float y = inset + 25f;
            BoardState s = _game.State;
            bool endless = _game.Mode == GameController.GameMode.Endless && _game.Endless != null;
            if (endless)
            {
                EndlessDirector d = _game.Endless;
                GUI.Label(new Rect(40, y - 5, 330, 100), $"{d.Score}", _bigNumber);
                int best = Mathf.Max(Progress.EndlessBest, d.Score);
                GUI.Label(new Rect(300, y, 300, 90), $"Rekor {best}", _label);
            }
            else
            {
                GUI.Label(new Rect(40, y, 260, 90), $"Bölüm {_game.LevelNumber}", _label);
                GUI.Label(new Rect(290, y, 300, 90), $"Kamyon: {s.TotalTrucks - s.DepartedTrucks}", _label);
            }

            if (GUI.Button(new Rect(w - 490, y, 210, 90), "Menü", _secondary)) _game.ShowMenu();
            if (_game.State != null && GUI.Button(new Rect(w - 260, y, 220, 90), "Baştan", _button)) _game.RestartLevel();

            float below = inset + TopBarHeight + 10;
            if (endless && _game.Endless != null)
            {
                // Zorluk çubuğu: bir sonraki kademeye ne kadar kaldı
                EndlessDirector d = _game.Endless;
                GUI.Label(new Rect(40, below, 260, 50), $"Zorluk {d.Tier + 1}", _cardDesc);
                Rect bar = new Rect(280, below + 15, w - 320, 22);
                GUI.DrawTexture(bar, _barBack);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(d.TierProgress), bar.height), _barFill);
                below += 55;
            }

            if (_game.State != null && _game.CurrentPhase == GameController.Phase.Playing
                && _game.State.BufferUsed() >= _game.State.Buffer.Length - 1)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.55f, 0.5f);
                GUI.Label(new Rect(40, below, w - 80, 70), "Dikkat: rafta son 1 yer!", _label);
                GUI.color = old;
            }
        }

        private void DrawWin(float w, float h)
        {
            Rect panel = new Rect(90, h / 2 - 300, w - 180, 600);
            GUI.Box(panel, GUIContent.none, _panel);
            Track(panel);
            GUI.Label(new Rect(panel.x, panel.y + 50, panel.width, 100), "Teslimat tamam!", _title);
            GUI.Label(new Rect(panel.x + 60, panel.y + 160, panel.width - 120, 80),
                $"{_game.Moves} hamlede bitirdin", _label);

            int nextSlots = LevelGenerator.BufferSizeFor(_game.LevelNumber + 1);
            if (nextSlots > LevelGenerator.BufferSizeFor(_game.LevelNumber))
                GUI.Label(new Rect(panel.x + 60, panel.y + 230, panel.width - 120, 80),
                    $"Yeni raf slotu açıldı! Artık {nextSlots} slot.", _label);

            if (GUI.Button(new Rect(panel.x + 100, panel.y + 340, panel.width - 200, 120), "Sonraki Bölüm", _button))
                _game.NextLevel();
            if (GUI.Button(new Rect(panel.x + 200, panel.y + 480, panel.width - 400, 80), "Menü", _secondary))
                _game.ShowMenu();
        }

        private void DrawLose(float w, float h)
        {
            bool endless = _game.Mode == GameController.GameMode.Endless && _game.Endless != null;
            float panelH = endless ? 900f : 600f;
            Rect panel = new Rect(90, h / 2 - panelH / 2, w - 180, panelH);
            GUI.Box(panel, GUIContent.none, _panel);
            Track(panel);

            float y;
            if (endless)
            {
                EndlessDirector d = _game.Endless;
                GUI.Label(new Rect(panel.x, panel.y + 40, panel.width, 100), "Raf doldu!", _title);
                GUI.Label(new Rect(panel.x, panel.y + 150, panel.width, 130), $"{d.Score}", _logo);
                string record = _game.NewRecord ? "YENİ REKOR!" : $"Rekor: {Progress.EndlessBest}";
                Color old = GUI.color;
                if (_game.NewRecord) GUI.color = Palette.Warning;
                GUI.Label(new Rect(panel.x, panel.y + 290, panel.width, 70), record, _cardSub);
                GUI.color = old;
                GUI.Label(new Rect(panel.x, panel.y + 360, panel.width, 60),
                    $"{d.TrucksSent} kamyon  ·  Zorluk {d.Tier + 1}", _cardDesc);
                y = panel.y + 450;
            }
            else
            {
                GUI.Label(new Rect(panel.x, panel.y + 50, panel.width, 100), "Raf doldu!", _title);
                y = panel.y + 200;
            }

            if (!_game.ExtraSlotsUsed)
            {
                // TODO (Aşama 3): önce ödüllü reklam göster, izlenince slot ver
                if (GUI.Button(new Rect(panel.x + 100, y, panel.width - 200, 120), "+3 Slot (Reklam)", _button))
                    _game.GrantExtraSlots();
                y += 150;
            }
            if (GUI.Button(new Rect(panel.x + 100, y, panel.width - 200, 120), endless ? "Tekrar Oyna" : "Tekrar Dene", _button))
                _game.RestartLevel();
            if (endless)
            {
                y += 150;
                if (GUI.Button(new Rect(panel.x + 200, y, panel.width - 400, 80), "Menü", _secondary))
                    _game.ShowMenu();
            }
        }

        /// <summary>GUI koordinatındaki alanı dokunma kontrolü için ekran koordinatına çevirir.</summary>
        private void Track(Rect guiRect)
        {
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
            var screen = new Rect(guiRect.x * _scale,
                                  Screen.height - (guiRect.y + guiRect.height) * _scale,
                                  guiRect.width * _scale,
                                  guiRect.height * _scale);
            if (!HudRects.Contains(screen)) HudRects.Add(screen);
        }

        private void OnDestroy()
        {
            HudRects.Clear();
            IsVideo = false;
            foreach (Texture2D t in _textures) if (t != null) Destroy(t);
            _ui?.Dispose();
        }
    }
}
