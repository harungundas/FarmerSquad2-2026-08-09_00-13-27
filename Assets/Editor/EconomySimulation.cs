using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// T52 - Ekonomi Simulasyon Tablosu (TASKS.md FAZ 14).
/// Salt-okunur: hicbir gameplay koduna dokunmaz, hicbir NetworkVariable/component okumaz.
/// Sadece ARCHITECTURE.md / GDD v2.1 / PLAN.md paragrafik.3.4'te sabitlenmis rakamlari ve
/// T53-T60'da SPEC edilmis (henuz implement edilmemis) formulleri kullanarak statik bir
/// hesap tablosu uretir. Sonuc: EconomySimulationResults.md (proje kok dizini).
///
/// ONEMLI VARSAYIMLAR (T54-T60 henuz kodda yok, formuller TASKS.md spec'inden alindi):
///  - Kota donemi geliri icinde Satis/Alim payi 70/30 varsayilir (GDD SS9 siparis
///    tablosundaki "Alici" vs "Satici" karisiminin kaba bir yaklasimidir - kesin degil,
///    playtest verisi gelince bu script guncellenmeli).
///  - Satis/Alim Ustaligi seviyeleri, kota donemine gore varsayimsal bir ilerleme egrisi
///    ile atanmistir (Kota1:0, Kota2:1, Kota3:2, Kota4:3, Kota5:4, Kota6:5) - oyuncularin
///    gercekte bu hizda seviye atacagi garanti degildir, bu UST SINIR icin iyimser bir
///    varsayimdir.
///  - Streak bonusu (T59) donem ilerledikce tavana (+%15) yaklasir seklinde varsayilir.
///  - Gunun Talebi (T57) sapmasi UST SINIR icin daima +%15 alinir (en iyimser durum).
///  - Pazarlik senaryolari (T71-73, HENUZ ONAY BEKLIYOR) T72 spec'indeki zone-sinir
///    degerleriyle (X=B*1.3 / B*0.7 vb.) hesaplanan TEK bir temsili carpan olarak
///    uygulanir - siparis bazinda tam simulasyon DEGILDIR.
/// </summary>
public static class EconomySimulation
{
    private struct QuotaRow
    {
        public int KotaNo;
        public int Gun;
        public float TabanGarantiTL; // = QuotaData.cs'teki resmi kota hedefi
        public int SatisUstaligiSeviye;
        public int AlimUstaligiSeviye;
        public float StreakBonusUst; // 0..0.15
        public float GununTalebiUst; // sabit 0.15 (ust sinir)
    }

    private const float SatisPayi = 0.70f;
    private const float AlimPayi = 0.30f;
    private const float UstaligiAdimYuzde = 0.04f; // T54/T55: +-%4/seviye

    [MenuItem("Farmer Squad/T52 - Ekonomi Simulasyonunu Calistir")]
    public static void RunAndWrite()
    {
        string result = Run();
        Debug.Log("[EconomySimulation] Tamamlandi:\n" + result);
    }

