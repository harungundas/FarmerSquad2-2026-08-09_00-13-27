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
    public void ClosePreviewServerRpc()
    {
        if (!IsServer) return;
        IsOpen.Value = false;
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

        walletManager.SubtractBalanceServerRpc(entry.price, TransactionReason.Harcama);
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



    // ===== T53: Leveled Upgrades (Satis/Alim Ustaligi) — mevcut PurchasedMask sisteminden TAMAMEN AYRI =====
    // NOT (kucuk sapma, HANDOFF'a loglanacak): TASKS.md spec'i "NetworkVariable<int>[] (2 eleman)" istiyordu,
    // ama Unity Netcode'un ILPP tabanli otomatik degisken kaydi sadece DOGRUDAN NetworkVariable<T> alanlarini
    // tarar - bir array'in ICINDEKI NetworkVariable elemanlarini guvenilir sekilde network-sync ETMEYEBILIR.
    // Garantili senkron icin iki AYRI adlandirilmis NetworkVariable<int> alani kullanildi; disariya (T54/T55,
    // UI) upgradeId (0/1) ile erisim GetLeveledUpgradeLevel() uzerinden ayni sekilde sunuluyor - davranissal
    // fark yok, sadece ic implementasyon NGO-guvenli hale getirildi.
    public const int SalesMasteryId = 0;
    public const int BuyMasteryId = 1;
    private const float LeveledUpgradeBaseCost = 25f;
    private const float LeveledUpgradeCostMultiplier = 1.4f;

    /// <summary>Satis Ustaligi seviyesi (upgradeId=0). Her seviye T54'te satis fiyatina +%4 ekler.</summary>
    public NetworkVariable<int> SalesMasteryLevel = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Alim Ustaligi seviyesi (upgradeId=1). Her seviye T55'te alim fiyatini -%4 dusurur.</summary>
    public NetworkVariable<int> BuyMasteryLevel = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int GetLeveledUpgradeLevel(int upgradeId)
    {
        if (upgradeId == SalesMasteryId) return SalesMasteryLevel.Value;
        if (upgradeId == BuyMasteryId) return BuyMasteryLevel.Value;
        Debug.LogWarning("[MarketManager] Gecersiz leveled upgradeId: " + upgradeId);
        return 0;
    }

    /// <summary>cost(n) = 25 * 1.4^(n-1), n = satin alinacak YENI seviye (1-tabanli).</summary>
    public static float GetLeveledUpgradeCost(int newLevel)
    {
        return LeveledUpgradeBaseCost * Mathf.Pow(LeveledUpgradeCostMultiplier, newLevel - 1);
    }

    /// <summary>
    /// T53: Jenerik leveled-upgrade satin alma. Mevcut PurchaseUpgradeServerRpc (bitmask, tek
    /// seferlik) sisteminden TAMAMEN AYRI - o sistemi etkilemez. Tekrar tekrar cagrilabilir,
    /// her cagrida bir seviye artar, maliyet katlanarak buyur (1.4x).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void PurchaseLeveledUpgradeServerRpc(int upgradeId)
    {
        if (!IsServer) return;

        if (upgradeId != SalesMasteryId && upgradeId != BuyMasteryId)
        {
            Debug.LogWarning("[MarketManager] PurchaseLeveledUpgradeServerRpc: gecersiz upgradeId " + upgradeId);
            return;
        }

        if (walletManager == null)
        {
            Debug.LogError("[MarketManager] walletManager atanmamis, leveled upgrade satin alinamadi.");
            return;
        }

        int currentLevel = GetLeveledUpgradeLevel(upgradeId);
        int newLevel = currentLevel + 1;
        float cost = GetLeveledUpgradeCost(newLevel);

        if (walletManager.Balance.Value < cost)
        {
            Debug.Log("[MarketManager] Yetersiz bakiye: leveled upgrade (id=" + upgradeId + ") seviye " +
                       newLevel + " icin " + cost + "$ gerekli, mevcut bakiye " + walletManager.Balance.Value + "$.");
            return;
        }

        walletManager.SubtractBalanceServerRpc(cost, TransactionReason.Harcama);

        if (upgradeId == SalesMasteryId) SalesMasteryLevel.Value = newLevel;
        else BuyMasteryLevel.Value = newLevel;

        Debug.Log("[MarketManager] Leveled upgrade satin alindi: id=" + upgradeId + " -> seviye " + newLevel +
                   " (" + cost + "$).");
    }
}
