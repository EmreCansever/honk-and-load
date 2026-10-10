using System;
using System.Collections.Generic;

namespace HonkAndLoad.Level
{
    /// <summary>
    /// Bölüm numarasına göre otomatik bölüm üretir. Aynı numara her zaman aynı
    /// bölümü verir (sabit tohum). Üretilen her bölüm çözücüden geçer.
    /// Zorluk "testere dişi": her 5. bölüm zor, ardından rahatlatan bölümler.
    ///
    /// Yöntem: koliler önce kamyonların geliş sırasına göre dizilir (en önde ilk
    /// kamyonun kolileri), sonra "Noise" kadar karıştırılır. Noise arttıkça
    /// oyuncunun rafı kullanması ve plan yapması gerekir.
    /// </summary>
    public static class LevelGenerator
    {
        public const int MaxColors = 8;
        public const int BaseBufferSize = 5;
        public const int MaxBufferSize = 8;
        public const int LevelsPerExtraSlot = 15;

        /// <summary>
        /// Oyun bu "sanal bölüm" zorluğundan başlar. Büyüttükçe ilk bölümler zorlaşır.
        /// </summary>
        public const int StartOffset = 6;

        public struct Settings
        {
            public int Colors;
            public int TrucksPerColor;
            public int Columns;
            public int TruckCapacity;
            public int BufferSize;
            public int DockCount;
            /// <summary>Sıralamanın ne kadar bozulacağı (koli sayısı cinsinden).</summary>
            public float Noise;
            /// <summary>
            /// Hedeflenen "dikkatli oyuncu" kazanma oranı. Adaylar arasından buna en yakın
            /// olan seçilir. Düşük = zor. Dikkatsiz oyuncu bundan çok daha sık kaybeder.
            /// </summary>
            public float TargetWinRate;
            /// <summary>Arkadaki kolilerin gizli (soru işaretli) olma olasılığı.</summary>
            public float HiddenRatio;
            /// <summary>Kilitli sütun sayısı ve en fazla kaç kamyon sonra açılacağı.</summary>
            public int LockedColumns;
            public int MaxLock;
        }

        /// <summary>Yeni mekaniklerin geldiği bölümler.</summary>
        public const int HiddenFromLevel = 15;
        public const int LocksFromLevel = 25;

        public static Settings SettingsFor(int levelNumber)
        {
            int n = Math.Max(1, levelNumber);
            int e = n + StartOffset; // efektif zorluk
            bool hard = n % 5 == 0;
            bool relief = n % 5 == 1 && n > 1;

            int colors = Math.Min(MaxColors, 3 + (e - 1) / 5);
            int trucksPerColor = Math.Min(3, 1 + (e - 1) / 8);

            // Karışıklık: büyük değer = koliler kamyon sırasından çok uzak, plan şart
            float noise = Math.Min(80f, 15f + e * 2.5f);
            if (hard) noise *= 1.3f;
            if (relief) noise *= 0.7f;

            float target = Math.Max(0.45f, 0.95f - 0.015f * n);
            if (hard) target -= 0.2f;
            if (relief) target += 0.1f;
            // Gizli koliler: 15. bölümde tanıtılır, yavaşça artar
            float hiddenRatio = 0f;
            if (n >= HiddenFromLevel)
                hiddenRatio = n == HiddenFromLevel ? 0.25f : Math.Min(0.45f, 0.15f + 0.01f * (n - HiddenFromLevel));
            // Kilitli sütunlar: 25. bölümde 1 sütun, 40'ta 2, 60'ta 3
            int lockedColumns = n >= LocksFromLevel ? 1 + (n >= 40 ? 1 : 0) + (n >= 60 ? 1 : 0) : 0;
            int maxLock = n >= 50 ? 3 : 2;
            // Botlar gizli koliyi görür, oyuncu görmez: biraz daha kolay aday seç
            if (hiddenRatio > 0f) target += 0.05f;
            target = Math.Min(1f, Math.Max(0.25f, target));

            return new Settings
            {
                Colors = colors,
                TrucksPerColor = trucksPerColor,
                Columns = colors + (e > 30 ? 1 : 0),
                TruckCapacity = 3,
                BufferSize = BufferSizeFor(n),
                DockCount = 3,
                Noise = noise,
                TargetWinRate = target,
                HiddenRatio = hiddenRatio,
                LockedColumns = lockedColumns,
                MaxLock = maxLock
            };
        }

