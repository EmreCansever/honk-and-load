using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>
    /// Gerçek parayla satılan paketler (Google Play Faturalandırma).
    ///
    /// Şimdilik ödeme sistemi bağlı değil: Editor'da ve geliştirme sürümünde satın alma
    /// simüle edilir (ücretsiz verilir) ki arayüz test edilebilsin. Yayın sürümünde
    /// "Mağaza henüz hazır değil" döner. Play Console'da ürünler tanımlanınca
    /// Purchase() içi Unity IAP ile değiştirilecek; ürün kimlikleri aynı kalmalı.
    /// </summary>
    public static class Store
    {
        public class Product
        {
            public string Id;            // Play Console ürün kimliği
            public string TitleTr;
            public string SubtitleTr;
            public string Title => Loc.T(TitleTr);
            public string Subtitle => Loc.T(SubtitleTr);
            public string PlaceholderPrice; // gerçek fiyat mağazadan gelecek
            public bool OneTime;         // bir kez alınabilir (reklamsız, başlangıç paketi)
            public int Coins;
            public bool RemovesAds;
            public int BoostersEach;     // her güçlendiriciden adet
            public string BadgeTr;       // "EN POPÜLER" vb.
            public string Badge => Loc.T(BadgeTr);
        }

        public static readonly List<Product> Products = new List<Product>
        {
            new Product { Id = "starter_pack", TitleTr = "Başlangıç Paketi", SubtitleTr = "Reklamsız + 1.000 altın + her güçlendiriciden 3",
                PlaceholderPrice = "₺149,99", OneTime = true, Coins = 1000, RemovesAds = true, BoostersEach = 3, BadgeTr = "%60 AVANTAJ" },
            new Product { Id = "no_ads", TitleTr = "Reklamsız", SubtitleTr = "Bölüm arası reklamlar kalkar",
                PlaceholderPrice = "₺99,99", OneTime = true, RemovesAds = true },
            new Product { Id = "coins_1000", TitleTr = "1.000", SubtitleTr = "altın", PlaceholderPrice = "₺49,99", Coins = 1000 },
            new Product { Id = "coins_3000", TitleTr = "3.000", SubtitleTr = "altın", PlaceholderPrice = "₺119,99", Coins = 3000, BadgeTr = "EN POPÜLER" },
            new Product { Id = "coins_7500", TitleTr = "7.500", SubtitleTr = "altın", PlaceholderPrice = "₺249,99", Coins = 7500, BadgeTr = "EN İYİ FİYAT" },
        };

        public static Product Find(string id) => Products.Find(p => p.Id == id);

        public static bool IsOwned(Product p) => p.OneTime && Progress.GetInt("owned_" + p.Id) == 1;

        /// <summary>Ödeme sistemi bağlı mı? (Bağlanana kadar yalnızca test için simülasyon.)</summary>
        public static bool IsSimulated => Application.isEditor || Debug.isDebugBuild;

        public static bool IsAvailable => IsSimulated; // Unity IAP bağlanınca: mağaza başlatıldı mı

        public static string PriceText(Product p) => p.PlaceholderPrice;

        /// <summary>Satın alır; sonuç (başarılı mı, mesaj) geri çağrıyla döner.</summary>
        public static void Purchase(Product p, Action<bool, string> done)
        {
            if (p == null) { done(false, Loc.T("Ürün bulunamadı")); return; }
            if (IsOwned(p)) { done(false, Loc.T("Zaten sahipsin")); return; }
            if (!IsAvailable) { done(false, Loc.T("Mağaza henüz hazır değil")); return; }

            // TODO: Unity IAP → Google Play. Şimdilik test simülasyonu.
            Grant(p);
            done(true, Loc.T(IsSimulated ? "Test satın alımı (ücret alınmadı)" : "Satın alındı"));
        }

        /// <summary>Satın alımları geri yükle (telefon değişince). IAP bağlanınca doldurulacak.</summary>
        public static void Restore(Action<string> done)
        {
            done(Loc.T(IsAvailable ? "Geri yüklenecek satın alım yok" : "Mağaza henüz hazır değil"));
        }

        private static void Grant(Product p)
        {
            if (p.OneTime) Progress.SetInt("owned_" + p.Id, 1);
            if (p.RemovesAds) Progress.NoAds = true;
            Economy.AddCoins(p.Coins);
            if (p.BoostersEach > 0)
                foreach (Economy.BoosterInfo b in Economy.Boosters)
                    Economy.AddBooster(b.Type, p.BoostersEach);
            // Başlangıç paketi alındıysa reklamsız da sahiplenilmiş sayılır
            if (p.RemovesAds) Progress.SetInt("owned_no_ads", 1);
        }
    }
}
