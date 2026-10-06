using System.Collections.Generic;
using HonkAndLoad.Core;
using UnityEngine;

namespace HonkAndLoad.Gameplay
{
    /// <summary>
    /// Oyundaki tüm görsel nesneleri üretir: koliler, kamyonlar, depo zemini.
    /// Hepsi kodla, Unity'nin hazır şekilleriyle çizilir; dışarıdan dosya gerekmez.
    ///
    /// İsteğe bağlı: Assets/Resources/Models/Truck (fbx/prefab) varsa kamyon gövdesi
    /// olarak o model kullanılır (kendi rengine boyanır).
    /// </summary>
    public class VisualFactory
    {
        public const float CrateSize = 0.78f;
        public const float CargoSpacing = 0.82f;

        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        private readonly Dictionary<int, Sprite> _symbols = new Dictionary<int, Sprite>();
        private Material _baseMaterial;
        private GameObject _truckModel;
        private bool _truckModelChecked;

        // ---------- Malzeme ----------

        public void Paint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (_baseMaterial == null) _baseMaterial = renderer.sharedMaterial;
            if (!_materials.TryGetValue(color, out Material mat))
            {
                mat = new Material(_baseMaterial) { color = color };
                _materials[color] = mat;
            }
            renderer.sharedMaterial = mat;
        }

        private GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Color color, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Paint(go, color);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        // ---------- Koli ----------

        /// <summary>Renkli koli: bant şeridi + renk körleri için üstte sembol.</summary>
        public Transform CreateCrate(int color, Transform parent)
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = $"Crate_{color}";
            crate.transform.SetParent(parent, false);
            crate.transform.localScale = Vector3.one * CrateSize;
            Paint(crate, Palette.Crate(color));
            crate.AddComponent<CrateInfo>().Color = color;

            // Koli bandı (üstten ve yanlardan geçen şerit). Koordinatlar kolinin kendi biriminde.
            Color tape = Color.Lerp(Palette.Crate(color), Color.white, 0.35f);
            Box("Tape", crate.transform, Vector3.zero, new Vector3(1.02f, 1.02f, 0.2f), tape);

            // Sembol (üst yüz)
            var symbol = new GameObject("Symbol");
            symbol.transform.SetParent(crate.transform, false);
            symbol.transform.localPosition = new Vector3(0f, 0.515f, 0f);
            symbol.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            symbol.transform.localScale = Vector3.one * 0.62f;
            var sr = symbol.AddComponent<SpriteRenderer>();
            sr.sprite = Symbol(color);
            sr.color = new Color(1f, 1f, 1f, 0.92f);