        /// <summary>Raf 5 slotla başlar, her 15 bölümde bir slot artar (en fazla 8).</summary>
        public static int BufferSizeFor(int levelNumber) =>
            Math.Min(MaxBufferSize, BaseBufferSize + (Math.Max(1, levelNumber) - 1) / LevelsPerExtraSlot);

        public static LevelData Generate(int levelNumber, int candidates = 12)
        {
            Settings s = SettingsFor(levelNumber);
            var rng = new Random(levelNumber * 7919 + 17);

            LevelData best = null, fallback = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < candidates; i++)
            {
                LevelData data = BuildRandom(s, rng);
                fallback ??= data;
                if (!LevelSolver.Solve(data, 3000).Solvable) continue;

                float winRate = LevelSolver.EstimateWinRate(data, 30, levelNumber);
                float distance = Math.Abs(winRate - s.TargetWinRate);
                if (distance < bestDistance) { bestDistance = distance; best = data; }
            }
            return best ?? fallback; // çözülebilir aday yoksa (çok nadir) oyuncu "+3 slot" alabilir
        }

        private static LevelData BuildRandom(Settings s, Random rng)
        {
            var data = new LevelData
            {
                truckCapacity = s.TruckCapacity,
                bufferSize = s.BufferSize,
                dockCount = s.DockCount
            };

            for (int c = 0; c < s.Colors; c++)
                for (int t = 0; t < s.TrucksPerColor; t++)
                    data.trucks.Add(c);
            Shuffle(data.trucks, rng);

            // Kamyon sırasına göre koli dizisi: ilk kamyonun kolileri en başta
            var sequence = new List<int>();
            foreach (int truck in data.trucks)
                for (int k = 0; k < s.TruckCapacity; k++)
                    sequence.Add(truck);

            // Her koliye "sıra + rastgele gürültü" anahtarı verip yeniden sırala
            var keys = new float[sequence.Count];
            var order = new int[sequence.Count];
            for (int i = 0; i < sequence.Count; i++)
            {
                keys[i] = i + (float)rng.NextDouble() * s.Noise;
                order[i] = i;
            }
            Array.Sort(keys, order);

            // Sondan başa dağıt: en son gerekecek koliler en derine, ilk gerekenler en öne
            for (int i = 0; i < s.Columns; i++) data.columns.Add(new ColumnData());
            // Sütun derinliği sınırı: ortalamanın 1 fazlası. Derin sütunlar dar ekranlarda
            // kolileri küçültür ve dokunmayı zorlaştırır.
            int maxHeight = (sequence.Count + s.Columns - 1) / s.Columns + 1;
            int dealt = 0;
            for (int i = order.Length - 1; i >= 0; i--, dealt++)
            {
                int column = rng.NextDouble() < 0.3 ? rng.Next(s.Columns) : dealt % s.Columns;
                if (data.columns[column].crates.Count >= maxHeight)
                {
                    int shortest = 0;
                    for (int c = 1; c < s.Columns; c++)
                        if (data.columns[c].crates.Count < data.columns[shortest].crates.Count) shortest = c;
                    column = shortest;
                }
                data.columns[column].crates.Add(sequence[order[i]]);
            }

            // Gizli koliler (en öndekiler hariç). Önceki bölümler değişmesin diye rng yalnızca
            // mekanik açıkken kullanılır.
            if (s.HiddenRatio > 0f)
                foreach (ColumnData col in data.columns)
                    for (int i = 0; i < col.crates.Count - 1; i++)
                        if (rng.NextDouble() < s.HiddenRatio) col.hidden.Add(i);

            // Kilitli sütunlar: en az iki sütun hep açık kalır
            if (s.LockedColumns > 0)
            {
                int count = Math.Min(s.LockedColumns, data.columns.Count - 2);
                for (int i = 0; i < data.columns.Count; i++) data.locks.Add(0);
                var candidates = new List<int>();
                for (int i = 0; i < data.columns.Count; i++) candidates.Add(i);
                Shuffle(candidates, rng);
                for (int i = 0; i < count; i++) data.locks[candidates[i]] = 1 + rng.Next(s.MaxLock);
            }

            return data;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
