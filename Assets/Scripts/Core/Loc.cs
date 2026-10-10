using System.Collections.Generic;
using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>
    /// Basit çeviri: kaynak dil Türkçe. Kodda metinler Türkçe yazılır ve Loc.T / Loc.F ile
    /// sarılır; İngilizce seçiliyse aşağıdaki sözlükten karşılığı gelir. Sözlükte olmayan
    /// metin Türkçe kalır (Editor'da uyarı verir).
    /// </summary>
    public static class Loc
    {
        public enum Language { Auto = 0, Turkish = 1, English = 2 }

        private const string Key = "language";

        /// <summary>Oyuncunun seçimi (Otomatik = telefonun dili).</summary>
        public static Language Setting
        {
            get => (Language)Progress.GetInt(Key, 0);
            set { Progress.SetInt(Key, (int)value); _cached = null; }
        }

        private static bool? _cached;

        public static bool IsEnglish
        {
            get
            {
                if (_cached.HasValue) return _cached.Value;
                Language s = Setting;
                bool en = s == Language.English
                          || (s == Language.Auto && Application.systemLanguage != SystemLanguage.Turkish);
                _cached = en;
                return en;
            }
        }

        public static string SettingName(Language l) =>
            l == Language.Turkish ? "Türkçe" : l == Language.English ? "English" : T("Otomatik");

        /// <summary>Metni çevirir.</summary>
        public static string T(string tr)
        {
            if (!IsEnglish || string.IsNullOrEmpty(tr)) return tr;
            if (En.TryGetValue(tr, out string en)) return en;
#if UNITY_EDITOR
            if (Missing.Add(tr)) Debug.LogWarning("[Loc] Çevirisi yok: " + tr);
#endif
            return tr;
        }

        /// <summary>Biçimli metin: Loc.F("Bölüm {0}", n).</summary>
        public static string F(string trFormat, params object[] args) => string.Format(T(trFormat), args);

#if UNITY_EDITOR
        private static readonly HashSet<string> Missing = new HashSet<string>();
#endif

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            // Menü
            { "Kolileri yükle, kamyonları yolla!", "Load the crates, send the trucks!" },
            { "MACERA", "ADVENTURE" },
            { "SONSUZ", "ENDLESS" },
            { "Bölüm {0}  ·  Devam et", "Level {0}  ·  Continue" },
            { "Rekor  {0}", "Best  {0}" },
            { "Puan topla, rekor kır", "Score points, beat your best" },
            { "Ses açık", "Sound on" },
            { "Ses kapalı", "Sound off" },
            { "Titreşim açık", "Vibration on" },
            { "Titreşim kapalı", "Vibration off" },
            { "Ayarlar", "Settings" },
            { "YENİ!", "NEW!" },
            { "Market", "Shop" },
            { "Çıkmak için tekrar bas", "Press back again to exit" },

            // Ayarlar
            { "AYARLAR", "SETTINGS" },
            { "Ses", "Sound" },
            { "Titreşim", "Vibration" },
            { "Dil", "Language" },
            { "Otomatik", "Automatic" },
            { "Açık", "On" },
            { "Kapalı", "Off" },
            { "Gizlilik politikası", "Privacy policy" },
            { "Satın alımları geri yükle", "Restore purchases" },
            { "İlerlemeyi sıfırla", "Reset progress" },
            { "Emin misin? Tekrar dokun", "Are you sure? Tap again" },
            { "İlerleme sıfırlandı", "Progress reset" },
            { "Sürüm {0}", "Version {0}" },
            { "Altın ve satın alımlar korunur", "Coins and purchases are kept" },

            // Oyun içi
            { "Bölüm {0}", "Level {0}" },
            { "Kamyon: {0}", "Trucks: {0}" },
            { "Rekor {0}", "Best {0}" },
            { "Rekor: {0}", "Best: {0}" },
            { "Menü", "Menu" },
            { "Baştan", "Restart" },
            { "Zorluk {0}", "Stage {0}" },
            { "Zorluk {0}!", "Stage {0}!" },
            { "Dikkat: rafta son 1 yer!", "Careful: 1 shelf spot left!" },
            { "Teslim!", "Delivered!" },
            { "x{0} Kombo!", "x{0} Combo!" },
            { "MÜKEMMEL! +{0}", "PERFECT! +{0}" },
            { "Yeni bir renk geldi!", "A new color arrived!" },
            { "Koliler daha karışık geliyor", "Crates are getting more mixed" },
            { "Yeni Rekor!", "New Best!" },
            { "{0} puan", "{0} points" },
            { "Raftaki koliye dokun: kendi kamyonuna yüklensin!", "Tap the crate on the shelf to load it onto its truck!" },
            { "Öndeki koliye dokun: aynı renkteki kamyona gider!", "Tap the front crate: it goes to the truck of the same color!" },
            { "Uygun kamyon yok: koli rafa gider, kamyonu gelince kendiliğinden biner. Raf dolarsa kaybedersin!",
              "No matching truck: the crate goes to the shelf and loads itself when its truck arrives. If the shelf fills up, you lose!" },

            // Paneller
            { "Teslimat tamam!", "All delivered!" },
            { "{0} hamlede bitirdin", "Finished in {0} moves" },
            { "Yeni raf slotu açıldı! Artık {0} slot.", "New shelf spot unlocked! Now {0} spots." },
            { "Reklam izle: {0} katı", "Watch ad: {0}x coins" },
            { "Reklam izle: altın {0} katı", "Watch ad: {0}x coins" },
            { "Sonraki Bölüm", "Next Level" },
            { "Raf doldu!", "Shelf is full!" },
            { "YENİ REKOR!", "NEW BEST!" },
            { "{0} kamyon  ·  Zorluk {1}", "{0} trucks  ·  Stage {1}" },
            { "Son hamleyi geri al ({0})", "Undo last move ({0})" },
            { "Son hamleyi geri al · {0}", "Undo last move · {0}" },
            { "Rafa {0} yer ekle, devam et:", "Add {0} shelf spots and continue:" },
            { "Reklam", "Ad" },
            { "Tekrar Oyna", "Play Again" },
            { "Tekrar Dene", "Try Again" },

            // Güçlendiriciler
            { "Geri Al", "Undo" },
            { "Mıknatıs", "Magnet" },
            { "Karıştır", "Shuffle" },
            { "+1 Raf", "+1 Shelf" },
            { "Son hamleni geri alır.", "Takes back your last move." },
            { "Bir kamyonun eksik kolilerini depodan çekip yükler.", "Pulls a truck's missing crates out of the warehouse and loads them." },
            { "Depodaki kolileri yeniden dizer.", "Rearranges the crates in the warehouse." },
            { "Rafa bu oyun için bir yer ekler.", "Adds one shelf spot for this game." },
            { "Yeni: {0}!", "New: {0}!" },
            { "{0} tane hediye. {1}", "{0} free. {1}" },
            { "Bölüm {0}'de açılır", "Unlocks at level {0}" },
            { "{0}: Bölüm {1}'de açılır", "{0}: unlocks at level {1}" },
            { "Sonsuz modda kullanılamaz", "Not available in Endless" },
            { "Geri alınacak hamle yok", "No move to undo" },
            { "Çekilecek koli yok", "No crates to pull" },
            { "Karıştırılacak koli yok", "No crates to shuffle" },
            { "Bir oyunda en fazla {0} kez", "At most {0} times per game" },
            { "Geri alındı", "Undone" },
            { "Mıknatıs!", "Magnet!" },
            { "{0} ile al ve kullan", "Buy for {0} and use" },
            { "Altın yetmiyor · Market", "Not enough coins · Shop" },
            { "Altının yetmiyor", "Not enough coins" },
            { "Vazgeç", "Cancel" },

            // Market
            { "MARKET", "SHOP" },
            { "Başlangıç Paketi", "Starter Pack" },
            { "Reklamsız + 1.000 altın + her güçlendiriciden 3", "No ads + 1,000 coins + 3 of each booster" },
            { "Reklamsız", "No Ads" },
            { "Bölüm arası reklamlar kalkar", "Removes ads between levels" },
            { "altın", "coins" },
            { "1.000", "1,000" },
            { "3.000", "3,000" },
            { "7.500", "7,500" },
            { "%60 AVANTAJ", "60% OFF" },
            { "EN POPÜLER", "MOST POPULAR" },
            { "EN İYİ FİYAT", "BEST VALUE" },
            { "Ücretsiz {0} altın", "Free {0} coins" },
            { "Bugün kalan: {0}/{1}", "Left today: {0}/{1}" },
            { "İzle", "Watch" },
            { "Yarın", "Tomorrow" },
            { "+{0} altın!", "+{0} coins!" },
            { "Güçlendiriciler", "Boosters" },
            { "Sende: {0}", "Owned: {0}" },
            { "Test modu: satın alımlar ücretsiz simüle edilir", "Test mode: purchases are simulated for free" },
            { "+{0} altın! ({1})", "+{0} coins! ({1})" },
            { "{0} alındı! ({1})", "{0} purchased! ({1})" },
            { "Ürün bulunamadı", "Product not found" },
            { "Zaten sahipsin", "Already owned" },
            { "Mağaza henüz hazır değil", "Store is not ready yet" },
            { "Test satın alımı (ücret alınmadı)", "Test purchase (not charged)" },
            { "Satın alındı", "Purchased" },
            { "Geri yüklenecek satın alım yok", "No purchases to restore" },
            { "Test reklamı", "Test ad" },
            { "Gerçek reklamlar AdMob bağlanınca gelecek", "Real ads will come once AdMob is connected" },

            // Yeni mekanikler
            { "Yeni: Kilitli sütun!", "New: Locked column!" },
            { "Sayı kadar kamyon gidince açılır", "Opens after that many trucks leave" },
            { "Kilitli sütuna dokunamazsın. Üstündeki sayı kadar kamyon yola çıkınca kilit açılır!",
              "You can't tap a locked column. It opens once as many trucks as its number have left!" },
            { "Yeni: Gizli koliler!", "New: Hidden crates!" },
            { "Rengi, öne gelince görünür", "Its color shows when it reaches the front" },
            { "Gri koliler gizli: hangi renk olduğu, sütunun önüne gelince ortaya çıkar. Rafı dikkatli kullan!",
              "Gray crates are hidden: their color is revealed when they reach the front. Use the shelf carefully!" },
            { "Açıldı!", "Unlocked!" },
            { "Kilitli: {0} kamyon", "Locked: {0} trucks" },
            { "Gizli koliler geliyor!", "Hidden crates incoming!" },

            // Günlük ödül
            { "Günlük Ödül", "Daily Reward" },
            { "Her gün gel, ödül büyüsün!", "Come back every day for bigger rewards!" },
            { "Gün {0}", "Day {0}" },
            { "Al", "Claim" },
            { "Reklam izle: 2 katı", "Watch ad: 2x" },
            { "Yarın yine gel!", "Come back tomorrow!" },
            { "Bugünün ödülü alındı", "Today's reward claimed" },
            { "Bir gün kaçırırsan seri baştan başlar", "Miss a day and the streak restarts" },

            // Reklam videosu
            { "Rekorumu\ngeçebilir misin?", "Can you beat\nmy score?" },
            { "Raf dolarsa\nkaybedersin!", "If the shelf fills,\nyou lose!" },
            { "Bu depoyu\nboşaltabilir misin?", "Can you clear\nthis warehouse?" },
            { "SKOR", "SCORE" },
            { "SKORUM", "MY SCORE" },
            { "Geçebilir misin?", "Can you beat it?" },
            { "Sen olsan\nhangisine dokunurdun?", "Which one\nwould you tap?" },
            { "Ücretsiz Oyna!", "Play Free!" },
            { "Son 1 yer!", "1 spot left!" },
            { "Raf doluyor!", "Shelf filling up!" },
            { "Kurtuldu!", "Saved!" },
            { "RAF DOLDU!", "SHELF FULL!" },
        };
    }
}