            return crate.transform;
        }

        /// <summary>Arkadaki (henüz alınamayan) kolileri soluk göster.</summary>
        public void SetCrateDimmed(Transform crate, bool dimmed)
        {
            var info = crate.GetComponent<CrateInfo>();
            if (info == null || info.Dimmed == dimmed) return;
            info.Dimmed = dimmed;
            Color c = Palette.Crate(info.Color);
            // Rengi beyaza değil koyuya doğru kaydır: ton korunur, renkler karışmaz
            Paint(crate.gameObject, dimmed ? Color.Lerp(c, new Color(0.25f, 0.25f, 0.3f), 0.3f) : c);
            Transform symbol = crate.Find("Symbol");
            if (symbol != null)
                symbol.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, dimmed ? 0.6f : 0.92f);
        }

        // ---------- Kamyon ----------

        /// <summary>Hazır model kullanılırken hedef kamyon genişliği (dünya birimi).</summary>
        private const float ModelTargetWidth = 1.9f;
        /// <summary>Hazır modelin boyuna hafif uzatılması (kasaya 3 koli sığsın diye).</summary>
        private const float ModelStretchZ = 1.2f;
        /// <summary>Kamyonun arka ucunun rampa noktasına göre konumu.</summary>
        private const float TruckRearZ = -1.45f;

        public class TruckParts
        {
            public Transform Root;
            /// <summary>Kasadaki her koli yerinin kamyona göre konumu.</summary>
            public Vector3[] Slots;
            /// <summary>Kasadaki kolilerin boyut çarpanı (küçük kasalarda koliler küçülür).</summary>
            public float CargoScale = 1f;
        }

        /// <summary>Hazır modelin bir kez yapılan ölçüm sonucu.</summary>
        private class ModelInfo
        {
            public float Yaw;
            public Vector3 Scale;
            public Vector3 Offset;
            public float BedStartZ, BedEndZ, BedTopY; // kamyon koordinatında
            public float FrontZ;
        }

        private ModelInfo _modelInfo;

        public bool UsesTruckModel
        {
            get { EnsureTruckModel(); return _truckModel != null; }
        }

        /// <summary>Rampalar arası mesafe: hazır model daha geniş olduğu için açılır.</summary>
        public float DockSpacing => UsesTruckModel ? ModelTargetWidth + 0.3f : 1.9f;

        /// <summary>Kamyonun rampa noktasından ne kadar ileri uzandığı (kamera sığdırma için).</summary>
        public float TruckFrontExtent(int capacity) =>
            UsesTruckModel ? _modelInfo.FrontZ : capacity * CargoSpacing / 2f + 0.95f;

        private void EnsureTruckModel()
        {
            if (_truckModelChecked) return;
            _truckModelChecked = true;
            _truckModel = Resources.Load<GameObject>("Models/Truck");
            if (_truckModel != null) _modelInfo = AnalyzeModel(_truckModel);
        }

        public TruckParts CreateTruck(int color, int capacity, Transform parent, Vector3 position)
        {
            EnsureTruckModel();
            var root = new GameObject($"Truck_{color}").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            var parts = new TruckParts { Root = root };

            if (_truckModel != null) BuildModelTruck(parts, color, capacity);
            else BuildProceduralTruck(parts, color, capacity);
            return parts;
        }

        private void BuildProceduralTruck(TruckParts p, int color, int capacity)
        {
            Color body = Palette.Crate(color);
            Color dark = Palette.Truck(color);
            float L = capacity * CargoSpacing + 0.2f;
            Transform r = p.Root;

            // Şasi ve kasa
            Box("Chassis", r, new Vector3(0f, -0.2f, 0.2f), new Vector3(1.2f, 0.12f, L + 1.0f), new Color(0.2f, 0.2f, 0.22f));
            Box("Bed", r, new Vector3(0f, -0.08f, 0f), new Vector3(1.3f, 0.14f, L), dark);
            Box("WallLeft", r, new Vector3(-0.61f, 0.12f, 0f), new Vector3(0.08f, 0.3f, L), body);
            Box("WallRight", r, new Vector3(0.61f, 0.12f, 0f), new Vector3(0.08f, 0.3f, L), body);
            Box("WallBack", r, new Vector3(0f, 0.12f, -L / 2f + 0.04f), new Vector3(1.3f, 0.3f, 0.08f), body);

            // Kabin
            float cz = L / 2f + 0.45f;
            Box("Cab", r, new Vector3(0f, 0.25f, cz), new Vector3(1.3f, 0.8f, 0.8f), body);
            Box("CabRoof", r, new Vector3(0f, 0.68f, cz - 0.05f), new Vector3(1.2f, 0.06f, 0.7f), Color.Lerp(body, Color.white, 0.4f));
            Box("Windshield", r, new Vector3(0f, 0.4f, cz + 0.41f), new Vector3(1.1f, 0.3f, 0.03f), new Color(0.25f, 0.4f, 0.6f));
            Box("Bumper", r, new Vector3(0f, -0.08f, cz + 0.43f), new Vector3(1.32f, 0.12f, 0.06f), new Color(0.75f, 0.75f, 0.78f));
            Color lamp = new Color(1f, 0.95f, 0.6f);
            Box("LampL", r, new Vector3(-0.45f, 0.05f, cz + 0.41f), new Vector3(0.18f, 0.1f, 0.04f), lamp);
            Box("LampR", r, new Vector3(0.45f, 0.05f, cz + 0.41f), new Vector3(0.18f, 0.1f, 0.04f), lamp);

            // Tekerlekler
            float[] zs = { -L / 2f + 0.35f, L / 2f - 0.1f, cz };
            foreach (float z in zs)
            {
                Wheel(r, new Vector3(-0.62f, -0.27f, z));
                Wheel(r, new Vector3(0.62f, -0.27f, z));
            }

            p.CargoScale = 1f;
            p.Slots = new Vector3[capacity];
            for (int i = 0; i < capacity; i++)
                p.Slots[i] = new Vector3(0f, -0.01f + CrateSize / 2f, -L / 2f + 0.5f + i * CargoSpacing);
        }

        private void Wheel(Transform parent, Vector3 pos)
        {
            GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            w.name = "Wheel";
            w.transform.SetParent(parent, false);
            w.transform.localPosition = pos;
            w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            w.transform.localScale = new Vector3(0.46f, 0.08f, 0.46f);
            Paint(w, new Color(0.12f, 0.12f, 0.13f));
            Object.Destroy(w.GetComponent<Collider>());
        }

        private void BuildModelTruck(TruckParts p, int color, int capacity)
        {
            ModelInfo m = _modelInfo;
            var wrapper = new GameObject("Model").transform;
            wrapper.SetParent(p.Root, false);
            wrapper.localPosition = m.Offset;
            wrapper.localScale = m.Scale;

            GameObject model = Object.Instantiate(_truckModel, wrapper, false);
            model.transform.localRotation = Quaternion.Euler(0f, m.Yaw, 0f) * _truckModel.transform.localRotation;
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);

            Color tint = Color.Lerp(Palette.Crate(color), Color.white, 0.15f);
            foreach (Renderer rend in model.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = rend.materials; // örnek kopyaları: yalnızca bu kamyon boyanır
                foreach (Material mat in mats) mat.color = tint;
                rend.materials = mats;
            }

            // Kolileri kasanın içine eşit aralıkla diz; sığmazsa küçült
            float pitch = (m.BedEndZ - m.BedStartZ) / capacity;
            p.CargoScale = Mathf.Clamp(pitch * 0.9f / CrateSize, 0.4f, 1f);
            float half = CrateSize * p.CargoScale / 2f;
            p.Slots = new Vector3[capacity];
            for (int i = 0; i < capacity; i++)
                p.Slots[i] = new Vector3(0f, m.BedTopY + half, m.BedStartZ + pitch * (i + 0.5f));
        }

        /// <summary>
        /// Hazır modeli bir kez ölçer: yönünü (kabin önde), ölçeğini ve kasanın nerede
        /// olduğunu bulur. Kasa = kabinin arkasındaki en uzun alçak bölge.
        /// </summary>
        private ModelInfo AnalyzeModel(GameObject prefab)
        {
            var temp = new GameObject("TruckModelProbe").transform;
            GameObject model = Object.Instantiate(prefab, temp, false);
            Quaternion baseRot = prefab.transform.localRotation;

            Bounds b = LocalBounds(model.transform, temp);
            float yaw = b.size.x > b.size.z ? 90f : 0f;
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * baseRot;

            float[] heights = HeightProfile(model.transform, temp, out Bounds mb, out bool readable);
            int n = heights.Length;

            // Kabin (en yüksek bölge) arkada kaldıysa modeli çevir
            int tallest = 0;
            for (int i = 1; i < n; i++) if (heights[i] > heights[tallest]) tallest = i;
            if (readable && tallest < n / 2)
            {
                yaw += 180f;
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * baseRot;
                heights = HeightProfile(model.transform, temp, out mb, out readable);
                tallest = 0;
                for (int i = 1; i < n; i++) if (heights[i] > heights[tallest]) tallest = i;
            }

            // Kasa: kabinin arkasında, yüksekliği alt %30'da kalan en uzun kesintisiz bölge
            float bedStartFrac = 0.09f, bedEndFrac = 0.51f, bedTopFrac = 0.47f; // Kenney truck-flat için yedek değerler
            if (readable)
            {
                float minH = float.MaxValue, maxH = float.MinValue;
                foreach (float h in heights)
                    if (!float.IsNegativeInfinity(h)) { minH = Mathf.Min(minH, h); maxH = Mathf.Max(maxH, h); }
                float limit = minH + 0.35f * (maxH - minH);
                int bestStart = -1, bestLen = 0;
                for (int i = 0; i < tallest; )
                {
                    if (heights[i] > limit || float.IsNegativeInfinity(heights[i])) { i++; continue; }
                    int j = i;
                    while (j < tallest && heights[j] <= limit && !float.IsNegativeInfinity(heights[j])) j++;
                    if (j - i > bestLen) { bestLen = j - i; bestStart = i; }
                    i = j;
                }
                if (bestLen >= 2)
                {
                    float top = float.MinValue;
                    for (int i = bestStart; i < bestStart + bestLen; i++) top = Mathf.Max(top, heights[i]);
                    bedStartFrac = (float)bestStart / n + 0.02f;
                    bedEndFrac = (float)(bestStart + bestLen) / n - 0.02f;
                    bedTopFrac = (top - mb.min.y) / Mathf.Max(0.001f, mb.size.y);
                }
            }
            else
            {
                Debug.LogWarning("[HonkAndLoad] Kamyon modeli okunamıyor; Truck.fbx için Inspector'da Read/Write'ı aç. Yaklaşık değerler kullanılıyor.");
            }

            // Ölçek: genişlik sabit, boy hafif uzatılmış
            float s = ModelTargetWidth / Mathf.Max(0.001f, mb.size.x);
            var info = new ModelInfo { Yaw = yaw, Scale = new Vector3(s, s, s * ModelStretchZ) };
            float length = mb.size.z * s * ModelStretchZ;
            float height = mb.size.y * s;
            // Arka uç TruckRearZ'de, tekerlekler zeminde (y = -0.5), ortalanmış
            info.Offset = new Vector3(-mb.center.x * s, -0.5f - mb.min.y * s, TruckRearZ - mb.min.z * s * ModelStretchZ);
            info.BedStartZ = TruckRearZ + bedStartFrac * length;
            info.BedEndZ = TruckRearZ + bedEndFrac * length;
            info.BedTopY = -0.5f + bedTopFrac * height;
            info.FrontZ = TruckRearZ + length;

            Object.DestroyImmediate(temp.gameObject); // aynı karede görünmesin
            return info;
        }

        /// <summary>
        /// Modelin boyuna (z) göre üst yüzey yüksekliği. Yalnızca orta çizgiden (x≈0) geçen
        /// üçgenler sayılır; böylece yan çamurluklar kasayı yüksek göstermez.
        /// </summary>
        private static float[] HeightProfile(Transform model, Transform space, out Bounds bounds, out bool readable)
        {
            const int bins = 32;
            var heights = new float[bins];
            for (int i = 0; i < bins; i++) heights[i] = float.NegativeInfinity;
            bounds = LocalBounds(model, space);
            readable = true;

            float z0 = bounds.min.z, zSize = Mathf.Max(0.001f, bounds.size.z);
            float cx = bounds.center.x, tolerance = bounds.size.x * 0.05f;
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable) { readable = false; continue; }
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                Matrix4x4 toSpace = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int k = 0; k < v.Length; k++) v[k] = toSpace.MultiplyPoint3x4(v[k]);
                for (int k = 0; k < t.Length; k += 3)
                {
                    Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                    float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                    if (minX > cx + tolerance || maxX < cx - tolerance) continue;
                    float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                    float y = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                    for (int i = 0; i < bins; i++)
                    {
                        float mid = z0 + zSize * (i + 0.5f) / bins;
                        if (mid >= minZ && mid <= maxZ && y > heights[i]) heights[i] = y;
                    }
                }
            }
            return heights;
        }

        private static Bounds LocalBounds(Transform model, Transform space)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            bool first = true;
            var result = new Bounds();
            foreach (Renderer r in renderers)
            {
                Bounds wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = wb.center + Vector3.Scale(wb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 local = space.InverseTransformPoint(corner);
                    if (first) { result = new Bounds(local, Vector3.zero); first = false; }
                    else result.Encapsulate(local);
                }
            }
            return result;
        }

        // ---------- Ortam ----------

        /// <summary>Depo zemini, kamyon rampası, raf ve koli sütunlarının altı.</summary>
        public void BuildEnvironment(Transform root, float width, float columnsFrontZ, float columnsBackZ,
            float bufferZ, float bufferWidth, float dockZ, int dockCount, float dockSpacing, float[] columnXs)
        {
            Box("Ground", root, new Vector3(0f, -0.55f, 1f), new Vector3(60f, 0.1f, 60f), Palette.Ground);

            // Rampa bölgesi: asfalt + şerit çizgileri + sarı yükleme çizgisi
            float asphaltStart = dockZ - 1.7f;
            float asphaltLength = 30f;
            Box("Asphalt", root, new Vector3(0f, -0.495f, asphaltStart + asphaltLength / 2f),
                new Vector3(Mathf.Max(width, dockCount * dockSpacing) + 6f, 0.02f, asphaltLength), Palette.Asphalt);
            Box("LoadingLine", root, new Vector3(0f, -0.48f, asphaltStart + 0.1f),
                new Vector3(dockCount * dockSpacing, 0.02f, 0.12f), Palette.Warning);
            for (int i = 0; i <= dockCount; i++)
            {
                float x = (i - dockCount / 2f) * dockSpacing;
                for (int k = 0; k < 6; k++)
                    Box("LaneDash", root, new Vector3(x, -0.48f, asphaltStart + 0.6f + k * 1.2f),
                        new Vector3(0.07f, 0.02f, 0.6f), new Color(0.95f, 0.95f, 0.95f));
            }

            // Raf: metal tezgâh
            Box("Shelf", root, new Vector3(0f, -0.49f, bufferZ),
                new Vector3(bufferWidth + 0.3f, 0.04f, 1.25f), Palette.Shelf);

            // Koli sütunlarının altındaki zemin şeritleri
            float length = columnsFrontZ - columnsBackZ + 1.0f;
            float centerZ = (columnsFrontZ + columnsBackZ) / 2f;
            foreach (float x in columnXs)
                Box("ColumnLane", root, new Vector3(x, -0.49f, centerZ), new Vector3(0.9f, 0.02f, length), Palette.Lane);
        }

        // ---------- Semboller ----------

        /// <summary>Her renk için farklı bir şekil (renk körü oyuncular için).</summary>
        private Sprite Symbol(int color)
        {
            int shape = ((color % 8) + 8) % 8;
            if (_symbols.TryGetValue(shape, out Sprite s)) return s;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // -1..1 koordinatları, birkaç alt örnekle kenar yumuşatma
                    int hits = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / 3f) / size) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / 3f) / size) * 2f - 1f;
                            if (Inside(shape, u, v)) hits++;
                        }
                    byte a = (byte)(255 * hits / 9);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _symbols[shape] = s;
            return s;
        }

        private static bool Inside(int shape, float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            switch (shape)
            {
                case 0: return r < 0.75f;                                           // daire
                case 1: return Mathf.Abs(u) < 0.62f && Mathf.Abs(v) < 0.62f;        // kare
                case 2: return v > -0.6f && Mathf.Abs(u) < (0.75f - v) * 0.62f && v < 0.75f; // üçgen
                case 3: return Mathf.Abs(u) + Mathf.Abs(v) < 0.8f;                  // elmas
                case 4:                                                             // yıldız
                {
                    float angle = Mathf.Atan2(v, u) - Mathf.PI / 2f;
                    float k = Mathf.Cos(5f * angle);
                    return r < 0.4f + 0.4f * Mathf.Max(0f, k);
                }
                case 5: return (Mathf.Abs(u) < 0.22f && Mathf.Abs(v) < 0.75f)
                            || (Mathf.Abs(v) < 0.22f && Mathf.Abs(u) < 0.75f);      // artı
                case 6: return r < 0.75f && r > 0.42f;                              // halka
                default:                                                            // kalp
                {
                    float x = u * 1.15f, y = v * 1.15f + 0.15f;
                    float q = x * x + y * y - 0.5f;
                    return q * q * q - x * x * y * y * y < 0f;
                }
            }
        }
    }
}
