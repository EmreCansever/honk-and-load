using System;
using System.Collections.Generic;

namespace HonkAndLoad.Level
{
    /// <summary>
    /// Bir bölümün JSON ile saklanan verisi (Resources/Levels/level_XXX.json).
    /// columns[i].crates listesinin SON elemanı en öndeki (alınabilir) kolidir.
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int truckCapacity = 3;
        public int bufferSize = 6;
        public int dockCount = 3;
        public List<int> trucks = new List<int>();
        public List<ColumnData> columns = new List<ColumnData>();

        public int ColorCount()
        {
            int max = -1;
            foreach (int t in trucks) if (t > max) max = t;
            foreach (ColumnData c in columns)
                foreach (int crate in c.crates) if (crate > max) max = crate;
            return max + 1;
        }

        /// <summary>Her renk için koli sayısı = kamyon sayısı × kapasite mi?</summary>
        public bool Validate(out string error)
        {
            error = null;
            if (truckCapacity <= 0) { error = "truckCapacity > 0 olmalı"; return false; }
            if (bufferSize <= 0) { error = "bufferSize > 0 olmalı"; return false; }
            if (dockCount <= 0) { error = "dockCount > 0 olmalı"; return false; }
            if (trucks == null || trucks.Count == 0) { error = "En az bir kamyon olmalı"; return false; }
            if (columns == null || columns.Count == 0) { error = "En az bir sütun olmalı"; return false; }

            int colors = ColorCount();
            var crateCount = new int[colors];
            var truckCount = new int[colors];
            foreach (int t in trucks)
            {
                if (t < 0) { error = "Negatif renk numarası"; return false; }
                truckCount[t]++;
            }
            foreach (ColumnData c in columns)
            {
                if (c.crates == null) { error = "Boş crates listesi"; return false; }
                foreach (int crate in c.crates)
                {
                    if (crate < 0) { error = "Negatif renk numarası"; return false; }
                    crateCount[crate]++;
                }
            }
            for (int i = 0; i < colors; i++)
            {
                if (crateCount[i] != truckCount[i] * truckCapacity)
                {
                    error = $"Renk {i}: {crateCount[i]} koli var, {truckCount[i] * truckCapacity} olmalı";
                    return false;
                }
            }
            return true;
        }
    }

    [Serializable]
    public class ColumnData
    {
        public List<int> crates = new List<int>();
    }
}
