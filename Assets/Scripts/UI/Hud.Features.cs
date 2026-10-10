using HonkAndLoad.Core;
using HonkAndLoad.Gameplay;
using UnityEngine;

namespace HonkAndLoad.UI
{
    /// <summary>Kilit rozetleri, yeni mekanik tanıtımı ve günlük ödül penceresi.</summary>
    public partial class Hud
    {
        // ---------- Kilitli sütun rozetleri ----------

        private void DrawLockBadges()
        {
            BoardState s = _game.State;
            Camera cam = Camera.main;
            if (s == null || s.LockAt == null || cam == null) return;
            Color old = GUI.color;
            for (int c = 0; c < s.Columns.Count; c++)
            {
                int left = s.LockRemaining(c);
                if (left <= 0) continue;
                Vector3 sp = cam.WorldToScreenPoint(_game.LockBadgeWorld(c));
                if (sp.z < 0f) continue;
                float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale;
                float size = 70f;
                GUI.color = new Color(0.12f, 0.14f, 0.22f, 0.92f);
                GUI.DrawTexture(new Rect(x - size / 2, y - size / 2, size, size), _circle);
                GUI.color = Palette.Warning;
                GUI.DrawTexture(new Rect(x - size / 2 - 34, y - 20, 40, 40), _lockTex);
                GUI.color = Color.white;
                var number = new GUIStyle(_bigNumber) { alignment = TextAnchor.MiddleCenter, fontSize = 42 };
                GUI.Label(new Rect(x - size / 2, y - size / 2, size, size), left.ToString(), number);
            }
            GUI.color = old;
        }

        // ---------- Yeni mekanik tanıtımı ----------

        private void DrawIntro(float w, float h)
        {
            string text = _game.IntroText;
            if (string.IsNullOrEmpty(text) || _game.CurrentPhase != GameController.Phase.Playing) return;
            float bottomInset = Screen.safeArea.y / _scale;
            if (BoosterBarVisible(_game)) bottomInset += BoosterBarHeight + 10f;
            Rect r = new Rect(60, h - bottomInset - 250, w - 120, 210);
            GUI.Label(r, text, _bubble);
        }

        // ---------- Günlük ödül ----------

        private bool GiftButton(Rect r)
        {
            bool clicked = GUI.Button(r, GUIContent.none, _pillOn);
            Color old = GUI.color;
            GUI.color = new Color(0.93f, 0.2f, 0.32f);
            GUI.DrawTexture(new Rect(r.x + 20, r.y + 18, r.width - 40, r.height - 38), _giftTex);
            if (Economy.DailyAvailable)
            {
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
                float d = 30f * pulse;
                GUI.color = new Color(0.93f, 0.2f, 0.32f);
                GUI.DrawTexture(new Rect(r.xMax - d + 4, r.y - 4, d, d), _circle);
            }
            GUI.color = old;
            return clicked;
        }

        private void DrawDaily(float w, float h)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = old;
            Track(new Rect(0, 0, w, h));

            bool available = Economy.DailyAvailable;
            int today = Economy.DailyDayIndex;
            float ph = 1180f;
            Rect panel = new Rect(60, h / 2 - ph / 2, w - 120, ph);
            GUI.DrawTexture(new Rect(panel.x - 14, panel.y + 6, panel.width + 28, panel.height + 34), _shadow);
            GUI.Box(panel, GUIContent.none, _cardBlue);

            if (GUI.Button(new Rect(panel.xMax - 116, panel.y + 24, 92, 92), GUIContent.none, _pillOff)) _dailyOpen = false;
            GUI.DrawTexture(new Rect(panel.xMax - 116 + 25, panel.y + 24 + 25, 42, 42), _closeTex);

            GUI.DrawTexture(new Rect(panel.x + panel.width / 2 - 70, panel.y + 30, 140, 140), _giftTex);
            GUI.Label(new Rect(panel.x, panel.y + 175, panel.width, 90), Loc.T("Günlük Ödül"), _title);
            GUI.Label(new Rect(panel.x + 40, panel.y + 262, panel.width - 80, 50), Loc.T("Her gün gel, ödül büyüsün!"), _cardDesc);

