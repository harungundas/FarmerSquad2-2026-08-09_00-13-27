using Unity.Netcode;

/// <summary>
/// Pazarlik surecinin asamasi. GDD Bolum 4 akisinin, TASKS.md T24'un istedigi sadelestirilmis
/// (BaseOffer/PlayerCounter/FinalOffer/Resolved) alanlarina gore uyarlanmis hali:
///
///   Inactive -[RequestStartNegotiationServerRpc]-> Offered (BaseOffer sunulur)
///   Offered  -[RequestAcceptBaseServerRpc]-> Resolved (accepted=true, base fiyatla, risk/bonus YOK)
///   Offered  -[RequestNegotiateServerRpc]-> reddetme riski atilir:
///              reddedilirse -> Resolved (accepted=false, musteri gider)
///              kabul edilirse -> FinalOffered (PlayerCounter + FinalOffer set edilir)
///   FinalOffered -[RequestAcceptFinalServerRpc]-> Resolved (accepted=true, FinalOffer ile)
///   FinalOffered -[RequestRejectFinalServerRpc]-> Resolved (accepted=false, oyuncu vazgecti)
///
/// Tam GDD akisindaki "musteri counter teklifi" (adim 3) ayri bir alan olarak TUTULMUYOR -
/// TASKS.md'nin T24 icin istedigi 4 alan (Base/PlayerCounter/Final/Resolved) buna yeterli;
/// coklu-adimli UI gorunumu NegotiationUI.cs'in (T25) isi.
/// </summary>
public enum NegotiationStage : byte
{
    Inactive,
    Offered,
    FinalOffered,
    // Kullanici geri bildirimi sonrasi eklendi: fiyat anlasildiktan sonra oyuncu hayvanlari
    // teslimat alanina tasiyip kasaya DONMELI, ancak o zaman islem sonuclanir (DeliveryZoneDetector
    // T16 okunarak). Onceki tasarimda Accept aninda direkt Resolved'a geciliyordu - bu, aracin
    // gercek teslimati beklemeden "islem bitti" sayilmasina sebep oluyordu.
    AwaitingDelivery,
    Resolved
}

/// <summary>
/// NegotiationManager.State (NetworkVariable) icinde tutulan pazarlik durumu.
/// ARCHITECTURE.md "## Pazarlik Sistemi": "asama, teklifler, hangi oyuncu iceride".
///
/// NOT: OrderData'yi (T21, Assets/Scripts/Vehicles/OrderData.cs) DOGRUDAN tasimiyor -
/// species/count/direction burada PRIMITIVE alanlar olarak ayrica tutuluyor (NegotiationManager
/// bir OrderData alip bu alanlara dagitir). Bu, T21'in dosyasina hic dokunmadan NetworkVariable
/// serilestirmesini basit ve garanti calisir tutmak icin bilincli bir tasarim tercihidir.
/// </summary>
public struct NegotiationState : INetworkSerializable, System.IEquatable<NegotiationState>
{
    public NegotiationStage stage;
    public ulong negotiatingClientId;

    // Siparis bilgisi (OrderData'dan RequestStartNegotiationServerRpc tarafindan kopyalanir)
    public AnimalSpecies species;
    public int count;
    public OrderDirection direction;

    public float baseOffer;
    public float playerCounter;
    public float finalOffer;
    public bool resolved;
    public bool accepted;

    // T25 NegotiationUI'nin "Musteri Reddetme Riski: %XX" gostergesi icin eklendi (T24'te
    // yoktu - HANDOFF.md T25 notu: bu deger State struct'inda saklanmiyordu, sadece anlik
    // hesaplaniyordu). RequestStartNegotiationServerRpc, Offered asamasina gecerken bu alani
    // CalculateRejectRiskPercent ile doldurur ki UI ilk acildiginda dogru risk yuzdesini gorsun.
    public float rejectRiskPercent;

    public bool deliverySuccess;

    // T59: Ardisik Teslimat Bonusu (Streak) sadece PAZARLIKSIZ (RequestAcceptBaseServerRpc)
    // akisla kapanan satislara uygulanir. RequestNegotiateServerRpc risk atisini kazanip
    // FinalOffered'a gecince bu alan true olarak isaretlenir; RequestFinalizeDeliveryServerRpc
    // bu alani okuyarak streak sayacini artirip artirmayacagina karar verir.
    public bool wasNegotiated;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref stage);
        serializer.SerializeValue(ref negotiatingClientId);
        serializer.SerializeValue(ref species);
        serializer.SerializeValue(ref count);
        serializer.SerializeValue(ref direction);
        serializer.SerializeValue(ref baseOffer);
        serializer.SerializeValue(ref playerCounter);
        serializer.SerializeValue(ref finalOffer);
        serializer.SerializeValue(ref resolved);
        serializer.SerializeValue(ref accepted);
        serializer.SerializeValue(ref rejectRiskPercent);
        serializer.SerializeValue(ref deliverySuccess);
        serializer.SerializeValue(ref wasNegotiated);
    }

    public bool Equals(NegotiationState other)
    {
        return stage == other.stage
            && negotiatingClientId == other.negotiatingClientId
            && species == other.species
            && count == other.count
            && direction == other.direction
            && baseOffer.Equals(other.baseOffer)
            && playerCounter.Equals(other.playerCounter)
            && finalOffer.Equals(other.finalOffer)
            && resolved == other.resolved
            && accepted == other.accepted
            && rejectRiskPercent.Equals(other.rejectRiskPercent)
            && deliverySuccess == other.deliverySuccess
            && wasNegotiated == other.wasNegotiated;
    }

    public override bool Equals(object obj) => obj is NegotiationState other && Equals(other);

    public override int GetHashCode() => System.HashCode.Combine(stage, negotiatingClientId, species, count, direction, baseOffer);

    public static NegotiationState Inactive()
    {
        return new NegotiationState
        {
            stage = NegotiationStage.Inactive,
            negotiatingClientId = 0,
            species = default,
            count = 0,
            direction = OrderDirection.Satis,
            baseOffer = 0f,
            playerCounter = 0f,
            finalOffer = 0f,
            resolved = false,
            accepted = false,
            rejectRiskPercent = 0f,
            deliverySuccess = false,
            wasNegotiated = false
        };
    }
}
