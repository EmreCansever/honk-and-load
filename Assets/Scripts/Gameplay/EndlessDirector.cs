using System;
using System.Collections.Generic;
using HonkAndLoad.Level;

namespace HonkAndLoad.Gameplay
{
    /// <summary>
    /// Sonsuz mod: koli ve kamyon akışını, puanı ve zorluk kademesini yönetir.
    ///
    /// Adalet kuralı: her kamyon için tam 3 koli üretilir ve koliler kamyonların geliş
    /// sırasına göre (biraz karıştırılarak) gelir. Böylece arz ile talep hep eşittir;
    /// zorluk, karışıklığın (Noise) ve renk sayısının artmasından gelir.
    /// </summary>
    public class EndlessDirector
    {
        public const int Columns = 6;
        public const int Depth = 5;
        public const int BufferSize = 5;
        public const int DockCount = 3;
        public const int Capacity = 3;

        // Puanlar
        public const int PointsLoad = 10;          // depodan kamyona
        public const int PointsLoadFromBuffer = 5; // raftan kamyona
        public const int PointsTruck = 30;         // dolan her kamyon
        public const int PointsPerfect = 50;       // 3 koli art arda aynı kamyona
        public const int MaxComboBonusSteps = 10;  // kombo çarpanı en fazla x2

        /// <summary>Zorluk kademesi eşikleri (puan). Son eşikten sonra her 2000 puanda +1.</summary>
        private static readonly int[] TierThresholds = { 0, 300, 800, 1500, 2400, 3500, 4800, 6300, 8000 };
        private const int TierStepAfterLast = 2000;

        public int Score { get; private set; }
        public int Tier { get; private set; }
        public int Combo { get; private set; }
        public int TrucksSent { get; private set; }

        private readonly Random _rng;
        private readonly List<(float key, int color)> _pending = new List<(float, int)>();
        private readonly Queue<int> _upcomingTrucks = new Queue<int>();
        private readonly List<int> _recentTrucks = new List<int>();
        private int _streamIndex;
        private int _streakDock = -1, _streak;

        public EndlessDirector(int seed)
        {
            _rng = new Random(seed);
        }

        // Yeni kurallarla (raf dolunca oyun biter) simülasyonla ayarlandı:
        // dikkatsiz oyuncu ~1 dk, dikkatli oyuncu ~6–8 dk dayanır.
        public int ColorCount => Math.Min(LevelGenerator.MaxColors, 5 + Tier / 2);
        public float Noise => Math.Min(120f, 10f + 8f * Tier);

        /// <summary>Gizli koliler bu kademeden itibaren gelir (ekranda "Zorluk 4").</summary>
        public const int HiddenFromTier = 3;
        public float HiddenChance => Tier < HiddenFromTier ? 0f : Math.Min(0.35f, 0.1f * (Tier - HiddenFromTier + 1));

        public static int TierStart(int tier)
        {
            if (tier < TierThresholds.Length) return TierThresholds[tier];
            return TierThresholds[TierThresholds.Length - 1] + (tier - TierThresholds.Length + 1) * TierStepAfterLast;
        }

        public static int TierFor(int score)
        {
            int t = 0;
            while (TierStart(t + 1) <= score) t++;
            return t;
        }

        /// <summary>Kademe içindeki ilerleme (0–1), arayüzdeki çubuk için.</summary>
        public float TierProgress
        {
            get
            {
                int a = TierStart(Tier), b = TierStart(Tier + 1);
                return b > a ? (float)(Score - a) / (b - a) : 0f;
            }
        }

        // ---------- Akış ----------

        private void GenerateTruck()
        {
            int k = ColorCount;
            int color;
            int n = _recentTrucks.Count;
            do color = _rng.Next(k);
            while (n >= 2 && _recentTrucks[n - 1] == color && _recentTrucks[n - 2] == color);
            _recentTrucks.Add(color);
            if (_recentTrucks.Count > 8) _recentTrucks.RemoveAt(0);
            _upcomingTrucks.Enqueue(color);

            for (int i = 0; i < Capacity; i++)
            {
                _pending.Add((_streamIndex + (float)_rng.NextDouble() * Noise, color));
                _streamIndex++;
            }
        }

