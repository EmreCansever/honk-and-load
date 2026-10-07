# Honk & Load!

Renkli kolileri doğru kamyona yükleyip sıkışan depoyu boşalttığın, tek dokunuşla oynanan hybrid-casual bir bulmaca oyunu (Sort + Jam).

- **Platform:** Önce Android (Google Play), sonra iOS
- **Motor:** Unity (C#)
- **Durum:** Aşama 1 – Prototip (Hafta 1–3)
- **Tasarım dokümanı:** [docs/GDD.md](docs/GDD.md)

## Nasıl oynanır

1. Depoda sütunlar halinde koliler var; bir sütuna dokununca **en öndeki koli** alınır.
2. Koli, rengi eşleşen ve yeri olan bir **rampadaki kamyona** gider. Uygun kamyon yoksa **bekleme rafına** gider.
3. Kamyon dolunca düdük çalıp gider ve sıradaki kamyon gelir.
4. Raftaki koliler, rengindeki kamyon gelince **kendiliğinden** biner.
5. **Kazanma:** Tüm koliler yüklendi. **Kaybetme:** Raf **tamamen dolduğu anda** oyun biter. Uygun kamyonu olmayan her koli bir slot yakar; dikkatli oynamak gerekir.
6. Raf 5 slotla başlar, her 15 bölümde bir slot artar (en fazla 8).

## Modlar

- **Macera:** Bölüm bölüm ilerleme (yukarıdaki kurallar).
- **Sonsuz:** Bölüm yok. Sütunlardan koli alındıkça arkadan yenisi gelir; raf dolup hamle kalmayınca oyun biter. En yüksek skor ana menüde görünür.
  - Puan: depodan kamyona 10 (art arda yüklemelerde kombo çarpanı, en fazla x2), raftan kamyona 5, dolan kamyon +30, 3 koli art arda aynı kamyona **Mükemmel** +50.
  - Zorluk kademeleri: 300, 800, 1500, 2400, 3500, 4800, 6300, 8000 puan, sonra her 2000 puanda bir. Her kademede koliler daha karışık gelir, her iki kademede bir yeni renk eklenir (5 → 8 renk).
  - Adalet: her kamyon için tam 3 koli üretilir; koliler kamyonların geliş sırasına göre gelir (`EndlessDirector`).

## Projeyi açma

1. Unity Hub → **Add → Add project from disk** → bu klasörü seç.
2. Unity 6 (veya 2022.3 LTS) sürümüyle aç. İlk açılışta Unity `Library/`, `Packages/` ve `ProjectSettings/` klasörlerini kendisi oluşturur.
3. Boş bir sahnede **Play**'e bas. Oyun, `GameBootstrap` sayesinde sahneyi kendisi kurar; giriş ekranı açılır, **Oyna** ile kaldığın bölümden devam edersin.
4. Android için: **File → Build Settings → Android → Switch Platform**.

## Klasör yapısı

```
Assets/
  Resources/Levels/     Elle tasarlanmış bölümler (level_001.json …)
  Scripts/
    Core/               Başlatma, renk paleti, kayıt
    Level/              Bölüm verisi, yükleyici, otomatik bölüm üretici
    Gameplay/           Oyun kuralları (BoardState) ve 3D görünüm (BoardView)
    UI/                 Prototip arayüzü (HUD)
docs/
  GDD.md                Oyun tasarım dokümanı özeti
```

## Görseller

Tüm görseller kodla üretilir (`VisualFactory`): kolilerde renk körleri için sembol, kamyonlarda tekerlek ve kabin, depo zemini.

**Uygulama ikonu ve açılış ekranı:** `Assets/Art/AppIcon/`; Unity'de **Honk & Load → Uygulama İkonu ve Açılış Ekranı** menüsüyle uygulanır. Mağaza görselleri `Store/` klasöründe (bkz. `Store/README.md`).

**İsteğe bağlı hazır kamyon modeli:** `Assets/Resources/Models/` klasörüne `Truck` adlı bir model (.fbx veya prefab) koyarsan oyun onu kullanır: yönünü (kabin önde), boyutunu ve kasanın yerini modelden otomatik bulur, kamyonun rengine boyar. Modelin Inspector'ında **Read/Write** açık olmalı (Kenney `truck-flat` için açıldı).

## Bölüm dosyası formatı

```json
{
  "truckCapacity": 3,
  "bufferSize": 6,
  "trucks": [0, 1, 2, 0],
  "columns": [
    { "crates": [0, 1, 2] },
    { "crates": [2, 0, 1] }
  ]
}
```

- `trucks`: Kamyonların geliş sırası (renk numaraları). İlk 3 kamyon rampalara yanaşır.
- `columns[].crates`: Her sütundaki koliler; **son eleman en öndeki** (alınabilir) kolidir.
- Her renk için koli sayısı = o renkteki kamyon sayısı × `truckCapacity` olmalı.
- `Resources/Levels` içinde dosya yoksa bölüm otomatik üretilir. Şu an yalnızca 1. bölüm (öğretici) elle tasarlandı.
- Başlangıç zorluğu `LevelGenerator.StartOffset` ile ayarlanır (büyüdükçe ilk bölümler zorlaşır).
- Zorluk, "dikkatli oyuncu" simülasyonunun kazanma oranına göre seçilir (`LevelSolver.EstimateWinRate`). İlk 3–4 bölüm öğretici kolaylıkta; 5. bölümden itibaren dikkatsiz oynayan sık kaybeder.

## Yol haritası

- [ ] **Aşama 1 – Prototip:** çekirdek mekanik, 20–30 bölüm, iç test (Kapı A)
- [ ] **Aşama 2 – Pazar testi:** reklam videoları, CPI ve D1 ölçümü (Kapı B)
- [ ] **Aşama 3 – Tam geliştirme:** meta sistem, 300 bölüm, reklam + IAP
- [ ] **Aşama 4 – Soft launch:** 2–3 test ülkesi, D7 ve ROAS
- [ ] **Aşama 5 – Global lansman**
