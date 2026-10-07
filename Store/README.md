# Mağaza ve uygulama görselleri

| Dosya | Nerede kullanılır |
|---|---|
| `icon_store_512.png` | Google Play Console → Mağaza girişi → Uygulama simgesi (512×512) |
| `feature_1024x500.png` | Google Play Console → Mağaza girişi → Öne çıkan grafik (1024×500) |
| `../Assets/Art/AppIcon/*` | Unity: **Honk & Load → Uygulama İkonu ve Açılış Ekranı** menüsü bunları Player Settings'e uygular |

- Android uyarlanabilir ikon: `adaptive_bg_432.png` (arka plan) + `adaptive_fg_432.png` (ön plan, şeffaf).
- Açılış ekranı: turuncu zemin (#FFA83A) üzerinde `splash_logo.png`, Unity logosu kapalı.
- Görseller kodla (SVG) üretildi: `assets.py` + `lib.py` (Python + Playwright/Chromium). Renk veya şekil değişikliği için bu dosyaları düzenleyip yeniden çalıştır.
- Yazı tipi: Poppins (SIL Open Font License).
