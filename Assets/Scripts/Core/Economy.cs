using System.Collections.Generic;

namespace HonkAndLoad.Core
{
    public enum BoosterType { Undo = 0, Magnet = 1, Shuffle = 2, ExtraSlot = 3 }

    /// <summary>
    /// Oyun içi ekonomi: altın kazanma/harcama ve güçlendirici envanteri.
    /// Değerler tek yerde; dengeyi buradan ayarla.
    /// </summary>
    public static class Economy
    {
        public class BoosterInfo
        {
            public BoosterType Type;
            public string NameTr;
            public string DescriptionTr;
            public string Name => Loc.T(NameTr);
            public string Description => Loc.T(DescriptionTr);
            public int Price;        // 1 adet, altın
            public int UnlockLevel;  // Macera'da bu bölüme gelince açılır
            public bool AdventureOnly;
        }

        public const int UnlockGift = 2;          // açılınca hediye adet
        public const int LevelReward = 20;        // bölüm geçme
        public const int HardLevelReward = 40;    // her 5. (zor) bölüm
        public const int RewardedMultiplier = 3;  // "Reklam izle, 3 katı al"
        public const int EndlessPointsPerCoin = 40;
        public const int FreeCoinsPerAd = 30;
        public const int FreeCoinAdsPerDay = 5;
        public const int MaxExtraSlotsPerGame = 3;

        public static readonly BoosterInfo[] Boosters =
        {
            new BoosterInfo { Type = BoosterType.Undo, NameTr = "Geri Al", Price = 60, UnlockLevel = 3, AdventureOnly = true,
                DescriptionTr = "Son hamleni geri alır." },
            new BoosterInfo { Type = BoosterType.Magnet, NameTr = "Mıknatıs", Price = 100, UnlockLevel = 6,
                DescriptionTr = "Bir kamyonun eksik kolilerini depodan çekip yükler." },
            new BoosterInfo { Type = BoosterType.Shuffle, NameTr = "Karıştır", Price = 80, UnlockLevel = 9,
                DescriptionTr = "Depodaki kolileri yeniden dizer." },
            new BoosterInfo { Type = BoosterType.ExtraSlot, NameTr = "+1 Raf", Price = 120, UnlockLevel = 12,
                DescriptionTr = "Rafa bu oyun için bir yer ekler." },
        };

        public static BoosterInfo Info(BoosterType t) => Boosters[(int)t];

        // ---------- Altın ----------

        public static int Coins => Progress.Coins;

        public static void AddCoins(int amount)
        {
            if (amount > 0) Progress.Coins += amount;
        }

        public static bool TrySpend(int amount)
        {
            if (amount < 0 || Progress.Coins < amount) return false;
            Progress.Coins -= amount;
            return true;
        }

        public static int CoinsForLevel(int level) => level % 5 == 0 ? HardLevelReward : LevelReward;

        public static int CoinsForEndless(int score) => score / EndlessPointsPerCoin;

        // ---------- Güçlendiriciler ----------

        public static int Count(BoosterType t) => Progress.GetInt("booster_" + (int)t);

        public static void AddBooster(BoosterType t, int amount) =>
            Progress.SetInt("booster_" + (int)t, System.Math.Max(0, Count(t) + amount));

        public static bool IsUnlocked(BoosterType t) => Progress.GetInt("booster_unlocked_" + (int)t) == 1;

        /// <summary>
        /// Oyuncu bir bölüme ulaştığında yeni güçlendiricileri açar ve hediye verir.
        /// Yeni açılanları döndürür (duyuru için).
        /// </summary>
        public static List<BoosterInfo> CheckUnlocks(int level)
        {
            var unlocked = new List<BoosterInfo>();
            foreach (BoosterInfo b in Boosters)
            {
                if (level < b.UnlockLevel || IsUnlocked(b.Type)) continue;
                Progress.SetInt("booster_unlocked_" + (int)b.Type, 1);
                AddBooster(b.Type, UnlockGift);
                unlocked.Add(b);
            }
            return unlocked;
        }

        /// <summary>Envanterden bir adet kullanır.</summary>
        public static bool TryConsume(BoosterType t)
        {
            if (Count(t) <= 0) return false;
            AddBooster(t, -1);
            return true;
        }

        /// <summary>Altınla bir adet satın alır (envantere ekler).</summary>
        public static bool TryBuyBooster(BoosterType t)
        {
            if (!TrySpend(Info(t).Price)) return false;
            AddBooster(t, 1);
            return true;
        }

        // ---------- Reklamla ücretsiz altın (günlük sınır) ----------

        private static string Today => System.DateTime.Now.ToString("yyyy-MM-dd");

        public static int FreeCoinAdsLeft
        {
            get
            {
                if (Progress.GetString("free_ads_day") != Today) return FreeCoinAdsPerDay;
                return System.Math.Max(0, FreeCoinAdsPerDay - Progress.GetInt("free_ads_used"));
            }
        }

        public static void UseFreeCoinAd()
        {
            if (Progress.GetString("free_ads_day") != Today)
            {
                Progress.SetString("free_ads_day", Today);
                Progress.SetInt("free_ads_used", 0);
            }
            Progress.SetInt("free_ads_used", Progress.GetInt("free_ads_used") + 1);
            AddCoins(FreeCoinsPerAd);
        }
    }
}
