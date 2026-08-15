using UnityEngine;
using Unity.Netcode;

/// <summary>
/// T38: WinScreenController'in (GDD Bolum 10.7 mockup) gosterecegi istatistikleri toplar.
/// ARCHITECTURE.md'de ayri bir sistem olarak listelenmemisti - bu dosya T38 kapsaminda
/// eklendi (bkz. HANDOFF.md T38 notu: "istatistik sayaclarinin kendisi de eklenmeli").
/// Host-authoritative NetworkVariable'lar, NegotiationManager (T24/T28) tarafindan ilgili
/// islemler sonuclandiginda DOGRUDAN cagrilir (ayri ServerRpc DEGIL - cagiran taraf zaten
/// server context'inde, WalletManager/PrestigeManager ile ayni desen).
///
/// TANIM NOTU (varsayim, GDD'de acik degildi): "Toplam Kar" = basarili Satis islemlerinden
/// gelen toplam gelir EKSI Alim islemlerine harcanan toplam tutar (sadece hayvan alim-satim
/// ticareti - kira kesintileri ve market upgrade harcamalari DAHIL DEGIL, cunku WalletManager.
/// Balance zaten o net etkiyi ayri gosteriyor). Bu basit ve savunmasi kolay bir tanim; farkli
/// bir tanim istenirse burasi tek degisiklik noktasi.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class GameStatsTracker : NetworkBehaviour
{
    [Header("Teslimat/Satis Istatistikleri")]
    public NetworkVariable<int> TotalAnimalsSold = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> TotalProfit = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> SuccessfulDeliveries = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> WrongDeliveries = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Pazarlik Istatistikleri")]
    public NetworkVariable<int> NegotiationsAttempted = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> NegotiationsSucceeded = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>NegotiationManager.RequestFinalizeDeliveryServerRpc icinden, Satis basarili teslimatta cagrilir.</summary>
    public void ReportSaleServer(int animalCount, float income)
    {
        if (!IsServer) { Debug.LogWarning("[GameStatsTracker] ReportSaleServer sadece server'da calisir."); return; }
        TotalAnimalsSold.Value += animalCount;
        TotalProfit.Value += income;
        SuccessfulDeliveries.Value += 1;
    }

    /// <summary>NegotiationManager.RequestFinalizeDeliveryServerRpc icinden, Satis yanlis/eksik teslimatta cagrilir.</summary>
    public void ReportWrongDeliveryServer()
    {
        if (!IsServer) { Debug.LogWarning("[GameStatsTracker] ReportWrongDeliveryServer sadece server'da calisir."); return; }
        WrongDeliveries.Value += 1;
    }

    /// <summary>NegotiationManager icinden, Alim siparisi Kabul Et ile sonuclaninca (odeme yapildiginda) cagrilir.</summary>
    public void ReportPurchaseServer(float expense)
    {
        if (!IsServer) { Debug.LogWarning("[GameStatsTracker] ReportPurchaseServer sadece server'da calisir."); return; }
        TotalProfit.Value -= expense;
    }

    /// <summary>NegotiationManager.RequestNegotiateServerRpc icinden, HER gercek risk atisinda (absurd teklifler HARIC) cagrilir.</summary>
    public void ReportNegotiationAttemptServer(bool succeeded)
    {
        if (!IsServer) { Debug.LogWarning("[GameStatsTracker] ReportNegotiationAttemptServer sadece server'da calisir."); return; }
        NegotiationsAttempted.Value += 1;
        if (succeeded) NegotiationsSucceeded.Value += 1;
    }
}
