using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

/// <summary>
/// GDD Bolum 10.8 mockup (Iflas Ekrani): Gun X Sonu Kaybedildi, Gerekli/Bakiye/Acik gosterimi +
/// [Ana Menuye Don]/[Tekrar Dene] (ARCHITECTURE.md "## Kazanma/Kaybetme Ekranlari" +
/// "## Ortak UI Kiti"). DayCycleManager.CompleteDayServer() bir kota gunu basarisiz olunca
/// (QuotaManager.IsBankrupt) bunu cagirir - bu baglanti T32'den beri MEVCUTTU (T38'deki Gun 18
/// ozel dalinin aksine, burada onceden de dogru calisiyordu).
/// T36 (HUDController) ile AYNI desen: quotaData'ya QuotaManager'in private alanindan degil,
/// buraya ayrica baglanan KENDI referansindan erisilir (QuotaManager'in encapsulation'ina
/// dokunulmadi). Gerekli/Bakiye/Acik degerleri Show() cagrildigi anda YENIDEN hesaplanir -
/// QuotaManager basarisizlikta kesinti YAPMADIGI icin (bkz. QuotaManager.cs) WalletManager.Balance
/// o an hala doğru degeri tasir, ayri bir "son basarisiz kontrol" state'i saklamaya gerek yok.
/// </summary>
public class LoseScreenController : MonoBehaviour
{
    [Header("Veri Kaynaklari")]
    [SerializeField] private QuotaData quotaData;
    [SerializeField] private WalletManager walletManager;

    [Header("Baglantilar")]
    public GameObject panelRoot;
    public TextMeshProUGUI dayFailedText;
    public TextMeshProUGUI requiredText;
    public TextMeshProUGUI balanceText;
    public TextMeshProUGUI deficitText;
    public Button retryButton;
    public Button mainMenuButton;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    /// <summary>
    /// Iflas ekranini acar. failedDay: kaybedilen kota gunu (orn. 6). Gerekli/Bakiye/Acik
    /// degerleri quotaData ve walletManager'dan o an okunur.
    /// </summary>
    public void Show(int failedDay)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        if (dayFailedText != null) dayFailedText.text = "Gün " + failedDay + " Sonu Kaybedildi";

        if (quotaData == null || walletManager == null)
        {
            Debug.LogWarning("[LoseScreenController] quotaData veya walletManager atanmamis, Gerekli/Bakiye/Acik gosterilemiyor.");
            SetMissingDataTexts();
            return;
        }

        if (!quotaData.TryGetQuota(failedDay, out float requiredAmount))
        {
            Debug.LogWarning("[LoseScreenController] Gun " + failedDay + " icin tanimli kota yok.");
            SetMissingDataTexts();
            return;
        }

        float balance = walletManager.Balance.Value;
        float deficit = balance - requiredAmount;

        if (requiredText != null) requiredText.text = "Gerekli: " + requiredAmount.ToString("0") + "$";
        if (balanceText != null) balanceText.text = "Bakiye: " + balance.ToString("0") + "$";
        if (deficitText != null) deficitText.text = "Açık: " + deficit.ToString("0") + "$";
    }

    private void SetMissingDataTexts()
    {
        if (requiredText != null) requiredText.text = "Gerekli: Veri yok";
        if (balanceText != null) balanceText.text = "Bakiye: Veri yok";
        if (deficitText != null) deficitText.text = "Açık: Veri yok";
    }

    /// <summary>[Tekrar Dene] (BlueBtn). BUG DUZELTMESI (kullanici raporu: butonlar hic bir sey
    /// yapmiyordu - eski kod sadece Debug.Log basan kasitli stub'ti, T40/T41 tamamlandigi halde
    /// hic baglanmamisti). Tek-sahne proje oldugu icin (MainMenuController Awake yorumu) hem
    /// Tekrar Dene hem Ana Menuye Don ayni RestartGame() akisini kullanir: NetworkManager kapatilir,
    /// sahne yeniden yuklenir - MainMenuController.Awake() Ana Menu panelini tekrar acar.</summary>
    private void OnRetryClicked()
    {
        Debug.Log("[LoseScreenController] Tekrar Dene tiklandi.");
        RestartGame();
    }

    /// <summary>[Ana Menüye Dön] (GrayBtn). Ayni RestartGame() akisi (bkz. OnRetryClicked yorumu).</summary>
    private void OnMainMenuClicked()
    {
        Debug.Log("[LoseScreenController] Ana Menuye Don tiklandi.");
        RestartGame();
    }

    /// <summary>NetworkManager'i temiz sekilde kapatir (spawned NetworkObject'lerin despawn
    /// edilmeden sahne ile birlikte yok olmasindan kaynaklanabilecek NGO hatalarini onlemek icin)
    /// ve aktif sahneyi yeniden yukler. Tek-sahne projede bu, Ana Menu'ye donmenin tek yolu.</summary>
    private void RestartGame()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
