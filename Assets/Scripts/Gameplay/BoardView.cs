using System.Collections;
using System.Collections.Generic;
using HonkAndLoad.Core;
using UnityEngine;

namespace HonkAndLoad.Gameplay
{
    /// <summary>
    /// BoardState'i 3D sahneye çizer ve hamleleri animasyona çevirir.
    /// Prototipte tüm görseller Unity'nin hazır küplerinden oluşur.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        // Yerleşim (dünya birimi)
        private const float CrateSize = VisualFactory.CrateSize;
        private const float ColumnSpacing = 1.0f;
        private const float RowSpacing = 0.92f;
        private const float BufferSpacing = 1.0f;
        private const float DockSpacing = 1.9f;
        private const float ColumnFrontZ = 0f;
        private const float BufferZ = 1.5f;
        private const float DockZ = 4.4f;
        private const float OffscreenZ = 14f;

        private BoardState _state;
        private readonly List<List<Transform>> _columns = new List<List<Transform>>();
        private Transform[] _bufferCrates = new Transform[0];
        private readonly List<GameObject> _bufferPlates = new List<GameObject>();
        private TruckView[] _docks = new TruckView[0];
        private readonly VisualFactory _factory = new VisualFactory();
        private Transform _root;
        private readonly Dictionary<Transform, Coroutine> _moves = new Dictionary<Transform, Coroutine>();

        public Bounds ContentBounds { get; private set; }

        private class TruckView
        {
            public Transform Root;
            public readonly List<Transform> Cargo = new List<Transform>();
            public float BedLength;
            public float CargoY;
        }

        // ---------- Kurulum ----------

        public void Build(BoardState state)
        {
            Clear();
            _state = state;
            _root = new GameObject("Board").transform;
            _root.SetParent(transform, false);

            BuildGround();

            for (int c = 0; c < state.Columns.Count; c++)
            {
                var list = new List<Transform>();
                List<int> col = state.Columns[c];
                for (int i = 0; i < col.Count; i++)
                {
                    Transform crate = MakeCrate(col[i], _root);
                    crate.localPosition = ColumnSlot(c, col.Count - 1 - i);
                    SetTappable(crate.gameObject, Tappable.TapKind.Column, c);
                    list.Add(crate);
                }
                _columns.Add(list);
                RefreshColumnTint(c);
            }

            BuildBuffer();

            _docks = new TruckView[state.Docks.Length];
            for (int d = 0; d < state.Docks.Length; d++)
                if (state.Docks[d] != null) _docks[d] = MakeTruck(state.Docks[d], DockPosition(d));

            ComputeBounds();
        }

        public void Clear()
        {
            StopAllCoroutines();
            _moves.Clear();
            if (_root != null) Destroy(_root.gameObject);
            _columns.Clear();
            _bufferPlates.Clear();
            _bufferCrates = new Transform[0];
            _docks = new TruckView[0];
        }

        /// <summary>Raf slot sayısı değiştiğinde (ör. +3 slot) rafı yeniden çiz.</summary>
        public void RebuildBuffer()
        {
            foreach (GameObject p in _bufferPlates) Destroy(p);
            _bufferPlates.Clear();
            Transform[] old = _bufferCrates;
            BuildBuffer();
            for (int i = 0; i < old.Length && i < _bufferCrates.Length; i++)
            {
                _bufferCrates[i] = old[i];
                if (old[i] != null)
                {
                    old[i].localPosition = BufferSlot(i) + Vector3.up * 0.1f;
                    SetTappable(old[i].gameObject, Tappable.TapKind.Buffer, i);
                }
            }
            ComputeBounds();
        }

        private void BuildBuffer()
        {
            var crates = new Transform[_state.Buffer.Length];
            for (int i = 0; i < _state.Buffer.Length; i++)
            {
                GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = $"BufferSlot{i}";
                plate.transform.SetParent(_root, false);
                plate.transform.localPosition = BufferSlot(i) - Vector3.up * 0.42f;
                plate.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
                _factory.Paint(plate, Palette.Slot);
                SetTappable(plate, Tappable.TapKind.Buffer, i);
                _bufferPlates.Add(plate);
            }
            _bufferCrates = crates;
        }

