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
        private readonly List<Texture2D> _textures = new List<Texture2D>();

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
            return SafeTopInsetPixels() + (TopBarHeight + 90f) * scale; // bar + "Raf dolmak üzere" yazısı
        }

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

            DrawTopBar(w, SafeTopInsetPixels() / _scale);
            if (_game.CurrentPhase == GameController.Phase.Won) DrawWin(w, h);
            else if (_game.CurrentPhase == GameController.Phase.Lost) DrawLose(w, h);
        }

        private void DrawMenu(float w, float h)
        {
            Track(new Rect(0, 0, w, h));

            float y = h * 0.16f;
            GUI.Label(new Rect(0, y, w, 160), "Honk & Load!", _logo);
            GUI.Label(new Rect(60, y + 170, w - 120, 70), "Kolileri yükle, kamyonları yolla!", _subtitle);

            // Basit bir "logo": renkli koli sırası
            float boxSize = 70f, gap = 18f;
            float rowWidth = 5 * boxSize + 4 * gap;
            Color old = GUI.color;
            for (int i = 0; i < 5; i++)
            {
                GUI.color = Palette.Crate(i);
                GUI.DrawTexture(new Rect((w - rowWidth) / 2 + i * (boxSize + gap), y + 280, boxSize, boxSize), Texture2D.whiteTexture);
            }
            GUI.color = old;

            float bw = w - 240, bx = 120;
            float by = h * 0.55f;
            if (GUI.Button(new Rect(bx, by, bw, 160), $"Oyna  ·  Bölüm {Progress.CurrentLevel}", _button))
            {
                _confirmReset = false;
                _game.Play();
            }

            by += 200;
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
            GUI.Label(new Rect(40, y, 260, 90), $"Bölüm {_game.LevelNumber}", _label);
            GUI.Label(new Rect(290, y, 300, 90), $"Kamyon: {s.TotalTrucks - s.DepartedTrucks}", _label);

            if (GUI.Button(new Rect(w - 490, y, 210, 90), "Menü", _secondary)) _game.ShowMenu();
            if (_game.State != null && GUI.Button(new Rect(w - 260, y, 220, 90), "Baştan", _button)) _game.RestartLevel();

            if (_game.State != null && _game.CurrentPhase == GameController.Phase.Playing
                && _game.State.BufferUsed() >= _game.State.Buffer.Length - 1)
            {
                GUI.Label(new Rect(40, inset + TopBarHeight + 10, w - 80, 70), "Raf dolmak üzere!", _label);
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
            Rect panel = new Rect(90, h / 2 - 300, w - 180, 600);
            GUI.Box(panel, GUIContent.none, _panel);
            Track(panel);
            GUI.Label(new Rect(panel.x, panel.y + 50, panel.width, 100), "Raf doldu!", _title);

            float y = panel.y + 200;
            if (!_game.ExtraSlotsUsed)
            {
                // TODO (Aşama 3): önce ödüllü reklam göster, izlenince slot ver
                if (GUI.Button(new Rect(panel.x + 100, y, panel.width - 200, 120), "+3 Slot (Reklam)", _button))
                    _game.GrantExtraSlots();
                y += 160;
            }
            if (GUI.Button(new Rect(panel.x + 100, y, panel.width - 200, 120), "Tekrar Dene", _button))
                _game.RestartLevel();
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
            foreach (Texture2D t in _textures) if (t != null) Destroy(t);
        }
    }
}
