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

    private void OnEnable()
    {
        if (walletManager != null)
        {
            walletManager.Balance.OnValueChanged += OnBalanceChanged;
        }

        if (dayCycleManager != null)
        {
            dayCycleManager.Timer.OnValueChanged += OnTimerChanged;
            dayCycleManager.CurrentDay.OnValueChanged += OnDayChanged;
            dayCycleManager.IsFreeMode.OnValueChanged += OnFreeModeChanged;
        }
    }

    private void OnDisable()
    {
        if (walletManager != null)
        {
            walletManager.Balance.OnValueChanged -= OnBalanceChanged;
        }

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

    private void OnBalanceChanged(float previous, float current)
    {
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

        bool freeMode = dayCycleManager.IsFreeMode.Value;
        float timer = dayCycleManager.Timer.Value;
        float window = dayCycleManager.customerWindowSeconds;

        if (timerText != null)
        {
            timerText.text = freeMode
                ? "Müşteri Gelişi Kapalı - Serbest Mod"
                : "Müşteri Gelişi: " + Mathf.CeilToInt(Mathf.Max(0f, timer)) + " sn";
        }

        if (timerFillBar != null)
        {
            timerFillBar.fillAmount = (!freeMode && window > 0f) ? Mathf.Clamp01(timer / window) : 0f;
        }
    }

    private void RefreshWalletQuota()
    {
        if (walletQuotaText == null || walletManager == null) return;

        float balance = walletManager.Balance.Value;

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
}
