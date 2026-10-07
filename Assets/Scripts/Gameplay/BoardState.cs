using System.Collections.Generic;
using System.Text;
using HonkAndLoad.Level;

namespace HonkAndLoad.Gameplay
{
    /// <summary>Rampadaki bir kamyon.</summary>
    public class Truck
    {
        public int Color;
        public int Load;
        public int Capacity;
        public bool IsFull => Load >= Capacity;

        public Truck(int color, int capacity) { Color = color; Capacity = capacity; }
        public Truck Clone() => new Truck(Color, Capacity) { Load = Load };
    }

    /// <summary>Bir hamlenin sonucu; görünüm (BoardView) bunu animasyona çevirir.</summary>
    public class MoveResult
    {
        public int Color;
        public int FromColumn = -1;   // depodan alındıysa sütun
        public int FromDepth;         // sütunda kaçıncı sıradan (0 = en ön; mıknatıs derinden çeker)
        public int FromBuffer = -1;   // raftan alındıysa slot
        public int ToDock = -1;       // kamyona yüklendiyse rampa
        public int ToBuffer = -1;     // rafa gittiyse slot
        public int SlotInTruck = -1;  // kamyondaki kaçıncı yer
        public bool TruckDeparted;    // kamyon doldu ve gitti mi
        public Truck ArrivedTruck;    // yerine gelen kamyon (yoksa null)
        public int RefillColor = -1;  // sonsuz mod: sütunun arkasına gelen yeni koli

        /// <summary>
        /// Bu hamle bir kamyonu doldurup yenisini getirdiyse, raftan yeni kamyona kendiliğinden
        /// binen koliler (sırayla). Her biri FromBuffer + ToDock dolu bir hamle sonucudur.
        /// </summary>
        public List<MoveResult> AutoLoads;
    }

    /// <summary>
    /// Oyun kuralları. UnityEngine kullanmaz; böylece çözücü ve testler
    /// motor dışında da çalışabilir.
    /// </summary>
    public class BoardState
    {
        public readonly List<List<int>> Columns = new List<List<int>>();
        public Truck[] Docks;
        public readonly Queue<int> TruckQueue = new Queue<int>();
        public int[] Buffer;
        public int TruckCapacity { get; private set; }
        public int TotalTrucks { get; private set; }
        public int DepartedTrucks { get; private set; }

        /// <summary>Sonsuz mod: bir sütundan koli alınınca arkasına eklenecek koliyi verir.</summary>
        public System.Func<int, int> Refill;

        /// <summary>Sonsuz mod: sıra boşalınca yeni kamyon rengini verir.</summary>
        public System.Func<int> TruckSupplier;

        public bool IsEndless => Refill != null;

        /// <summary>Raf tamamen doldu: oyun kaybedildi.</summary>
        public bool Lost { get; private set; }

        private BoardState() { }

        public BoardState(LevelData data)
        {
            TruckCapacity = data.truckCapacity;
            TotalTrucks = data.trucks.Count;
            foreach (ColumnData c in data.columns) Columns.Add(new List<int>(c.crates));
            foreach (int t in data.trucks) TruckQueue.Enqueue(t);

            Docks = new Truck[data.dockCount];
            for (int i = 0; i < Docks.Length; i++) Docks[i] = NextTruck();

            Buffer = new int[data.bufferSize];
            for (int i = 0; i < Buffer.Length; i++) Buffer[i] = -1;
        }

        private Truck NextTruck()
        {
            if (TruckQueue.Count > 0) return new Truck(TruckQueue.Dequeue(), TruckCapacity);
            if (TruckSupplier != null) return new Truck(TruckSupplier(), TruckCapacity);
            return null;
        }

        // ---------- Sorgular ----------

        public int FrontCrate(int column)
        {
            List<int> col = Columns[column];
            return col.Count > 0 ? col[col.Count - 1] : -1;
        }

        public int FindDockFor(int color)
        {
            for (int i = 0; i < Docks.Length; i++)
                if (Docks[i] != null && Docks[i].Color == color && !Docks[i].IsFull) return i;
            return -1;
        }

        public int FreeBufferSlot()
        {
            for (int i = 0; i < Buffer.Length; i++) if (Buffer[i] < 0) return i;
            return -1;
        }

        public int BufferUsed()
        {
            int n = 0;
            foreach (int b in Buffer) if (b >= 0) n++;
            return n;
        }

        public bool CanTapColumn(int column)
        {
            if (Lost) return false;
            int color = FrontCrate(column);
            if (color < 0) return false;
            return FindDockFor(color) >= 0 || FreeBufferSlot() >= 0;
        }

