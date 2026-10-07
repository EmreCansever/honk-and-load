using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonkAndLoad.UI
{
    /// <summary>
    /// Arayüz için kodla üretilen dokular: yuvarlak köşeli degrade kartlar, yumuşak gölge,
    /// ikonlar. Kenarlar yumuşatılır; kartlar 9-dilim (GUIStyle.border) ile her boyuta uyar.
    /// </summary>
    public class UiTextures : IDisposable
    {
        private readonly List<Texture2D> _owned = new List<Texture2D>();

        private Texture2D Track(Texture2D t)
        {
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            _owned.Add(t);
            return t;
        }

        /// <summary>Yuvarlak köşe için işaretli uzaklık (içeride negatif).</summary>
        private static float RoundRectDistance(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w / 2f) - (w / 2f - r);
            float qy = Mathf.Abs(y - h / 2f) - (h / 2f - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>
        /// Oyun kartı: üstten alta degrade, üstte ince parlak çizgi, altta koyu "3B kenar".
        /// </summary>
        public Texture2D RoundedCard(Color top, Color bottom, int size = 128, int radius = 40, int lip = 10)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Color lipColor = Color.Lerp(bottom, Color.black, 0.3f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture y'si aşağıdan yukarı; görsel olarak "üst" = yüksek y
                    float d = RoundRectDistance(x + 0.5f, y + 0.5f, size, size, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) { px[y * size + x] = Color.clear; continue; }

                    float v = (float)y / (size - 1); // 0 alt, 1 üst
                    Color c = Color.Lerp(bottom, top, v);
                    // Alt kenar: koyu dudak (basılabilir buton hissi)
                    float dLip = RoundRectDistance(x + 0.5f, y + 0.5f - lip, size, size - lip, radius);
                    if (y < lip + radius && dLip > -0.5f) c = lipColor;
                    // Üst iç parlaklık
                    if (d > -4f && y > size / 2) c = Color.Lerp(c, Color.white, 0.25f);
                    c.a = alpha;
                    px[y * size + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply();
            return Track(t);
        }

        /// <summary>Kartların altındaki yumuşak gölge.</summary>
        public Texture2D SoftShadow(int size = 128, int radius = 44, float softness = 18f, float strength = 0.35f)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float inset = softness;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundRectDistance(x + 0.5f - inset, y + 0.5f - inset,
                        size - 2 * inset, size - 2 * inset, Mathf.Max(1f, radius - inset));
                    float a = Mathf.Clamp01(1f - (d + softness) / (2f * softness));
                    px[y * size + x] = new Color(0f, 0f, 0.1f, a * a * strength);
                }
            t.SetPixels(px);
            t.Apply();
            return Track(t);
        }

        /// <summary>Düz renkli yuvarlak hap (rozet, ayar butonları).</summary>
        public Texture2D Pill(Color color, int size = 96)
        {
            return RoundedFlat(color, size, size / 2);
        }

        public Texture2D RoundedFlat(Color color, int size, int radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundRectDistance(x + 0.5f, y + 0.5f, size, size, radius);
                    Color c = color;
                    c.a *= Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return Track(t);
        }

        /// <summary>Dikey degrade (arka plan).</summary>
        public Texture2D VerticalGradient(Color top, Color bottom, int height = 256)
        {
            var t = new Texture2D(1, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
                t.SetPixel(0, y, Color.Lerp(bottom, top, (float)y / (height - 1)));
            t.Apply();
            return Track(t);
        }

        /// <summary>Beyaz şekil ikonu; inside(u, v) -1..1 koordinatında (v yukarı).</summary>
        public Texture2D Icon(int size, Func<float, float, bool> inside)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float u = (x + (sx + 0.5f) / 3f) / size * 2f - 1f;
                            float v = (y + (sy + 0.5f) / 3f) / size * 2f - 1f;
                            if (inside(u, v)) hits++;
                        }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / 9));
                }
            t.SetPixels32(px);
            t.Apply();
            return Track(t);
        }

        // ---------- Hazır ikonlar ----------

        public Texture2D Circle() => Icon(128, (u, v) => u * u + v * v < 0.96f);

        /// <summary>Oynat üçgeni (sağa bakan).</summary>
        public Texture2D Play() => Icon(96, (u, v) => u > -0.45f && Mathf.Abs(v) < (0.65f - u) * 0.62f && u < 0.65f);

        /// <summary>Bayrak: direk + dalgalı bayrak (Macera).</summary>
        public Texture2D Flag() => Icon(128, (u, v) =>
        {
            bool pole = u > -0.62f && u < -0.48f && v > -0.85f && v < 0.85f;
            bool baseOk = v > -0.92f && v < -0.78f && u > -0.8f && u < -0.3f;
            float wave = 0.08f * Mathf.Sin((u + 0.48f) * 6f);
            bool cloth = u >= -0.48f && u < 0.7f && v < 0.8f + wave && v > 0.12f + wave;
            return pole || baseOk || cloth;
        });

        /// <summary>Sonsuzluk işareti (Sonsuz mod): kalın lemniskat.</summary>
        public Texture2D Infinity() => Icon(128, (u, v) =>
        {
            float x = u * 1.05f, y = v * 1.6f, a = 0.92f;
            float r2 = x * x + y * y;
            float f = r2 * r2 - a * a * (x * x - y * y);
            float gx = 4f * x * r2 - 2f * a * a * x;
            float gy = 4f * y * r2 + 2f * a * a * y;
            float grad = Mathf.Sqrt(gx * gx + gy * gy) + 1e-4f;
            return Mathf.Abs(f) / grad < 0.11f;
        });

        // ---------- Ekonomi ikonları ----------

        private static float Seg(float u, float v, float ax, float ay, float bx, float by)
        {
            float px = u - ax, py = v - ay, dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            float ex = px - dx * t, ey = py - dy * t;
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        private static bool Tri(float u, float v, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (u - b.x) * (a.y - b.y) - (a.x - b.x) * (v - b.y);
            float d2 = (u - c.x) * (b.y - c.y) - (b.x - c.x) * (v - c.y);
            float d3 = (u - a.x) * (c.y - a.y) - (c.x - a.x) * (v - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>Altın: sarı disk, koyu iç halka, parlama.</summary>
        public Texture2D Coin(int size = 96)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Color rim = new Color(0.85f, 0.55f, 0.05f), face = new Color(1f, 0.80f, 0.18f), inner = new Color(0.95f, 0.66f, 0.08f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((0.97f - r) * size * 0.5f);
                    Color c = r > 0.82f ? rim : (r > 0.62f && r < 0.7f ? inner : face);
                    if (r < 0.62f && u - v < -0.25f && u - v > -0.55f) c = Color.Lerp(c, Color.white, 0.55f);
                    c.a = a;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return Track(t);
        }

        /// <summary>Alışveriş çantası (Market).</summary>
        public Texture2D Bag() => Icon(128, (u, v) =>
        {
            // Aşağı doğru genişleyen gövde, üstte ince sap, ortada iki delik
            float half = Mathf.Lerp(0.78f, 0.58f, (v + 0.85f) / 1.05f);
            bool body = v > -0.85f && v < 0.2f && Mathf.Abs(u) < half;
            float r = Mathf.Sqrt(u * u + (v - 0.2f) * (v - 0.2f));
            bool handle = v > 0.2f && r > 0.24f && r < 0.36f;
            bool holes = Mathf.Abs(Mathf.Abs(u) - 0.3f) < 0.07f && v > -0.05f && v < 0.08f;
            return (body && !holes) || handle;
        });

        /// <summary>Geri al: kıvrık ok (sol uçta aşağı bakan ok başı).</summary>
        public Texture2D Undo() => Icon(128, (u, v) =>
        {
            float cx = 0.12f, cy = -0.05f;
            float dx = u - cx, dy = v - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            bool arc = r > 0.4f && r < 0.66f && ang > -55f;
            bool head = Tri(u, v, new Vector2(cx - 0.9f, cy + 0.02f), new Vector2(cx - 0.16f, cy + 0.02f), new Vector2(cx - 0.53f, cy - 0.45f));
            return arc || head;
        });

        /// <summary>Mıknatıs: U şekli, uçları ayrık.</summary>
        public Texture2D Magnet() => Icon(128, (u, v) =>
        {
            float cy = -0.05f;
            float r = Mathf.Sqrt(u * u + (v - cy) * (v - cy));
            bool bottom = v <= cy && r > 0.3f && r < 0.78f;
            bool legs = v > cy && v < 0.85f && Mathf.Abs(u) > 0.3f && Mathf.Abs(u) < 0.78f;
            bool gap = v > 0.5f && v < 0.6f;
            return (bottom || legs) && !gap;
        });

        /// <summary>Karıştır: çapraz iki ok.</summary>
        public Texture2D Shuffle() => Icon(128, (u, v) =>
        {
            bool a = Seg(u, v, -0.85f, -0.55f, 0.45f, 0.55f) < 0.12f;
            bool b = Seg(u, v, -0.85f, 0.55f, 0.45f, -0.55f) < 0.12f;
            bool h1 = Tri(u, v, new Vector2(0.95f, 0.55f), new Vector2(0.4f, 0.9f), new Vector2(0.4f, 0.2f));
            bool h2 = Tri(u, v, new Vector2(0.95f, -0.55f), new Vector2(0.4f, -0.2f), new Vector2(0.4f, -0.9f));
            return a || b || h1 || h2;
        });

        public Texture2D Plus() => Icon(96, (u, v) =>
            (Mathf.Abs(u) < 0.2f && Mathf.Abs(v) < 0.8f) || (Mathf.Abs(v) < 0.2f && Mathf.Abs(u) < 0.8f));

        public Texture2D Close() => Icon(96, (u, v) =>
            Seg(u, v, -0.6f, -0.6f, 0.6f, 0.6f) < 0.16f || Seg(u, v, -0.6f, 0.6f, 0.6f, -0.6f) < 0.16f);

        public Texture2D Lock() => Icon(96, (u, v) =>
        {
            bool body = u > -0.62f && u < 0.62f && v > -0.85f && v < 0.1f;
            float r = Mathf.Sqrt(u * u + (v - 0.1f) * (v - 0.1f));
            bool shackle = v > 0.1f && r > 0.28f && r < 0.46f;
            return body || shackle;
        });

        /// <summary>Reklam: oynat düğmeli ekran.</summary>
        public Texture2D AdIcon() => Icon(96, (u, v) =>
        {
            bool frame = Mathf.Abs(u) < 0.9f && Mathf.Abs(v) < 0.65f;
            bool inside = Mathf.Abs(u) < 0.74f && Mathf.Abs(v) < 0.5f;
            bool play = Tri(u, v, new Vector2(-0.22f, 0.32f), new Vector2(-0.22f, -0.32f), new Vector2(0.35f, 0f));
            return (frame && !inside) || play;
        });

        public void Dispose()
        {
            foreach (Texture2D t in _owned) if (t != null) UnityEngine.Object.Destroy(t);
            _owned.Clear();
        }
    }
}
