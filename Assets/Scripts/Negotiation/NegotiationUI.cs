using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Globalization;

/// <summary>
/// GDD Bolum 10.4 pazarlik modali + kullanicinin kendi cizdigi mockup (numpad ile serbest
/// teklif girisi). NegotiationManager.State (NetworkVariable) OKUYARAK acilip kapanir,
/// buton/numpad tiklamalari NegotiationManager'in ServerRpc'lerini cagirir.
///
/// KULLANICI GERI BILDIRIMI SONRASI (T25'in ilk halinden) DEGISEN TASARIM:
/// - Offered asamasinda artik SABIT bir formul yok: oyuncu numpad ile (1-9,0,.,<) bir sayi
///   yazar, bu sayi PlayerInputText'te canli gosterilir, [Pazarlik Yap] bu sayiyi
///   RequestNegotiateServerRpc(float) ile sunucuya gonderir.
/// - [Reddet] artik Offered asamasinda da RequestRejectBaseServerRpc cagiriyor (eskiden
///   ResetNegotiationServerRpc cagriliyordu - bu, musterinin sonucu hic ogrenmeden sessizce
///   Inactive'e donmesine sebep oluyordu, aracin da hic ayrilmamasina yol aciyordu).
/// - AwaitingDelivery asamasinda bu panel GORUNMEZ (oyuncu artik hayvanlari teslimat alanina
///   tasimaya gidiyor - StandInteraction, kasaya donup tekrar F'e basilinca finalize akisini
///   tetikler, bu panelin isi degil).
/// - UI boyutu buyutuldu (kullanici "biraz daha buyuk olabilir" dedi).
/// </summary>
public class NegotiationUI : MonoBehaviour
{
    [Header("Bağlantılar")]
    public NegotiationManager negotiationManager;
    public GameObject panelRoot;
    public TextMeshProUGUI orderText;
    public TextMeshProUGUI offerText;
    public TextMeshProUGUI riskText;
    public TextMeshProUGUI playerInputText;
    public Button negotiateButton;
    public Button acceptButton;
    public Button rejectButton;

    [Header("Numpad (child'lari GrayBtn instance'lari, her birinin TMP Text'i '1'..'9','0','.','<' olmali)")]
    public Transform numpadContainer;

