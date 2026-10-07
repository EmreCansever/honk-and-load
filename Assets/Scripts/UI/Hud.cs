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

            float y = h * 0.12f;
            GUI.Label(new Rect(0, y, w, 160), "Honk & Load!", _logo);
            GUI.Label(new Rect(60, y + 170, w - 120, 70), "Kolileri yükle, kamyonları yolla!", _subtitle);

            // Basit bir "logo": renkli koli sırası
            float boxSize = 70f, gap = 18f;
            float rowWidth = 5 * boxSize + 4 * gap;
            Color old = GUI.color;
            for (int i = 0; i < 5; i++)
            {
                float bounce = Mathf.Abs(Mathf.Sin(Time.time * 3f + i * 0.6f)) * 10f;
                GUI.color = Palette.Crate(i);
                GUI.DrawTexture(new Rect((w - rowWidth) / 2 + i * (boxSize + gap), y + 280 - bounce, boxSize, boxSize), Texture2D.whiteTexture);
            }
            GUI.color = old;

            // İki mod kartı: eşit boyut, yan yana
            float cardGap = 40f, side = 100f;
            float cw = (w - 2 * side - cardGap) / 2f, ch = 440f;
            float cy = Mathf.Max(y + 420f, h * 0.40f);
            Rect left = new Rect(side, cy, cw, ch);
            Rect right = new Rect(side + cw + cardGap, cy, cw, ch);

            if (GUI.Button(left, GUIContent.none, _cardAdventure))
            {
                _confirmReset = false;
                _game.Play();
            }
            GUI.Label(new Rect(left.x, left.y + 60, left.width, 90), "MACERA", _cardTitle);
            GUI.Label(new Rect(left.x, left.y + 170, left.width, 70), $"Bölüm {Progress.CurrentLevel}", _cardSub);
            GUI.Label(new Rect(left.x + 20, left.y + 280, left.width - 40, 110), "Bölüm bölüm\nilerle", _cardDesc);

            if (GUI.Button(right, GUIContent.none, _cardEndless))
            {
                _confirmReset = false;
                _game.StartEndless();
            }
            int best = Progress.EndlessBest;
            GUI.Label(new Rect(right.x, right.y + 60, right.width, 90), "SONSUZ", _cardTitle);
            GUI.Label(new Rect(right.x, right.y + 170, right.width, 70), best > 0 ? $"Rekor: {best}" : "Rekor yok", _cardSub);
            GUI.Label(new Rect(right.x + 20, right.y + 280, right.width - 40, 110), "Puan topla,\nrekor kır", _cardDesc);

            if (best == 0)
            {
                // Yeni modu öne çıkaran atan rozet
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
                float bw2 = 150f * pulse, bh2 = 64f * pulse;
                GUI.Label(new Rect(right.xMax - bw2 + 20, right.y - bh2 / 2, bw2, bh2), "YENİ!", _badge);
            }

            float bw = w - 2 * side, bx = side;
            float by = cy + ch + 50f;
            if (GUI.Button(new Rect(bx, by, bw / 2 - 12, 110), Progress.SoundOn ? "Ses: Açık" : "Ses: Kapalı", _secondary))
            {
                Progress.SoundOn = !Progress.SoundOn;
                _game.ApplySettings();
            }
            if (GUI.Button(new Rect(bx + bw / 2 + 12, by, bw / 2 - 12, 110), Progress.HapticsOn ? "Titreşim: Açık" : "Titreşim: Kapalı", _secondary))
            {
                Progress.HapticsOn = !Progress.HapticsOn;
                _game.ApplySettings();
            }

            by += 150;
            if (Progress.CurrentLevel > 1)
            {
                string text = _confirmReset ? "Emin misin? Tekrar dokun" : "İlerlemeyi sıfırla";
                if (GUI.Button(new Rect(bx + bw / 4, by, bw / 2, 90), text, _secondary))
                {
                    if (_confirmReset) { Progress.ResetLevels(); _confirmReset = false; }
                    else _confirmReset = true;
                }
            }

            GUI.Label(new Rect(0, h - 90, w, 60), Version, _small);
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
        }
    }
}
