using UnityEngine;
using Unity.Netcode;

/// <summary>
/// T34: Geniş Çitler satın alınınca ikinci, BAĞIMSIZ bir ağıl (kendi PenManager'ı, kendi stok
/// sayacı) sahneye eklenir (ARCHITECTURE.md "## Ağıl & Teslimat Alanı" + kullanıcı kararı:
/// "Bağımsız iki stok" — ikinci ağılın kendi PenManager'ı ve kendi stok sayacı olur).
/// MarketManager.PurchaseUpgradeServerRpc içindeki WidePens dalından ÇAĞRILIR (bkz.
/// MarketManager.cs değişikliği) - ayrı bir ServerRpc DEĞİL, çünkü çağıran taraf zaten server
/// context'inde (MarketManager'ın kendi [ServerRpc] metodu içinden).
///
/// Kaynak ağıl `citler` klonlanır (mevcut PenManager, bounds BoxCollider, NetworkObject dahil
/// TÜM component'leriyle) - klon kendi bağımsız stok Dictionary'sine (PenManager.Awake() her
/// instance için sıfırdan oluşturuyor) ve kendi bounds'una (bounds alanı aynı objenin ikinci
/// BoxCollider'ına self-reference, Instantiate ile Unity tarafından otomatik doğru remap edilir)
/// sahip olur. İkinci ağılın konumu PLACEHOLDER - LD/TA nihai yerleşimi belirleyecek
/// (ARCHITECTURE.md notu: "bu task'ta placeholder konum kullan, gerekirse kullanıcıya sor").
///
/// Plain MonoBehaviour olarak yazıldı (NetworkBehaviour DEĞİL) - kendi başına hiçbir
/// NetworkVariable/state tutmuyor, tek işi sunucuda bir kez Instantiate+NetworkObject.Spawn()
/// çağırmak; bu tek başına klonun tüm client'lara replike olması için yeterli.
/// </summary>
public class PenUpgradeSecondPen : MonoBehaviour
{
    [Header("Kaynak Ağıl (klonlanacak)")]
    [Tooltip("Klonlanacak orijinal ağıl objesi. Sahnedeki 'citler' objesi atanmalı.")]
    public GameObject sourcePen;

    [Header("İkinci Ağılın Konumu (PLACEHOLDER, LD/TA nihai yerleşimi belirleyecek)")]
    public Vector3 secondPenPosition = new Vector3(614.5f, 6.01f, 522.4f);
    public Quaternion secondPenRotation = Quaternion.identity;

    private bool alreadyAdded = false;

    /// <summary>
    /// MarketManager.PurchaseUpgradeServerRpc içinden (server-side, WidePens satın alınca)
    /// doğrudan çağrılır. Tekrar çağrılırsa (ör. ileride bir hata ile iki kez tetiklenirse)
    /// ikinci bir klon EKLEMEZ - PurchasedMask zaten tekrar satın almayı engelliyor olsa da
    /// burada da savunmacı bir kontrol var.
    /// </summary>
    public void AddSecondPenServer()
    {
        bool isServer = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        if (!isServer)
        {
            Debug.LogWarning("[PenUpgradeSecondPen] AddSecondPenServer sadece server'da calisir.");
            return;
        }

        if (alreadyAdded)
        {
            Debug.Log("[PenUpgradeSecondPen] Ikinci agil zaten eklenmis, tekrar eklenmedi.");
            return;
        }

        if (sourcePen == null)
        {
            Debug.LogError("[PenUpgradeSecondPen] sourcePen atanmamis, ikinci agil eklenemedi.");
            return;
        }

        GameObject clone = Instantiate(sourcePen, secondPenPosition, secondPenRotation);
        clone.name = sourcePen.name + "_Second";

        NetworkObject netObj = clone.GetComponent<NetworkObject>();
        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            netObj.Spawn();
        }

        alreadyAdded = true;
        Debug.Log("[PenUpgradeSecondPen] Ikinci agil eklendi: " + clone.name + " @ " + secondPenPosition +
                   " (bagimsiz PenManager + stok, kaynak agildan tamamen ayri).");
    }
}
