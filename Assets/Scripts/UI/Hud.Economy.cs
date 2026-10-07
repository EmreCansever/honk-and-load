using HonkAndLoad.Core;
using HonkAndLoad.Gameplay;
using UnityEngine;

namespace HonkAndLoad.UI
{
    /// <summary>Altın göstergesi, güçlendirici çubuğu, satın alma penceresi ve market ekranı.</summary>
    public partial class Hud
    {
        /// <summary>Android geri tuşu: açık pencere varsa kapatır (true döner).</summary>
        public bool HandleBack()
        {
            if (AdService.Instance != null && AdService.Instance.IsShowing) return true;
            if (_buyDialog >= 0) { _buyDialog = -1; return true; }
            if (_shopOpen) { _shopOpen = false; return true; }
            return false;
        }

        public void CloseModals()
        {
            _buyDialog = -1;
            _shopOpen = false;
        }

        /// <summary>Ekran taraması için: satın alma penceresini aç.</summary>
        public void OpenBuyDialog(BoosterType type) => _buyDialog = (int)type;

        // ---------- Küçük parçalar ----------

        /// <summary>Altın göstergesi; sayı değişince yumuşakça sayar. Dokununca true.</summary>
        private bool CoinPill(Rect r)
        {
            int coins = Economy.Coins;
            if (_coinShown < 0f) _coinShown = coins;
            _coinShown = Mathf.MoveTowards(_coinShown, coins, Mathf.Max(4f, Mathf.Abs(coins - _coinShown) * 6f) * Time.unscaledDeltaTime);
            bool clicked = GUI.Button(r, GUIContent.none, _tile);
            float ic = r.height - 20;
            GUI.DrawTexture(new Rect(r.x + 12, r.y + 10, ic, ic), _coinTex);
            GUI.Label(new Rect(r.x + ic + 28, r.y, r.width - ic - 40, r.height), Mathf.RoundToInt(_coinShown).ToString("N0"), _coinText);
            return clicked;
        }

        private bool MarketButton(Rect r)
        {
            bool clicked = GUI.Button(r, GUIContent.none, _pillOn);
            Color old = GUI.color;
            GUI.color = new Color(0.95f, 0.55f, 0.08f);
            float ic = r.height - 34;
            GUI.DrawTexture(new Rect(r.x + 26, r.y + 15, ic, ic), _bagTex);
            GUI.color = old;
            _pillText.normal.textColor = new Color(0.2f, 0.3f, 0.6f);
            GUI.Label(new Rect(r.x + ic + 30, r.y, r.width - ic - 50, r.height), "Market", _pillText);
            return clicked;
        }

        /// <summary>Panellerde "+20 [altın]" satırı.</summary>
        private void RewardRow(Rect r, int amount, bool boosted)
        {
            if (amount <= 0) return;
            string text = $"+{amount}";
            float tw = _bigNumber.CalcSize(new GUIContent(text)).x;
            float ic = 80f;
            float total = ic + 16 + tw;
            float x = r.x + (r.width - total) / 2;
            GUI.DrawTexture(new Rect(x, r.y + (r.height - ic) / 2, ic, ic), _coinTex);
            Color old = GUI.color;
            if (boosted) GUI.color = Palette.Warning;
            GUI.Label(new Rect(x + ic + 16, r.y, tw + 20, r.height), text, _bigNumber);
            GUI.color = old;
        }

