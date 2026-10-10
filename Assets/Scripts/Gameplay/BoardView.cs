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
        private float DockSpacing => _factory.DockSpacing;
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
        private readonly Dictionary<int, GameObject> _locks = new Dictionary<int, GameObject>();

        public Bounds ContentBounds { get; private set; }

        private class TruckView
        {
            public Transform Root;
            public readonly List<Transform> Cargo = new List<Transform>();
            public Vector3[] Slots;
            public float CargoScale = 1f;
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
                    crate.GetComponent<CrateInfo>().Hidden = state.Hidden[c][i];
                    SetTappable(crate.gameObject, Tappable.TapKind.Column, c);
                    list.Add(crate);
                }
                _columns.Add(list);
                RefreshColumnTint(c, false);
            }

            BuildBuffer();
            BuildLocks();

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
            _locks.Clear();
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
            float maxZ = DockZ + _factory.TruckFrontExtent(_state.TruckCapacity) + 0.2f;
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
            truck.Slots[Mathf.Clamp(slot, 0, truck.Slots.Length - 1)];

        // ---------- Nesne üretimi ----------

        private Transform MakeCrate(int color, Transform parent) => _factory.CreateCrate(color, parent);

        private TruckView MakeTruck(Truck truck, Vector3 position)
        {
            VisualFactory.TruckParts parts = _factory.CreateTruck(truck.Color, truck.Capacity, _root, position);
            var view = new TruckView { Root = parts.Root, Slots = parts.Slots, CargoScale = parts.CargoScale };

            // Kamyonda zaten yük varsa (ör. yeniden çizim) göster
            for (int i = 0; i < truck.Load; i++)
            {
                Transform crate = MakeCrate(truck.Color, view.Root);
                crate.localPosition = CargoSlot(view, i);
                crate.localScale = Vector3.one * CrateSize * view.CargoScale;
                Destroy(crate.GetComponent<Collider>());
                view.Cargo.Add(crate);
            }
            return view;
        }

        /// <summary>Yalnızca en öndeki koli parlak; arkadakiler soluk. Öne gelen gizli koli açılır.</summary>
        private void RefreshColumnTint(int column, bool animateReveal = true)
        {
            List<Transform> col = _columns[column];
            for (int i = 0; i < col.Count; i++)
            {
                if (col[i] == null) continue;
                bool front = i == col.Count - 1;
                var info = col[i].GetComponent<CrateInfo>();
                bool reveal = front && info != null && info.Hidden;
                if (reveal) info.Hidden = false;
                // Kilitli sütunun öndeki kolisi de soluk: dokunulamayacağı belli olsun
                _factory.SetCrateDimmed(col[i], !front || _state.IsLocked(column));
                if (reveal && animateReveal) StartCoroutine(RevealPop(col[i]));
            }
        }

        /// <summary>Gizli koli açılınca kısa bir zıplama.</summary>
        private IEnumerator RevealPop(Transform t)
        {
            yield return new WaitForSeconds(0.12f);
            float elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration && t != null)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                t.localScale = Vector3.one * CrateSize * (1f + 0.22f * k);
                t.localRotation = Quaternion.Euler(0f, 180f * Mathf.Clamp01(elapsed / duration), 0f);
                yield return null;
            }
            if (t != null)
            {
                t.localScale = Vector3.one * CrateSize;
                t.localRotation = Quaternion.identity;
            }
        }

        // ---------- Kilitli sütunlar ----------

        private void BuildLocks()
        {
            if (_state.LockAt == null) return;
            for (int c = 0; c < _state.Columns.Count; c++)
            {
                if (!_state.IsLocked(c)) continue;
                var root = new GameObject($"Lock{c}").transform;
                root.SetParent(_root, false);
                // Kamera kolilere üstten baktığı için bariyer öndeki kolinin üst yüzüne yatırılır
                root.localPosition = ColumnSlot(c, 0) + new Vector3(0f, CrateSize / 2f + 0.03f, 0f);
                foreach (float angle in new[] { 40f, -40f })
                {
                    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(b.GetComponent<Collider>());
                    b.transform.SetParent(root, false);
                    b.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                    b.transform.localScale = new Vector3(0.98f, 0.05f, 0.2f);
                    _factory.Paint(b, new Color(0.15f, 0.17f, 0.25f));
                    GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(stripe.GetComponent<Collider>());
                    stripe.transform.SetParent(root, false);
                    stripe.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                    stripe.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                    stripe.transform.localScale = new Vector3(0.9f, 0.02f, 0.11f);
                    _factory.Paint(stripe, Palette.Warning);
                }
                _locks[c] = root.gameObject;
            }
        }

        /// <summary>Kamyon gidince açılan kilitleri kaldırır. Açılan sütunları döndürür.</summary>
        public List<int> RefreshLocks()
        {
            var opened = new List<int>();
            foreach (var kv in new List<KeyValuePair<int, GameObject>>(_locks))
            {
                if (_state.IsLocked(kv.Key)) continue;
                opened.Add(kv.Key);
                _locks.Remove(kv.Key);
                if (kv.Key < _columns.Count) RefreshColumnTint(kv.Key);
                StartCoroutine(Confetti(ColumnSlot(kv.Key, 0) + Vector3.up * 0.6f, 2, 14, 0f, 0.6f));
                StartCoroutine(ShrinkAndDestroy(kv.Value.transform, 0.25f));
            }
            return opened;
        }

        private static IEnumerator ShrinkAndDestroy(Transform t, float duration)
        {
            float elapsed = 0f;
            Vector3 start = t.localScale;
            while (elapsed < duration && t != null)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                t.localScale = start * (1f + 0.4f * k) * (1f - k);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        /// <summary>Kilit rozeti için dünya konumu (sütunun önünün biraz üstü).</summary>
        public Vector3 LockBadgeWorld(int column) =>
            _root == null ? Vector3.zero : _root.TransformPoint(ColumnSlot(column, 0) + new Vector3(0f, 0.45f, 0.72f));

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
                int index = Mathf.Clamp(col.Count - 1 - move.FromDepth, 0, col.Count - 1);
                crate = col[index];
                col.RemoveAt(index);

                // Sonsuz mod: sütunun arkasından yeni koli gelir
                if (move.RefillColor >= 0)
                {
                    Transform fresh = MakeCrate(move.RefillColor, _root);
                    fresh.GetComponent<CrateInfo>().Hidden = move.RefillHidden;
                    fresh.localScale = Vector3.zero;
                    fresh.localPosition = ColumnSlot(move.FromColumn, col.Count + 1) + Vector3.down * 0.3f;
                    SetTappable(fresh.gameObject, Tappable.TapKind.Column, move.FromColumn);
                    col.Insert(0, fresh);
                    StartCoroutine(PopIn(fresh, CrateSize, 0.2f));
                }
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
            crate.localScale = Vector3.one * CrateSize * truck.CargoScale;
            truck.Cargo.Add(crate);
            StartMove(crate, CargoSlot(truck, move.SlotInTruck), 0.22f, 1.4f);
            StartCoroutine(Squash(truck.Root, 0.22f));

            if (move.TruckDeparted)
            {
                StartCoroutine(Depart(truck.Root, 0.3f));
                StartCoroutine(Confetti(DockPosition(move.ToDock) + Vector3.up * 0.8f, move.Color, 18, 0.25f, 1f));
                _docks[move.ToDock] = null;
                if (move.ArrivedTruck != null)
                {
                    Vector3 dock = DockPosition(move.ToDock);
                    TruckView arriving = MakeTruck(move.ArrivedTruck, dock + Vector3.forward * OffscreenZ);
                    _docks[move.ToDock] = arriving;
                    StartMove(arriving.Root, dock, 0.35f, 0f, 0.35f);
                }
            }

            // Raftan yeni kamyona kendiliğinden binen koliler
            if (move.AutoLoads != null)
                foreach (MoveResult auto in move.AutoLoads) Apply(auto);
        }

        /// <summary>Karıştırma sonrası: sütun kolilerini yeniden çizer, kısa bir zıplamayla.</summary>
        public void RebuildColumns()
        {
            for (int c = 0; c < _columns.Count; c++)
            {
                foreach (Transform t in _columns[c]) if (t != null) Destroy(t.gameObject);
                _columns[c].Clear();
                List<int> col = _state.Columns[c];
                for (int i = 0; i < col.Count; i++)
                {
                    Transform crate = MakeCrate(col[i], _root);
                    crate.localPosition = ColumnSlot(c, col.Count - 1 - i);
                    crate.GetComponent<CrateInfo>().Hidden = _state.Hidden[c][i];
                    crate.localScale = Vector3.zero;
                    SetTappable(crate.gameObject, Tappable.TapKind.Column, c);
                    _columns[c].Add(crate);
                    StartCoroutine(DelayedPop(crate, 0.03f * (c + i)));
                }
                RefreshColumnTint(c, false);
            }
        }

        private IEnumerator DelayedPop(Transform t, float delay)
        {
            yield return new WaitForSeconds(delay);
            yield return PopIn(t, CrateSize, 0.22f);
        }

        /// <summary>Mıknatıs efekti: kamyonun üstünde renkli parçacıklar.</summary>
        public void MagnetBurst(int dock, int color)
        {
            if (_root == null) return;
            StartCoroutine(Confetti(DockPosition(dock) + Vector3.up * 0.8f, color, 14, 0f, 0.7f));
        }

        /// <summary>Raftan kendiliğinden binen koliler ve mıknatıs gibi dizi hamleler için.</summary>
        public void ApplyAll(List<MoveResult> moves, float stagger)
        {
            StartCoroutine(ApplySequence(moves, stagger));
        }

        private IEnumerator ApplySequence(List<MoveResult> moves, float stagger)
        {
            foreach (MoveResult m in moves)
            {
                Apply(m);
                yield return new WaitForSeconds(stagger);
            }
        }

        // ---------- Konumlar (ipucu ve yazılar için, dünya koordinatı) ----------

        public Vector3 ColumnFrontWorld(int column)
        {
            if (_root == null || column < 0 || column >= _columns.Count) return Vector3.zero;
            return _root.TransformPoint(ColumnSlot(column, 0));
        }

        public Vector3 BufferWorld(int slot) =>
            _root == null ? Vector3.zero : _root.TransformPoint(BufferSlot(slot));

        public Vector3 DockWorld(int dock) =>
            _root == null ? Vector3.zero : _root.TransformPoint(DockPosition(dock) + Vector3.up * 0.8f);

        // ---------- Efektler ----------

        /// <summary>Raf dolmak üzereyken boş slotlar kırmızıya döner.</summary>
        public void SetBufferWarning(bool on)
        {
            Color c = on ? new Color(0.95f, 0.5f, 0.45f) : Palette.Slot;
            foreach (GameObject plate in _bufferPlates)
                if (plate != null) _factory.Paint(plate, c);
        }

        /// <summary>Küçük renkli parçacıklar: kamyon doldu / bölüm bitti.</summary>
        private IEnumerator Confetti(Vector3 localPos, int color, int count, float delay, float power)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (_root == null) yield break;
            Transform parent = _root;
            var pieces = new Transform[count];
            var velocity = new Vector3[count];
            var spin = new Vector3[count];
            Color[] colors = { Palette.Crate(color), Color.white, Palette.Warning, Palette.Crate(color + 3) };
            for (int i = 0; i < count; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Confetti";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localScale = new Vector3(0.16f, 0.03f, 0.1f);
                go.transform.localRotation = Random.rotation;
                _factory.Paint(go, colors[i % colors.Length]);
                pieces[i] = go.transform;
                Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f) * power;
                velocity[i] = new Vector3(dir.x, Random.Range(4f, 7f) * power, dir.y);
                spin[i] = Random.insideUnitSphere * 720f;
            }

            const float life = 1.1f;
            float t = 0f;
            while (t < life)
            {
                float dt = Time.deltaTime;
                t += dt;
                for (int i = 0; i < count; i++)
                {
                    if (pieces[i] == null) continue;
                    velocity[i] += Vector3.down * 14f * dt;
                    velocity[i] *= 1f - 1.5f * dt; // hava direnci
                    pieces[i].localPosition += velocity[i] * dt;
                    pieces[i].Rotate(spin[i] * dt);
                    if (t > life * 0.6f) pieces[i].localScale *= 1f - 4f * dt;
                }
                yield return null;
            }
            foreach (Transform p in pieces) if (p != null) Destroy(p.gameObject);
        }

        /// <summary>Bölüm sonu: renkli kamyonlar konvoy halinde rampanın önünden geçer.</summary>
        public void PlayWinCelebration(int[] colors)
        {
            if (_root == null) return;
            for (int i = 0; i < colors.Length; i++)
                StartCoroutine(ConvoyTruck(colors[i], i * 0.35f));
            StartCoroutine(Confetti(new Vector3(0f, 1f, BufferZ + 1f), colors.Length > 0 ? colors[0] : 0, 40, 0.1f, 1.4f));
        }

        private IEnumerator ConvoyTruck(int color, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_root == null) yield break;
            float laneZ = DockZ + 0.3f;
            VisualFactory.TruckParts parts = _factory.CreateTruck(color, _state.TruckCapacity, _root, new Vector3(-14f, 0f, laneZ));
            Transform truck = parts.Root;
            truck.localRotation = Quaternion.Euler(0f, 90f, 0f); // +x yönüne sürer
            float t = 0f;
            const float duration = 2.2f;
            while (t < duration && truck != null)
            {
                t += Time.deltaTime;
                float k = t / duration;
                truck.localPosition = new Vector3(Mathf.Lerp(-14f, 14f, k), Mathf.Abs(Mathf.Sin(t * 18f)) * 0.04f, laneZ);
                yield return null;
            }
            if (truck != null) Destroy(truck.gameObject);
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

        private static IEnumerator PopIn(Transform t, float size, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && t != null)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                float overshoot = 1f + 0.15f * Mathf.Sin(k * Mathf.PI);
                t.localScale = Vector3.one * size * k * overshoot;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one * size;
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
