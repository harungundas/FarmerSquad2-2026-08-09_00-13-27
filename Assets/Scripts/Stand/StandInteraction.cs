using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// `kasa` objesine eklenir (ARCHITECTURE.md "## Stant/Kasa Etkileşimi"). Trigger collider
/// içindeki (IsOwner) oyuncu F tuşuna basınca, StandFront'ta bekleyen müşteri aracının
/// (VehicleSpawner/CustomerVehicle, T22/T23) GERÇEK siparişini NegotiationManager'a
/// (T24) RequestStartNegotiationServerRpc ile iletir.
///
/// BASİT LOCK: NegotiationManager.State bir NetworkVariable (Everyone okuyabilir, T24),
/// bu yüzden sunucuya sormadan ÖNCE lokal kopyası kontrol edilip "meşgul" durumu tespit
/// edilir. Küçük bir yarış durumu ihtimali var (iki oyuncu TAM AYNI ANDA basarsa) ama
/// NegotiationManager zaten sunucu tarafında da Inactive kontrolü yapıyor (T24) - bu yüzden
/// yanlış bir pazarlık asla İKİ KERE başlamaz, sadece "meşgul" mesajı ender durumda
/// gecikebilir. TASKS.md T26 "basit lock mekanizması" istiyor, bu yeterli.
///
/// HANDOFF.md T26 notu gereği: OrderData T22/T23'te sadece VehicleSpawner içinde
/// hesaplanıp LOGLANIYORDU, hiçbir yerde SAKLANMIYORDU. Bu script'in gerçek bir sipariş
/// okuyabilmesi için CustomerVehicle.CurrentOrder (yeni alan) ve
/// VehicleSpawner.GetVehicleWaitingAtStand() (yeni public metod) eklendi - bu KÜÇÜK,
/// ZORUNLU bir bağlantı tamamlamasıdır (T26'nın kendi kapsamında). Pazarlık SONUCUNUN
/// CustomerVehicle.ResolveOrder()'ı tetiklemesi (asıl "kritik bağlantı eksiği", T23'ten
/// beri not ediliyor) ise BİLEREK T27 DeliveryResolver'a bırakıldı - doğru/yanlış teslimat
/// sonucunu bilen sistem o, StandInteraction değil.
///
/// "BOB PAZARLIK YAPIYOR" BROADCAST: Oyuncu İSİM sistemi HENÜZ YOK (T43 Karakter Seçim UI
/// bağlanana kadar) - bu yüzden "Oyuncu {clientId}" şeklinde JENERİK bir isim kullanılır
/// (BİLİNÇLİ DEVİASYON, HANDOFF.md'ye kaydedildi). Ayrıca bu broadcast şimdilik sadece
/// Debug.Log ile konsola yazılıyor - ekranda görünen bir HUD mesajı DEĞİL (T37 Dinamik
/// Uyarılar'ın işi, HUDController henüz yok).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class StandInteraction : NetworkBehaviour
{
    [Header("Bağlantılar")]
    public NegotiationManager negotiationManager;
    
    [Header("Teslimat Onay Ekrani")]
    public DeliveryConfirmUI deliveryConfirmUI;
public VehicleSpawner vehicleSpawner;

    [Header("Etkileşim (trigger collider boyutu)")]
    public Vector3 triggerSize = new Vector3(2.5f, 2.5f, 2.5f);

    private PlayerController playerInRange;

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        // BUG DUZELTMESI (kullanici bildirdi: "F basinca hicbir sey olmuyor / yazi cikmiyor"):
        // Bu obje (kasa/bilgisayar) FAZ13/14 olcek calismalari sirasinda market_stall
        // tezgahinin USTUNE (~8-8.5 birim yukseklik farkiyla) yerlestirilmis, ama oyuncu
        // YERDE (terrain seviyesinde) yuruyor - iki nokta arasinda BUYUK bir dikey bosluk var.
        // Sabit boyutlu trigger bu bosluga hic ulasamiyordu, bu yuzden OnTriggerEnter HICBIR
        // ZAMAN tetiklenmiyordu. Kalici/kesin dogru yukseklik FAZ14 T61'de netlesecek (henuz
        // cozulmedi) - o zamana kadar GUVENLI/GENIS bir dikey trigger kullaniyoruz: objenin
        // ALTINA dogru uzayan, dunya-uzayinda sabit boyutlu (lossyScale'e bolunerek local'e
        // cevrilen) bir kutu.
        float worldDownReachY = 18f; // dunya biriminde, asagi dogru kapsama mesafesi
        float lossyY = Mathf.Max(0.0001f, transform.lossyScale.y);
        float localSizeY = worldDownReachY / lossyY;
        col.size = new Vector3(triggerSize.x, localSizeY, triggerSize.z);
        col.center = new Vector3(0f, -localSizeY * 0.45f, 0f);

        if (negotiationManager == null) negotiationManager = GetComponent<NegotiationManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc.IsOwner)
        {
            playerInRange = pc;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc == playerInRange)
        {
            playerInRange = null;
        }
    }

