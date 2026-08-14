using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Her kota gununde balance kontrolu yapar (ARCHITECTURE.md "## Ekonomi (Kasa) Sistemi").
/// Kira kesintisi = o kotanin kendi $ degeri (cumulative bakiye DEGIL, tek seferlik kesinti).
/// Akis: Balance >= requiredAmount ise BASARILI -> requiredAmount kadar kira kesilir.
/// Balance &lt; requiredAmount ise BASARISIZ -> IsBankrupt = true (kesinti YAPILMAZ).
/// Host-authoritative: sadece server kontrolu gercekten uygular.
/// DayCycleManager (T31) henuz yok; bu yuzden gun bilgisi disaridan (debug/ContextMenu veya
/// ileride DayCycleManager) CheckQuotaServerRpc(day) ile verilir.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class QuotaManager : NetworkBehaviour
{
    [SerializeField] private QuotaData quotaData;
    [SerializeField] private WalletManager walletManager;

    public NetworkVariable<bool> IsBankrupt = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (walletManager == null)
        {
            walletManager = FindObjectOfType<WalletManager>();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CheckQuotaServerRpc(int day)
    {
        if (!IsServer)
        {
            return;
        }

        if (quotaData == null || walletManager == null)
        {
            Debug.LogError("[QuotaManager] quotaData veya walletManager atanmamis, kontrol yapilamadi.");
            return;
        }

        if (!quotaData.TryGetQuota(day, out float requiredAmount))
        {
            Debug.LogWarning("[QuotaManager] Gun " + day + " icin tanimli kota yok, kontrol atlandi.");
            return;
        }

        float currentBalance = walletManager.Balance.Value;

        if (currentBalance >= requiredAmount)
        {
            walletManager.SubtractBalanceServerRpc(requiredAmount);
            Debug.Log("[QuotaManager] Gun " + day + " kotasi BASARILI. Gerekli: " + requiredAmount +
                "$, Bakiye (kesinti oncesi): " + currentBalance + "$. Kira kesildi, kalan: " +
                (currentBalance - requiredAmount) + "$.");
        }
        else
        {
            IsBankrupt.Value = true;
            Debug.Log("[QuotaManager] Gun " + day + " kotasi BASARISIZ - IFLAS. Gerekli: " + requiredAmount +
                "$, Bakiye: " + currentBalance + "$, Acik: " + (currentBalance - requiredAmount) + "$.");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetBankruptcyServerRpc()
    {
        if (!IsServer)
        {
            return;
        }

        IsBankrupt.Value = false;
    }

    [ContextMenu("DEBUG: Check Day 3 Quota")]
    private void DebugCheckDay3()
    {
        CheckQuotaServerRpc(3);
    }

    [ContextMenu("DEBUG: Check Day 6 Quota")]
    private void DebugCheckDay6()
    {
        CheckQuotaServerRpc(6);
    }

    [ContextMenu("DEBUG: Reset Bankruptcy")]
    private void DebugResetBankruptcy()
    {
        ResetBankruptcyServerRpc();
    }
}
