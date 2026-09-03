using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GECICI TEST UI (kullanici istegi, 02.09.2026): T53'un backend'i (MarketManager.
/// PurchaseLeveledUpgradeServerRpc / SalesMasteryLevel / BuyMasteryLevel) zaten hazirdi ama
/// hicbir UI'a baglanmamisti - bu script o eksigi kapatir. MarketUI.cs'e DOKUNULMADI, tamamen
/// ayri/ek bir script. Uzun-ince tek satirlik buton: solda isim+fiyat (sol hizali), saginda
/// seviye (sag hizali).
/// </summary>
public class LeveledUpgradeButtonUI : MonoBehaviour
{
    [Header("Baglantilar")]
    public MarketManager marketManager;
    public int upgradeId; // 0 = Satis Ustaligi, 1 = Alim Ustaligi
    public string displayNameTr = "Satış Ustalığı";

    public Button button;
    public TextMeshProUGUI leftLabel;  // isim + fiyat
    public TextMeshProUGUI rightLabel; // seviye

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClicked);
    }

    private void OnEnable()
    {
        if (marketManager != null)
        {
            if (upgradeId == MarketManager.SalesMasteryId)
                marketManager.SalesMasteryLevel.OnValueChanged += OnLevelChanged;
            else
                marketManager.BuyMasteryLevel.OnValueChanged += OnLevelChanged;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (marketManager != null)
        {
            if (upgradeId == MarketManager.SalesMasteryId)
                marketManager.SalesMasteryLevel.OnValueChanged -= OnLevelChanged;
            else
                marketManager.BuyMasteryLevel.OnValueChanged -= OnLevelChanged;
        }
    }

    private void OnLevelChanged(int previous, int current)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (marketManager == null) return;

        int level = marketManager.GetLeveledUpgradeLevel(upgradeId);
        int nextLevel = level + 1;
        float cost = MarketManager.GetLeveledUpgradeCost(nextLevel);

        if (leftLabel != null)
            leftLabel.text = displayNameTr + " - " + cost.ToString("0.##") + "$";

        if (rightLabel != null)
            rightLabel.text = "Seviye " + level;
    }

    private void OnClicked()
    {
        if (marketManager == null) return;
        marketManager.PurchaseLeveledUpgradeServerRpc(upgradeId);
    }
}
