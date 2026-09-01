# EconomySimulationResults.md — T52 Ekonomi Simulasyon Tablosu

Otomatik uretildi: `Assets/Scripts/Editor/EconomySimulationTool.cs` (Unity menu: Farmer Squad > T52 - Ekonomi Simulasyonu Calistir).

**Kapsam:** Salt-okunur analiz. Gameplay koduna dokunulmadi. T54/T55/T57/T59/T60 henuz implement EDILMEDI — bu belgedeki bonuslar TASKS.md'deki spec'lerden turetilen projeksiyonlardir, gercek oyun degerleri degil. T71-73 (pazarlik yeniden tasarimi) **onay bekliyor**, bu belgedeki pazarlik senaryolari da ayni sekilde projeksiyondur.

## 0. Okunan Gercek Degerler (canli AnimalData asset'lerinden, varsayim degil)

| Hayvan | Alis | Satis | Agirlik |
|---|---|---|---|
| Chicken | 5$ | 10$ | Light |
| Cow | 20$ | 55$ | Heavy |
| Goat | 12$ | 25$ | Light |
| Horse | 20$ | 55$ | Heavy |
| Sheep | 12$ | 25$ | Light |

> **BULUNAN UYUMSUZLUK (bu calistirmada tespit edildi, T52 kapsaminda DUZELTILMEDI):** Kece (Goat) asset'i Alis=12$ / Satis=25$ — Koyun (Sheep) ile BIREBIR AYNI. GDD v2.1 SS12 Kece icin 10$/20$ yaziyor (Tavuk 5$/10$ ile Koyun 12$/25$ arasina konumlandirilmis, dokumanda acikca boyle tanimli). Asset muhtemelen T04'te Sheep'ten kopyalanip degeri guncellenmemis. PM/LD karari/duzeltmesi gerekiyor.

## 1. Taban (Garanti, Pazarliksiz) — QuotaData'dan Okunan Donem Hedefleri

PLAN.md SS3.4 ilkesi: base fiyatlarla (pazarlik yapilmadan) her donemin hedefine ulasmak GARANTI olmali. GDD SS9'daki gun-gun siparis tablosu 'gosterge niteliginde, henuz kesinlesmemis' oldugu icin bu simulasyon asagidan-yukari (gun gun siparis toplama) DEGIL, QuotaData'nin resmi hedefinden kuruludur — yani 'Taban' burada, tasarimin kendi garanti cizgisidir (ARCHITECTURE.md SS0: '+%15 tampon, taban fiyatla' notuyla tutarli).

| Kota No | Gun | Taban (Hedef, Normal Zorluk) | Taban x1.15 (tasarim tamponu) |
|---|---|---|---|
| 1 | 3 | 45$ | 51,8$ |
| 2 | 6 | 115$ | 132,3$ |
| 3 | 9 | 250$ | 287,5$ |
| 4 | 12 | 400$ | 460$ |
| 5 | 15 | 520$ | 598$ |
| 6 | 18 | 700$ | 805$ |

## 2. Sistem Ustuste Binmesi (T54/T55/T57/T59 — henuz implement EDILMEDI, projeksiyon)

Temsili degerler (T53 leveled-upgrade sisteminde gercek seviye tavani YOK, ornek olarak seviye 3 secildi):
- **Satis Ustaligi seviye 3** -> +%12 (T54: +%4/seviye additive)
- **Gunun Talebi** -> +%15 (T57 tavani)
- **Ardisik Teslimat (Streak)** -> +%15 (T59 tavani, 9+ ardisik hatasiz islem)
- Ucu carpimsal ustuste biner: carpan = 1,12 x 1,15 x 1,15 = **1,481**

| Kota No | Gun | Taban (Garanti) | Ust Sinir (sistem ustuste, pazarliksiz) |
|---|---|---|---|
| 1 | 3 | 45$ | 66,7$ |
| 2 | 6 | 115$ | 170,3$ |
| 3 | 9 | 250$ | 370,3$ |
| 4 | 12 | 400$ | 592,5$ |
| 5 | 15 | 520$ | 770,2$ |
| 6 | 18 | 700$ | 1036,8$ |

## 3. Zorluk Ayarlari Ekseni (T60 — henuz implement EDILMEDI)

