using System.Collections.Generic;
using HonkAndLoad.Gameplay;
using UnityEngine;

namespace HonkAndLoad.UI
{
    /// <summary>
    /// Prototip arayüzü (IMGUI). Aşama 3'te uGUI / UI Toolkit ile değiştirilecek.
    /// 1080 px genişliğe göre tasarlanır ve ekrana göre ölçeklenir.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        private const float DesignWidth = 1080f;

        private GameController _game;
        private float _scale = 1f;
        private GUIStyle _title, _label, _button, _panel;
        private Texture2D _panelTex, _buttonTex;

        // Bu karede çizilen arayüz alanları (ekran koordinatı, y aşağıdan yukarı)
        private static readonly List<Rect> HudRects = new List<Rect>();

        public void Init(GameController game) => _game = game;

        public static bool IsPointerOverHud(Vector2 screenPos)
        {
            foreach (Rect r in HudRects) if (r.Contains(screenPos)) return true;
            return false;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _panelTex = MakeTex(new Color(0f, 0f, 0f, 0.55f));
            _buttonTex = MakeTex(new Color(0.15f, 0.65f, 0.35f, 1f));

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _title.normal.textColor = Color.white;

            _label = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleLeft };
            _label.normal.textColor = Color.white;

            _button = new GUIStyle(GUI.skin.button) { fontSize = 42, fontStyle = FontStyle.Bold };
            _button.normal.background = _buttonTex;
            _button.hover.background = _buttonTex;
            _button.active.background = _buttonTex;
            _button.normal.textColor = Color.white;
            _button.hover.textColor = Color.white;
            _button.active.textColor = new Color(0.85f, 1f, 0.9f);

            _panel = new GUIStyle(GUI.skin.box);
            _panel.normal.background = _panelTex;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (_game == null || _game.State == null) return;
            EnsureStyles();
            if (Event.current.type == EventType.Layout) HudRects.Clear();

            _scale = Screen.width / DesignWidth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float w = DesignWidth;
            float h = Screen.height / _scale;

            // Üst bar
            Rect top = new Rect(0, 0, w, 120);
            GUI.Box(top, GUIContent.none, _panel);
            GUI.Label(new Rect(40, 20, 500, 80), $"Bölüm {_game.LevelNumber}", _label);
            BoardState s = _game.State;
            int left = s.TotalTrucks - s.DepartedTrucks;
            GUI.Label(new Rect(w / 2 - 140, 20, 400, 80), $"Kamyon: {left}", _label);
            Track(top);

            Rect restart = new Rect(w - 260, 20, 220, 80);
            if (GUI.Button(restart, "Baştan", _button)) _game.RestartLevel();

            // Raf doluluk bilgisi
            int used = s.BufferUsed();
            if (_game.CurrentPhase == GameController.Phase.Playing && used >= s.Buffer.Length - 1)
            {
                GUI.Label(new Rect(40, 130, w - 80, 60), "Raf dolmak üzere!", _label);
            }

            if (_game.CurrentPhase == GameController.Phase.Won) DrawWin(w, h);
            else if (_game.CurrentPhase == GameController.Phase.Lost) DrawLose(w, h);
        }

        private void DrawWin(float w, float h)
        {
            Rect panel = new Rect(90, h / 2 - 260, w - 180, 520);
            GUI.Box(panel, GUIContent.none, _panel);
            Track(panel);
            GUI.Label(new Rect(panel.x, panel.y + 60, panel.width, 100), "Teslimat tamam!", _title);
            GUI.Label(new Rect(panel.x + 60, panel.y + 180, panel.width - 120, 80),
                $"{_game.Moves} hamlede bitirdin", _label);
            if (GUI.Button(new Rect(panel.x + 100, panel.y + 330, panel.width - 200, 120), "Sonraki Bölüm", _button))
                _game.NextLevel();
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
            if (_panelTex != null) Destroy(_panelTex);
            if (_buttonTex != null) Destroy(_buttonTex);
        }
    }
}
