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

        /// <summary>Hazır kamyon modeli ters yöne bakıyorsa bunu 180 yap.</summary>
        public const float TruckModelExtraYaw = 0f;

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

        public class TruckParts
        {
            public Transform Root;
            public float BedLength;
            public float CargoY;
        }

        public TruckParts CreateTruck(int color, int capacity, Transform parent, Vector3 position)
        {
            var parts = new TruckParts { BedLength = capacity * CargoSpacing + 0.2f };
            var root = new GameObject($"Truck_{color}").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            parts.Root = root;

            if (!_truckModelChecked)
            {
                _truckModel = Resources.Load<GameObject>("Models/Truck");
                _truckModelChecked = true;
            }

            if (_truckModel != null) BuildModelTruck(parts, color);
            else BuildProceduralTruck(parts, color);
            return parts;
        }

        private void BuildProceduralTruck(TruckParts p, int color)
        {
            Color body = Palette.Crate(color);
            Color dark = Palette.Truck(color);
            float L = p.BedLength;
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

            p.CargoY = -0.01f + CrateSize / 2f;
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

        /// <summary>
        /// Hazır modeli kamyon boyutuna sığdırır: en uzun yatay ekseni ileri (+z) çevirir,
        /// uzunluğu kasaya göre ölçekler, modeli kamyonun rengine boyar.
        /// </summary>
        private void BuildModelTruck(TruckParts p, int color)
        {
            GameObject model = Object.Instantiate(_truckModel, p.Root, false);
            model.name = "Model";
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);

            Bounds b = LocalBounds(model.transform, p.Root);
            float yaw = (b.size.x > b.size.z ? 90f : 0f) + TruckModelExtraYaw;
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;

            b = LocalBounds(model.transform, p.Root);
            float targetLength = p.BedLength + 1.0f;
            float scale = b.size.z > 0.001f ? targetLength / b.size.z : 1f;
            model.transform.localScale *= scale;

            b = LocalBounds(model.transform, p.Root);
            // Tekerlekler zemine (y = -0.5) otursun, kasa ortalansın (kabin önde)
            model.transform.localPosition += new Vector3(-b.center.x, -0.5f - b.min.y, 0.5f - b.center.z);

            Color tint = Color.Lerp(Palette.Crate(color), Color.white, 0.15f);
            foreach (Renderer rend in model.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = rend.materials; // örnek kopyaları: yalnızca bu kamyon boyanır
                foreach (Material m in mats) m.color = tint;
                rend.materials = mats;
            }

            b = LocalBounds(model.transform, p.Root);
            // Kasa yüksekliği tahmini: modelin alt %40'ı
            p.CargoY = b.min.y + b.size.y * 0.4f + CrateSize / 2f;
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
