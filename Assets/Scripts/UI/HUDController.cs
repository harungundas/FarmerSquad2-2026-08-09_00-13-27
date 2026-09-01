using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ARCHITECTURE.md "## HUD". Sol üst (Gün/Kota), sol alt (240sn timer bar), sağ üst
/// (Kasa/Kota hedefi) - GDD Bölüm 10.3 mockup.
/// WalletManager.Balance ve DayCycleManager.Timer/CurrentDay/IsFreeMode'a OnValueChanged ile
/// abone olunur (bu projede yerleşik desen - bkz. WalletManager.cs OnBalanceChanged,
/// DayCycleManager.cs OnFreeModeChanged örnekleri). NetworkVariable.Value her değiştiğinde
/// (host'ta Update() içinde Timer.Value -= Time.deltaTime dahil) callback anında tetiklenir,
/// bu yüzden HUDController ayrıca poll (Update()) YAPMAZ - sadece event'lere abone olur.
///
/// Sipariş gösteriminde hayvan ikonu YOK (pakette sadece ekmek/un/buğday ikonu var, T37'de
/// dinamik uyarılar için de aynı yaklaşım tekrarlanacak) - bu task ikon içermiyor zaten.
///
/// NOT (bilinçli tasarım): Kota hedefi QuotaManager'ın İÇİNDEN OKUNMUYOR - QuotaManager.quotaData
/// alanı private (T30, değiştirilmedi). Bunun yerine HUDController kendi public QuotaData
/// alanına (Inspector'dan AYNI QuotaData asset'i bağlanır: Assets/Data/Economy/QuotaData.asset)
/// doğrudan bakıp "bugüne eşit veya sonraki ilk kota günü" mantığıyla hedefi hesaplar.
///
/// BUG FIX (kullanıcı raporu: "HUD'lar lobideki UI'ların üstüne çıkıyor"): HUDCanvas'ın
/// TopLeftPanel/BottomLeftPanel/TopRightPanel'i öteden beri hep aktifti, hiçbir gizleme mantığı
/// yoktu - bu yüzden MainMenu/Lobby ekranlarının üstüne çiziliyordu. Diğer ekranlarla (LobbyUI,
/// WinScreenController, LoseScreenController, MarketUI) AYNI Show()/Hide() deseni eklendi.
/// Gerçek oyun başlama akışı (T48/T49) henüz yok, bu yüzden Awake()'te varsayılan olarak
/// Hide() çağrılır - HUD, oyun gerçekten başladığında (ileride) Show() ile açılacak.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Bağlı Sistemler")]
    public WalletManager walletManager;
    public DayCycleManager dayCycleManager;
    [Tooltip("QuotaManager'daki ile AYNI asset: Assets/Data/Economy/QuotaData.asset")]
    public QuotaData quotaData;

    [Header("Sol Üst - Gün / Kota Günü")]
    public TextMeshProUGUI dayQuotaText;

    [Header("Sol Alt - 240sn Müşteri Gelişi Timer'ı")]
    public TextMeshProUGUI timerText;
    [Tooltip("Image, Image Type = Filled olmalı. fillAmount = kalan süre / customerWindowSeconds.")]
    public Image timerFillBar;

    [Header("Sağ Üst - Kasa / Kota Hedefi")]
    public TextMeshProUGUI walletQuotaText;

    [Header("Dinamik Uyarılar (T37)")]
    [Tooltip("Geçici mesajlar için (\"Araç geldi!\", \"Yanlış hayvan teslimat alanına konuldu!\" gibi). Başlangıçta kapalı olmalı.")]
    public TextMeshProUGUI alertText;
    [Tooltip("Opsiyonel: alertText'in arka plan paneli (panel_1.png). Atanmışsa gizleme/gösterme BUNUN uzerinden yapılır (metin + arka plan birlikte); atanmamışsa sadece alertText.gameObject kullanılır.")]
    public GameObject alertPanel;

    [Header("Görünürlük (Bug fix: HUD paneli varsayılan olarak her zaman açıktı, Lobi/MainMenu gibi diğer ekranların üstüne çıkıyordu)")]
    [Tooltip("Sol üst / sol alt / sağ üst panelleri - Show()/Hide() ile birlikte açılıp kapanır. AlertPanel BURAYA DAHİL DEĞİL, o kendi SetAlertVisible mantığını kullanmaya devam eder.")]
    public GameObject[] hudPanels;

    [Header("Lobiye Don / Cikis (kullanici istegi)")]
    public Button exitButton;
    public GameObject mainMenuPanelRoot;
    [Tooltip("KULLANICI ISTEGI: Cikis butonuna basinca direkt cikmak yerine once onay sorulsun. LobbyUI.forceStartModalPanel ile AYNI desen.")]
    public GameObject exitConfirmModalPanel;
    public Button exitConfirmButton;
    public Button exitCancelButton;

    /// <summary>
    /// Sahnede tek bir HUDController varsayılır (Faz 9). VehicleSpawner ve NegotiationManager
    /// gibi ağ scriptleri Inspector referansı yerine bu static Instance üzerinden ShowAlert
    /// çağırır - her client'ın KENDİ local HUDController'ına ClientRpc ile ulaşılır.
    /// </summary>
    public static HUDController Instance;

    private void Awake()
    {
        Instance = this;
        if (exitButton != null) exitButton.onClick.AddListener(OnExitButtonClicked);
        if (exitConfirmButton != null) exitConfirmButton.onClick.AddListener(OnExitConfirmed);
        if (exitCancelButton != null) exitCancelButton.onClick.AddListener(OnExitCancelled);
        if (exitConfirmModalPanel != null) exitConfirmModalPanel.SetActive(false);
        SetAlertVisible(false);
        Hide(); // Bug fix: sahne açılışında HUD'un Lobi/MainMenu üstüne çıkmasını engeller - gerçek oyun başlayınca (T48/T49 start-game akışında) Show() çağrılacak.
    }

    private void OnEnable()
    {
        // T75: Balance.OnValueChanged'e bagli anlik (zipla-yazi) guncelleme KALDIRILDI -
        // artik MoneyFeedbackController, WalletManager.OnTransactionNotified'i dinleyip
        // SetWalletDisplayValue() uzerinden ~0.4sn'de sayarak guncelliyor. Burada instant
        // subscription tutulursa MoneyFeedbackController'in animasyonuyla ayni frame'de
        // yazi icin yaris durumu (race) olusur. Balance her zaman ayni sunucu metodu icinde
        // NotifyTransactionClientRpc ile birlikte degistigi icin (WalletManager.cs) bu
        // guncellemeyi kacirmiyoruz - sadece kim yaziyor sorumlulugu degisti.
        if (dayCycleManager != null)
        {
            dayCycleManager.Timer.OnValueChanged += OnTimerChanged;
            dayCycleManager.CurrentDay.OnValueChanged += OnDayChanged;
            dayCycleManager.IsFreeMode.OnValueChanged += OnFreeModeChanged;
        }
    }

    private void OnDisable()
    {
        if (dayCycleManager != null)
        {
            dayCycleManager.Timer.OnValueChanged -= OnTimerChanged;
            dayCycleManager.CurrentDay.OnValueChanged -= OnDayChanged;
            dayCycleManager.IsFreeMode.OnValueChanged -= OnFreeModeChanged;
        }
    }

    private void Start()
    {
        // Sahne yüklenir yüklenmez mevcut (belki henüz spawn olmamış, o zaman default) değerlerle
        // ilk çizimi yap - ilk gerçek NetworkVariable değişikliği geldiğinde zaten güncellenecek.
        RefreshDayQuota();
        RefreshTimer();
        RefreshWalletQuota();
    }

    private void OnTimerChanged(float previous, float current)
    {
        RefreshTimer();
    }

    private void OnDayChanged(int previous, int current)
    {
        RefreshDayQuota();
        RefreshWalletQuota();
    }

    private void OnFreeModeChanged(bool previous, bool current)
    {
        RefreshTimer();
    }

    private void RefreshDayQuota()
    {
        if (dayQuotaText == null || dayCycleManager == null) return;

        int currentDay = dayCycleManager.CurrentDay.Value;
        int nextQuotaDay = GetNextQuotaDay(currentDay);

        dayQuotaText.text = "Gün: " + currentDay + " / 18\nKota Günü: " + nextQuotaDay;
    }

