using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ARCHITECTURE.md "## Prestij Sistemi". +0.25 basarili teslimat / -0.25 hatali teslimat
/// (GDD Bolum 5) - SADECE Satis yonundeki teslimatlar icin. NegotiationManager.
/// RequestFinalizeDeliveryServerRpc (T24, deliveryZoneDetector kontrolu SADECE Satis'ta var)
/// sonucu ReportDeliveryResultServer'a bildirir. Alim siparisleri Kabul Et aninda spawn olup
/// hicbir delivery-zone kontrolunden gecmiyor (GDD'nin "Yanlis Teslimat / Arac Kacmasi" ceza
/// mekanizmasi kavramsal olarak sadece Satis'a uygulanabilir) - bu yuzden Alim, Prestij'e
/// SINYAL GONDERMEZ. Bu, mevcut NegotiationManager kod yorumlarindaki T35 baglanti noktasiyla
/// (sadece RequestFinalizeDeliveryServerRpc icinde referans var) tutarlidir.
///
/// 1.0 puana ulasilinca (birikimli, tasan kisim bir sonraki esige tasinir - while dongusu ile,
/// ayni gunde birden fazla esik asilirsa birden fazla bonus arac eklenebilir) +1 bonus arac
/// "bekleyen havuza" (PendingBonusVehicles) eklenir. DayCycleManager, YENI bir gun basladiginda
/// (StartDayServer) ConsumeBonusServer() ile bu havuzu OKUYUP SIFIRLAR - bonus SADECE bir sonraki
/// gune uygulanir (GDD: "+1 Bonus Arac ertesi gune eklenir"), kalici/cok-gunluk bir artis DEGIL.
/// Ust sinir (10 normal / 6 solo Gun10+, GDD: "Maksimum Arac Sinir: 10 arac/gun (bonus dahil)")
/// burada UYGULANMAZ - DayCycleManager.GetTotalVehicleCountForToday() icinde uygulanir, cunku
/// solo/gun bilgisi DayCycleManager'da tutuluyor.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PrestigeManager : NetworkBehaviour
{
    private const float SuccessDelta = 0.25f;
    private const float FailureDelta = -0.25f;
    private const float BonusThreshold = 1.0f;

    /// <summary>Ham prestij puani. GDD'de taban/tavan siniri belirtilmiyor, negatife de inebilir.</summary>
    public NetworkVariable<float> PrestigePoints = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>Henuz "ertesi gune" aktarilmamis, birikmis bonus arac sayisi. DayCycleManager
    /// gun basinda bunu tuketir (ConsumeBonusServer) ve sifirlar.</summary>
    public NetworkVariable<int> PendingBonusVehicles = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>
    /// NegotiationManager.RequestFinalizeDeliveryServerRpc icinden (server-context'te, zaten bir
    /// [ServerRpc] metodun icinden) DOGRUDAN cagrilir - ayri bir ServerRpc DEGIL. Bu, MarketManager/
    /// DayCycleManager'daki "cagiran taraf zaten server" deseniyle tutarlidir.
    /// </summary>
    public void ReportDeliveryResultServer(bool success)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[PrestigeManager] ReportDeliveryResultServer sadece server'da calisir.");
            return;
        }

        float delta = success ? SuccessDelta : FailureDelta;
        PrestigePoints.Value += delta;

        while (PrestigePoints.Value >= BonusThreshold)
        {
            PrestigePoints.Value -= BonusThreshold;
            PendingBonusVehicles.Value += 1;
            Debug.Log("[PrestigeManager] 1.0 puan dolduruldu, +1 bonus arac havuza eklendi (havuz: " +
                       PendingBonusVehicles.Value + ").");
        }

        Debug.Log("[PrestigeManager] " + (success ? "Basarili" : "Hatali") + " teslimat: " + delta +
                   " puan, yeni toplam: " + PrestigePoints.Value);
    }

    /// <summary>
    /// DayCycleManager.StartDayServer, yeni bir gun basladiginda cagirir. Havuzdaki bonusu
    /// dondurur VE SIFIRLAR (bonus sadece o gune ozel, kalici degil - GDD Bolum 5).
    /// </summary>
    public int ConsumeBonusServer()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[PrestigeManager] ConsumeBonusServer sadece server'da calisir.");
            return 0;
        }

        int bonus = PendingBonusVehicles.Value;
        PendingBonusVehicles.Value = 0;
        return bonus;
    }

    [ContextMenu("DEBUG: +0.25 (basarili teslimat)")]
    private void DebugSuccess()
    {
        ReportDeliveryResultServer(true);
    }

    [ContextMenu("DEBUG: -0.25 (hatali teslimat)")]
    private void DebugFailure()
    {
        ReportDeliveryResultServer(false);
    }
}
