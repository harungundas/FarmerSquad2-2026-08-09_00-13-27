using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD Bolum 10.7 mockup (Basari Ekrani): Basari Orani, Toplam Hayvan Satisi, Toplam Kar,
/// Hata Sayisi, Pazarlik Basarisi + [New Game+]/[Ana Menuye Don] (ARCHITECTURE.md
/// "## Kazanma/Kaybetme Ekranlari" + "## Ortak UI Kiti").
/// DayCycleManager.CompleteDayServer() (T32 baglantisi bu T38 sohbetinde EKLENDI - onceden
/// HICBIR yerde Gun 18 icin ozel bir dal yoktu, her basarili gun MarketManager'i aciyordu,
/// bkz. HANDOFF.md T38 notu) Gun 18 kotasi basariyla gecilince Show() cagirir.
/// NegotiationUI (T25) ile ayni desen: sahnedeki panel/text/button referanslari Inspector'dan
/// baglanir, bu script sadece OKUR/tetikler - runtime'da UI kurmaz.
/// </summary>
public class WinScreenController : MonoBehaviour
{
    [Header("Baglantilar")]
    public GameObject panelRoot;
    public TextMeshProUGUI successRateText;
    public TextMeshProUGUI totalAnimalsSoldText;
    public TextMeshProUGUI totalProfitText;
    public TextMeshProUGUI errorCountText;
    public TextMeshProUGUI negotiationSuccessText;
    public Button newGamePlusButton;
    public Button mainMenuButton;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (newGamePlusButton != null) newGamePlusButton.onClick.AddListener(OnNewGamePlusClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    /// <summary>
    /// Basari ekranini acar ve verilen GameStatsTracker'daki (T38'de eklendi) NetworkVariable
    /// degerlerinden 5 istatistigi hesaplayip yazar. stats null gelirse (henuz sahneye
    /// baglanmadiysa) sadece paneli acip "Veri yok" gosterir, exception atmaz.
    /// </summary>
    public void Show(GameStatsTracker stats)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        if (stats == null)
        {
            Debug.LogWarning("[WinScreenController] GameStatsTracker atanmamis, istatistiksiz gosteriliyor.");
            SetAllTexts("Veri yok");
            return;
        }

        int successCount = stats.SuccessfulDeliveries.Value;
        int wrongCount = stats.WrongDeliveries.Value;
        int totalDeliveries = successCount + wrongCount;
        float successRate = totalDeliveries > 0 ? (successCount / (float)totalDeliveries) * 100f : 100f;

        int attempted = stats.NegotiationsAttempted.Value;
        int succeeded = stats.NegotiationsSucceeded.Value;
        float negotiationRate = attempted > 0 ? (succeeded / (float)attempted) * 100f : 0f;

        if (successRateText != null) successRateText.text = "Başarı Oranı: " + successRate.ToString("0") + "%";
        if (totalAnimalsSoldText != null) totalAnimalsSoldText.text = "Toplam Hayvan Satışı: " + stats.TotalAnimalsSold.Value;
        if (totalProfitText != null) totalProfitText.text = "Toplam Kâr: " + stats.TotalProfit.Value.ToString("0") + "$";
        if (errorCountText != null) errorCountText.text = "Hata Sayısı: " + wrongCount + " (yanlış teslimat)";
        if (negotiationSuccessText != null)
            negotiationSuccessText.text = "Pazarlık Başarısı: " + succeeded + "/" + attempted + " (%" + negotiationRate.ToString("0") + ")";
    }

    private void SetAllTexts(string message)
    {
        if (successRateText != null) successRateText.text = message;
        if (totalAnimalsSoldText != null) totalAnimalsSoldText.text = message;
        if (totalProfitText != null) totalProfitText.text = message;
        if (errorCountText != null) errorCountText.text = message;
        if (negotiationSuccessText != null) negotiationSuccessText.text = message;
    }

    /// <summary>
    /// [New Game+] (BlueBtn). Gercek sahne/oyun yeniden baslatma baglantisi Ana Menu & Lobi
    /// UI'ya (T40/T41) bagli - bu task'in kapsami DEGIL, kasitli stub.
    /// </summary>
    private void OnNewGamePlusClicked()
    {
        Debug.Log("[WinScreenController] New Game+ tiklandi - sahne yeniden baslatma T40/T41'e bagli, henuz kurulu degil.");
    }

    /// <summary>[Ana Menüye Dön] (GrayBtn). Ayni sekilde T40'a bagli, kasitli stub.</summary>
    private void OnMainMenuClicked()
    {
        Debug.Log("[WinScreenController] Ana Menuye Don tiklandi - Ana Menu sahne gecisi T40'a bagli, henuz kurulu degil.");
    }
}