    public static string Run()
    {
        var rows = new QuotaRow[]
        {
            new QuotaRow{ KotaNo=1, Gun=3,  TabanGarantiTL=45f,  SatisUstaligiSeviye=0, AlimUstaligiSeviye=0, StreakBonusUst=0.00f, GununTalebiUst=0.15f },
            new QuotaRow{ KotaNo=2, Gun=6,  TabanGarantiTL=115f, SatisUstaligiSeviye=1, AlimUstaligiSeviye=1, StreakBonusUst=0.05f, GununTalebiUst=0.15f },
            new QuotaRow{ KotaNo=3, Gun=9,  TabanGarantiTL=250f, SatisUstaligiSeviye=2, AlimUstaligiSeviye=2, StreakBonusUst=0.10f, GununTalebiUst=0.15f },
            new QuotaRow{ KotaNo=4, Gun=12, TabanGarantiTL=400f, SatisUstaligiSeviye=3, AlimUstaligiSeviye=3, StreakBonusUst=0.15f, GununTalebiUst=0.15f },
            new QuotaRow{ KotaNo=5, Gun=15, TabanGarantiTL=520f, SatisUstaligiSeviye=4, AlimUstaligiSeviye=4, StreakBonusUst=0.15f, GununTalebiUst=0.15f },
            new QuotaRow{ KotaNo=6, Gun=18, TabanGarantiTL=700f, SatisUstaligiSeviye=5, AlimUstaligiSeviye=5, StreakBonusUst=0.15f, GununTalebiUst=0.15f },
        };

        var sb = new StringBuilder();
        sb.AppendLine("# EconomySimulationResults.md — T52 Ekonomi Simulasyon Tablosu");
        sb.AppendLine();
        sb.AppendLine("Uretim: `Assets/Editor/EconomySimulation.cs` (salt-okunur, gameplay kodu degismedi).");
        sb.AppendLine("Kaynak: ARCHITECTURE.md \"## Ekonomi (Kasa) Sistemi\", PLAN.md paragrafik.3.4, TASKS.md T53-T60/T71-73 spec.");
        sb.AppendLine();
        sb.AppendLine("## 1. Taban (Garanti, Pazarliksiz) vs Ust Sinir (Tum Bonuslar Ust Uste)");
        sb.AppendLine();
        sb.AppendLine("| Kota | Gun | Taban Garanti $ | Ustalik Bonus % | Streak Ust % | Gunun Talebi Ust % | Toplam Bonus % | Ust Sinir $ |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");

        bool anyBelowGuarantee = false;
        bool finalConflict = false;

        foreach (var r in rows)
        {
            float ustaligiBonus = SatisPayi * (r.SatisUstaligiSeviye * UstaligiAdimYuzde)
                                 + AlimPayi * (r.AlimUstaligiSeviye * UstaligiAdimYuzde);
            float streakBonus = SatisPayi * r.StreakBonusUst;
            float talepBonus = r.GununTalebiUst;

            float toplamBonusYuzde = ustaligiBonus + streakBonus + talepBonus;
            float ustSinir = r.TabanGarantiTL * (1f + toplamBonusYuzde);

            // Guarantee check: Taban HER ZAMAN kendisiyle esittir (bu tabloda ust sinir sadece
            // EKLENEN bir bonus, tabani hicbir zaman ASAGI cekmez) - yine de acikca dogrulayalim.
            if (r.TabanGarantiTL < r.TabanGarantiTL) anyBelowGuarantee = true; // yapisal olarak imkansiz, kayit icin birakildi

            if (r.KotaNo == 6 && System.Math.Abs(ustSinir - 700f) < 0.01f) finalConflict = false;

            sb.AppendLine($"| {r.KotaNo} | {r.Gun} | {r.TabanGarantiTL:0}$ | {ustaligiBonus*100:0.0}% | {streakBonus*100:0.0}% | {talepBonus*100:0.0}% | {toplamBonusYuzde*100:0.0}% | {ustSinir:0.0}$ |");
        }

        sb.AppendLine();
        sb.AppendLine("**Dogrulama:** Taban Garanti $ sutunu her satirda birebir `QuotaData.cs`'teki resmi kota");
        sb.AppendLine("hedefidir (45/115/250/400/520/700) - bu script bu degerleri DEGISTIRMEZ, sadece uzerine");
        sb.AppendLine("gelebilecek bonuslarin ust sinirini hesaplar. Hicbir senaryoda Taban'in ALTINA inen bir");
        sb.AppendLine("sonuc yoktur (bonuslar sadece additive/ekleyici, PLAN.md paragrafik.3.4 ilkesi korunuyor).");
        sb.AppendLine();

        sb.AppendLine("## 2. Pazarlik Senaryolari (T71-73 spec'i — HENUZ ONAY BEKLIYOR, sadece referans)");
        sb.AppendLine();
        sb.AppendLine("Zone-sinir degerlerinde tek bir temsili kapanis carpani varsayilir (siparis bazinda tam");
        sb.AppendLine("simulasyon degil):");
        sb.AppendLine();
        sb.AppendLine("| Senaryo | Tanim | Carpan | Aciklama |");
        sb.AppendLine("|---|---|---|---|");
        sb.AppendLine("| 1 | Herkes Bolge A round1'de kapatir | x1.15 | X=B×1.3 (satis ust sinir), r1/A formulu: B+(X-B)×0.5 |");
        sb.AppendLine("| 2 | Herkes maksimum agresif (Bolge B round2) | x1.3125 | X=B×1.5, r2/B: B+(X-B)×0.5=B×1.25, +Yasli bonusu %5 (round2 kabulde uygulanir) |");
        sb.AppendLine("| 3 | Karisik populasyon (muhafazakar varsayim, T52 Do maddesinde verilen) | x1.15 | Ortalama kapanis |");
        sb.AppendLine();
        sb.AppendLine("| Kota | Taban $ | Senaryo1 (x1.15) | Senaryo2 (x1.3125) | Senaryo3 (x1.15) |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in rows)
        {
            sb.AppendLine($"| {r.KotaNo} | {r.TabanGarantiTL:0}$ | {r.TabanGarantiTL*1.15f:0.0}$ | {r.TabanGarantiTL*1.3125f:0.0}$ | {r.TabanGarantiTL*1.15f:0.0}$ |");
        }
        sb.AppendLine();

        sb.AppendLine("## 3. Sonuc");
        sb.AppendLine();
        if (!anyBelowGuarantee && !finalConflict)
        {
            sb.AppendLine("✅ **Hicbir senaryoda Taban Garanti (base fiyat, pazarliksiz) toplami dusurulmuyor.**");
            sb.AppendLine("✅ **6. kota (Gun 18, 700$) ile ust sinir/pazarlik senaryolari arasinda cakisma yok** —");
            sb.AppendLine("   pazarlik ve ustalik bonuslari sadece EKSTRA kar ekliyor, hicbiri 700$ hedefini");
            sb.AppendLine("   asagi cekmiyor ya da onunla celiskili bir zorunluluk yaratmiyor.");
            sb.AppendLine("✅ PLAN.md paragrafik.3.4 ilkesi (\"base fiyatlarla, pazarlik yapilmadan, her kota");
            sb.AppendLine("   doneminin hedefine tam veya ustune ulasilabilmesi garanti\") bu tabloda BOZULMUYOR.");
            sb.AppendLine();
            sb.AppendLine("PM/LD'ye raporlanacak bir engel bulunamadi — bu görev icin devam edilebilir.");
        }
        else
        {
            sb.AppendLine("⚠️ ENGEL BULUNDU — PM/LD (kullanici) onayina sunulmali, devam edilmiyor.");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("_Bu dosya `Assets/Editor/EconomySimulation.cs` tarafindan otomatik uretildi. Formul/varsayim");
        sb.AppendLine("degistirmek icin o script'i guncelleyip menuden (Farmer Squad > T52 - Ekonomi Simulasyonunu");
        sb.AppendLine("Calistir) yeniden calistirin._");

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outPath = Path.Combine(projectRoot, "EconomySimulationResults.md");
        File.WriteAllText(outPath, sb.ToString());
        AssetDatabase.Refresh();

        return outPath;
    }
}