        private void BuildGround()
        {
            int maxDepth = 1;
            foreach (List<int> c in _state.Columns) maxDepth = Mathf.Max(maxDepth, c.Count);
            var xs = new float[_state.Columns.Count];
            for (int i = 0; i < xs.Length; i++) xs[i] = ColumnSlot(i, 0).x;
            float width = Mathf.Max(_state.Columns.Count * ColumnSpacing, _state.Buffer.Length * BufferSpacing);
            _factory.BuildEnvironment(_root, width,
                ColumnFrontZ, ColumnFrontZ - (maxDepth - 1) * RowSpacing,
                BufferZ, _state.Buffer.Length * BufferSpacing,
                DockZ, _state.Docks.Length, DockSpacing, xs);
        }

        private void ComputeBounds()
        {
            int maxDepth = 1;
            foreach (List<int> c in _state.Columns) maxDepth = Mathf.Max(maxDepth, c.Count);
            float width = Mathf.Max(_state.Columns.Count * ColumnSpacing,
                                    _state.Buffer.Length * BufferSpacing,
                                    _state.Docks.Length * DockSpacing);
            float minZ = ColumnFrontZ - (maxDepth - 1) * RowSpacing - CrateSize;
            float maxZ = DockZ + 2.2f;
            var b = new Bounds(new Vector3(0f, 0f, (minZ + maxZ) / 2f), new Vector3(width + 0.6f, 1.5f, maxZ - minZ));
            ContentBounds = b;
        }

        // ---------- Konumlar ----------

        private Vector3 ColumnSlot(int column, int depth)
        {
            float x = (column - (_state.Columns.Count - 1) / 2f) * ColumnSpacing;
            return new Vector3(x, 0f, ColumnFrontZ - depth * RowSpacing);
        }

        private Vector3 BufferSlot(int slot)
        {
            float x = (slot - (_state.Buffer.Length - 1) / 2f) * BufferSpacing;
            return new Vector3(x, 0f, BufferZ);
        }

        private Vector3 DockPosition(int dock)
        {
            float x = (dock - (_state.Docks.Length - 1) / 2f) * DockSpacing;
            return new Vector3(x, 0f, DockZ);
        }

        private static Vector3 CargoSlot(TruckView truck, int slot) =>
            new Vector3(0f, truck.CargoY, -truck.BedLength / 2f + 0.5f + slot * VisualFactory.CargoSpacing);

        // ---------- Nesne üretimi ----------

        private Transform MakeCrate(int color, Transform parent) => _factory.CreateCrate(color, parent);

        private TruckView MakeTruck(Truck truck, Vector3 position)
        {
            VisualFactory.TruckParts parts = _factory.CreateTruck(truck.Color, truck.Capacity, _root, position);
            var view = new TruckView { Root = parts.Root, BedLength = parts.BedLength, CargoY = parts.CargoY };

            // Kamyonda zaten yük varsa (ör. yeniden çizim) göster
            for (int i = 0; i < truck.Load; i++)
            {
                Transform crate = MakeCrate(truck.Color, view.Root);
                crate.localPosition = CargoSlot(view, i);
                Destroy(crate.GetComponent<Collider>());
                view.Cargo.Add(crate);
            }
            return view;
        }

        /// <summary>Yalnızca en öndeki koli parlak; arkadakiler soluk.</summary>
        private void RefreshColumnTint(int column)
        {
            List<Transform> col = _columns[column];
            for (int i = 0; i < col.Count; i++)
                if (col[i] != null) _factory.SetCrateDimmed(col[i], i != col.Count - 1);
        }

        private static void SetTappable(GameObject go, Tappable.TapKind kind, int index)
        {
            var t = go.GetComponent<Tappable>();
            if (t == null) t = go.AddComponent<Tappable>();
            t.Kind = kind;
            t.Index = index;
        }

        // ---------- Hamle animasyonları ----------

