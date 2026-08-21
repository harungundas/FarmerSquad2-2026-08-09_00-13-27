using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Gun sonu marketi (ARCHITECTURE.md "## Market & Yükseltme"). T32'deki stub'in yerini alir.
/// NetworkBehaviour'a cevrildi (T33) - IsOpen/PurchasedMask tum client'larda ayni gorunmeli ki
/// NegotiationUI ile ayni desende bir MarketUI (plain MonoBehaviour) bunu okuyup panel acabilsin.
///
/// AKIS: DayCycleManager.CompleteDayServer() (sadece server'da, IsServer kontrolu zaten orada)
/// -> OpenMarket(day) DOGRUDAN cagrilir (ServerRpc DEGIL, QuotaManager.CheckQuotaServerRpc'nin
///ayni sekilde dogrudan cagrilma deseniyle tutarli - cunku cagiran taraf zaten server).
/// Oyuncu bir/birden fazla upgrade satin alabilir (PurchaseUpgradeServerRpc, client'tan da
/// cagrilabilir - NGO otomatik RPC'ye cevirir), sonra [Sonraki Gune Gec] butonu
/// RequestCloseAndAdvanceServerRpc'yi tetikler - bu da IsOpen'i kapatir VE
/// DayCycleManager.AdvanceToNextDayServer()'i cagirir (T33'ten once bu gecis DayCycleManager
/// icinde OTOMATIKTI, artik MarketManager tarafindan TETIKLENIYOR - bkz. DayCycleManager.cs
/// degisikligi).
///
/// KAPSAM DISI (bilincli, T33 sadece satin alma/para dususu yapar): 6 upgrade'in 5 tanesinin
/// (El Arabasi, Yem Dagitici, Lojistik Kondisyonu, Ciftlik Becerisi, Pazarlik Ustaligi) gercek
/// oyun-ici etkisi HENUZ UYGULANMIYOR. Genis Citler (id="WidePens") icin asagida bir TODO/hook
/// noktasi birakildi - T34'te PenUpgradeSecondPen yazilinca oraya baglanacak.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class MarketManager : NetworkBehaviour
{
    [Header("Bağlantılar")]
    [SerializeField] private UpgradeData upgradeData;
    [SerializeField] private WalletManager walletManager;
    [SerializeField] private DayCycleManager dayCycleManager;
    [SerializeField] private PenUpgradeSecondPen penUpgradeSecondPen;

    /// <summary>Market ekrani acik mi. Tum client'lar okur, sadece server yazar.</summary>
    public NetworkVariable<bool> IsOpen = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>Marketin hangi gunun sonunda acildigi (UI baslik icin, orn. "Gun 3 Sonu").</summary>
    public NetworkVariable<int> DayJustCompleted = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>
    /// Bitmask: bit i set ise upgradeData.upgrades[i] bu oyun boyunca (kalici, gun basi
    /// SIFIRLANMAZ) satin alinmis demektir. 6 upgrade oldugu icin int (32 bit) yeterli.
    /// </summary>
    public NetworkVariable<int> PurchasedMask = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public UpgradeData UpgradeCatalog => upgradeData;

    private void Awake()
    {
        if (walletManager == null) walletManager = FindObjectOfType<WalletManager>();
        if (dayCycleManager == null) dayCycleManager = FindObjectOfType<DayCycleManager>();
        if (penUpgradeSecondPen == null) penUpgradeSecondPen = FindObjectOfType<PenUpgradeSecondPen>();
    }

    /// <summary>
    /// Gun sonu marketini acar. DayCycleManager.CompleteDayServer() tarafindan DOGRUDAN cagrilir
    /// (zaten IsServer kontrolu yapilmis bir akisin icinden), bu yuzden ServerRpc DEGIL.
    /// </summary>
    public void OpenMarket(int dayJustCompleted)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[MarketManager] OpenMarket sadece server'da calisir.");
            return;
        }

        DayJustCompleted.Value = dayJustCompleted;
        IsOpen.Value = true;
        Debug.Log("[MarketManager] Gun " + dayJustCompleted + " sonu market acildi.");
    }

    /// <summary>
    /// Bir upgrade satin alma denemesi. Client'tan cagrilirsa NGO otomatik RPC'ye cevirir.
    /// Yetersiz bakiye veya zaten satin alinmissa sessizce reddeder (log ile bildirir).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void OpenMarketPreviewServerRpc()
    {
        if (!IsServer) return;
        OpenMarket(dayCycleManager != null ? dayCycleManager.CurrentDay.Value : 0);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEndDayServerRpc()
    {
        if (!IsServer) return;

        IsOpen.Value = false;

        if (dayCycleManager != null)
        {
            dayCycleManager.CompleteDayServer();
        }
        else
        {
            Debug.LogError("[MarketManager] dayCycleManager atanmamis, gun bitirilemedi.");
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void PurchaseUpgradeServerRpc(int upgradeIndex)
    {
        if (!IsServer) return;

        if (upgradeData == null || walletManager == null)
        {
            Debug.LogError("[MarketManager] upgradeData veya walletManager atanmamis, satin alma yapilamadi.");
            return;
        }

        if (!upgradeData.TryGetUpgrade(upgradeIndex, out var entry))
        {
            Debug.LogWarning("[MarketManager] Gecersiz upgrade index: " + upgradeIndex);
            return;
        }

        int bit = 1 << upgradeIndex;
        if ((PurchasedMask.Value & bit) != 0)
        {
            Debug.Log("[MarketManager] " + entry.displayNameTr + " zaten satin alinmis, tekrar alinamaz.");
            return;
        }

        if (walletManager.Balance.Value < entry.price)
        {
            Debug.Log("[MarketManager] Yetersiz bakiye: " + entry.displayNameTr + " icin " + entry.price +
                       "$ gerekli, mevcut bakiye " + walletManager.Balance.Value + "$.");
            return;
        }

        walletManager.SubtractBalanceServerRpc(entry.price);
        PurchasedMask.Value |= bit;

        Debug.Log("[MarketManager] Satin alindi: " + entry.displayNameTr + " (" + entry.price + "$).");

        // T34: Genis Citler satin alinirsa ikinci agili ekle (bagimsiz PenManager/stok).
        // Diger 5 upgrade'in oyun-ici etkisi hala kendi sistemlerine baglanmadi (Wheelbarrow
        // aktiflestirme, AutoFeeder, hiz/yemleme/pazarlik bonuslari) - o baglar T34 kapsami DEGIL.
        if (entry.id == "WidePens")
        {
            if (penUpgradeSecondPen != null)
            {
                penUpgradeSecondPen.AddSecondPenServer();
            }
            else
            {
                Debug.LogError("[MarketManager] penUpgradeSecondPen atanmamis, ikinci agil eklenemedi.");
            }
        }
    }

    /// <summary>
    /// [Sonraki Gune Gec] butonu. Marketi kapatir ve DayCycleManager'in gun gecisini tetikler.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestCloseAndAdvanceServerRpc()
    {
        if (!IsServer) return;

        IsOpen.Value = false;

        if (dayCycleManager != null)
        {
            dayCycleManager.AdvanceToNextDayServer();
        }
        else
        {
            Debug.LogError("[MarketManager] dayCycleManager atanmamis, sonraki gune gecilemedi.");
        }
    }
}
