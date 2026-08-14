using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Tum para hareketleri buradan gecer (ARCHITECTURE.md "## Ekonomi (Kasa) Sistemi").
/// Sahnede TEK bir instance olmasi beklenir, host-authoritative NetworkVariable&lt;float&gt; Balance.
/// Diger client'lar Balance.OnValueChanged'a abone olarak HUD'u guncelleyecek (Faz 9, henuz yok).
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
    public void AddBalanceServerRpc(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogWarning("[WalletManager] AddBalanceServerRpc negatif miktar aldi (" + amount + "), yok sayildi. SubtractBalanceServerRpc kullanin.");
            return;
        }
        Balance.Value += amount;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubtractBalanceServerRpc(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogWarning("[WalletManager] SubtractBalanceServerRpc negatif miktar aldi (" + amount + "), yok sayildi. AddBalanceServerRpc kullanin.");
            return;
        }
        Balance.Value -= amount;
    }

    [ContextMenu("DEBUG: Add 100")]
    private void DebugAdd100()
    {
        AddBalanceServerRpc(100f);
    }

    [ContextMenu("DEBUG: Subtract 50")]
    private void DebugSubtract50()
    {
        SubtractBalanceServerRpc(50f);
    }
}
