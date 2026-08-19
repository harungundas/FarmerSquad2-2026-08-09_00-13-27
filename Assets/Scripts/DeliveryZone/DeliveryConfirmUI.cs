using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KULLANICI GERI BILDIRIMI SONRASI EKLENDI: StandInteraction'da AwaitingDelivery
/// asamasinda F'e basmak ESKIDEN NegotiationManager.RequestFinalizeDeliveryServerRpc()'yi
/// DOGRUDAN cagiriyordu - yanlislikla kasaya F'e basmak (ya da siparisi hatirlamadan sadece
/// kontrol etmek istemek) bile teslimati aninda SONUCLANDIRIYORDU, geri alinamiyordu.
///
/// Bu script StandInteraction ile RequestFinalizeDeliveryServerRpc arasina bir ONAY EKRANI
/// sokar. F artik bu paneli acar (Show()), RequestFinalizeDeliveryServerRpc SADECE panelde
/// [Teslim Et] tiklaninca cagirilir:
/// - [Teslim Et] (Green) -> RequestFinalizeDeliveryServerRpc() cagrilir, panel kapanir.
/// - [Iptal Et] (Red) veya ESC -> hicbir state degismez, panel kapanir, NegotiationState
///   AwaitingDelivery'de kalmaya devam eder (arac beklemeye devam eder, oyuncu istedigi an
///   tekrar F ile acabilir).
///
/// Panel acikken DeliveryZoneDetector'dan CANLI okunan dogru/yanlis durumu (yesil/kirmizi)
/// gosterilir - oyuncu hayvani alana koyup cikarsa panel acikken bile guncellenir.
///
/// SADECE LOKAL bir UI durumu: Show/Hide ag uzerinden senkronize edilmez (her oyuncu kendi
/// ekraninda acar/kapatir), yalnizca [Teslim Et] tiklaninca sunucuya gercek istek gider.
/// </summary>
public class DeliveryConfirmUI : MonoBehaviour
{
    public static DeliveryConfirmUI Instance { get; private set; }

    [Header("Bağlantılar")]
    public NegotiationManager negotiationManager;
    public DeliveryZoneDetector deliveryZoneDetector;
    public GameObject panelRoot;
    public TextMeshProUGUI orderText;
    public TextMeshProUGUI statusText;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Durum Renkleri (statusText icin)")]
    public Color correctColor = new Color(0.25f, 0.7f, 0.3f);
    public Color wrongColor = new Color(0.8f, 0.25f, 0.2f);

    private void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);

        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    /// <summary>StandInteraction, AwaitingDelivery asamasinda F'e basilinca bunu cagirir.</summary>
    public void Show()
    {
        if (negotiationManager == null || negotiationManager.State.Value.stage != NegotiationStage.AwaitingDelivery)
        {
            Debug.LogWarning("[DeliveryConfirmUI] Show() cagirildi ama AwaitingDelivery asamasinda degil, gormezden geliniyor.");
            return;
        }
        if (panelRoot != null) panelRoot.SetActive(true);
        RefreshLiveStatus();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

        RefreshLiveStatus();

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            OnCancelClicked();
        }
    }

    private void RefreshLiveStatus()
    {
        if (negotiationManager == null) return;
        var s = negotiationManager.State.Value;

        // Baska bir yerden (orn. arac zaman asimi) state degismis olabilir - panel artik
        // gecersiz, kapat.
        if (s.stage != NegotiationStage.AwaitingDelivery)
        {
            Hide();
            return;
        }

        if (orderText != null)
            orderText.text = "SİPARİŞ: " + s.count + "x " + SpeciesToTurkish(s.species) + " (Satış)";

        bool correct = false;
        int matchingCount = 0;
        int totalInside = 0;
        if (deliveryZoneDetector != null)
        {
            var inside = deliveryZoneDetector.AnimalsInside;
            totalInside = inside.Count;
            for (int i = 0; i < inside.Count; i++)
            {
                var animal = inside[i];
                if (animal != null && animal.animalData != null && animal.animalData.species == s.species) matchingCount++;
            }
            correct = matchingCount == s.count && totalInside == s.count;
        }

        Color c = correct ? correctColor : wrongColor;
        if (statusText != null)
        {
            statusText.color = c;
            statusText.text = correct
                ? "Doğru teslimat hazır (" + matchingCount + "/" + s.count + ")"
                : "Eksik/yanlış! Alanda: " + totalInside + " (doğru tür: " + matchingCount + "/" + s.count + ")";
        }
    }

    private void OnConfirmClicked()
    {
        if (negotiationManager != null) negotiationManager.RequestFinalizeDeliveryServerRpc();
        Hide();
    }

    private void OnCancelClicked()
    {
        Hide();
    }

    private string SpeciesToTurkish(AnimalSpecies species)
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