private void Update()
    {
        if (playerInRange == null)
        {
            if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Hide();
            return;
        }

        UpdatePrompt();

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.fKey.wasPressedThisFrame)
        {
            TryOpenNegotiation();
        }
    }

    /// <summary>Kasanin mevcut asamasina gore InteractionIndicator prompt metnini gunceller
    /// ("F - Bas - Kasayi Kullan" / "F - Bas - Teslimati Onayla" / mesgul).</summary>
    private void UpdatePrompt()
    {
        if (InteractionIndicator.Instance == null) return;

        var stage = negotiationManager != null ? negotiationManager.State.Value.stage : NegotiationStage.Inactive;

        if (stage == NegotiationStage.Inactive)
        {
            InteractionIndicator.Instance.Show(transform, "F - Bas - Kasayı Kullan");
        }
        else if (stage == NegotiationStage.AwaitingDelivery)
        {
            InteractionIndicator.Instance.Show(transform, "F - Bas - Teslimatı Onayla");
        }
        else
        {
            InteractionIndicator.Instance.ShowTextOnly("Kasa Meşgul");
        }
    }

private void TryOpenNegotiation()
    {
        if (negotiationManager == null)
        {
            Debug.LogWarning("[StandInteraction] negotiationManager atanmamis.");
            return;
        }

        var currentStage = negotiationManager.State.Value.stage;

        // Kullanici geri bildirimi sonrasi eklendi: fiyat AwaitingDelivery'de kilitliyken
        // (oyuncu hayvanlari teslimat alanina tasiyip kasaya DONDUGUNDE) F artik dogrudan
        // finalize etmez - once bir onay ekrani (DeliveryConfirmUI.Show/Hide) acar, siparis
        // detayini ve canli dogru/yanlis durumunu gosterir. RequestFinalizeDeliveryServerRpc
        // SADECE o ekrandaki [Teslim Et] butonuna basilinca cagrilir.
        if (currentStage == NegotiationStage.AwaitingDelivery)
        {
            if (deliveryConfirmUI != null)
            {
                if (deliveryConfirmUI.IsOpen) deliveryConfirmUI.Hide();
                else deliveryConfirmUI.Show();
            }
            else
            {
                Debug.LogWarning("[StandInteraction] deliveryConfirmUI atanmamis, guvenlik icin dogrudan finalize edilmiyor.");
            }
            return;
        }

        if (currentStage != NegotiationStage.Inactive)
        {
            Debug.Log("[StandInteraction] Stant meşgul, pazarlık açılamadı (yerel kontrol).");
            return;
        }

        if (vehicleSpawner == null)
        {
            Debug.LogWarning("[StandInteraction] vehicleSpawner atanmamis.");
            return;
        }

        var waitingVehicle = vehicleSpawner.GetVehicleWaitingAtStand();
        if (waitingVehicle == null)
        {
            Debug.Log("[StandInteraction] StandFront'ta bekleyen müşteri yok, pazarlık açılamadı.");
            return;
        }

        var order = waitingVehicle.CurrentOrder;
        negotiationManager.RequestStartNegotiationServerRpc(order.species, order.count, order.direction, order.basePrice);

        BroadcastNegotiationStartedServerRpc(playerInRange.OwnerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void BroadcastNegotiationStartedServerRpc(ulong startingClientId)
    {
        NotifyNegotiationStartedClientRpc(startingClientId);
    }

    [ClientRpc]
    private void NotifyNegotiationStartedClientRpc(ulong startingClientId)
    {
        Debug.Log("[StandInteraction] Oyuncu " + startingClientId + " pazarlık yapıyor.");
    }
}
