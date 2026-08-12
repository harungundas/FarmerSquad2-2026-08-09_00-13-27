using UnityEngine;
using Unity.Netcode;

/// <summary>
/// `kasa` objesine eklenir (ARCHITECTURE.md "## Pazarlik Sistemi"). ServerRpc'lerle teklif
/// akisini yonetir, reddetme riskini hesaplar (Base %30, Yasli -15 puan; Pazarlik Ustaligi
/// henuz market'te yok - TASKS.md T24: "ustalik henuz market'te yok - simdilik sabit %30/%15
/// test et" - o yuzden sadece Yasli'nin CharacterClassData.negotiationRejectReduction alani
/// kullanilir, Ustalik'in -10 puanlik katkisi T33 Market baglaninca eklenecek).
///
/// Yon kurali (ARCHITECTURE.md): Satis basarili pazarlikta odul x1.2, Alim x0.8. Yasli'nin
/// +%5 ek bonusu (CharacterClassData.negotiationRewardBonus): GDD bu bonusun Alim yonundeki
/// tam formulunu belirtmiyordu - burada YORUM/karar olarak Satis'ta CARPANA EKLENIR (1.2+0.05),
/// Alim'de ise fiyati DAHA DA dusurecek sekilde CARPANDAN CIKARILIR (0.8-0.05, yani daha ucuza
/// alinir) seklinde uygulandi - her iki yonde de "oyuncu lehine" sonuc verir.
///
/// Basit akis (T24 "temel akis" kapsami, NegotiationState.cs'deki state machine yorumuna bkz.):
/// tam GDD Bolum 4'teki coklu-adimli (musteri-counter, oyuncu-teklif, musteri-final-counter)
/// pazarlik gorusmesini degil, TASKS.md'nin istedigi 4 alanli (BaseOffer/PlayerCounter/
/// FinalOffer/Resolved) sadelestirilmis akisi uygular. Tam gorsel/coklu-adim akis NegotiationUI
/// (T25) ile birlikte gerekirse genisletilir.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NegotiationManager : NetworkBehaviour
{
    [Header("Reddetme Riski (yuzde puan)")]
    public float baseRejectRiskPercent = 30f;

    [Header("Odul Carpanlari (basarili pazarlik)")]
    public float saleMultiplier = 1.2f;
    public float buyMultiplier = 0.8f;

    public NetworkVariable<NegotiationState> State = new NetworkVariable<NegotiationState>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake()
    {
        // NetworkVariable'in baslangic degerini kod-ici Inactive() fabrika metoduyla eslestir
        // (Awake'te NetworkVariable henuz network'e baglanmadigi icin dogrudan .Value atanabilir).
        State.Value = NegotiationState.Inactive();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        State.OnValueChanged += OnStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        State.OnValueChanged -= OnStateChanged;
    }

    /// <summary>
    /// T24 test kriteri: "Iki client'ta biri pazarlik baslatinca digeri de state degisikligini
    /// goruyor mu (Debug.Log ile dogrula)?" - NetworkVariable her client'ta otomatik replicate
    /// olur, bu callback HER client'ta (host dahil) kendi tarafinda tetiklenir.
    /// </summary>
    private void OnStateChanged(NegotiationState previous, NegotiationState current)
    {
        string who = NetworkManager.Singleton != null ? ("client" + NetworkManager.Singleton.LocalClientId) : "?";
        Debug.Log("[NegotiationManager] (" + who + ") State degisti: stage=" + current.stage +
                   " negotiatingClientId=" + current.negotiatingClientId +
                   " baseOffer=" + current.baseOffer + " playerCounter=" + current.playerCounter +
                   " finalOffer=" + current.finalOffer + " resolved=" + current.resolved +
                   " accepted=" + current.accepted);
    }

    /// <summary>
    /// Bir client (gelecekte StandInteraction, T26) pazarlik baslatmak icin cagirir.
    /// Ayni anda sadece BIR pazarlik aktif olabilir (coklu-oyuncu kilidi StandInteraction'in
    /// isi olacak - burada sadece "zaten aktifse yeni istek reddedilir" guvenligi var).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestStartNegotiationServerRpc(AnimalSpecies species, int count, OrderDirection direction, float basePrice, ServerRpcParams rpcParams = default)
    {
        if (State.Value.stage != NegotiationStage.Inactive)
        {
            Debug.LogWarning("[NegotiationManager] Zaten aktif bir pazarlik var, yeni istek reddedildi.");
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        State.Value = new NegotiationState
        {
            stage = NegotiationStage.Offered,
            negotiatingClientId = clientId,
            species = species,
            count = count,
            direction = direction,
            baseOffer = basePrice,
            playerCounter = 0f,
            finalOffer = 0f,
            resolved = false,
            accepted = false,
            rejectRiskPercent = CalculateRejectRiskPercent(clientId) // T25: UI'nin "Musteri Reddetme Riski: %XX" gostergesi icin Offered'a girerken hesaplanip saklanir
        };
    }

    /// <summary>Oyuncu pazarliksiz, base fiyata dogrudan [Kabul Et] der - risk/bonus YOK.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestAcceptBaseServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsValidCaller(rpcParams, NegotiationStage.Offered)) return;

        var s = State.Value;
        s.finalOffer = s.baseOffer;
        s.stage = NegotiationStage.Resolved;
        s.resolved = true;
        s.accepted = true;
        State.Value = s;
    }

    /// <summary>Oyuncu [Pazarlik Yap] der - reddetme riski atilir, kabul edilirse FinalOffer hesaplanir.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestNegotiateServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsValidCaller(rpcParams, NegotiationStage.Offered)) return;

        var s = State.Value;
        float rejectRisk = CalculateRejectRiskPercent(s.negotiatingClientId);
        bool rejected = Random.Range(0f, 100f) < rejectRisk;

        if (rejected)
        {
            s.stage = NegotiationStage.Resolved;
            s.resolved = true;
            s.accepted = false;
            State.Value = s;
            return;
        }

        float multiplier = CalculateRewardMultiplier(s.direction, s.negotiatingClientId);
        s.playerCounter = s.baseOffer; // T24 basit akis: ayri bir oyuncu-teklif GIRISI yok (UI T25'te), sembolik olarak baseOffer kaydedilir
        s.finalOffer = s.baseOffer * multiplier;
        s.stage = NegotiationStage.FinalOffered;
        State.Value = s;
    }

    /// <summary>Oyuncu musterinin son teklifini (FinalOffer) kabul eder.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestAcceptFinalServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsValidCaller(rpcParams, NegotiationStage.FinalOffered)) return;

        var s = State.Value;
        s.stage = NegotiationStage.Resolved;
        s.resolved = true;
        s.accepted = true;
        State.Value = s;
    }

    /// <summary>Oyuncu musterinin son teklifini reddedip pazarliktan vazgecer.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestRejectFinalServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsValidCaller(rpcParams, NegotiationStage.FinalOffered)) return;

        var s = State.Value;
        s.stage = NegotiationStage.Resolved;
        s.resolved = true;
        s.accepted = false;
        State.Value = s;
    }

    /// <summary>
    /// Pazarligi Inactive durumuna sifirlar. T26 (StandInteraction) Resolved sonrasi (veya
    /// oyuncu stanttan ayrilinca) bunu cagirmali - simdilik disaridan manuel de cagirilabilecek
    /// sekilde public birakildi.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ResetNegotiationServerRpc()
    {
        State.Value = NegotiationState.Inactive();
    }

    private bool IsValidCaller(ServerRpcParams rpcParams, NegotiationStage expectedStage)
    {
        var s = State.Value;
        if (s.stage != expectedStage)
        {
            Debug.LogWarning("[NegotiationManager] Beklenmeyen asamada istek: mevcut=" + s.stage + " beklenen=" + expectedStage);
            return false;
        }
        if (rpcParams.Receive.SenderClientId != s.negotiatingClientId)
        {
            Debug.LogWarning("[NegotiationManager] Bu pazarligi baslatan oyuncu degil, istek reddedildi.");
            return false;
        }
        return true;
    }

    private float CalculateRejectRiskPercent(ulong clientId)
    {
        float risk = baseRejectRiskPercent;
        var classData = GetClassData(clientId);
        if (classData != null)
        {
            risk -= classData.negotiationRejectReduction;
        }
        return Mathf.Clamp(risk, 0f, 100f);
    }

    private float CalculateRewardMultiplier(OrderDirection direction, ulong clientId)
    {
        float bonus = 0f;
        var classData = GetClassData(clientId);
        if (classData != null)
        {
            bonus = classData.negotiationRewardBonus;
        }

        return direction == OrderDirection.Satis
            ? saleMultiplier + bonus
            : buyMultiplier - bonus;
    }

    private CharacterClassData GetClassData(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return null;
        if (client.PlayerObject == null) return null;

        var pc = client.PlayerObject.GetComponent<PlayerController>();
        return pc != null ? pc.classData : null;
    }
}
