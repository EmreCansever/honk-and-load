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

        public void Dispose()
        {
            foreach (Texture2D t in _owned) if (t != null) UnityEngine.Object.Destroy(t);
            _owned.Clear();
        }
    }
}
