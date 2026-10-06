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

        public static Result Solve(LevelData data, int nodeLimit = 50000)
        {
            var visited = new HashSet<string>();
            var result = new Result { PeakBuffer = int.MaxValue };
            var start = new BoardState(data);
            Search(start, 0, visited, ref result, nodeLimit);
            if (!result.Solvable) result.PeakBuffer = 0;
            return result;
        }

        private static void Search(BoardState state, int peak, HashSet<string> visited,
            ref Result result, int nodeLimit)
        {
            if (result.NodesExplored >= nodeLimit) { result.HitLimit = true; return; }
            result.NodesExplored++;

            ApplyForcedLoads(state);
            if (state.BufferUsed() > peak) peak = state.BufferUsed();

            if (state.IsWon())
            {
                result.Solvable = true;
                if (peak < result.PeakBuffer) result.PeakBuffer = peak;
                return;
            }
            if (!visited.Add(state.Key())) return;
            if (state.FreeBufferSlot() < 0) return; // doğrudan yükleme yok, raf dolu

            // Her sütunun ön kolisini rafa koymayı dene.
            for (int c = 0; c < state.Columns.Count; c++)
            {
                if (state.FrontCrate(c) < 0) continue;
                BoardState next = state.Clone();
                next.TapColumn(c);
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
                    int color = state.FrontCrate(c);
                    if (color >= 0 && state.FindDockFor(color) >= 0) { state.TapColumn(c); changed = true; }
                }
            }
        }

        /// <summary>
        /// Zorluk ölçüsü: doğrudan yüklenebilen koliyi hep yükleyen, yoksa rastgele bir
        /// sütuna dokunan basit bir oyuncunun kaç denemede kazandığı (0–1).
        /// </summary>
        public static float EstimateWinRate(LevelData data, int runs, int seed)
        {
            var rng = new System.Random(seed);
            int wins = 0;
            var options = new List<int>();
            for (int run = 0; run < runs; run++)
            {
                var state = new BoardState(data);
                while (true)
                {
                    ApplyForcedLoads(state);
                    if (state.IsWon()) { wins++; break; }
                    options.Clear();
                    for (int c = 0; c < state.Columns.Count; c++)
                        if (state.CanTapColumn(c)) options.Add(c);
                    if (options.Count == 0) break;
                    state.TapColumn(options[rng.Next(options.Count)]);
                }
            }
            return runs > 0 ? (float)wins / runs : 0f;
        }
    }
}
