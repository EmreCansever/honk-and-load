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
        public int FromBuffer = -1;   // raftan alındıysa slot
        public int ToDock = -1;       // kamyona yüklendiyse rampa
        public int ToBuffer = -1;     // rafa gittiyse slot
        public int SlotInTruck = -1;  // kamyondaki kaçıncı yer
        public bool TruckDeparted;    // kamyon doldu ve gitti mi
        public Truck ArrivedTruck;    // yerine gelen kamyon (yoksa null)
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

        private Truck NextTruck() =>
            TruckQueue.Count > 0 ? new Truck(TruckQueue.Dequeue(), TruckCapacity) : null;

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
            int color = FrontCrate(column);
            if (color < 0) return false;
            return FindDockFor(color) >= 0 || FreeBufferSlot() >= 0;
        }

        public bool CanTapBuffer(int slot)
        {
            int color = Buffer[slot];
            return color >= 0 && FindDockFor(color) >= 0;
        }

        public bool IsWon()
        {
            foreach (List<int> c in Columns) if (c.Count > 0) return false;
            foreach (int b in Buffer) if (b >= 0) return false;
            return true;
        }

        /// <summary>Kazanılmadı ve yapılacak hiçbir hamle yok.</summary>
        public bool IsStuck()
        {
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
            int dock = FindDockFor(color);
            if (dock >= 0) LoadInto(dock, result);
            else
            {
                int slot = FreeBufferSlot();
                Buffer[slot] = color;
                result.ToBuffer = slot;
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
            }
        }

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