        public bool CanTapBuffer(int slot)
        {
            if (Lost) return false;
            int color = Buffer[slot];
            return color >= 0 && FindDockFor(color) >= 0;
        }

        public bool IsWon()
        {
            if (IsEndless) return false;
            foreach (List<int> c in Columns) if (c.Count > 0) return false;
            foreach (int b in Buffer) if (b >= 0) return false;
            return true;
        }

        /// <summary>Oyun bitti: raf doldu ya da yapılacak hiçbir hamle yok.</summary>
        public bool IsStuck()
        {
            if (Lost) return true;
            if (IsWon()) return false;
            for (int c = 0; c < Columns.Count; c++) if (CanTapColumn(c)) return false;
            for (int s = 0; s < Buffer.Length; s++) if (CanTapBuffer(s)) return false;
            return true;
        }

        // ---------- Hamleler ----------

        /// <summary>Sütundaki en öndeki koliyi al. Geçersizse null döner.</summary>
        public MoveResult TapColumn(int column)
        {
            if (column < 0 || column >= Columns.Count || !CanTapColumn(column)) return null;
            List<int> col = Columns[column];
            int color = col[col.Count - 1];
            col.RemoveAt(col.Count - 1);

            var result = new MoveResult { Color = color, FromColumn = column };
            if (Refill != null)
            {
                result.RefillColor = Refill(column);
                col.Insert(0, result.RefillColor); // arkaya (listenin başı) eklenir
            }
            int dock = FindDockFor(color);
            if (dock >= 0) LoadInto(dock, result);
            else
            {
                int slot = FreeBufferSlot();
                Buffer[slot] = color;
                result.ToBuffer = slot;
                // Kural: raf tamamen dolarsa oyun biter
                if (FreeBufferSlot() < 0) Lost = true;
            }
            return result;
        }

        /// <summary>Raftaki koliyi uygun kamyona yükle. Geçersizse null döner.</summary>
        public MoveResult TapBuffer(int slot)
        {
            if (slot < 0 || slot >= Buffer.Length || !CanTapBuffer(slot)) return null;
            int color = Buffer[slot];
            Buffer[slot] = -1;
            var result = new MoveResult { Color = color, FromBuffer = slot };
            LoadInto(FindDockFor(color), result);
            return result;
        }

        private void LoadInto(int dock, MoveResult result)
        {
            Truck truck = Docks[dock];
            result.ToDock = dock;
            result.SlotInTruck = truck.Load;
            truck.Load++;
            if (truck.IsFull)
            {
                result.TruckDeparted = true;
                DepartedTrucks++;
                Docks[dock] = NextTruck();
                result.ArrivedTruck = Docks[dock];
                AutoLoadFromBuffer(result);
            }
        }

        /// <summary>
        /// Yeni gelen kamyonlara raftaki uygun koliler kendiliğinden biner.
        /// Bu kamyonu da doldurursa zincirleme devam eder.
        /// </summary>
        private void AutoLoadFromBuffer(MoveResult parent)
        {
            for (int s = 0; s < Buffer.Length; s++)
            {
                int color = Buffer[s];
                if (color < 0) continue;
                int dock = FindDockFor(color);
                if (dock < 0) continue;
                Buffer[s] = -1;
                var auto = new MoveResult { Color = color, FromBuffer = s };
                if (parent.AutoLoads == null) parent.AutoLoads = new List<MoveResult>();
                parent.AutoLoads.Add(auto);
                LoadInto(dock, auto); // kendi zincirini auto.AutoLoads içine yazar
                s = -1; // raf değişti, baştan tara
            }
        }

        // ---------- Güçlendiriciler ----------

        /// <summary>
        /// Mıknatıs için en faydalı rampa: eksik kolilerinden en çoğu depoda olan,
        /// eşitlikte kolileri en derinde olan kamyon. Uygun yoksa -1.
        /// </summary>
        public int ChooseMagnetDock()
        {
            int best = -1, bestGain = 0, bestDepth = -1;
            for (int d = 0; d < Docks.Length; d++)
            {
                Truck t = Docks[d];
                if (t == null || t.IsFull) continue;
                int need = t.Capacity - t.Load;
                var depths = new List<int>();
                foreach (List<int> col in Columns)
                    for (int i = 0; i < col.Count; i++)
                        if (col[i] == t.Color) depths.Add(col.Count - 1 - i);
                foreach (int b in Buffer) if (b == t.Color) depths.Add(0);
                depths.Sort((a, b) => b.CompareTo(a)); // derinler önce
                int gain = System.Math.Min(need, depths.Count);
                int depthSum = 0;
                for (int i = 0; i < gain; i++) depthSum += depths[i];
                if (gain > bestGain || (gain == bestGain && gain > 0 && depthSum > bestDepth))
                {
                    best = d; bestGain = gain; bestDepth = depthSum;
                }
            }
            return best;
        }