        /// <summary>Sıradaki koli: en erken "ihtiyaç duyulacak" olan (karışıklık payıyla).</summary>
        private int DrawCrate()
        {
            while (_pending.Count < (int)Noise + Capacity * 2) GenerateTruck();
            int best = 0;
            for (int i = 1; i < _pending.Count; i++)
                if (_pending[i].key < _pending[best].key) best = i;
            int color = _pending[best].color;
            _pending.RemoveAt(best);
            return color;
        }

        /// <summary>Sıradaki n kamyonun rengi (reklam videosu botu için).</summary>
        public List<int> PeekUpcoming(int n)
        {
            while (_upcomingTrucks.Count < n) GenerateTruck();
            var list = new List<int>(_upcomingTrucks);
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list;
        }

        private int NextTruckColor()
        {
            while (_upcomingTrucks.Count == 0) GenerateTruck();
            return _upcomingTrucks.Dequeue();
        }

        /// <summary>Başlangıç tahtası; kamyon ve yeniden doldurma kancaları bağlı.</summary>
        public BoardState CreateBoard()
        {
            var draws = new int[Columns * Depth];
            for (int i = 0; i < draws.Length; i++) draws[i] = DrawCrate();

            var data = new LevelData
            {
                truckCapacity = Capacity,
                bufferSize = BufferSize,
                dockCount = DockCount
            };
            for (int c = 0; c < Columns; c++) data.columns.Add(new ColumnData());
            // İlk çekilenler en öne (listenin sonu), sonrakiler arkaya
            for (int layer = Depth - 1; layer >= 0; layer--)
                for (int c = 0; c < Columns; c++)
                    data.columns[c].crates.Add(draws[layer * Columns + c]);
            for (int d = 0; d < DockCount; d++) data.trucks.Add(NextTruckColor());

            var board = new BoardState(data)
            {
                Refill = _ => DrawCrate(),
                TruckSupplier = NextTruckColor,
                // Kademe düşükken rng hiç kullanılmaz: eski tohumlu reklam videoları aynı kalır
                RefillHidden = () => Tier >= HiddenFromTier && _rng.NextDouble() < HiddenChance
            };
            return board;
        }

        // ---------- Puan ----------

        public class ScoreEvent
        {
            public int Points;
            public bool TruckDone;
            public bool Perfect;
            public bool TierUp;
            public int NewTier;
            public int Dock = -1;
            public int ComboMilestone; // 0 = yok
        }

        public ScoreEvent OnMove(MoveResult move)
        {
            var e = new ScoreEvent { Dock = move.ToDock };
            int oldTier = Tier;

            if (move.ToDock < 0)
            {
                // Rafa koymak: seriler bozulur, puan yok
                Combo = 0;
                _streakDock = -1;
                _streak = 0;
                return e;
            }

            bool fromBuffer = move.FromBuffer >= 0;
            if (fromBuffer) { _streakDock = -1; _streak = 0; } // Mükemmel yalnızca 3 doğrudan yüklemeyle
            else if (_streakDock == move.ToDock) _streak++;
            else { _streakDock = move.ToDock; _streak = 1; }

            if (fromBuffer)
            {
                e.Points += PointsLoadFromBuffer;
            }
            else
            {
                Combo++;
                float mult = 1f + Math.Min(Combo - 1, MaxComboBonusSteps) * 0.1f;
                e.Points += (int)Math.Round(PointsLoad * mult);
                if (Combo == 5 || Combo == 10 || (Combo > 10 && Combo % 10 == 0)) e.ComboMilestone = Combo;
            }

            if (move.TruckDeparted)
            {
                e.TruckDone = true;
                e.Points += PointsTruck;
                TrucksSent++;
                if (_streak >= Capacity)
                {
                    e.Perfect = true;
                    e.Points += PointsPerfect;
                }
                _streakDock = -1;
                _streak = 0;
            }

            Score += e.Points;
            Tier = TierFor(Score);
            if (Tier > oldTier)
            {
                e.TierUp = true;
                e.NewTier = Tier;
            }
            return e;
        }
    }
}
