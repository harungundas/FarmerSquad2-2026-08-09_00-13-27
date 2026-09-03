using UnityEngine;
using Unity.Netcode;

/// <summary>
/// T62: Gun boyunca biriken istatistikleri tutan host-authoritative script. GameStatsTracker.cs
/// (T38) ile KARISTIRILMAMALI - o OYUN BOYU kumulatif sayaclar tutuyor (WinScreen icin, hic
/// sifirlanmiyor). Bu script ise GUNLUK sayaclar tutar: DayCycleManager.CompleteDayServer()
/// gun sonunda bu veriyi (T63'e, henuz yoksa Debug.Log'a) aktarir, SONRA ResetForNewDayServer()
/// ile sifirlar (TASKS.md T62: "gun bitince bu veriler sifirlanmadan once T63'teki Istatistik
/// Ekrani'na aktarilir").
///
/// Alan semasi GDD_Farmer_Squad_v2.0.md Bolum10 "Basari Ekrani" mockup'iyla uyumlu (Context
/// gereksinimi): Basari Orani, Toplam Hayvan Satisi, Toplam Kar, Hata Sayisi, Pazarlik Basarisi.
/// + T58'in kacirilan Ozel Musteri sayaci (VehicleSpawner.MissedSpecialCustomers'tan gunluk
/// olarak buraya da raporlanir - VehicleSpawner kendi sayacini hic sifirlamiyor, oyun boyu
/// kumulatif kaliyor, burada AYRI bir gunluk kopya tutulur).
///
/// Rapor noktalari NegotiationManager (T24/T28) ve VehicleSpawner (T58) icinden, GameStatsTracker
/// ile AYNI cagri noktalarinda, ona PARALEL olarak (tek satir ek cagri) tetiklenir - mevcut T38
/// akisina dokunulmaz.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class DailyStatsAccumulator : NetworkBehaviour
{
    [Header("Gunluk Satis/Teslimat Istatistikleri")]
    public NetworkVariable<int> DailyAnimalsSold = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> DailyProfit = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> DailySuccessfulDeliveries = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> DailyWrongDeliveries = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Gunluk Pazarlik Istatistikleri")]
    public NetworkVariable<int> DailyNegotiationsAttempted = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> DailyNegotiationsSucceeded = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("T58: Gunluk Kacirilan Ozel Musteri")]
    public NetworkVariable<int> DailyMissedSpecialCustomers = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>NegotiationManager.RequestFinalizeDeliveryServerRpc icinden, Satis basarili
    /// teslimatta, GameStatsTracker.ReportSaleServer ile AYNI noktada cagrilir.</summary>
    public void ReportSaleServer(int animalCount, float income)
    {
        if (!IsServer) { Debug.LogWarning("[DailyStatsAccumulator] ReportSaleServer sadece server'da calisir."); return; }
        DailyAnimalsSold.Value += animalCount;
        DailyProfit.Value += income;
        DailySuccessfulDeliveries.Value += 1;
    }

    /// <summary>NegotiationManager.RequestFinalizeDeliveryServerRpc icinden, Satis yanlis/eksik
    /// teslimatta cagrilir.</summary>
    public void ReportWrongDeliveryServer()
    {
        if (!IsServer) { Debug.LogWarning("[DailyStatsAccumulator] ReportWrongDeliveryServer sadece server'da calisir."); return; }
        DailyWrongDeliveries.Value += 1;
    }

    /// <summary>NegotiationManager icinden, Alim siparisi Kabul Et ile sonuclaninca (odeme
    /// yapildiginda) cagrilir. Kar tanimi GameStatsTracker ile ayni: Alim harcamasi kardan duser.</summary>
    public void ReportPurchaseServer(float expense)
    {
        if (!IsServer) { Debug.LogWarning("[DailyStatsAccumulator] ReportPurchaseServer sadece server'da calisir."); return; }
        DailyProfit.Value -= expense;
    }

    /// <summary>NegotiationManager.RequestNegotiateServerRpc icinden, HER gercek risk atisinda
    /// (absurt teklifler HARIC) cagrilir.</summary>
    public void ReportNegotiationAttemptServer(bool succeeded)
    {
        if (!IsServer) { Debug.LogWarning("[DailyStatsAccumulator] ReportNegotiationAttemptServer sadece server'da calisir."); return; }
        DailyNegotiationsAttempted.Value += 1;
        if (succeeded) DailyNegotiationsSucceeded.Value += 1;
    }

    /// <summary>VehicleSpawner.HandleVehicleMissedSpecialWindow icinden, Ozel Musteri kabul
    /// penceresi dolup kacirildiginda cagrilir (T58). Streak'i etkilemez, sadece istatistik.</summary>
    public void ReportMissedSpecialCustomerServer()
    {
        if (!IsServer) { Debug.LogWarning("[DailyStatsAccumulator] ReportMissedSpecialCustomerServer sadece server'da calisir."); return; }
        DailyMissedSpecialCustomers.Value += 1;
    }

    /// <summary>DayCycleManager.CompleteDayServer() tarafindan, gun gecisi kesinlesince (kota
    /// basarili VEYA o gun kota yoksa - iflas/final-kazanma durumlarinda bu CAGRILMAZ, cunku
    /// oyun akisi orada durur) cagrilir. Guncel gunun ozetini dondurur, SONRA sifirlar - T63
    /// (Istatistik Ekrani) henuz yazilmadigi icin ozet simdilik sadece Debug.Log ile "aktarilir".</summary>
    public DailyStatsSnapshot ConsumeAndResetServer(int dayNumber)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[DailyStatsAccumulator] ConsumeAndResetServer sadece server'da calisir.");
            return default;
        }

        var snapshot = new DailyStatsSnapshot
        {
            day = dayNumber,
            animalsSold = DailyAnimalsSold.Value,
            profit = DailyProfit.Value,
            successfulDeliveries = DailySuccessfulDeliveries.Value,
            wrongDeliveries = DailyWrongDeliveries.Value,
            negotiationsAttempted = DailyNegotiationsAttempted.Value,
            negotiationsSucceeded = DailyNegotiationsSucceeded.Value,
            missedSpecialCustomers = DailyMissedSpecialCustomers.Value
        };

        // T63 henuz yazilmadi - suan icin gun sonu ozeti host konsoluna basiliyor. T63
        // eklendiginde bu Debug.Log yerine/yaninda snapshot dogrudan UI'a gecirilecek.
        Debug.Log("[DailyStatsAccumulator] Gun " + dayNumber + " ozeti (T63 bekleniyor, simdilik log): " +
                   "Satis=" + snapshot.animalsSold +
                   " Kar=" + snapshot.profit +
                   " BasariliTeslimat=" + snapshot.successfulDeliveries +
                   " YanlisTeslimat=" + snapshot.wrongDeliveries +
                   " Pazarlik=" + snapshot.negotiationsSucceeded + "/" + snapshot.negotiationsAttempted +
                   " KacirilanOzelMusteri=" + snapshot.missedSpecialCustomers);

        DailyAnimalsSold.Value = 0;
        DailyProfit.Value = 0f;
        DailySuccessfulDeliveries.Value = 0;
        DailyWrongDeliveries.Value = 0;
        DailyNegotiationsAttempted.Value = 0;
        DailyNegotiationsSucceeded.Value = 0;
        DailyMissedSpecialCustomers.Value = 0;

        return snapshot;
    }
}

/// <summary>T62: Bir gunun donuk (immutable) ozet verisi - ConsumeAndResetServer'in dondurdugu
/// deger, ileride T63'un UI'ina dogrudan gecirilecek.</summary>
public struct DailyStatsSnapshot
{
    public int day;
    public int animalsSold;
    public float profit;
    public int successfulDeliveries;
    public int wrongDeliveries;
    public int negotiationsAttempted;
    public int negotiationsSucceeded;
    public int missedSpecialCustomers;
}
