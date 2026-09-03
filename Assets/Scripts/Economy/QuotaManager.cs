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

    /// <summary>T60: Zorluk Ayarlari. Kolay=0.8, Normal=1.0 (varsayilan), Zor=1.3. Sadece
    /// host, LobbyUI'daki preset butonlarindan degistirebilir - SetDifficultyServerRpc
    /// server-side reddeder: (a) sunucu degilse, (b) oyun zaten basladiysa
    /// (LobbyNetworkState.GameStarted true). Bu deger tum kota hesaplarinda (QuotaManager +
    /// HUDController) CARPAN olarak kullanilir: requiredAmount * DifficultyMultiplier.Value.</summary>
    public NetworkVariable<float> DifficultyMultiplier = new NetworkVariable<float>(
        1.0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public static QuotaManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (walletManager == null)
        {
            walletManager = FindObjectOfType<WalletManager>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>T60: Host-only zorluk secimi. UI gizleme YETMEZ - burada server-side
    /// dogrulama yapiliyor (FAZ 12 "Host-only Start Game" desenindeki ayni standart):
    /// (1) sadece sunucu gercekten uygular, (2) lobi kapanip oyun basladiktan sonra
    /// (LobbyNetworkState.GameStarted == true) degisiklik REDDEDILIR - oyun ortasinda
    /// zorluk degistirilemez.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetDifficultyServerRpc(float multiplier, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            return;
        }

        if (LobbyNetworkState.Instance != null && LobbyNetworkState.Instance.GameStarted.Value)
        {
            Debug.LogWarning("[QuotaManager] Zorluk degistirme reddedildi - oyun zaten basladi (GameStarted=true).");
            return;
        }

        DifficultyMultiplier.Value = multiplier;
        Debug.Log("[QuotaManager] Zorluk carpani ayarlandi: x" + multiplier + " (clientId=" + rpcParams.Receive.SenderClientId + ")");
    }

    /// <summary>Verilen gun icin, zorluk carpani UYGULANMIS kota tutarini dondurur. HUDController
    /// (T60) ve CheckQuotaServerRpc AYNI bu metodu kullanmali - iki ayri hesaplama yolu
    /// senkronsuzluga (HUD'da 45$, gercek kesintide 36$ gibi) yol acar.</summary>
    public bool TryGetAdjustedQuota(int day, out float adjustedAmount)
    {
        adjustedAmount = 0f;
        if (quotaData == null) return false;
        if (!quotaData.TryGetQuota(day, out float baseAmount)) return false;
        adjustedAmount = baseAmount * DifficultyMultiplier.Value;
        return true;
    }

    /// <summary>T38: DayCycleManager'in Gun 18 (final kota) sonrasi WinScreenController.Show()
    /// cagirip cagirmayacagina karar vermesi icin - quotaData'ya (private field) buradan erisilir.</summary>
    public bool IsFinalQuotaDay(int day)
    {
        return quotaData != null && quotaData.IsFinalDay(day);
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

        if (!TryGetAdjustedQuota(day, out float requiredAmount))
        {
            Debug.LogWarning("[QuotaManager] Gun " + day + " icin tanimli kota yok, kontrol atlandi.");
            return;
        }

        float currentBalance = walletManager.Balance.Value;

        if (currentBalance >= requiredAmount)
        {
            walletManager.SubtractBalanceServerRpc(requiredAmount, TransactionReason.Kira);
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