private void RefreshTimer()
    {
        if (dayCycleManager == null) return;

        // DEGISIKLIK (kullanici karari): ham "X sn" sayaci yerine, 240sn'lik musteri gelisi
        // penceresini 06:00-18:00 arasi 12 oyun-ici saatine oranla eslestiren dijital saat
        // gosterimi ("09:45" gibi). TimerFillBar KALDIRILDI (kullanici karari) - sadece metin var.
        bool freeMode = dayCycleManager.IsFreeMode.Value;

        if (timerText != null)
        {
            if (freeMode)
            {
                timerText.text = "18:00 - Serbest Mod";
            }
            else
            {
                float window = dayCycleManager.customerWindowSeconds;
                float timer = dayCycleManager.Timer.Value;
                float elapsed = Mathf.Clamp(window - timer, 0f, window);
                float progress = window > 0f ? elapsed / window : 0f;

                float totalGameMinutes = progress * 12f * 60f; // 06:00 -> 18:00 = 12 saat = 720 dakika
                int gameHour = 6 + Mathf.FloorToInt(totalGameMinutes / 60f);
                int gameMinute = Mathf.FloorToInt(totalGameMinutes % 60f);

                timerText.text = gameHour.ToString("00") + ":" + gameMinute.ToString("00");
            }
        }
    }

    private void RefreshWalletQuota()
    {
        if (walletManager == null) return;
        SetWalletDisplayValue(walletManager.Balance.Value);
    }

    /// <summary>
    /// T75: RefreshWalletQuota'nin ayni formatlama mantigi disari acildi. MoneyFeedbackController
    /// bunu, WalletManager.Balance.Value yerine kendi animasyonlu ara-deger (displayedValue)
    /// ile her frame cagirir - boylece kota hedefi ("/ Y$ (Kota)") kismi hep dogru kalirken
    /// sadece bakiye sayisi sayarak degisir. Bu metot bagimsiz cagrilabilir olmali (balance
    /// parametresi disaridan verilir), WalletManager'a direkt bakmaz.
    /// </summary>
    public void SetWalletDisplayValue(float balance)
    {
        if (walletQuotaText == null) return;

        if (quotaData != null && dayCycleManager != null)
        {
            int nextQuotaDay = GetNextQuotaDay(dayCycleManager.CurrentDay.Value);
            if (quotaData.TryGetQuota(nextQuotaDay, out float requiredAmount))
            {
                walletQuotaText.text = "Kasada: " + balance.ToString("0.##") + "$ / " + requiredAmount.ToString("0.##") + "$ (Kota)";
                return;
            }
        }

        walletQuotaText.text = "Kasada: " + balance.ToString("0.##") + "$";
    }

    /// <summary>
    /// GDD Bölüm 5 kota günleri (3/6/9/12/15/18) içinden bugüne eşit veya sonraki ilkini
    /// döndürür. Tüm kotalar geçildiyse (currentDay &gt; 18) son kota gününü (18) döndürür.
    /// </summary>
    private int GetNextQuotaDay(int currentDay)
    {
        if (quotaData == null || quotaData.quotas == null || quotaData.quotas.Length == 0)
        {
            return currentDay;
        }

        int best = -1;
        foreach (var entry in quotaData.quotas)
        {
            if (entry.day >= currentDay && (best == -1 || entry.day < best))
            {
                best = entry.day;
            }
        }

        return best != -1 ? best : quotaData.quotas[quotaData.quotas.Length - 1].day;
    }

    /// <summary>Gerçek oyun başladığında (T48/T49 start-game akışı) çağrılacak - HUD panellerini
    /// gösterir. AlertPanel bunun dışında, kendi ShowAlert/HideAlert mantığıyla ayrı yönetilir.</summary>
    public void Show()
    {
        if (hudPanels == null) return;
        foreach (var panel in hudPanels)
        {
            if (panel != null) panel.SetActive(true);
        }
    }

    /// <summary>Lobi/MainMenu/Market/Kazanma-Kaybetme ekranları açıkken HUD'un üstlerine
    /// çıkmaması için çağrılır. Awake()'te varsayılan olarak çağrılır.</summary>
    public void Hide()
    {
        if (hudPanels == null) return;
        foreach (var panel in hudPanels)
        {
            if (panel != null) panel.SetActive(false);
        }
    }