        /// <summary>
        /// Mıknatıs: rampadaki kamyonun eksik kolilerini (önce en derindekiler) çekip yükler.
        /// Her çekiş ayrı bir hamle sonucudur; kamyon dolarsa normal kalkış/varış olur.
        /// </summary>
        public List<MoveResult> MagnetPull(int dock)
        {
            var results = new List<MoveResult>();
            if (Lost || dock < 0 || dock >= Docks.Length || Docks[dock] == null) return results;
            Truck truck = Docks[dock];
            int color = truck.Color;
            while (Docks[dock] == truck && !truck.IsFull)
            {
                MoveResult r = null;
                int bestColumn = -1, bestDepth = -1;
                for (int c = 0; c < Columns.Count; c++)
                {
                    List<int> col = Columns[c];
                    for (int i = 0; i < col.Count; i++)
                    {
                        int depth = col.Count - 1 - i;
                        if (col[i] == color && depth > bestDepth) { bestColumn = c; bestDepth = depth; }
                    }
                }
                if (bestColumn >= 0)
                {
                    List<int> col = Columns[bestColumn];
                    col.RemoveAt(col.Count - 1 - bestDepth);
                    r = new MoveResult { Color = color, FromColumn = bestColumn, FromDepth = bestDepth };
                    if (Refill != null)
                    {
                        r.RefillColor = Refill(bestColumn);
                        col.Insert(0, r.RefillColor);
                    }
                }
                else
                {
                    int slot = System.Array.IndexOf(Buffer, color);
                    if (slot < 0) break;
                    Buffer[slot] = -1;
                    r = new MoveResult { Color = color, FromBuffer = slot };
                }
                LoadInto(dock, r);
                results.Add(r);
            }
            return results;
        }

        /// <summary>Depodaki kolileri karıştırır; sütun yükseklikleri aynı kalır.</summary>
        public void ShuffleColumns(System.Random rng)
        {
            var all = new List<int>();
            foreach (List<int> col in Columns) all.AddRange(col);
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            int k = 0;
            foreach (List<int> col in Columns)
                for (int i = 0; i < col.Count; i++) col[i] = all[k++];
        }

        /// <summary>Öndeki kolilerden kaçı doğrudan bir kamyona gidebilir.</summary>
        public int DirectFrontCount()
        {
            int n = 0;
            for (int c = 0; c < Columns.Count; c++)
            {
                int f = FrontCrate(c);
                if (f >= 0 && FindDockFor(f) >= 0) n++;
            }
            return n;
        }

        /// <summary>"+3 slot" ile oyuna devam.</summary>
        public void Revive() => Lost = false;

        /// <summary>Kaybedince "+3 slot" ödülü.</summary>
        public void AddBufferSlots(int count)
        {
            var bigger = new int[Buffer.Length + count];
            for (int i = 0; i < bigger.Length; i++) bigger[i] = i < Buffer.Length ? Buffer[i] : -1;
            Buffer = bigger;
        }

        // ---------- Çözücü desteği ----------

        public BoardState Clone()
        {
            var b = new BoardState
            {
                TruckCapacity = TruckCapacity,
                TotalTrucks = TotalTrucks,
                DepartedTrucks = DepartedTrucks,
                Lost = Lost,
                Docks = new Truck[Docks.Length],
                Buffer = (int[])Buffer.Clone()
            };
            foreach (List<int> c in Columns) b.Columns.Add(new List<int>(c));
            foreach (int t in TruckQueue) b.TruckQueue.Enqueue(t);
            for (int i = 0; i < Docks.Length; i++) b.Docks[i] = Docks[i]?.Clone();
            return b;
        }

        /// <summary>Aynı durumları tekrar aramamak için kısa bir anahtar.</summary>
        public string Key()
        {
            var sb = new StringBuilder();
            foreach (List<int> c in Columns) { sb.Append(c.Count); sb.Append(','); }
            sb.Append('|');
            foreach (Truck t in Docks)
            {
                if (t == null) sb.Append('-');
                else { sb.Append(t.Color); sb.Append(':'); sb.Append(t.Load); }
                sb.Append(',');
            }
            sb.Append('|');
            var buf = new List<int>();
            foreach (int x in Buffer) if (x >= 0) buf.Add(x);
            buf.Sort();
            foreach (int x in buf) { sb.Append(x); sb.Append(','); }
            return sb.ToString();
        }
    }
}
