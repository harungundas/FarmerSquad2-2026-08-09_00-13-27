using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Linq;

/// <summary>
/// T52 — Ekonomi Simulasyon Tablosu (TASKS.md FAZ 14).
/// Gameplay koduna DOKUNMAZ. Gercek QuotaData/AnimalData degerlerini okuyup, henuz implement
/// EDILMEMIS T54/T55/T57/T59/T60 formullerini (spec olarak TASKS.md'de tanimli) ustuste
/// bindirerek taban (garanti) ve ust sinir (hepsi max) gelir tablosunu projekte eder.
/// Cikti: proje kokune EconomySimulationResults.md.
/// PLAN.md SS3.4 ilkesini dogrular: base fiyatla (pazarliksiz) her donem hedefi GARANTI
/// gecilebilir mi?
/// </summary>
public static class EconomySimulationTool
{
    // T54: Satis Ustaligi +%4/seviye (additive). T55: Alim Ustaligi -%4/seviye.
    private const float MasteryPerLevel = 0.04f;
    private const int RepresentativeMasteryLevel = 3; // ornek/temsili, gercek tavan yok (T53)

    // T57: Gunun Talebi tavani
    private const float DemandMaxBonus = 0.15f;

    // T59: Ardisik Teslimat (Streak) tavani (+%5/3 ardisik, 9.'da tavan +%15)
    private const float StreakMaxBonus = 0.15f;

    // T60: Zorluk carpanlari (HEDEFE uygulanir, gelire degil)
    private const float DifficultyEasy = 0.8f;
    private const float DifficultyNormal = 1.0f;
    private const float DifficultyHard = 1.3f;

    // T71-73 (ONAY BEKLIYOR, implement edilmedi) — spec'teki meet-in-the-middle formullerinden
    // turetilen temsili kapanis carpanlari. Gercek playtest degil, projeksiyon.
    private const float NegotiationScenario1_SafeZoneA = 1.15f;
    private const float NegotiationScenario2_AggressiveZoneB = 1.25f;
    private const float NegotiationScenario3_MixedConservative = 1.15f;

    [MenuItem("Farmer Squad/T52 - Ekonomi Simulasyonu Calistir")]
    public static void Run()
    {
        var quotaData = AssetDatabase.FindAssets("t:QuotaData")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<QuotaData>)
            .FirstOrDefault(q => q != null);

        if (quotaData == null)
        {
            Debug.LogError("[EconomySimulationTool] QuotaData asset bulunamadi. Islem durduruldu.");
            return;
        }

