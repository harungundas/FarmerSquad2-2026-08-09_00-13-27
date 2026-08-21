using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// `bilgisayar.glb` sahne objesine eklenir (kasaya yakın yerleştirildi, kullanıcı kararı).
/// GDD'nin "Serbest Mod Menüsü" (Bölüm 10.5: "[Market Aç] [Günü Tamamla]") için ayrı bir
/// her-zaman-açık UI paneli yerine, StandInteraction.cs (T26) ile AYNI desende F-tuşu
/// dünya-etkileşimi kullanılır - bilgisayara yaklaşıp F basınca DayCycleManager.CompleteDayServer()
/// tetiklenir (kota kontrolü -> başarılıysa Market açılır (MarketUI zaten NetworkVariable'a
/// reaktif, otomatik görünür) -> başarısızsa İflas ekranı - bu zincir DEĞİŞMEDİ, sadece
/// tetikleyici artık bir dünya objesi).
///
/// KISIT (GDD akışıyla tutarlılık için, kullanıcı ayrıca istemedi ama mevcut akışın anlamı bu):
/// sadece Serbest Mod'dayken (DayCycleManager.IsFreeMode==true) çalışır - müşteri gelişi
/// penceresi (240sn) sürerken günü bitirmek YOK (GDD Bölüm 3 Adım 5: "240 saniye ... süresi
/// dolunca ... Oyuncular ... [Günü Tamamla] butonuna basarak sonraki güne geçerler"). Market
/// zaten açıkken (MarketManager.IsOpen==true) tekrar tetiklenmesin diye ayrıca kontrol var -
/// bu iki durumda InteractionIndicator hiç prompt göstermez (aksiyon alınamaz durumda).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class ComputerInteraction : NetworkBehaviour
{
    [Header("Bağlantılar")]
    public DayCycleManager dayCycleManager;
    public MarketManager marketManager;

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

        if (dayCycleManager == null) dayCycleManager = FindObjectOfType<DayCycleManager>();
        if (marketManager == null) marketManager = FindObjectOfType<MarketManager>();
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

        bool actionable = IsActionable();

        if (InteractionIndicator.Instance != null)
        {
            if (actionable) InteractionIndicator.Instance.Show(transform, "F - Bas - Market");
            else InteractionIndicator.Instance.Hide();
        }

        if (!actionable) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.fKey.wasPressedThisFrame)
        {
            RequestOpenMarketPreviewServerRpc();
        }
    }

    /// <summary>Sadece Serbest Mod'dayken VE Market kapalıyken aksiyon alınabilir - bkz. sınıf üstü not.</summary>
    private bool IsActionable()
    {
        if (dayCycleManager == null) return false;
        if (!dayCycleManager.IsFreeMode.Value) return false;
        if (marketManager != null && marketManager.IsOpen.Value) return false;
        return true;
    }

// KULLANICI ISTEGI: F artik gunu DOGRUDAN bitirmiyor, sadece market ekranini acar - gercek
    // gun bitirme artik MarketManager.RequestEndDayServerRpc uzerinden market ekranindaki
    // KIRMIZI [Gunu Bitir] butonuyla yapiliyor (bkz. MarketUI.cs). Bu metod artik MarketManager'a
    // vekalet ediyor.
    [ServerRpc(RequireOwnership = false)]
    private void RequestOpenMarketPreviewServerRpc()
    {
        if (!IsServer) return;

        if (marketManager == null)
        {
            Debug.LogWarning("[ComputerInteraction] marketManager atanmamış, market açılamadı.");
            return;
        }

        marketManager.OpenMarketPreviewServerRpc();
    }
}
