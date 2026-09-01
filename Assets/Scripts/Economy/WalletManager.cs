using UnityEngine;
using Unity.Netcode;

/// <summary>
/// T74: Bir para hareketinin hangi sistemden geldigini tasir. Uc kaynak: NegotiationManager
/// (satis VE alim tamamlanmasi - yon degil, KAYNAK SISTEM esas alinir) = Satis,
/// MarketManager (upgrade satin alma) = Harcama, QuotaManager (gun sonu kira kesintisi) = Kira.
/// </summary>
public enum TransactionReason
{
    Satis,
    Harcama,
    Kira
}

/// <summary>
/// Tum para hareketleri buradan gecer (ARCHITECTURE.md "## Ekonomi (Kasa) Sistemi").
/// Sahnede TEK bir instance olmasi beklenir, host-authoritative NetworkVariable<float> Balance.
/// Diger client'lar Balance.OnValueChanged'a abone olarak HUD'u guncelleyecek (Faz 9, henuz yok).
///
/// T74: Balance NetworkVariable.OnValueChanged'e tek basina guvenilmez - ayni network tick'inde
/// birden fazla degisiklik olursa ara adimlar client'ta kaybolabilir. Bu yuzden Balance.Value
/// guncellendikten HEMEN SONRA, ayni sunucu metodu icinde, reliable bir ClientRpc
/// (NotifyTransactionClientRpc) ile tum client'lara delta+reason acikca bildirilir.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class WalletManager : NetworkBehaviour
{
    public NetworkVariable<float> Balance = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Balance.OnValueChanged += OnBalanceChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        Balance.OnValueChanged -= OnBalanceChanged;
    }

    private void OnBalanceChanged(float previous, float current)
    {
        string who = NetworkManager.Singleton != null ? ("client" + NetworkManager.Singleton.LocalClientId) : "?";
        Debug.Log("[WalletManager] (" + who + ") Balance degisti: " + previous + " -> " + current);
    }

    /// <summary>
    /// Diger server-authoritative scriptler (orn. NegotiationManager, zaten kendi ServerRpc'leri
    /// icinde calisir) buradan DOGRUDAN cagirabilir. Client'tan cagrilirsa NGO otomatik olarak
    /// sunucuya yonlendirir (ServerRpc semantigi).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AddBalanceServerRpc(float amount, TransactionReason reason)
    {
        if (amount < 0f)
        {
            Debug.LogWarning("[WalletManager] AddBalanceServerRpc negatif miktar aldi (" + amount + "), yok sayildi. SubtractBalanceServerRpc kullanin.");
            return;
        }
        Balance.Value += amount;
        NotifyTransactionClientRpc(amount, reason);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubtractBalanceServerRpc(float amount, TransactionReason reason)
    {
        if (amount < 0f)
        {
            Debug.LogWarning("[WalletManager] SubtractBalanceServerRpc negatif miktar aldi (" + amount + "), yok sayildi. AddBalanceServerRpc kullanin.");
            return;
        }
        Balance.Value -= amount;
        NotifyTransactionClientRpc(-amount, reason);
    }

    /// <summary>
    /// T74: Balance guncellendikten hemen sonra ayni sunucu metodu icinde cagrilir (reliable
    /// ClientRpc - NGO'da varsayilan olarak reliable, garantili sirali teslimat saglar). Host
    /// kendi ClientRpc'sini de local olarak alir (IsHost=true iken NGO bunu otomatik yapar).
    /// Gecici Debug.Log: T75'teki MoneyFeedbackController bu bildirimi dinleyip HUD animasyonunu
    /// tetikleyecek (henuz yok) - simdilik sadece log ile 2-client senkron testi yapilabilir.
    /// </summary>
    [ClientRpc]
    private void NotifyTransactionClientRpc(float delta, TransactionReason reason)
    {
        string who = NetworkManager.Singleton != null ? ("client" + NetworkManager.Singleton.LocalClientId) : "?";
        Debug.Log("[WalletManager] (" + who + ") Transaction bildirimi: delta=" + delta + ", reason=" + reason);
        OnTransactionNotified?.Invoke(delta, reason);
    }

    /// <summary>
    /// T75: MoneyFeedbackController (NetworkBehaviour DEGIL, tamamen client-side) bu static
    /// event'e abone olup Kasa HUD animasyonunu tetikler. Static kullanildi cunku sahnede
    /// TEK bir WalletManager instance'i beklendigi icin (yukaridaki yorum) instance referansi
    /// aramaya gerek yok - HUDController.Instance deseniyle tutarli bir yaklasim.
    /// NotifyTransactionClientRpc her client'ta (host dahil) calistiginda tetiklenir, yani
    /// bu event de ayni sekilde her client'ta kendi local degerleriyle ateslenir.
    /// </summary>
    public static event System.Action<float, TransactionReason> OnTransactionNotified;

    [ContextMenu("DEBUG: Add 100")]
    private void DebugAdd100()
    {
        AddBalanceServerRpc(100f, TransactionReason.Satis);
    }

    [ContextMenu("DEBUG: Subtract 50")]
    private void DebugSubtract50()
    {
        SubtractBalanceServerRpc(50f, TransactionReason.Harcama);
    }
}
