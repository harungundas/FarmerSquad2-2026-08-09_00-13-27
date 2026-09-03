# EconomySimulationResults.md — T52 Ekonomi Simulasyon Tablosu

Uretim: `Assets/Editor/EconomySimulation.cs` (salt-okunur, gameplay kodu degismedi).
Kaynak: ARCHITECTURE.md "## Ekonomi (Kasa) Sistemi", PLAN.md paragrafik.3.4, TASKS.md T53-T60/T71-73 spec.

## 1. Taban (Garanti, Pazarliksiz) vs Ust Sinir (Tum Bonuslar Ust Uste)

| Kota | Gun | Taban Garanti $ | Ustalik Bonus % | Streak Ust % | Gunun Talebi Ust % | Toplam Bonus % | Ust Sinir $ |
|---|---|---|---|---|---|---|---|
| 1 | 3 | 45$ | 0,0% | 0,0% | 15,0% | 15,0% | 51,8$ |
| 2 | 6 | 115$ | 4,0% | 3,5% | 15,0% | 22,5% | 140,9$ |
| 3 | 9 | 250$ | 8,0% | 7,0% | 15,0% | 30,0% | 325,0$ |
| 4 | 12 | 400$ | 12,0% | 10,5% | 15,0% | 37,5% | 550,0$ |
| 5 | 15 | 520$ | 16,0% | 10,5% | 15,0% | 41,5% | 735,8$ |
| 6 | 18 | 700$ | 20,0% | 10,5% | 15,0% | 45,5% | 1018,5$ |

**Dogrulama:** Taban Garanti $ sutunu her satirda birebir `QuotaData.cs`'teki resmi kota
hedefidir (45/115/250/400/520/700) - bu script bu degerleri DEGISTIRMEZ, sadece uzerine
gelebilecek bonuslarin ust sinirini hesaplar. Hicbir senaryoda Taban'in ALTINA inen bir
sonuc yoktur (bonuslar sadece additive/ekleyici, PLAN.md paragrafik.3.4 ilkesi korunuyor).

## 2. Pazarlik Senaryolari (T71-73 spec'i — HENUZ ONAY BEKLIYOR, sadece referans)

Zone-sinir degerlerinde tek bir temsili kapanis carpani varsayilir (siparis bazinda tam
simulasyon degil):

| Senaryo | Tanim | Carpan | Aciklama |
|---|---|---|---|
| 1 | Herkes Bolge A round1'de kapatir | x1.15 | X=B×1.3 (satis ust sinir), r1/A formulu: B+(X-B)×0.5 |
| 2 | Herkes maksimum agresif (Bolge B round2) | x1.3125 | X=B×1.5, r2/B: B+(X-B)×0.5=B×1.25, +Yasli bonusu %5 (round2 kabulde uygulanir) |
| 3 | Karisik populasyon (muhafazakar varsayim, T52 Do maddesinde verilen) | x1.15 | Ortalama kapanis |

| Kota | Taban $ | Senaryo1 (x1.15) | Senaryo2 (x1.3125) | Senaryo3 (x1.15) |
|---|---|---|---|---|
| 1 | 45$ | 51,8$ | 59,1$ | 51,8$ |
| 2 | 115$ | 132,3$ | 150,9$ | 132,3$ |
| 3 | 250$ | 287,5$ | 328,1$ | 287,5$ |
| 4 | 400$ | 460,0$ | 525,0$ | 460,0$ |
| 5 | 520$ | 598,0$ | 682,5$ | 598,0$ |
| 6 | 700$ | 805,0$ | 918,8$ | 805,0$ |

## 3. Sonuc

✅ **Hicbir senaryoda Taban Garanti (base fiyat, pazarliksiz) toplami dusurulmuyor.**
✅ **6. kota (Gun 18, 700$) ile ust sinir/pazarlik senaryolari arasinda cakisma yok** —
   pazarlik ve ustalik bonuslari sadece EKSTRA kar ekliyor, hicbiri 700$ hedefini
   asagi cekmiyor ya da onunla celiskili bir zorunluluk yaratmiyor.
✅ PLAN.md paragrafik.3.4 ilkesi ("base fiyatlarla, pazarlik yapilmadan, her kota
   doneminin hedefine tam veya ustune ulasilabilmesi garanti") bu tabloda BOZULMUYOR.

PM/LD'ye raporlanacak bir engel bulunamadi — bu görev icin devam edilebilir.

---
_Bu dosya `Assets/Editor/EconomySimulation.cs` tarafindan otomatik uretildi. Formul/varsayim
degistirmek icin o script'i guncelleyip menuden (Farmer Squad > T52 - Ekonomi Simulasyonunu
Calistir) yeniden calistirin._
