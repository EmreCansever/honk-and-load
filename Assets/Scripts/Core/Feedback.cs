using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>
    /// Ses ve titreşim. Prototipte sesler kodla üretilir; Aşama 3'te gerçek ses
    /// dosyalarıyla değiştirilecek.
    /// </summary>
    public class Feedback : MonoBehaviour
    {
        private AudioSource _source;
        private AudioClip _thud, _honk, _error, _win;
        private int _combo;

        public bool HapticsEnabled = true;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _thud = Tone("thud", 0.09f, 220f, 0f, 0.6f, true);
            _honk = Honk();
            _error = Tone("error", 0.12f, 140f, -40f, 0.35f, false);
            _win = Tone("win", 0.35f, 520f, 600f, 0.4f, false);
        }

        /// <summary>Kamyona yükleme: art arda yüklemelerde ses perdesi yükselir.</summary>
        public void Load()
        {
            _source.pitch = 1f + Mathf.Min(_combo, 8) * 0.07f;
            _source.PlayOneShot(_thud);
            _combo++;
        }

        /// <summary>Rafa koyma: seriyi sıfırlar.</summary>
        public void ToBuffer()
        {
            _combo = 0;
            _source.pitch = 0.85f;
            _source.PlayOneShot(_thud, 0.7f);
        }

        public void TruckDeparts()
        {
            _source.pitch = 1f;
            _source.PlayOneShot(_honk, 0.8f);
            Vibrate();
        }

        public void Invalid()
        {
            _source.pitch = 1f;
            _source.PlayOneShot(_error, 0.6f);
        }

        public void Win()
        {
            _combo = 0;
            _source.pitch = 1f;
            _source.PlayOneShot(_win);
            Vibrate();
        }

        public void ResetCombo() => _combo = 0;

        private void Vibrate()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (HapticsEnabled) Handheld.Vibrate();
#endif
        }

        // ---------- Basit ses üretimi ----------

        private static AudioClip Tone(string name, float seconds, float freq, float sweep, float volume, bool noisy)
        {
            const int rate = 44100;
            int samples = Mathf.CeilToInt(rate * seconds);
            var data = new float[samples];
            var rng = new System.Random(1);
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float f = freq + sweep * t;
                phase += 2f * Mathf.PI * f / rate;
                float env = Mathf.Exp(-5f * t) * Mathf.Min(1f, i / 200f);
                float s = Mathf.Sin(phase);
                if (noisy) s = s * 0.8f + ((float)rng.NextDouble() * 2f - 1f) * 0.2f * (1f - t);
                data[i] = s * env * volume;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Honk()
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.32f);
            var data = new float[samples];
            float p1 = 0f, p2 = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                p1 += 2f * Mathf.PI * 370f / rate;
                p2 += 2f * Mathf.PI * 466f / rate;
                // Kare dalgaya yakın, iki notalı korna
                float s = Mathf.Sign(Mathf.Sin(p1)) * 0.5f + Mathf.Sign(Mathf.Sin(p2)) * 0.5f;
                float env = Mathf.Min(1f, i / 400f) * (t > 0.85f ? (1f - t) / 0.15f : 1f);
                data[i] = s * env * 0.18f;
            }
            AudioClip clip = AudioClip.Create("honk", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