        /// <summary>Solda ikon, ortada yazı olan buton.</summary>
        private bool IconButton(Rect r, Texture2D icon, string text, GUIStyle style)
        {
            bool clicked = GUI.Button(r, GUIContent.none, style);
            var content = new GUIContent(text);
            float tw = style.CalcSize(content).x;
            float ic = Mathf.Min(r.height - 40, 70);
            float total = ic + 18 + tw;
            float x = r.x + Mathf.Max(20, (r.width - total) / 2);
            GUI.DrawTexture(new Rect(x, r.y + (r.height - ic) / 2, ic, ic), icon);
            // Buton stilinin kopyası arka planını (scaledBackgrounds) taşıdığı için düz yazı stili
            var label = Text(style.fontSize, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            label.wordWrap = false;
            GUI.Label(new Rect(x + ic + 18, r.y, r.width - (x - r.x) - ic - 18, r.height), text, label);
            return clicked;
        }

        // ---------- Güçlendirici çubuğu ----------

        private void DrawBoosterBar(float w, float h)
        {
            float bottomInset = Screen.safeArea.y / _scale;
            float side = 40f, gap = 24f;
            int n = Economy.Boosters.Length;
            float bw = (w - 2 * side - (n - 1) * gap) / n;
            float bh = BoosterBarHeight - 20f;
            float y = h - bottomInset - BoosterBarHeight;
            Track(new Rect(0, y - 30, w, BoosterBarHeight + 30));

            for (int i = 0; i < n; i++)
            {
                Economy.BoosterInfo info = Economy.Boosters[i];
                Rect r = new Rect(side + i * (bw + gap), y, bw, bh);
                bool unlocked = Economy.IsUnlocked(info.Type);
                bool usable = unlocked && _game.CanUseBooster(info.Type, out _);
                bool hiddenInMode = info.AdventureOnly && _game.Mode == GameController.GameMode.Endless;

                Color old = GUI.color;
                if (!usable) GUI.color = new Color(1f, 1f, 1f, hiddenInMode ? 0.35f : 0.6f);
                bool clicked = GUI.Button(r, GUIContent.none, unlocked ? _tileOn : _tile);

                float ic = 74f;
                Rect iconRect = new Rect(r.x + (r.width - ic) / 2, r.y + 20, ic, ic);
                if (unlocked)
                {
                    GUI.color = new Color(0.2f, 0.35f, 0.75f, usable ? 1f : 0.5f);
                    GUI.DrawTexture(iconRect, _boosterIcons[i]);
                    GUI.color = usable ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                    GUI.Label(new Rect(r.x, r.y + r.height - 62, r.width, 50), info.Name, _boosterName);

                    int count = Economy.Count(info.Type);
                    if (count > 0)
                        GUI.Label(new Rect(r.xMax - 64, r.y - 18, 76, 58), count.ToString(), _countBadge);
                    else
                    {
                        Rect pr = new Rect(r.xMax - 128, r.y - 18, 140, 58);
                        GUI.Label(pr, info.Price.ToString(), _priceBadge);
                        GUI.DrawTexture(new Rect(pr.x + 10, pr.y + 9, 40, 40), _coinTex);
                    }
                }
                else
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.8f);
                    GUI.DrawTexture(new Rect(r.x + (r.width - 60) / 2, r.y + 26, 60, 60), _lockTex);
                    GUI.Label(new Rect(r.x, r.y + r.height - 62, r.width, 50), $"Bölüm {info.UnlockLevel}", _cardDesc);
                }
                GUI.color = old;

                if (clicked)
                {
                    if (!unlocked) Toast($"{info.Name}: Bölüm {info.UnlockLevel}'de açılır");
                    else TapBooster(info.Type);
                }
            }
        }

        /// <summary>Güçlendiriciye dokunuldu: varsa kullan, yoksa satın alma penceresi.</summary>
        private void TapBooster(BoosterType type)
        {
            if (!_game.CanUseBooster(type, out string reason))
            {
                if (!string.IsNullOrEmpty(reason)) Toast(reason);
                return;
            }
            if (Economy.Count(type) > 0) _game.UseBooster(type);
            else _buyDialog = (int)type;
        }

        // ---------- Satın alma penceresi ----------

        private void DrawBuyDialog(float w, float h)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = old;
            Track(new Rect(0, 0, w, h));

            Economy.BoosterInfo info = Economy.Boosters[_buyDialog];
            bool enough = Economy.Coins >= info.Price;
            Rect panel = new Rect(90, h / 2 - 420, w - 180, 840);
            GUI.DrawTexture(new Rect(panel.x - 14, panel.y + 6, panel.width + 28, panel.height + 34), _shadow);
            GUI.Box(panel, GUIContent.none, _cardBlue);

            float ic = 170f;
            Rect circle = new Rect(panel.x + (panel.width - ic) / 2, panel.y + 50, ic, ic);
            GUI.DrawTexture(circle, _circle);
            GUI.color = new Color(0.2f, 0.35f, 0.75f);
            GUI.DrawTexture(new Rect(circle.x + 40, circle.y + 40, ic - 80, ic - 80), _boosterIcons[_buyDialog]);
            GUI.color = old;

