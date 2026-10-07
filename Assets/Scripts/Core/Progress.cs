using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>Oyuncunun ilerlemesi ve ayarları (şimdilik PlayerPrefs).</summary>
    public static class Progress
    {
        private const string LevelKey = "hal_level";
        private const string SoundKey = "hal_sound";
        private const string HapticsKey = "hal_haptics";
        private const string TutorialKey = "hal_tutorial_done";
        private const string EndlessBestKey = "hal_endless_best";

        public static int CurrentLevel
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
            set { PlayerPrefs.SetInt(LevelKey, Mathf.Max(1, value)); PlayerPrefs.Save(); }
        }

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool HapticsOn
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            set { PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Sonsuz modda en yüksek skor.</summary>
        public static int EndlessBest
        {
            get => PlayerPrefs.GetInt(EndlessBestKey, 0);
            set { PlayerPrefs.SetInt(EndlessBestKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>Öğretici (1. bölüm) bir kez tamamlandı mı?</summary>
        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt(TutorialKey, 0) == 1;
            set { PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ---------- Ekonomi (altın, güçlendirici, satın alımlar) ----------

        private const string CoinsKey = "hal_coins";
        private const string NoAdsKey = "hal_no_ads";
        public const int StartingCoins = 100;

        public static int Coins
        {
            get => PlayerPrefs.GetInt(CoinsKey, StartingCoins);
            set { PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>"Reklamsız" satın alındı: bölüm arası reklamlar gösterilmez.</summary>
        public static bool NoAds
        {
            get => PlayerPrefs.GetInt(NoAdsKey, 0) == 1;
            set { PlayerPrefs.SetInt(NoAdsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int GetInt(string key, int fallback = 0) => PlayerPrefs.GetInt("hal_" + key, fallback);

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt("hal_" + key, value);
            PlayerPrefs.Save();
        }

        public static string GetString(string key) => PlayerPrefs.GetString("hal_" + key, "");

        public static void SetString(string key, string value)
        {
            PlayerPrefs.SetString("hal_" + key, value);
            PlayerPrefs.Save();
        }

        /// <summary>Bölüm ilerlemesini ve öğreticiyi sıfırlar; ses ve titreşim ayarları korunur.</summary>
        public static void ResetLevels()
        {
            CurrentLevel = 1;
            TutorialDone = false;
        }
    }
}