/// <summary>KULLANICI ISTEGI: HUD'daki Ayarlar butonunun yanina eklenen Cikis butonu.
    /// Su anki proje durumunda (LobbySessionManager sadece lokal/sahte lobi kaydi tutuyor,
    /// gercek Steam/NGO baglantisi henuz yok - bkz. LobbySessionManager.cs ust yorumu) bu,
    /// LobbyUI.OnLeaveClicked ile AYNI deseni izler: lobi kaydini birakir, HUD'u kapatir,
    /// Ana Menu'yu acar. NOT: gun/kasa/kota durumu SIFIRLANMAZ - tekrar [Lobi Olustur] ile
    /// yeni oyun baslatildiginda kaldigi yerden devam eder (DayCycleManager'da ayri bir
    /// "oyunu sifirla" mekanizmasi henuz yok, bu ayri bir gorev).</summary>
    /// <summary>KULLANICI ISTEGI: Cikis butonuna basinca ARTIK direkt cikmiyor - once onay
    /// modali aciliyor (LobbyUI.forceStartModalPanel ile ayni desen). Gercek cikis islemi
    /// OnExitConfirmed()'e tasindi.</summary>
    private void OnExitButtonClicked()
    {
        if (exitConfirmModalPanel != null)
        {
            exitConfirmModalPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[HUDController] exitConfirmModalPanel atanmamis - guvenlik icin dogrudan cikilmiyor. Sahnede modal olusturulup atanmali.");
        }
    }

    /// <summary>Onay modalindaki [Evet, Çık] butonu. Gercek cikis islemini yapar.</summary>
    private void OnExitConfirmed()
    {
        if (exitConfirmModalPanel != null) exitConfirmModalPanel.SetActive(false);

        LobbySessionManager.LeaveLobby();
        Hide();

        if (mainMenuPanelRoot != null)
        {
            mainMenuPanelRoot.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[HUDController] mainMenuPanelRoot atanmamis - Cikis sonrasi Ana Menuye donulemedi.");
        }

        Debug.Log("[HUDController] Cikis onaylandi: LobbySessionManager.LeaveLobby() cagirildi, Ana Menuye donuluyor.");
    }

    /// <summary>Onay modalindaki [Vazgeç] butonu. Modali kapatir, hicbir sey yapmaz.</summary>
    private void OnExitCancelled()
    {
        if (exitConfirmModalPanel != null) exitConfirmModalPanel.SetActive(false);
    }


    /// <summary>
    /// T37: Geçici bir uyarı mesajı gösterir, `duration` saniye sonra otomatik gizler.
    /// Coroutine/animasyon YOK (basit tutulmalı, TASKS.md T37 notu) - CancelInvoke+Invoke yeterli.
    /// alertText Inspector'dan bağlanmamışsa sessizce hiçbir şey yapmaz (Debug.LogWarning ile uyarır).
    /// </summary>
    public void ShowAlert(string message, float duration)
    {
        if (alertText == null)
        {
            Debug.LogWarning("[HUDController] alertText atanmamis, uyari gosterilemedi: " + message);
            return;
        }

        CancelInvoke(nameof(HideAlert));
        alertText.text = message;
        SetAlertVisible(true);
        Invoke(nameof(HideAlert), duration);
    }

    private void HideAlert()
    {
        SetAlertVisible(false);
    }

    private void SetAlertVisible(bool visible)
    {
        if (alertPanel != null) alertPanel.SetActive(visible);
        else if (alertText != null) alertText.gameObject.SetActive(visible);
    }

    /// <summary>T37: VehicleSpawner'in ClientRpc'si bunu cagirir - "Arac geldi! Siparis: 2x Tavuk" gibi.
    /// KULLANICI BUG RAPORU DUZELTMESI: eskiden yon (Satis/Alim) bu uyarida hic belirtilmiyordu,
    /// oyuncu aracin SATMAK mi yoksa SATIN ALMAK mi istedigini anlayamiyordu. Artik yon ACIKCA
    /// yazilir: Satis = musteri bizden hayvan ALMAK istiyor (biz satiyoruz), Alim = musteri bize
    /// hayvan SATMAK istiyor (biz satin aliyoruz).</summary>
    public void ShowVehicleArrivedAlert(AnimalSpecies species, int count, OrderDirection direction)
    {
        string msg = direction == OrderDirection.Satis
            ? "Araç geldi! Müşteri " + count + "x " + TurkishSpeciesName(species) + " SATIN ALMAK istiyor (SATIŞ)"
            : "Araç geldi! Müşteri " + count + "x " + TurkishSpeciesName(species) + " SATMAK istiyor (ALIM)";
        ShowAlert(msg, 4f);
    }

    /// <summary>T37: NegotiationManager'in ClientRpc'si bunu cagirir (yanlis/eksik teslimat).</summary>
    public void ShowWrongDeliveryAlert()
    {
        ShowAlert("Yanlış hayvan teslimat alanına konuldu!", 4f);
    }

    /// <summary>
    /// AnimalSpecies enum'u (Chicken/Sheep/Goat/Cow/Horse, ingilizce) - bu projede baska hicbir
    /// yerde Turkce isim eslemesi yok (AnimalData.cs sadece enum'u tutuyor), bu yuzden HUD
    /// katmaninda (kullaniciya gorunen tek yer) burada tanimlandi.
    /// </summary>
    private string TurkishSpeciesName(AnimalSpecies species)
    {
        switch (species)
        {
            case AnimalSpecies.Chicken: return "Tavuk";
            case AnimalSpecies.Sheep: return "Koyun";
            case AnimalSpecies.Goat: return "Keçi";
            case AnimalSpecies.Cow: return "İnek";
            case AnimalSpecies.Horse: return "At";
            default: return species.ToString();
        }
    }
}
