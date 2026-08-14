using UnityEngine;
using Unity.Netcode;

/// <summary>
/// `kasa` objesine eklenir (ARCHITECTURE.md "## Pazarlik Sistemi"). ServerRpc'lerle teklif
/// akisini yonetir, reddetme riskini hesaplar (Base %30, Yasli -15 puan; Pazarlik Ustaligi
/// henuz market'te yok - T24 karari: sadece Yasli'nin CharacterClassData.negotiationRejectReduction
/// alani kullanilir, Ustalik'in -10 puanlik katkisi T33 Market baglaninca eklenecek).
///
/// KULLANICI GERI BILDIRIMI SONRASI (T25/T26 sonrasi revizyon) DEGISEN TASARIM:
/// 1) SERBEST TEKLIF: Oyuncu artik sabit bir formule gore degil, NUMPAD ile kendi yazdigi bir
///    sayiyi (RequestNegotiateServerRpc(float playerOfferedPrice)) musteriye sunuyor. "ABSURD"
///    bir deger (Satis'ta baseOffer'in 1.5 katindan fazla, Alim'da 0.5 katindan az, veya <=0)
///    girilirse musteri HEMEN reddedip gider (risk atisi bile yapilmaz).
/// 2) TESLIMAT ONAYI (SADECE SATIS): Kabul Et -> AwaitingDelivery (fiyat kilitlendi ama islem
///    BITMEDI). Oyuncu hayvanlari teslimat alanina tasiyip kasaya DONMELI, tekrar F'e basinca
///    RequestFinalizeDeliveryServerRpc() DeliveryZoneDetector'i (T16) okuyup dogru/yanlis
///    teslimati kontrol eder.
/// 3) ALIM: Kabul Et ANINDA hayvanlar spawn olur, ikinci onaya GEREK YOK (T28 kullanici karari).
/// 4) ARAC HER ZAMAN AYRILIR: Reddedilen/absurd/basarisiz TUM yollarda ConcludeDealAndReset()
///    cagrilir.
/// 5) PARA (T29): Satis basarili teslimat -> WalletManager.AddBalanceServerRpc(finalOffer).
///    Alim (her zaman "basarili" sayilir, Kabul Et aninda spawn) -> SubtractBalanceServerRpc(finalOffer).
///    Satis basarisiz teslimat -> para hareketi YOK (sadece Prestij -0.25, T35 baglaninca).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NegotiationManager : NetworkBehaviour
{
    [Header("Reddetme Riski (yuzde puan)")]
    public float baseRejectRiskPercent = 30f;

    [Header("Odul Carpanlari (SADECE pazarliksiz dogrudan Kabul Et icin - bkz. sinif yorumu)")]
    public float saleMultiplier = 1.2f;
    public float buyMultiplier = 0.8f;

    [Header("Serbest Teklif Sinirlari (bu araligin disi 'absurd' sayilir, musteri hemen gider)")]
    [Tooltip("Satista oyuncunun teklif edebilecegi ust sinir: baseOffer * bu deger.")]
    public float maxAcceptableMultiplierSell = 1.5f;
    [Tooltip("Alimda oyuncunun teklif edebilecegi alt sinir: baseOffer * bu deger.")]
    public float minAcceptableMultiplierBuy = 0.5f;

    [Header("Baglantilar")]
    [Tooltip("StandFront'ta bekleyen araci bulup ResolveOrder() cagirmak icin.")]
    public VehicleSpawner vehicleSpawner;
    [Tooltip("RequestFinalizeDeliveryServerRpc, teslimatin dogru/yanlis oldugunu buradan okur (T16).")]
    public DeliveryZoneDetector deliveryZoneDetector;
    [Tooltip("T29: Satis/Alim para hareketleri buradan islenir.")]
    public WalletManager walletManager;
    [Tooltip("T35: Satis teslimat sonucu (basarili/hatali) buraya bildirilir. Alim sinyal GONDERMEZ - bkz. PrestigeManager.cs sinif yorumu.")]
    public PrestigeManager prestigeManager;

    [Header("Hayvan Veritabani (T28 - Alim siparisinde dogru prefabi spawn etmek icin)")]
    [Tooltip("5 turun tamami icin AnimalData referanslarini (T04'teki asset'ler) buraya elle surukle.")]
    public AnimalData[] animalDatabase;

    public NetworkVariable<NegotiationState> State = new NetworkVariable<NegotiationState>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake()
    {
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

    private void OnStateChanged(NegotiationState previous, NegotiationState current)
    {
        string who = NetworkManager.Singleton != null ? ("client" + NetworkManager.Singleton.LocalClientId) : "?";
        Debug.Log("[NegotiationManager] (" + who + ") State degisti: stage=" + current.stage +
                   " negotiatingClientId=" + current.negotiatingClientId +
                   " baseOffer=" + current.baseOffer + " playerCounter=" + current.playerCounter +
                   " finalOffer=" + current.finalOffer + " resolved=" + current.resolved +
                   " accepted=" + current.accepted + " deliverySuccess=" + current.deliverySuccess);
    }

    /// <summary>StandInteraction (T26) pazarlik baslatmak icin cagirir.</summary>
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
            rejectRiskPercent = CalculateRejectRiskPercent(clientId),
            deliverySuccess = false
        };
    }

    /// <summary>Oyuncu pazarliksiz, base fiyata dogrudan [Kabul Et] der - risk/bonus YOK. Fiyat kilitlenir, teslimat BEKLENIR (AwaitingDelivery, sadece Satis'ta).</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestAcceptBaseServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsValidCaller(rpcParams, NegotiationStage.Offered)) return;

        var s = State.Value;
        s.finalOffer = s.baseOffer;
        s.accepted = true;

        if (s.direction == OrderDirection.Alim)
        {
            SpawnAlimAnimals(s);
            ProcessPayment(s);
            s.stage = NegotiationStage.Resolved;
            s.resolved = true;
            s.deliverySuccess = true;
            State.Value = s;
            ConcludeDealAndReset();
            return;
        }

        s.stage = NegotiationStage.AwaitingDelivery;
        State.Value = s;
    }

    /// <summary>Oyuncu Offered asamasinda base teklifi reddedip vazgecer - musteri HEMEN gider.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestRejectBaseServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsValidCaller(rpcParams, NegotiationStage.Offered)) return;

        var s = State.Value;
        s.stage = NegotiationStage.Resolved;
        s.resolved = true;
        s.accepted = false;
        State.Value = s;
        ConcludeDealAndReset();
    }

    /// <summary>
    /// Oyuncu [Pazarlik Yap] der ve NUMPAD ile yazdigi kendi fiyatini (playerOfferedPrice)
    /// sunar. Once "absurd" kontrolu, sonra normal reddetme riski atisi yapilir.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestNegotiateServerRpc(float playerOfferedPrice, ServerRpcParams rpcParams = default)
    {
        if (!IsValidCaller(rpcParams, NegotiationStage.Offered)) return;

        var s = State.Value;

        bool absurd = playerOfferedPrice <= 0f ||
            (s.direction == OrderDirection.Satis && playerOfferedPrice > s.baseOffer * maxAcceptableMultiplierSell) ||
            (s.direction == OrderDirection.Alim && playerOfferedPrice < s.baseOffer * minAcceptableMultiplierBuy);

        if (absurd)
        {
            Debug.Log("[NegotiationManager] Absurd teklif (" + playerOfferedPrice + "), musteri hemen gidiyor.");
            s.stage = NegotiationStage.Resolved;
            s.resolved = true;
            s.accepted = false;
            State.Value = s;
            ConcludeDealAndReset();
            return;
        }

        float rejectRisk = CalculateRejectRiskPercent(s.negotiatingClientId);
        bool rejected = Random.Range(0f, 100f) < rejectRisk;

        if (rejected)
        {
            s.stage = NegotiationStage.Resolved;
            s.resolved = true;
            s.accepted = false;
            State.Value = s;
            ConcludeDealAndReset();
            return;
        }

        s.playerCounter = playerOfferedPrice;
        s.finalOffer = playerOfferedPrice;
        s.stage = NegotiationStage.FinalOffered;
        State.Value = s;
    }

    /// <summary>Oyuncu musterinin (kendi yazdigi ve kabul edilen) son teklifini onaylar. Fiyat kilitlenir, teslimat BEKLENIR (sadece Satis'ta).</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestAcceptFinalServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsValidCaller(rpcParams, NegotiationStage.FinalOffered)) return;

        var s = State.Value;
        s.accepted = true;

        if (s.direction == OrderDirection.Alim)
        {
            SpawnAlimAnimals(s);
            ProcessPayment(s);
            s.stage = NegotiationStage.Resolved;
            s.resolved = true;
            s.deliverySuccess = true;
            State.Value = s;
            ConcludeDealAndReset();
            return;
        }

        s.stage = NegotiationStage.AwaitingDelivery;
        State.Value = s;
    }

    /// <summary>Oyuncu son teklifi reddedip pazarliktan vazgecer - musteri HEMEN gider.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestRejectFinalServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsValidCaller(rpcParams, NegotiationStage.FinalOffered)) return;

        var s = State.Value;
        s.stage = NegotiationStage.Resolved;
        s.resolved = true;
        s.accepted = false;
        State.Value = s;
        ConcludeDealAndReset();
    }

    /// <summary>
    /// Oyuncu (fiyat AwaitingDelivery'de kilitliyken) hayvanlari teslimat alanina tasiyip
    /// kasaya DONUP tekrar F'e basinca StandInteraction bunu cagirir. DeliveryZoneDetector'i
    /// (T16) okuyup dogru tur+sayida hayvan var mi kontrol eder, araci HER TURLU yola cikartir.
    /// T29: basariliysa WalletManager.AddBalanceServerRpc(finalOffer) cagrilir (Satis = para gelir).
    /// Basarisizsa para hareketi yok (Prestij -0.25, T35 baglaninca).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestFinalizeDeliveryServerRpc(ServerRpcParams rpcParams = default)
    {
        var s = State.Value;
        if (s.stage != NegotiationStage.AwaitingDelivery)
        {
            Debug.LogWarning("[NegotiationManager] Teslimat onayi icin uygun asamada degil: " + s.stage);
            return;
        }

        bool success = false;
        if (deliveryZoneDetector != null)
        {
            var inside = deliveryZoneDetector.AnimalsInside;
            int matchingCount = 0;
            foreach (var animal in inside)
            {
                if (animal != null && animal.animalData != null && animal.animalData.species == s.species) matchingCount++;
            }
            success = matchingCount == s.count && inside.Count == s.count;
        }
        else
        {
            Debug.LogWarning("[NegotiationManager] deliveryZoneDetector atanmamis, teslimat kontrolu yapilamiyor - basarisiz sayiliyor.");
        }

        if (success)
        {
            ProcessPayment(s);
            Debug.Log("[NegotiationManager] Dogru teslimat! " + s.count + "x " + s.species + " (" + s.direction + ") " + s.finalOffer + "$ WalletManager'a islendi.");
        }
        else
        {
            Debug.Log("[NegotiationManager] Yanlis/eksik teslimat! Beklenen " + s.count + "x " + s.species + " - para hareketi yok.");
        }

        if (prestigeManager != null)
        {
            prestigeManager.ReportDeliveryResultServer(success);
        }
        else
        {
            Debug.LogWarning("[NegotiationManager] prestigeManager atanmamis, Prestij puani islenemedi.");
        }

        s.stage = NegotiationStage.Resolved;
        s.resolved = true;
        s.deliverySuccess = success;
        State.Value = s;
        ConcludeDealAndReset();
    }

    /// <summary>
    /// Pazarligi Inactive durumuna sifirlar. Normal akiste artik gerekmiyor (ConcludeDealAndReset
    /// tum sonlanma yollarinda otomatik cagriliyor) - acil durum/manuel mudahale icin public birakildi.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ResetNegotiationServerRpc()
    {
        State.Value = NegotiationState.Inactive();
    }

    /// <summary>
    /// TUM sonlanma yollarinin (reddedildi/absurd/basarili teslimat/basarisiz teslimat) ortak
    /// son adimi: StandFront'ta bekleyen musteri aracini bulup HER TURLU yola cikartir, sonra
    /// State'i Inactive'e sifirlar.
    /// </summary>
    private void ConcludeDealAndReset()
    {
        var vehicle = vehicleSpawner != null ? vehicleSpawner.GetVehicleWaitingAtStand() : null;
        if (vehicle != null) vehicle.ResolveOrder();
        else Debug.LogWarning("[NegotiationManager] ConcludeDealAndReset: bekleyen arac bulunamadi (vehicleSpawner atanmamis olabilir).");

        State.Value = NegotiationState.Inactive();
    }

    /// <summary>
    /// T29: Yon kuralina gore para hareketini WalletManager'a isler. Satis (ciftlik satiyor) ->
    /// para GELIR (AddBalance). Alim (ciftlik satin aliyor) -> para GIDER (SubtractBalance).
    /// walletManager atanmamissa sadece uyari basar, State/arac akisini BLOKE ETMEZ.
    /// </summary>
    private void ProcessPayment(NegotiationState s)
    {
        if (walletManager == null)
        {
            Debug.LogWarning("[NegotiationManager] walletManager atanmamis, para hareketi (" + s.finalOffer + "$, " + s.direction + ") islenemedi.");
            return;
        }

        if (s.direction == OrderDirection.Satis)
        {
            walletManager.AddBalanceServerRpc(s.finalOffer);
        }
        else
        {
            walletManager.SubtractBalanceServerRpc(s.finalOffer);
        }
    }

    /// <summary>
    /// T28: Alim siparisi Kabul Et ile sonuclaninca cagirilir. Musterinin getirdigi hayvanlari
    /// hayvan_birakma_alani'nin (deliveryZoneDetector uzerindeki BoxCollider) sinirlari icinde
    /// rastgele noktalara spawn eder. Oyuncunun bunlari AGILA tasimasi (PenManager.AddStock
    /// cagrisi) ayri/sonraki bir gorev - burada sadece FIZIKSEL SPAWN yapilir.
    /// </summary>
    private void SpawnAlimAnimals(NegotiationState s)
    {
        var data = GetAnimalData(s.species);
        if (data == null || data.prefabRef == null)
        {
            Debug.LogWarning("[NegotiationManager] Alim: AnimalData/prefabRef bulunamadi (species=" + s.species + "), spawn atlandi.");
            return;
        }
        if (deliveryZoneDetector == null)
        {
            Debug.LogWarning("[NegotiationManager] Alim: deliveryZoneDetector atanmamis, spawn noktasi bulunamadi.");
            return;
        }

        BoxCollider zoneCollider = deliveryZoneDetector.GetComponent<BoxCollider>();
        Vector3 fallbackCenter = deliveryZoneDetector.transform.position;
        bool networked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        for (int i = 0; i < s.count; i++)
        {
            Vector3 pos = fallbackCenter;
            if (zoneCollider != null)
            {
                Bounds b = zoneCollider.bounds;
                pos = new Vector3(Random.Range(b.min.x, b.max.x), b.center.y, Random.Range(b.min.z, b.max.z));
            }

            GameObject instance = Instantiate(data.prefabRef, pos, Quaternion.identity);

            if (networked)
            {
                var netObj = instance.GetComponent<NetworkObject>();
                if (netObj != null) netObj.Spawn();
                else Debug.LogWarning("[NegotiationManager] Alim: spawn edilen hayvan prefabinda NetworkObject yok (" + data.prefabRef.name + ").");
            }
        }

        Debug.Log("[NegotiationManager] Alim tamamlandi - " + s.count + "x " + s.species +
                   " teslimat alanina spawn edildi, arac ayriliyor. Odeme WalletManager'a islenecek." +
                   " Agila tasima/stok girisi oyuncunun elle tasimasiyla olacak, ayri bir gorev (henuz yok).");
    }

    private AnimalData GetAnimalData(AnimalSpecies species)
    {
        if (animalDatabase == null) return null;
        foreach (var d in animalDatabase)
        {
            if (d != null && d.species == species) return d;
        }
        return null;
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

    private CharacterClassData GetClassData(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return null;
        if (client.PlayerObject == null) return null;

        var pc = client.PlayerObject.GetComponent<PlayerController>();
        return pc != null ? pc.classData : null;
    }
}
