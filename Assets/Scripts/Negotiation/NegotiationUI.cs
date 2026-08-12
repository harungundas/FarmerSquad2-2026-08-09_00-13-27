using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// T25 - GDD Bolum 10.4 pazarlik modali. NegotiationManager.State (NetworkVariable) OKUYARAK
/// acilip kapanir (client-authoritative degil, sadece goruntuleme), buton tiklamalari
/// NegotiationManager'in ServerRpc'lerini cagirir.
///
/// SADECE pazarligi baslatan client (negotiatingClientId == LocalClientId) bu paneli gorur -
/// diger oyunculara "Bob pazarlik yapiyor" broadcast'i ayri bir mekanizmanin (T26
/// StandInteraction / T37 HUD Alert) isi, bu script'in kapsami DEGIL.
///
/// GDD 10.4'teki iki-modal gorunumu (ilk teklif / musteri-counter + oyuncu-teklif-input) TASKS.md
/// T24'un sadelestirdigi backend'e gore UYARLANDI: NegotiationManager'da Offered -[Pazarlik Yap]->
/// tek bir risk atisi -> FinalOffered akisi var, AYRI bir "oyuncu kendi teklifini yazar" ADIMI/RPC'si
/// YOK. Bu yuzden ayni 3 buton (Pazarlik Yap / Kabul Et / Reddet) iki asamada farkli davranislarla
/// YENIDEN KULLANILIYOR, serbest metin teklif input alani YOK (HANDOFF.md T25 notunda bu bilinen ve
/// bilincli bir sadelestirme olarak isaretlenmisti).
/// </summary>
public class NegotiationUI : MonoBehaviour
{
    [Header("Baglantilar")]
    public NegotiationManager negotiationManager;
    public GameObject panelRoot;
    public TextMeshProUGUI orderText;
    public TextMeshProUGUI offerText;
    public TextMeshProUGUI riskText;
    public Button negotiateButton;
    public Button acceptButton;
    public Button rejectButton;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (negotiateButton != null) negotiateButton.onClick.AddListener(OnNegotiateClicked);
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAcceptClicked);
        if (rejectButton != null) rejectButton.onClick.AddListener(OnRejectClicked);
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
        // NegotiationManager.Awake() State.Value'yu Inactive() yapiyor ama bu OnValueChanged'i
        // TETIKLEMEZ (ilk deger atamasi degisiklik sayilmaz). Sahne yuklenirken bu script,
        // NegotiationManager'dan SONRA Enable olursa mevcut durumu kacirmamak icin burada
        // manuel bir kontrol yapiyoruz.
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
            if (offerText != null) offerText.text = "Müşteri Teklifi: " + state.baseOffer.ToString("0.##") + "$";
            if (riskText != null) riskText.text = "Müşteri Reddetme Riski: %" + state.rejectRiskPercent.ToString("0.#");

            SetButtonActive(negotiateButton, true);
            SetButtonActive(acceptButton, true);
            SetButtonActive(rejectButton, true);
        }
        else // FinalOffered
        {
            if (offerText != null) offerText.text = "Müşteri Final Teklifi: " + state.finalOffer.ToString("0.##") + "$";
            if (riskText != null) riskText.text = "Pazarlık sonuçlandı, teklifi değerlendir.";

            SetButtonActive(negotiateButton, false);
            SetButtonActive(acceptButton, true);
            SetButtonActive(rejectButton, true);
        }
    }

    private void SetButtonActive(Button b, bool active)
    {
        if (b != null) b.gameObject.SetActive(active);
    }

    private void OnNegotiateClicked()
    {
        if (negotiationManager != null) negotiationManager.RequestNegotiateServerRpc();
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
        if (stage == NegotiationStage.Offered)
        {
            // NegotiationManager'da Offered asamasinda "base teklifi reddet / vazgec" icin AYRI
            // bir ServerRpc YOK (sadece RequestAcceptBaseServerRpc ve RequestNegotiateServerRpc
            // var). GDD 10.4'teki ilk modaldaki [Reddet] butonunun karsiligi olarak burada
            // ResetNegotiationServerRpc cagriliyor: pazarlik Inactive'e doner, hicbir sonuc/ceza
            // islenmez (musteri araci T26/T27 baglaninca ayri ele alinacak). T25 BILINCLI
            // DEVIASYON - HANDOFF.md'ye kaydedildi.
            negotiationManager.ResetNegotiationServerRpc();
        }
        else if (stage == NegotiationStage.FinalOffered)
        {
            negotiationManager.RequestRejectFinalServerRpc();
        }
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
