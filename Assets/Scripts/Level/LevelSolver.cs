using System.Collections.Generic;
using HonkAndLoad.Gameplay;

namespace HonkAndLoad.Level
{
    /// <summary>
    /// Bir bölümün çözülebilir olup olmadığını arar.
    /// Kamyona doğrudan yükleme her zaman iyi bir hamle olduğu için önce onlar
    /// otomatik yapılır; yalnızca "rafa koy" hamleleri dallandırılır.
    /// </summary>
    public static class LevelSolver
    {
        public struct Result
        {
            public bool Solvable;
            public int NodesExplored;
            public bool HitLimit;
            /// <summary>Çözümdeki en yüksek raf doluluğu (zorluk göstergesi).</summary>
            public int PeakBuffer;
        }

        /// <summary>Oyunun o anki durumundan çözüm var mı? (ipucu için)</summary>
        public static bool IsSolvableFrom(BoardState state, int nodeLimit = 3000)
        {
            var visited = new HashSet<string>();
            var result = new Result { PeakBuffer = int.MaxValue };
            Search(state.Clone(), 0, visited, ref result, nodeLimit);
            return result.Solvable;
        }

        public static Result Solve(LevelData data, int nodeLimit = 50000)
        {
            var visited = new HashSet<string>();
            var result = new Result { PeakBuffer = int.MaxValue };
            Search(new BoardState(data), 0, visited, ref result, nodeLimit);
            if (!result.Solvable) result.PeakBuffer = 0;
            return result;
        }

        private static void Search(BoardState state, int peak, HashSet<string> visited,
            ref Result result, int nodeLimit)
        {
            if (result.NodesExplored >= nodeLimit) { result.HitLimit = true; return; }
            result.NodesExplored++;

            ApplyForcedLoads(state);
            if (state.Lost) return;
            if (state.BufferUsed() > peak) peak = state.BufferUsed();

            if (state.IsWon())
            {
                result.Solvable = true;
                if (peak < result.PeakBuffer) result.PeakBuffer = peak;
                return;
            }
            if (!visited.Add(state.Key())) return;

            // Her sütunun ön kolisini rafa koymayı dene.
            for (int c = 0; c < state.Columns.Count; c++)
            {
                if (!state.CanTapColumn(c)) continue;
                BoardState next = state.Clone();
                next.TapColumn(c);
                if (next.Lost) continue; // bu hamle rafı doldurur
                Search(next, peak, visited, ref result, nodeLimit);
                if (result.Solvable || result.HitLimit) return;
            }
        }

        /// <summary>Raftan ve depodan, kamyona doğrudan gidebilen her koliyi yükle.</summary>
        public static void ApplyForcedLoads(BoardState state)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int s = 0; s < state.Buffer.Length; s++)
                {
                    if (state.CanTapBuffer(s)) { state.TapBuffer(s); changed = true; }
                }
                for (int c = 0; c < state.Columns.Count; c++)
                {
                    if (!state.CanTapColumn(c)) continue;
                    int color = state.FrontCrate(c);
                    if (state.FindDockFor(color) >= 0) { state.TapColumn(c); changed = true; }
                }
            }
        }

        /// <summary>
        /// Zorluk ölçüsü: "dikkatli oyuncu" simülasyonunun kazanma oranı (0–1).
        /// Doğrudan yüklenebilen koliyi hep yükler; yoksa arkasındaki koli bir kamyona uyan
        /// ya da rengi rafta zaten bekleyen sütunu seçer. Gerçek dikkatli bir oyuncuya yakın.
        /// </summary>
        public static float EstimateWinRate(LevelData data, int runs, int seed)
        {
            var rng = new System.Random(seed);
            int wins = 0;
            for (int run = 0; run < runs; run++)
            {
                var state = new BoardState(data);
                int guard = 0;
                while (!state.Lost && !state.IsWon() && guard++ < 2000)
                {
                    int c = CarefulChoice(state, rng);
                    if (c < 0) break;
                    state.TapColumn(c);
                }
                if (state.IsWon()) wins++;
            }
            return runs > 0 ? (float)wins / runs : 0f;
        }

        /// <summary>Dikkatli oyuncunun seçeceği sütun (yoksa -1). İpucu sistemi de kullanır.</summary>
        public static int CarefulChoice(BoardState state, System.Random rng)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int c = 0; c < state.Columns.Count; c++)
            {
                if (!state.CanTapColumn(c)) continue;
                int front = state.FrontCrate(c);
                if (state.FindDockFor(front) >= 0) return c; // doğrudan yükleme
                List<int> col = state.Columns[c];
                float score = 0f;
                if (col.Count >= 2 && state.FindDockFor(col[col.Count - 2]) >= 0) score += 2f;
                foreach (int b in state.Buffer) if (b == front) { score += 1f; break; }
                score += rng != null ? (float)rng.NextDouble() * 0.5f : 0f;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }
    }
}
