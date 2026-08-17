using UnityEngine;
using TMPro;

/// <summary>
/// Ekranda kisa bir etkilesim ipucu gosterir (orn. "E - Tasi", ileride "F - Etkilesime Gir").
/// HUDController.Instance ile ayni singleton deseni: her client kendi local Instance'ina erisir.
/// Tek seferde tek ipucu gosterilir - birden fazla kaynak ayni anda cagirirsa sonuncusu kazanir
/// (bu prototip asamasi icin yeterli, coklu-kaynak onceliklendirme gerekmiyor).
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance;

    [Header("UI Referanslari")]
    public GameObject promptPanel;
    public TextMeshProUGUI promptText;

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    public void Show(string text)
    {
        if (promptText != null) promptText.text = text;
        if (promptPanel != null) promptPanel.SetActive(true);
    }

    public void Hide()
    {
        if (promptPanel != null) promptPanel.SetActive(false);
    }
}
