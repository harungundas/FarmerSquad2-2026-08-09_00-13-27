using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD Bölüm 10.6 market ekranı — NegotiationUI.cs (T25) ile AYNI DESEN: plain MonoBehaviour,
/// MarketManager'ın NetworkVariable'larını (IsOpen, PurchasedMask) OKUYARAK açılıp kapanır,
/// tıklamalar MarketManager'ın ServerRpc'lerini çağırır.
///
/// BASİTLEŞTİRME (T33 kapsamı): GDD mockup'ındaki "Günlük Kâr" / "Otomatik Kira" ayrı satırları
/// gösterilmiyor — bu değerleri hesaplayan bir gelir sayacı sistemi projede henüz yok. Sadece
/// güncel Kasa bakiyesi gösteriliyor.
/// </summary>
public class MarketUI : MonoBehaviour
{
    [Header("Bağlantılar")]
    public MarketManager marketManager;
    public WalletManager walletManager;
    public GameObject panelRoot;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI balanceText;

    [System.Serializable]
    public struct UpgradeButtonRefs
    {
        public Button button;
        public TextMeshProUGUI label;
    }

    [Header("6 upgrade butonu, UpgradeData.upgrades ile AYNI SIRADA (index 0..5)")]
    public UpgradeButtonRefs[] upgradeButtons;

    public Button nextDayButton;

    [Header("KIRMIZI Gunu Bitir butonu (kullanici istegi - kota kontrolu YAPAR)")]
    public Button endDayButton;

    [Header("KULLANICI GERI BILDIRIMI: onizlemeyi gunu bitirmeden kapatan buton (gri/mavi, ClosePreviewServerRpc cagirir)")]
    public Button closeButton;


private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            int index = i; // closure icin lokal kopya
            if (upgradeButtons[i].button != null)
                upgradeButtons[i].button.onClick.AddListener(() => OnBuyClicked(index));
        }

        if (nextDayButton != null) nextDayButton.onClick.AddListener(OnNextDayClicked);
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnEnable()
    {
        if (marketManager != null)
        {
            marketManager.IsOpen.OnValueChanged += OnOpenChanged;
            marketManager.PurchasedMask.OnValueChanged += OnMaskChanged;
        }
        if (walletManager != null)
        {
            walletManager.Balance.OnValueChanged += OnBalanceChanged;
        }
    }

    private void OnDisable()
    {
        if (marketManager != null)
        {
            marketManager.IsOpen.OnValueChanged -= OnOpenChanged;
            marketManager.PurchasedMask.OnValueChanged -= OnMaskChanged;
        }
        if (walletManager != null)
        {
            walletManager.Balance.OnValueChanged -= OnBalanceChanged;
        }
    }

    private void Start()
    {
        if (marketManager != null) Refresh();
    }

    private void OnOpenChanged(bool previous, bool current)
    {
        Refresh();
    }

    private void OnMaskChanged(int previous, int current)
    {
        RefreshButtons();
    }

    private void OnBalanceChanged(float previous, float current)
    {
        RefreshBalance();
    }

    private void Refresh()
    {
        bool open = marketManager != null && marketManager.IsOpen.Value;
        if (panelRoot != null) panelRoot.SetActive(open);
        if (!open) return;

        if (titleText != null) titleText.text = "MARKET - Gün " + marketManager.DayJustCompleted.Value + " Sonu";
        RefreshBalance();
        RefreshButtons();
    }

    private void RefreshBalance()
    {
        if (balanceText != null && walletManager != null)
            balanceText.text = "Kasada: " + walletManager.Balance.Value.ToString("0.##") + "$";
    }

    private void RefreshButtons()
    {
        if (marketManager == null || marketManager.UpgradeCatalog == null) return;

        int mask = marketManager.PurchasedMask.Value;
        var catalog = marketManager.UpgradeCatalog;
        int dayForRotation = marketManager.DayJustCompleted.Value;

        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            if (!catalog.TryGetUpgrade(i, out var entry)) continue;
            bool purchased = (mask & (1 << i)) != 0;
            // T56: Pazar Rotasyonu - bu kota doneminde kilitli mi (satin alinmis olsa bile
            // kilitliyse tekrar gosterge amacli griye alinir, ama zaten satin alinmis olan
            // bir upgrade'in "Alindi" durumu oncelikli gosterilir).
            bool locked = !purchased && MarketManager.IsUpgradeLocked(i, dayForRotation);

            if (upgradeButtons[i].label != null)
            {
                string status;
                if (purchased) status = " (Alındı)";
                else if (locked) status = " (Kilitli)";
                else status = " - " + entry.price.ToString("0.##") + "$";
                upgradeButtons[i].label.text = entry.displayNameTr + status;
            }
            if (upgradeButtons[i].button != null)
                upgradeButtons[i].button.interactable = !purchased && !locked;
        }
    }

    private void OnBuyClicked(int index)
    {
        if (marketManager == null) return;
        marketManager.PurchaseUpgradeServerRpc(index);
    }



/// <summary>KULLANICI GERI BILDIRIMI SONRASI EKLENDI: bilgisayardan acilan onizlemeyi gunu
    /// bitirmeden/upgrade almadan kapatir - MarketManager.ClosePreviewServerRpc SADECE
    /// IsOpen'i false yapar, QuotaManager/DayCycleManager'a hicbir sekilde dokunmaz.</summary>
    private void OnCloseClicked()
    {
        if (marketManager == null) return;
        marketManager.ClosePreviewServerRpc();
    }


    
/// <summary>KULLANICI ISTEGI: kirmizi [Gunu Bitir] butonu kaldirildi, onun islevi (kota
    /// kontrolu YAPAN RequestEndDayServerRpc -> DayCycleManager.CompleteDayServer) bu TEK
    /// kalan butona tasindi. CompleteDayServer artik basarili gunde marketi TEKRAR ACMIYOR,
    /// dogrudan sonraki gune geciyor (bkz. DayCycleManager.cs) - yani bu buton hem kota
    /// kontrolunu YAPAR hem de basariliysa sonraki gune GECER, tek tikla.</summary>
    private void OnNextDayClicked()
    {
        if (marketManager == null) return;
        marketManager.RequestEndDayServerRpc();
    }
}
