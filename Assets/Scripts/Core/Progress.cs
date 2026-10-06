using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>Oyuncunun ilerlemesi (şimdilik PlayerPrefs).</summary>
    public static class Progress
    {
        private const string LevelKey = "hal_level";

        public static int CurrentLevel
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
            set { PlayerPrefs.SetInt(LevelKey, Mathf.Max(1, value)); PlayerPrefs.Save(); }
        }
    }
}