            GUI.Label(new Rect(panel.x, panel.y + 240, panel.width, 90), info.Name, _title);
            GUI.Label(new Rect(panel.x + 60, panel.y + 335, panel.width - 120, 100), info.Description, _cardDesc);

            CoinPill(new Rect(panel.x + (panel.width - 300) / 2, panel.y + 450, 300, 90));

            Rect buy = new Rect(panel.x + 90, panel.y + 570, panel.width - 180, 120);
            if (enough)
            {
                if (IconButton(buy, _coinTex, $"{info.Price} ile al ve kullan", _goldButton))
                {
                    if (Economy.TryBuyBooster(info.Type))
                    {
                        _buyDialog = -1;
                        _game.UseBooster(info.Type);
                    }
                }
            }
            else if (IconButton(buy, _bagTex, $"Altın yetmiyor · Market", _goldButton))
            {
                _buyDialog = -1;
                OpenShop();
            }
            if (GUI.Button(new Rect(panel.x + 200, panel.y + 715, panel.width - 400, 90), "Vazgeç", _secondary))
                _buyDialog = -1;
        }

        // ---------- Market ----------

        private void DrawShop(float w, float h)
        {
            Track(new Rect(0, 0, w, h));
            GUI.DrawTexture(new Rect(0, 0, w, h), _menuBg);
            Color old = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.22f, 0.25f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = old;

            float inset = SafeTopInsetPixels() / _scale;
            float top = inset + 30f, side = 50f, cw = w - 2 * side;
            CoinPill(new Rect(side, top, 300, 96));
            GUI.Label(new Rect(0, top - 4, w, 104), "MARKET", _shopTitle);
            if (GUI.Button(new Rect(w - side - 96, top, 96, 96), GUIContent.none, _pillOff)) _shopOpen = false;
            GUI.DrawTexture(new Rect(w - side - 96 + 26, top + 26, 44, 44), _closeTex);

            // Ekran kısa ise kartları sıkıştır
            float y = top + 130f;
            float available = h - y - Screen.safeArea.y / _scale - 40f;
            Store.Product starter = Store.Find("starter_pack"), noAds = Store.Find("no_ads");
            bool showStarter = !Store.IsOwned(starter), showNoAds = !Store.IsOwned(noAds) && !Progress.NoAds;
            float need = (showStarter ? 290 : 0) + (showNoAds ? 190 : 0) + 330 + 180 + 60 + 260 + 90;
            float k = Mathf.Clamp(available / need, 0.75f, 1f);
            float gap = 24f * k;

            if (showStarter)
            {
                Rect r = new Rect(side, y, cw, 270 * k);
                ProductCard(r, starter, _cardPurple, _bagTex);
                y += r.height + gap;
            }
            if (showNoAds)
            {
                Rect r = new Rect(side, y, cw, 170 * k);
                ProductCard(r, noAds, _cardBlue, _adTex);
                y += r.height + gap;
            }

            // Altın paketleri
            float tw = (cw - 2 * 24f) / 3f, th = 320 * k;
            int i = 0;
            foreach (string id in new[] { "coins_1000", "coins_3000", "coins_7500" })
            {
                Store.Product p = Store.Find(id);
                CoinPackCard(new Rect(side + i * (tw + 24f), y, tw, th), p, i);
                i++;
            }
            y += th + gap;

            // Reklamla ücretsiz altın
            {
                Rect r = new Rect(side, y, cw, 160 * k);
                int left = Economy.FreeCoinAdsLeft;
                GUI.Box(r, GUIContent.none, _modeAdventure);
                GUI.DrawTexture(new Rect(r.x + 36, r.y + (r.height - 10 - 90) / 2, 90, 90), _coinTex);
                GUI.Label(new Rect(r.x + 150, r.y + 18 * k, r.width - 500, 70), $"Ücretsiz {Economy.FreeCoinsPerAd} altın", _itemTitle);
                GUI.Label(new Rect(r.x + 152, r.y + 86 * k, r.width - 500, 50), $"Bugün kalan: {left}/{Economy.FreeCoinAdsPerDay}", _itemSub);
                Rect btn = new Rect(r.xMax - 290, r.y + (r.height - 10 - 96) / 2, 250, 96);
                GUI.enabled = left > 0;
                if (PriceButton(btn, left > 0 ? "İzle" : "Yarın", _adTex))
                    AdService.Instance.ShowRewarded(ok =>
                    {
                        if (!ok) return;
                        Economy.UseFreeCoinAd();
                        Toast($"+{Economy.FreeCoinsPerAd} altın!");
                    });
                GUI.enabled = true;
                y += r.height + gap;
            }

            // Güçlendiriciler (altınla)
            GUI.Label(new Rect(side + 10, y, cw, 56), "Güçlendiriciler", _sectionLabel);
            y += 60;
            float bw = (cw - 3 * 20f) / 4f, bh = 250 * k;
            for (int b = 0; b < Economy.Boosters.Length; b++)
                BoosterShopCard(new Rect(side + b * (bw + 20f), y, bw, bh), Economy.Boosters[b]);
            y += bh + gap;

            // Alt bilgi
            string note = Store.IsSimulated ? "Test modu: satın alımlar ücretsiz simüle edilir" : "";
            if (GUI.Button(new Rect(w / 2 - 300, y, 600, 56), "Satın alımları geri yükle", _link))
                Store.Restore(Toast);
            if (note.Length > 0) GUI.Label(new Rect(0, y + 52, w, 44), note, _small);
        }

        private bool PriceButton(Rect r, string text, Texture2D icon = null)
        {
            bool clicked = GUI.Button(r, GUIContent.none, _priceButton);
            if (icon != null)
            {
                Color old = GUI.color;
                float ic = r.height - 44;
                float tw = _priceButtonText.CalcSize(new GUIContent(text)).x;
                float x = r.x + (r.width - ic - 12 - tw) / 2;
                GUI.color = new Color(0.15f, 0.22f, 0.45f);
                GUI.DrawTexture(new Rect(x, r.y + 22, ic, ic), icon);
                GUI.color = old;
                GUI.Label(new Rect(x + ic + 12, r.y, tw + 10, r.height), text, _priceButtonText);
            }
            else GUI.Label(r, text, _priceButtonText);
            return clicked;
        }

        private void ProductCard(Rect r, Store.Product p, GUIStyle style, Texture2D icon)
        {
            GUI.DrawTexture(new Rect(r.x - 14, r.y + 6, r.width + 28, r.height + 34), _shadow);
            GUI.Box(r, GUIContent.none, style);
            float ic = Mathf.Min(130f, r.height - 50);
            Rect circle = new Rect(r.x + 30, r.y + (r.height - 10 - ic) / 2, ic, ic);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(circle, _circle);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(circle.x + ic * 0.22f, circle.y + ic * 0.22f, ic * 0.56f, ic * 0.56f), icon);
            GUI.color = old;

            float tx = circle.xMax + 26;
            float textW = r.xMax - 300 - tx;
            bool tall = r.height > 200;
            GUI.Label(new Rect(tx, r.y + (tall ? 30 : 18), textW, 70), p.Title, _itemTitle);
            GUI.Label(new Rect(tx + 2, r.y + (tall ? 100 : 86), textW, tall ? 120 : 60), p.Subtitle, _itemSub);

            Rect btn = new Rect(r.xMax - 280, r.y + (r.height - 10 - 100) / 2, 250, 100);
            if (PriceButton(btn, Store.PriceText(p))) Buy(p);
            if (!string.IsNullOrEmpty(p.Badge))
                GUI.Label(new Rect(r.xMax - 270, r.y - 26, 240, 54), p.Badge, _cardBadge);
        }

        private void CoinPackCard(Rect r, Store.Product p, int index)
        {
            GUI.DrawTexture(new Rect(r.x - 10, r.y + 6, r.width + 20, r.height + 26), _shadow);
            GUI.Box(r, GUIContent.none, _cardGold);
            // Paket büyüdükçe daha çok altın yığını
            float ic = 80f;
            int coins = index + 1;
            float cx = r.x + r.width / 2, cy = r.y + 30;
            for (int c = 0; c < coins; c++)
            {
                float ox = (c - (coins - 1) / 2f) * 46f;
                GUI.DrawTexture(new Rect(cx - ic / 2 + ox, cy + (c % 2) * 10, ic, ic), _coinTex);
            }
            var title = new GUIStyle(_itemTitle) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(r.x, r.y + r.height * 0.36f, r.width, 64), p.Title, title);
            var sub = new GUIStyle(_itemSub) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(r.x, r.y + r.height * 0.36f + 56, r.width, 40), p.Subtitle, sub);
            Rect btn = new Rect(r.x + 18, r.yMax - 110, r.width - 36, 84);
            if (PriceButton(btn, Store.PriceText(p))) Buy(p);
            if (!string.IsNullOrEmpty(p.Badge))
                GUI.Label(new Rect(r.x + 10, r.y - 24, r.width - 20, 50), p.Badge, _cardBadge);
        }

        private void BoosterShopCard(Rect r, Economy.BoosterInfo info)
        {
            GUI.Box(r, GUIContent.none, _tileOn);
            float ic = 70f;
            Color old = GUI.color;
            GUI.color = new Color(0.2f, 0.35f, 0.75f);
            GUI.DrawTexture(new Rect(r.x + (r.width - ic) / 2, r.y + 18, ic, ic), _boosterIcons[(int)info.Type]);
            GUI.color = old;
            GUI.Label(new Rect(r.x, r.y + 92, r.width, 44), info.Name, _boosterName);
            var have = new GUIStyle(_boosterName) { fontSize = 26, fontStyle = FontStyle.Normal };
            GUI.Label(new Rect(r.x, r.y + 128, r.width, 36), $"Sende: {Economy.Count(info.Type)}", have);

            Rect btn = new Rect(r.x + 12, r.yMax - 76, r.width - 24, 64);
            bool clicked = GUI.Button(btn, GUIContent.none, _priceBadge);
            GUI.DrawTexture(new Rect(btn.x + 14, btn.y + 12, 40, 40), _coinTex);
            var price = Text(_priceBadge.fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, _priceBadge.normal.textColor);
            price.padding = new RectOffset(44, 0, 0, 0);
            GUI.Label(btn, info.Price.ToString(), price);
            if (clicked)
            {
                if (Economy.TryBuyBooster(info.Type)) Toast($"+1 {info.Name}");
                else Toast("Altının yetmiyor");
            }
        }

        private void Buy(Store.Product p)
        {
            Store.Purchase(p, (ok, message) =>
            {
                if (ok) Toast(p.Coins > 0 ? $"+{p.Coins:N0} altın! ({message})" : $"{p.Title} alındı! ({message})");
                else Toast(message);
            });
        }

        // ---------- Üst katman: bildirim ve test reklamı ----------

        private void DrawOverlays(float w, float h, bool adShowing)
        {
            float age = Time.time - _toastStart;
            if (age < ToastLife && !string.IsNullOrEmpty(_toastText))
            {
                float a = age > ToastLife - 0.3f ? (ToastLife - age) / 0.3f : Mathf.Clamp01(age / 0.15f);
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, a);
                float tw = Mathf.Min(w - 80, _toast.CalcSize(new GUIContent(_toastText)).x + 20);
                float bottom = Screen.safeArea.y / _scale;
                GUI.Label(new Rect((w - tw) / 2, h - bottom - BoosterBarHeight - 130, tw, 90), _toastText, _toast);
                GUI.color = old;
            }

            if (adShowing)
            {
                Track(new Rect(0, 0, w, h));
                Color old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.9f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(w / 2 - 90, h * 0.4f - 180, 180, 180), _adTex);
                GUI.Label(new Rect(0, h * 0.4f + 20, w, 90), "Test reklamı", _title);
                GUI.Label(new Rect(0, h * 0.4f + 110, w, 60), "Gerçek reklamlar AdMob bağlanınca gelecek", _cardDesc);
                Rect bar = new Rect(140, h * 0.4f + 210, w - 280, 26);
                GUI.DrawTexture(bar, _barBack);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * AdService.Instance.ShowProgress, bar.height), _barFill);
                GUI.color = old;
            }
        }
    }
}