    private string typedValue = "";

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (negotiateButton != null) negotiateButton.onClick.AddListener(OnNegotiateClicked);
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAcceptClicked);
        if (rejectButton != null) rejectButton.onClick.AddListener(OnRejectClicked);

        SetupNumpad();
    }

    /// <summary>
    /// Numpad butonlarina TEK TEK onClick baglamak yerine (12 farkli MCP cagrisi gerektirirdi),
    /// her buton child'inin TMP metnini ("1".."9","0",".","<") okuyup generik OnNumpadKey(key)'e
    /// yonlendiriyoruz - butonlar sahnede sadece dogru etiketle var olmali yeterli.
    /// </summary>
    private void SetupNumpad()
    {
        if (numpadContainer == null) return;
        foreach (Transform child in numpadContainer)
        {
            var btn = child.GetComponent<Button>();
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (btn == null || label == null) continue;
            string key = label.text;
            btn.onClick.AddListener(() => OnNumpadKey(key));
        }
    }

    private void OnNumpadKey(string key)
    {
        if (key == "<")
        {
            if (typedValue.Length > 0) typedValue = typedValue.Substring(0, typedValue.Length - 1);
        }
        else if (key == ".")
        {
            if (!typedValue.Contains(".")) typedValue += typedValue.Length == 0 ? "0." : ".";
        }
        else
        {
            typedValue += key;
        }
        UpdatePlayerInputDisplay();
    }

    private void UpdatePlayerInputDisplay()
    {
        if (playerInputText != null)
            playerInputText.text = "Teklifin: " + (typedValue.Length > 0 ? typedValue : "0") + "$";
    }

    private void OnEnable()
    {
        if (negotiationManager != null)
            negotiationManager.State.OnValueChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        if (negotiationManager != null)
            negotiationManager.State.OnValueChanged -= OnStateChanged;
    }

    private void Start()
    {
        if (negotiationManager != null)
            Refresh(negotiationManager.State.Value);
    }

    private void OnStateChanged(NegotiationState previous, NegotiationState current)
    {
        Refresh(current);
    }

    private void Refresh(NegotiationState state)
    {
        bool isMine = NetworkManager.Singleton != null && state.negotiatingClientId == NetworkManager.Singleton.LocalClientId;
        bool showPanel = isMine && (state.stage == NegotiationStage.Offered || state.stage == NegotiationStage.FinalOffered);

        if (panelRoot != null) panelRoot.SetActive(showPanel);
        if (!showPanel) return;

        string speciesTr = SpeciesToTurkish(state.species);
        string directionTr = state.direction == OrderDirection.Satis ? "Satış" : "Alım";
        if (orderText != null) orderText.text = "SİPARİŞ: " + state.count + "x " + speciesTr + " " + directionTr;

        if (state.stage == NegotiationStage.Offered)
        {
            typedValue = ""; // Offered'a her yeni giriste (yeni musteri) teklif kutusu sıfırlanır
            UpdatePlayerInputDisplay();

            if (offerText != null) offerText.text = "Müşteri Teklifi: " + state.baseOffer.ToString("0.##") + "$";
            if (riskText != null) riskText.text = RiskToVagueText(state.rejectRiskPercent); // KESIN YUZDE GOSTERILMIYOR - kullanici istegi: "biraz muallakta olmali ki heyecan olustursun"

            SetButtonActive(negotiateButton, true);
            SetButtonActive(acceptButton, true);
            SetButtonActive(rejectButton, true);
            if (numpadContainer != null) numpadContainer.gameObject.SetActive(true);
            if (playerInputText != null) playerInputText.gameObject.SetActive(true);
        }
        else // FinalOffered - artik yazilacak bir sey yok, sadece sonucu onayla/reddet
        {
            if (offerText != null) offerText.text = "Müşteri Final Teklifi: " + state.finalOffer.ToString("0.##") + "$";
            if (riskText != null) riskText.text = "Pazarlık sonuçlandı, teklifi değerlendir.";

            SetButtonActive(negotiateButton, false);
            SetButtonActive(acceptButton, true);
            SetButtonActive(rejectButton, true);
            if (numpadContainer != null) numpadContainer.gameObject.SetActive(false);
            if (playerInputText != null) playerInputText.gameObject.SetActive(false);
        }
    }

    private void SetButtonActive(Button b, bool active)
    {
        if (b != null) b.gameObject.SetActive(active);
    }

    private void OnNegotiateClicked()
    {
        if (negotiationManager == null) return;

        if (!float.TryParse(typedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || value <= 0f)
        {
            Debug.Log("[NegotiationUI] Gecerli bir teklif yazilmadi, pazarlik gonderilmedi.");
            return;
        }

        negotiationManager.RequestNegotiateServerRpc(value);
    }

    private void OnAcceptClicked()
    {
        if (negotiationManager == null) return;
        var stage = negotiationManager.State.Value.stage;
        if (stage == NegotiationStage.Offered) negotiationManager.RequestAcceptBaseServerRpc();
        else if (stage == NegotiationStage.FinalOffered) negotiationManager.RequestAcceptFinalServerRpc();
    }

    private void OnRejectClicked()
    {
        if (negotiationManager == null) return;
        var stage = negotiationManager.State.Value.stage;
        if (stage == NegotiationStage.Offered) negotiationManager.RequestRejectBaseServerRpc();
        else if (stage == NegotiationStage.FinalOffered) negotiationManager.RequestRejectFinalServerRpc();
    }

    /// <summary>
    /// Reddetme riskini KESIN YUZDE olarak degil, belirsiz/gerilim yaratan bir ifade olarak
    /// gosterir (kullanici geri bildirimi: "Oyuncu icin bu kisim biraz muallakta olmali ki
    /// heyecan olustursun"). Bilincli olarak 5 kaba kategoriye ayrildi - tam sayiyi asla
    /// gostermez, oyuncu riski TAHMIN etmek zorunda kalir.
    /// </summary>
    private string RiskToVagueText(float riskPercent)
    {
        if (riskPercent < 12f) return "Müşteri oldukça istekli görünüyor.";
        if (riskPercent < 28f) return "Müşteri fiyata sıcak bakıyor gibi.";
        if (riskPercent < 48f) return "Müşterinin tepkisi belirsiz...";
        if (riskPercent < 68f) return "Müşteri tereddütlü görünüyor.";
        return "Müşteri oldukça huzursuz, dikkatli ol.";
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
