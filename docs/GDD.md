# Honk & Load! – Tasarım Özeti

Güncel ve ayrıntılı tasarım dokümanı: https://claude.ai/code/artifact/989441fa-f3b9-466c-9bd6-6d7eb403327e

## Konsept
Renkli kolileri doğru kamyona yükleyip sıkışan depoyu boşaltma. Sort (sıralama) türünün kolay öğrenilmesi + Jam türünün "trafiği aç" tatmini.

## Çekirdek oynanış
- Ekran (yukarıdan aşağı): kamyon rampaları (3) → bekleme rafı (6 slot) → depo ızgarası.
- Dokun → koli, rengi eşleşen rampadaki kamyona gider; eşleşme yoksa rafa gider.
- Kamyon dolunca (3 koli) gider, sıradaki gelir. Raftaki koliler otomatik binmez; oyuncu dokunur.
- Kazanma: tüm koliler yüklendi. Kaybetme: raf dolu ve hamle yok → "+3 slot (reklam)" veya "tekrar dene".
- Booster'lar (sonra): geri al, karıştır, ekstra slot, mıknatıs.

## Zorluk eğrisi
| Bölüm | Yeni öğe |
| --- | --- |
| 1–5 | Temel dokunma, 3 renk |
| 6–20 | Gizli koli |
| 21–40 | Zincirli koli |
| 41–60 | Büyük / küçük kamyon |
| 61–90 | Buzlu koli |
| 90+ | Karışık engeller, acil teslimat |

## Para kazanma
Ödüllü video (+3 slot), bölüm 10'dan sonra geçiş reklamı (en fazla 2 bölümde 1), reklamsız paket, booster paketleri, sonra sezon kartı.

## Teknik
Unity, Firebase Analytics + GameAnalytics, Firebase Remote Config, AppLovin MAX / AdMob, Adjust / AppsFlyer.
Hedef: 2–3 GB RAM'li Android'de 60 FPS, ilk indirme < 100 MB.

## Karar kapıları
- **A (Hafta 3):** 10 kişi zorlanmadan 10+ bölüm oynuyor mu?
- **B (Hafta 6):** CPI < 0,40 $ ve D1 ≥ %35
- **C (Hafta 14):** Yayıncı mı, kendi yayınımız mı?
- **D (Hafta 20):** D7 ≥ %12 ve ROAS hedefi
