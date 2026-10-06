using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>Oyuncunun ilerlemesi ve ayarları (şimdilik PlayerPrefs).</summary>
    public static class Progress
    {
        private const string LevelKey = "hal_level";
        private const string SoundKey = "hal_sound";
        private const string HapticsKey = "hal_haptics";

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

        /// <summary>Bölüm ilerlemesini sıfırlar; ses ve titreşim ayarları korunur.</summary>
        public static void ResetLevels() => CurrentLevel = 1;
    }
}