        public void Apply(MoveResult move)
        {
            Transform crate;
            if (move.FromColumn >= 0)
            {
                List<Transform> col = _columns[move.FromColumn];
                crate = col[col.Count - 1];
                col.RemoveAt(col.Count - 1);
                // Kalan koliler öne kayar
                for (int i = 0; i < col.Count; i++)
                    StartMove(col[i], ColumnSlot(move.FromColumn, col.Count - 1 - i), 0.15f, 0f);
                RefreshColumnTint(move.FromColumn);
            }
            else
            {
                crate = _bufferCrates[move.FromBuffer];
                _bufferCrates[move.FromBuffer] = null;
            }

            if (move.ToBuffer >= 0)
            {
                _bufferCrates[move.ToBuffer] = crate;
                SetTappable(crate.gameObject, Tappable.TapKind.Buffer, move.ToBuffer);
                StartMove(crate, BufferSlot(move.ToBuffer) + Vector3.up * 0.1f, 0.2f, 0.8f);
                return;
            }

            // Kamyona yükleme
            TruckView truck = _docks[move.ToDock];
            Destroy(crate.GetComponent<Tappable>());
            Destroy(crate.GetComponent<Collider>());
            crate.SetParent(truck.Root, true);
            truck.Cargo.Add(crate);
            StartMove(crate, CargoSlot(truck, move.SlotInTruck), 0.22f, 1.4f);
            StartCoroutine(Squash(truck.Root, 0.22f));

            if (move.TruckDeparted)
            {
                StartCoroutine(Depart(truck.Root, 0.3f));
                _docks[move.ToDock] = null;
                if (move.ArrivedTruck != null)
                {
                    Vector3 dock = DockPosition(move.ToDock);
                    TruckView arriving = MakeTruck(move.ArrivedTruck, dock + Vector3.forward * OffscreenZ);
                    _docks[move.ToDock] = arriving;
                    StartMove(arriving.Root, dock, 0.35f, 0f, 0.35f);
                }
            }
        }

        /// <summary>Bir nesnenin süren hareketini durdurup yenisini başlatır.</summary>
        private void StartMove(Transform t, Vector3 target, float duration, float arc, float delay = 0f)
        {
            if (_moves.TryGetValue(t, out Coroutine running) && running != null) StopCoroutine(running);
            _moves[t] = StartCoroutine(Move(t, target, duration, arc, delay));
        }

        public void ShakeColumn(int column)
        {
            if (column < 0 || column >= _columns.Count || _columns[column].Count == 0) return;
            List<Transform> col = _columns[column];
            StartCoroutine(Shake(col[col.Count - 1]));
        }

        public void ShakeBuffer(int slot)
        {
            if (slot >= 0 && slot < _bufferCrates.Length && _bufferCrates[slot] != null)
                StartCoroutine(Shake(_bufferCrates[slot]));
        }

        private static IEnumerator Move(Transform t, Vector3 target, float duration, float arc, float delay = 0f)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (t == null) yield break;
            Vector3 start = t.localPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - k) * (1f - k);
                Vector3 p = Vector3.Lerp(start, target, eased);
                p.y += Mathf.Sin(k * Mathf.PI) * arc;
                t.localPosition = p;
                yield return null;
            }
            if (t != null) t.localPosition = target;
        }

        private static IEnumerator Squash(Transform t, float delay)
        {
            yield return new WaitForSeconds(delay);
            float elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration && t != null)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                t.localScale = new Vector3(1f + 0.06f * k, 1f - 0.1f * k, 1f + 0.04f * k);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        private IEnumerator Depart(Transform truck, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (truck == null) yield break;
            Vector3 start = truck.localPosition;
            float elapsed = 0f;
            const float duration = 0.45f;
            while (elapsed < duration && truck != null)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                truck.localPosition = start + Vector3.forward * (k * k * OffscreenZ);
                yield return null;
            }
            if (truck != null) Destroy(truck.gameObject);
        }

        private static IEnumerator Shake(Transform t)
        {
            Vector3 start = t.localPosition;
            float elapsed = 0f;
            const float duration = 0.25f;
            while (elapsed < duration && t != null)
            {
                elapsed += Time.deltaTime;
                float k = elapsed / duration;
                t.localPosition = start + Vector3.right * Mathf.Sin(k * Mathf.PI * 6f) * 0.08f * (1f - k);
                yield return null;
            }
            if (t != null) t.localPosition = start;
        }
    }
}
