using System;
using System.Collections;
using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>
    /// Reklamlar. AdMob bağlanana kadar ödüllü reklam kısa bir "test reklamı"
    /// ekranıyla simüle edilir ve ödül verilir; tam ekran reklam gösterilmez.
    /// AdMob eklenince yalnızca bu sınıfın içi değişecek.
    /// </summary>
    public class AdService : MonoBehaviour
    {
        public static AdService Instance { get; private set; }

        /// <summary>Test reklamı ekranda mı (arayüz bunu çizer).</summary>
        public bool IsShowing { get; private set; }
        public float ShowProgress { get; private set; }
        public const float SimulatedLength = 1.6f;

        /// <summary>Bölüm arası tam ekran reklam sıklığı (her N bölümde bir).</summary>
        public const int InterstitialEveryLevels = 3;

        private void Awake() => Instance = this;

        public bool RewardedReady => !IsShowing;

        /// <summary>Ödüllü reklam: sonuna kadar izlenirse done(true).</summary>
        public void ShowRewarded(Action<bool> done)
        {
            if (IsShowing) { done(false); return; }
            StartCoroutine(Simulate(done));
        }

        /// <summary>Bölüm arası reklam. "Reklamsız" alındıysa hiç gösterilmez.</summary>
        public void MaybeShowInterstitial(int levelJustWon)
        {
            if (Progress.NoAds) return;
            if (levelJustWon < 4 || levelJustWon % InterstitialEveryLevels != 0) return;
            // TODO: AdMob tam ekran reklam
        }

        private IEnumerator Simulate(Action<bool> done)
        {
            IsShowing = true;
            float t = 0f;
            while (t < SimulatedLength)
            {
                t += Time.unscaledDeltaTime;
                ShowProgress = t / SimulatedLength;
                yield return null;
            }
            IsShowing = false;
            done(true);
        }
    }
}