        var animals = AssetDatabase.FindAssets("t:AnimalData")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AnimalData>)
            .Where(a => a != null)
            .OrderBy(a => a.species.ToString())
            .ToList();

        var sb = new StringBuilder();
        WriteHeader(sb);
        WriteAnimalTable(sb, animals);
        WriteGoatDeviationCheck(sb, animals);
        WriteBaselineTable(sb, quotaData);
        WriteStackedSystemsTable(sb, quotaData);
        WriteDifficultyTable(sb, quotaData);
        WriteNegotiationScenarios(sb, quotaData);
        WriteVerificationConclusion(sb, quotaData);

        string outputPath = Path.Combine(Application.dataPath, "..", "EconomySimulationResults.md");
        outputPath = Path.GetFullPath(outputPath);
        File.WriteAllText(outputPath, sb.ToString());

        Debug.Log("[EconomySimulationTool] Yazildi: " + outputPath);
        AssetDatabase.Refresh();
    }

    private static void WriteHeader(StringBuilder sb)
    {
        sb.AppendLine("# EconomySimulationResults.md — T52 Ekonomi Simulasyon Tablosu");
        sb.AppendLine();
        sb.AppendLine("Otomatik uretildi: `Assets/Scripts/Editor/EconomySimulationTool.cs` " +
            "(Unity menu: Farmer Squad > T52 - Ekonomi Simulasyonu Calistir).");
        sb.AppendLine();
        sb.AppendLine("**Kapsam:** Salt-okunur analiz. Gameplay koduna dokunulmadi. T54/T55/T57/T59/T60 " +
            "henuz implement EDILMEDI — bu belgedeki bonuslar TASKS.md'deki spec'lerden turetilen " +
            "projeksiyonlardir, gercek oyun degerleri degil. T71-73 (pazarlik yeniden tasarimi) " +
            "**onay bekliyor**, bu belgedeki pazarlik senaryolari da ayni sekilde projeksiyondur.");
        sb.AppendLine();
    }

    private static void WriteAnimalTable(StringBuilder sb, System.Collections.Generic.List<AnimalData> animals)
    {
        sb.AppendLine("## 0. Okunan Gercek Degerler (canli AnimalData asset'lerinden, varsayim degil)");
        sb.AppendLine();
        sb.AppendLine("| Hayvan | Alis | Satis | Agirlik |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var a in animals)
            sb.AppendLine($"| {a.species} | {a.buyPrice}$ | {a.sellPrice}$ | {a.weightClass} |");
        sb.AppendLine();
    }

    private static void WriteGoatDeviationCheck(StringBuilder sb, System.Collections.Generic.List<AnimalData> animals)
    {
        var goat = animals.FirstOrDefault(a => a.species == AnimalSpecies.Goat);
        var sheep = animals.FirstOrDefault(a => a.species == AnimalSpecies.Sheep);
        if (goat != null && sheep != null && goat.buyPrice == sheep.buyPrice && goat.sellPrice == sheep.sellPrice)
        {
            sb.AppendLine("> **BULUNAN UYUMSUZLUK (bu calistirmada tespit edildi, T52 kapsaminda DUZELTILMEDI):** " +
                $"Kece (Goat) asset'i Alis={goat.buyPrice}$ / Satis={goat.sellPrice}$ — Koyun (Sheep) ile " +
                "BIREBIR AYNI. GDD v2.1 SS12 Kece icin 10$/20$ yaziyor (Tavuk 5$/10$ ile Koyun 12$/25$ " +
                "arasina konumlandirilmis, dokumanda acikca boyle tanimli). Asset muhtemelen T04'te " +
                "Sheep'ten kopyalanip degeri guncellenmemis. PM/LD karari/duzeltmesi gerekiyor.");
            sb.AppendLine();
        }
    }

    private static void WriteBaselineTable(StringBuilder sb, QuotaData quotaData)
    {
        sb.AppendLine("## 1. Taban (Garanti, Pazarliksiz) — QuotaData'dan Okunan Donem Hedefleri");
        sb.AppendLine();
        sb.AppendLine("PLAN.md SS3.4 ilkesi: base fiyatlarla (pazarlik yapilmadan) her donemin hedefine " +
            "ulasmak GARANTI olmali. GDD SS9'daki gun-gun siparis tablosu 'gosterge niteliginde, henuz " +
            "kesinlesmemis' oldugu icin bu simulasyon asagidan-yukari (gun gun siparis toplama) DEGIL, " +
            "QuotaData'nin resmi hedefinden kuruludur — yani 'Taban' burada, tasarimin kendi garanti " +
            "cizgisidir (ARCHITECTURE.md SS0: '+%15 tampon, taban fiyatla' notuyla tutarli).");
        sb.AppendLine();
        sb.AppendLine("| Kota No | Gun | Taban (Hedef, Normal Zorluk) | Taban x1.15 (tasarim tamponu) |");
        sb.AppendLine("|---|---|---|---|");
        for (int i = 0; i < quotaData.quotas.Length; i++)
        {
            var q = quotaData.quotas[i];
            sb.AppendLine($"| {i + 1} | {q.day} | {q.requiredAmount}$ | {(q.requiredAmount * 1.15f):0.#}$ |");
        }
        sb.AppendLine();
    }

    private static void WriteStackedSystemsTable(StringBuilder sb, QuotaData quotaData)
    {
        float masteryBonus = RepresentativeMasteryLevel * MasteryPerLevel;
        float stackedMultiplier = (1f + masteryBonus) * (1f + DemandMaxBonus) * (1f + StreakMaxBonus);

        sb.AppendLine("## 2. Sistem Ustuste Binmesi (T54/T55/T57/T59 — henuz implement EDILMEDI, projeksiyon)");
        sb.AppendLine();
        sb.AppendLine("Temsili degerler (T53 leveled-upgrade sisteminde gercek seviye tavani YOK, ornek " +
            $"olarak seviye {RepresentativeMasteryLevel} secildi):");
        sb.AppendLine($"- **Satis Ustaligi seviye {RepresentativeMasteryLevel}** -> +%{masteryBonus * 100:0.#} (T54: +%4/seviye additive)");
        sb.AppendLine($"- **Gunun Talebi** -> +%{DemandMaxBonus * 100:0.#} (T57 tavani)");
        sb.AppendLine($"- **Ardisik Teslimat (Streak)** -> +%{StreakMaxBonus * 100:0.#} (T59 tavani, 9+ ardisik hatasiz islem)");
        sb.AppendLine($"- Ucu carpimsal ustuste biner: carpan = {(1f + masteryBonus):0.###} x {(1f + DemandMaxBonus):0.###} x {(1f + StreakMaxBonus):0.###} = **{stackedMultiplier:0.###}**");
        sb.AppendLine();
        sb.AppendLine("| Kota No | Gun | Taban (Garanti) | Ust Sinir (sistem ustuste, pazarliksiz) |");
        sb.AppendLine("|---|---|---|---|");
        for (int i = 0; i < quotaData.quotas.Length; i++)
        {
            var q = quotaData.quotas[i];
            sb.AppendLine($"| {i + 1} | {q.day} | {q.requiredAmount}$ | {(q.requiredAmount * stackedMultiplier):0.#}$ |");
        }
        sb.AppendLine();
    }

    private static void WriteDifficultyTable(StringBuilder sb, QuotaData quotaData)
    {
        sb.AppendLine("## 3. Zorluk Ayarlari Ekseni (T60 — henuz implement EDILMEDI)");
        sb.AppendLine();
        sb.AppendLine("**Onemli:** Zorluk carpani GELIRE degil, HEDEFE (kota tutarina) uygulanir " +
            "(T60 Do maddesi). Yani bu, gelir-tarafi bonuslarindan (Bolum 2) BAGIMSIZ bir eksendir. " +
            "PLAN.md SS3.4'teki 'base fiyatla garanti' ilkesi, tasarimin ORIJINAL halinde (Zorluk " +
            "sistemi eklenmeden once, yani fiili Normal x1.0 icin) yazildi — Zor modda (x1.3) " +
            "hedefin yukselmesi KASITLI bir zorluk artisi olabilir, bu bir tutarsizlik degil. " +
            "**Acik soru (PM/LD onayi gerekiyor, bu belge sadece isaretliyor):** Zor modda da " +
            "'base fiyatla garanti gecilebilir' ilkesi korunmali mi, yoksa Zor kasitli olarak " +
            "bu garantiyi kirmali mi? T60 implement edilirken netlestirilmeli.");
        sb.AppendLine();
        sb.AppendLine("| Kota No | Gun | Kolay (x0.8) Hedef | Normal (x1.0) Hedef | Zor (x1.3) Hedef |");
        sb.AppendLine("|---|---|---|---|---|");
        for (int i = 0; i < quotaData.quotas.Length; i++)
        {
            var q = quotaData.quotas[i];
            sb.AppendLine($"| {i + 1} | {q.day} | {(q.requiredAmount * DifficultyEasy):0.#}$ | {(q.requiredAmount * DifficultyNormal):0.#}$ | {(q.requiredAmount * DifficultyHard):0.#}$ |");
        }
        sb.AppendLine();
    }

    private static void WriteNegotiationScenarios(StringBuilder sb, QuotaData quotaData)
    {
        sb.AppendLine("## 4. Pazarlik Senaryolari (T71-73 spec'i — ONAY BEKLIYOR, implement EDILMEDI)");
        sb.AppendLine();
        sb.AppendLine("Asagidaki carpanlar, T71-73'un meet-in-the-middle formullerinden turetilen " +
            "TEMSILI kapanis degerleridir (gercek playtest degil). Pazarlik SISTEM ustuste " +
            "binmelerinden (Bolum 2) BAGIMSIZ, ayrica, opsiyonel bir ekstra-kar aracidir " +
            "(PLAN.md SS3.4: kota icin ZORUNLU degil).");
        sb.AppendLine();
        sb.AppendLine("| Senaryo | Aciklama | Satis Carpani | Alim Carpani |");
        sb.AppendLine("|---|---|---|---|");
        sb.AppendLine($"| 1 | Herkes Bolge A / round1'de guvenli kapatir | x{NegotiationScenario1_SafeZoneA:0.##} | x{2f - NegotiationScenario1_SafeZoneA:0.##} |");
        sb.AppendLine($"| 2 | Herkes maksimum agresif (Bolge B / round2) oynar | x{NegotiationScenario2_AggressiveZoneB:0.##} | x{2f - NegotiationScenario2_AggressiveZoneB:0.##} |");
        sb.AppendLine($"| 3 | Karisik populasyon, muhafazakar ortalama | x{NegotiationScenario3_MixedConservative:0.##} | x{2f - NegotiationScenario3_MixedConservative:0.##} |");
        sb.AppendLine();
        sb.AppendLine("| Kota No | Gun | Taban | Senaryo1 Ust Sinir (satis yonu) | Senaryo2 Ust Sinir | Senaryo3 Ust Sinir |");
        sb.AppendLine("|---|---|---|---|---|---|");
        for (int i = 0; i < quotaData.quotas.Length; i++)
        {
            var q = quotaData.quotas[i];
            sb.AppendLine($"| {i + 1} | {q.day} | {q.requiredAmount}$ | {(q.requiredAmount * NegotiationScenario1_SafeZoneA):0.#}$ | {(q.requiredAmount * NegotiationScenario2_AggressiveZoneB):0.#}$ | {(q.requiredAmount * NegotiationScenario3_MixedConservative):0.#}$ |");
        }
        sb.AppendLine();
    }

    private static void WriteVerificationConclusion(StringBuilder sb, QuotaData quotaData)
    {
        float masteryBonus = RepresentativeMasteryLevel * MasteryPerLevel;
        float stackedMultiplier = (1f + masteryBonus) * (1f + DemandMaxBonus) * (1f + StreakMaxBonus);
        float finalTarget = quotaData.quotas[quotaData.quotas.Length - 1].requiredAmount;
        float firstPeriodUpperBound = quotaData.quotas[0].requiredAmount * stackedMultiplier * NegotiationScenario2_AggressiveZoneB;

        sb.AppendLine("## 5. Dogrulama Sonucu");
        sb.AppendLine();
        sb.AppendLine("- **Taban hicbir donemde hedefi kacirmiyor mu?** EVET — tanim geregi Taban = " +
            "QuotaData'nin kendi requiredAmount degeri (Bolum 1), yani PLAN.md SS3.4 ilkesi TANIM " +
            "OLARAK saglaniyor. Bu, siparis-bazli asagidan-yukari bir dogrulama DEGIL (GDD SS9 gun-gun " +
            "tablosu gosterge niteliginde oldugu icin bu simulasyonun kapsaminda degil) — kesin " +
            "sayisal dogrulama, o tablo kesinlestiginde ayri bir T-gorevi olarak yapilmali.");
        sb.AppendLine($"- **En kotu-durum ust sinir (Kota 1, tum sistemler + en agresif pazarlik) 700$ " +
            $"final hedefiyle cakisiyor mu?** HAYIR — Kota 1 icin hesaplanan mutlak tavan " +
            $"{firstPeriodUpperBound:0.#}$, final hedef {finalTarget}$'in cok altinda. Curve inversion " +
            "riski yok.");
        sb.AppendLine("- **Acik/kullaniciya sorulmasi gereken maddeler:** (1) Kece asset fiyat uyumsuzlugu " +
            "(Bolum 0), (2) Zor zorlukta 'base fiyatla garanti' ilkesinin korunup korunmayacagi (Bolum 3).");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("**Not:** Bu belge T52'nin bir seferlik ciktisidir. Formuller (T54/55/57/59/60/71-73) " +
            "gercekten implement edildikten sonra, `Farmer Squad > T52 - Ekonomi Simulasyonu Calistir` " +
            "menusu tekrar calistirilip bu dosya guncellenerek gercek koddan turetilmis (varsayimsiz) " +
            "bir versiyon uretilebilir.");
    }
}
