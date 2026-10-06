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
            /// <summary>0 = en kolay aday, 1 = en zor aday.</summary>
            public float Difficulty;
        }

        public static Settings SettingsFor(int levelNumber)
        {
            int n = Math.Max(1, levelNumber);
            int colors = Math.Min(MaxColors, 3 + (n - 1) / 6);
            int trucksPerColor = Math.Min(3, 1 + (n - 1) / 10);
            bool hard = n % 5 == 0;
            bool relief = n % 5 == 1 && n > 1;

            float noise = n <= 3 ? 1.5f : Math.Min(24f, 3f + n * 0.4f);
            if (hard) noise *= 1.4f;
            if (relief) noise *= 0.6f;

            return new Settings
            {
                Colors = colors,
                TrucksPerColor = trucksPerColor,
                Columns = colors + 1 + (n > 20 ? 1 : 0) + (n > 40 ? 1 : 0),
                TruckCapacity = 3,
                BufferSize = 6,
                DockCount = 3,
                Noise = noise,
                Difficulty = n <= 3 ? 0f : hard ? 1f : relief ? 0.1f : 0.5f
            };
        }

        public static LevelData Generate(int levelNumber, int candidates = 8)
        {
            Settings s = SettingsFor(levelNumber);
            var rng = new Random(levelNumber * 7919 + 17);

            var solvable = new List<(LevelData data, int peak)>();
            LevelData fallback = null;
            for (int i = 0; i < candidates; i++)
            {
                LevelData data = BuildRandom(s, rng);
                fallback ??= data;
                LevelSolver.Result r = LevelSolver.Solve(data, 5000);
                if (r.Solvable) solvable.Add((data, r.PeakBuffer));
            }

            if (solvable.Count == 0) return fallback; // çok nadir; oyuncu "+3 slot" alabilir

            solvable.Sort((a, b) => a.peak.CompareTo(b.peak));
            int index = (int)Math.Round(s.Difficulty * (solvable.Count - 1));
            return solvable[index].data;
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
