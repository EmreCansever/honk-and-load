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
        public const int StartOffset = 9;

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
            /// Hedeflenen "rastgele oyuncu" kazanma oranı. Adaylar arasından buna en yakın
            /// olan seçilir. Düşük = zor. Gerçek oyuncular plan yaptığı için bundan iyi oynar.
            /// </summary>
            public float TargetWinRate;
        }

        public static Settings SettingsFor(int levelNumber)
        {
            int n = Math.Max(1, levelNumber);
            int e = n + StartOffset; // efektif zorluk
            bool hard = n % 5 == 0;
            bool relief = n % 5 == 1 && n > 1;

            int colors = Math.Min(MaxColors, 3 + (e - 1) / 6);
            int trucksPerColor = Math.Min(3, 1 + (e - 1) / 10);

            float noise = Math.Min(28f, 10f + e * 0.55f);
            if (hard) noise *= 1.3f;
            if (relief) noise *= 0.7f;

            float target = Math.Max(0.4f, 0.8f - 0.01f * n);
            if (hard) target -= 0.2f;
            if (relief) target += 0.15f;
            target = Math.Min(0.95f, Math.Max(0.2f, target));

            return new Settings
            {
                Colors = colors,
                TrucksPerColor = trucksPerColor,
                Columns = colors + (e > 30 ? 1 : 0),
                TruckCapacity = 3,
                BufferSize = BufferSizeFor(n),
                DockCount = 3,
                Noise = noise,
                TargetWinRate = target
            };
        }

        /// <summary>Raf 5 slotla başlar, her 15 bölümde bir slot artar (en fazla 8).</summary>
        public static int BufferSizeFor(int levelNumber) =>
            Math.Min(MaxBufferSize, BaseBufferSize + (Math.Max(1, levelNumber) - 1) / LevelsPerExtraSlot);

        public static LevelData Generate(int levelNumber, int candidates = 10)
        {
            Settings s = SettingsFor(levelNumber);
            var rng = new Random(levelNumber * 7919 + 17);

            LevelData best = null, fallback = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < candidates; i++)
            {
                LevelData data = BuildRandom(s, rng);
                fallback ??= data;
                if (!LevelSolver.Solve(data, 5000).Solvable) continue;

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
            int dealt = 0;
            for (int i = order.Length - 1; i >= 0; i--, dealt++)
            {
                int column = rng.NextDouble() < 0.3 ? rng.Next(s.Columns) : dealt % s.Columns;
                data.columns[column].crates.Add(sequence[order[i]]);
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