**Onemli:** Zorluk carpani GELIRE degil, HEDEFE (kota tutarina) uygulanir (T60 Do maddesi). Yani bu, gelir-tarafi bonuslarindan (Bolum 2) BAGIMSIZ bir eksendir. PLAN.md SS3.4'teki 'base fiyatla garanti' ilkesi, tasarimin ORIJINAL halinde (Zorluk sistemi eklenmeden once, yani fiili Normal x1.0 icin) yazildi — Zor modda (x1.3) hedefin yukselmesi KASITLI bir zorluk artisi olabilir, bu bir tutarsizlik degil. **Acik soru (PM/LD onayi gerekiyor, bu belge sadece isaretliyor):** Zor modda da 'base fiyatla garanti gecilebilir' ilkesi korunmali mi, yoksa Zor kasitli olarak bu garantiyi kirmali mi? T60 implement edilirken netlestirilmeli.

| Kota No | Gun | Kolay (x0.8) Hedef | Normal (x1.0) Hedef | Zor (x1.3) Hedef |
|---|---|---|---|---|
| 1 | 3 | 36$ | 45$ | 58,5$ |
| 2 | 6 | 92$ | 115$ | 149,5$ |
| 3 | 9 | 200$ | 250$ | 325$ |
| 4 | 12 | 320$ | 400$ | 520$ |
| 5 | 15 | 416$ | 520$ | 676$ |
| 6 | 18 | 560$ | 700$ | 910$ |

## 4. Pazarlik Senaryolari (T71-73 spec'i — ONAY BEKLIYOR, implement EDILMEDI)

Asagidaki carpanlar, T71-73'un meet-in-the-middle formullerinden turetilen TEMSILI kapanis degerleridir (gercek playtest degil). Pazarlik SISTEM ustuste binmelerinden (Bolum 2) BAGIMSIZ, ayrica, opsiyonel bir ekstra-kar aracidir (PLAN.md SS3.4: kota icin ZORUNLU degil).

| Senaryo | Aciklama | Satis Carpani | Alim Carpani |
|---|---|---|---|
| 1 | Herkes Bolge A / round1'de guvenli kapatir | x1,15 | x0,85 |
| 2 | Herkes maksimum agresif (Bolge B / round2) oynar | x1,25 | x0,75 |
| 3 | Karisik populasyon, muhafazakar ortalama | x1,15 | x0,85 |

| Kota No | Gun | Taban | Senaryo1 Ust Sinir (satis yonu) | Senaryo2 Ust Sinir | Senaryo3 Ust Sinir |
|---|---|---|---|---|---|
| 1 | 3 | 45$ | 51,8$ | 56,3$ | 51,8$ |
| 2 | 6 | 115$ | 132,3$ | 143,8$ | 132,3$ |
| 3 | 9 | 250$ | 287,5$ | 312,5$ | 287,5$ |
| 4 | 12 | 400$ | 460$ | 500$ | 460$ |
| 5 | 15 | 520$ | 598$ | 650$ | 598$ |
| 6 | 18 | 700$ | 805$ | 875$ | 805$ |

## 5. Dogrulama Sonucu

- **Taban hicbir donemde hedefi kacirmiyor mu?** EVET — tanim geregi Taban = QuotaData'nin kendi requiredAmount degeri (Bolum 1), yani PLAN.md SS3.4 ilkesi TANIM OLARAK saglaniyor. Bu, siparis-bazli asagidan-yukari bir dogrulama DEGIL (GDD SS9 gun-gun tablosu gosterge niteliginde oldugu icin bu simulasyonun kapsaminda degil) — kesin sayisal dogrulama, o tablo kesinlestiginde ayri bir T-gorevi olarak yapilmali.
- **En kotu-durum ust sinir (Kota 1, tum sistemler + en agresif pazarlik) 700$ final hedefiyle cakisiyor mu?** HAYIR — Kota 1 icin hesaplanan mutlak tavan 83,3$, final hedef 700$'in cok altinda. Curve inversion riski yok.
- **Acik/kullaniciya sorulmasi gereken maddeler:** (1) Kece asset fiyat uyumsuzlugu (Bolum 0), (2) Zor zorlukta 'base fiyatla garanti' ilkesinin korunup korunmayacagi (Bolum 3).

---

**Not:** Bu belge T52'nin bir seferlik ciktisidir. Formuller (T54/55/57/59/60/71-73) gercekten implement edildikten sonra, `Farmer Squad > T52 - Ekonomi Simulasyonu Calistir` menusu tekrar calistirilip bu dosya guncellenerek gercek koddan turetilmis (varsayimsiz) bir versiyon uretilebilir.