            // 4 + 3 kutucuk
            float gx = panel.x + 36, gw = panel.width - 72, gap = 18f;
            float tw = (gw - 3 * gap) / 4f, th = 220f;
            float y = panel.y + 340;
            for (int i = 0; i < 7; i++)
            {
                Rect t;
                if (i < 4) t = new Rect(gx + i * (tw + gap), y, tw, th);
                else
                {
                    float tw2 = (gw - 2 * gap) / 3f;
                    t = new Rect(gx + (i - 4) * (tw2 + gap), y + th + gap, tw2, th);
                }
                bool claimed = available ? i < today : i <= today;
                bool isToday = available && i == today;
                DailyTile(t, i, claimed, isToday);
            }

            float by = y + 2 * th + gap + 40;
            if (available)
            {
                Economy.DailyReward r = Economy.DailyRewards[today];
                if (IconButton(new Rect(panel.x + 70, by, panel.width - 140, 120), _adTex, Loc.T("Reklam izle: 2 katı"), _goldButton))
                    AdService.Instance.ShowRewarded(ok => { if (ok) ClaimDaily(true); });
                if (GUI.Button(new Rect(panel.x + 70, by + 140, panel.width - 140, 110), Loc.T("Al"), _button))
                    ClaimDaily(false);
            }
            else
            {
                GUI.Label(new Rect(panel.x, by + 20, panel.width, 70), Loc.T("Yarın yine gel!"), _cardSub);
                GUI.Label(new Rect(panel.x, by + 90, panel.width, 50), Loc.T("Bir gün kaçırırsan seri baştan başlar"), _cardDesc);
            }
        }

        private void ClaimDaily(bool doubled)
        {
            if (!Economy.DailyAvailable) return;
            Economy.DailyReward r = Economy.ClaimDaily(doubled);
            int coins = doubled ? r.Coins * 2 : r.Coins;
            string text = Loc.F("+{0} altın!", coins);
            if (r.Booster >= 0) text += "  +1 " + Economy.Boosters[r.Booster].Name;
            Toast(text);
            _dailyOpen = false;
        }

        private void DailyTile(Rect t, int index, bool claimed, bool isToday)
        {
            Economy.DailyReward r = Economy.DailyRewards[index];
            Color old = GUI.color;
            if (claimed) GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.Box(t, GUIContent.none, isToday ? _cardGold : _tileOn);
            var day = new GUIStyle(_boosterName) { fontSize = 30 };
            GUI.Label(new Rect(t.x, t.y + 10, t.width, 44), Loc.F("Gün {0}", index + 1), day);

            float ic = 64f;
            bool hasBooster = r.Booster >= 0;
            float cx = t.x + t.width / 2;
            if (hasBooster)
            {
                GUI.DrawTexture(new Rect(cx - ic - 4, t.y + 62, ic, ic), _coinTex);
                Color o2 = GUI.color;
                GUI.color = new Color(0.2f, 0.35f, 0.75f, GUI.color.a);
                GUI.DrawTexture(new Rect(cx + 4, t.y + 62, ic, ic), _boosterIcons[r.Booster]);
                GUI.color = o2;
            }
            else GUI.DrawTexture(new Rect(cx - ic / 2, t.y + 62, ic, ic), _coinTex);

            var amount = new GUIStyle(_boosterName) { fontSize = 40 };
            GUI.Label(new Rect(t.x, t.y + 136, t.width, 60), r.Coins.ToString(), amount);

            if (claimed)
            {
                GUI.color = new Color(0.15f, 0.7f, 0.35f);
                GUI.DrawTexture(new Rect(t.xMax - 58, t.y + 8, 50, 50), _checkTex);
            }
            GUI.color = old;
        }
    }
}
