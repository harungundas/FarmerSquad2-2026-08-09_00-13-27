using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ARCHITECTURE.md "## Gün Döngüsü & Zamanlayıcı".
/// Host-authoritative NetworkVariable&lt;float&gt; geri sayım (varsayılan 240sn). Süre dolunca
/// VehicleSpawner'i durdurur (StopAcceptingCustomers) ve Serbest Mod flag'ini (IsFreeMode) açar.
/// Günlük araç sayısı tablosu (GDD Bölüm 5) burada tutulur; solo modda (oyuncu sayısı==1)
/// Gün10+ için 6 ile sınırlanır (ARCHITECTURE.md Bölüm 0 delta tablosu).
///
/// NOT: Prestij bonus araçları (T35 PrestigeManager) GetVehicleCountForDay'in döndürdüğü tabana
/// GetTotalVehicleCountForToday() içinde AYRICA eklenir - GetVehicleCountForDay SADECE
/// taban+solo-cap hesaplar, bilerek boyle birakildi (PrestigeManager'in cagirdigi ConsumeBonusServer
/// gun basinda BonusVehiclesToday'i doldurur).
/// Gerçek gün geçişi / QuotaManager bağlantısı T32'de kurulacak (bu task'ta YOK, kasıtlı).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(AudioSource))]
public class DayCycleManager : NetworkBehaviour
{





    [Header("Musteri Gelis Penceresi (GDD Bolum 5: 240sn kurali)")]
    public float customerWindowSeconds = 180f;

    [Header("Bagli Sistemler")]
    [Tooltip("240sn dolunca StopAcceptingCustomers() cagrilir.")]
    public VehicleSpawner vehicleSpawner;

    [Header("T65: Horoz Sesi (Gun Basi)")]
    [Tooltip("Gun basinda calinacak horoz sesi. T64 (Kararma/Kart/Aydinlanma zinciri) eklendiginde bu tetikleme Aydinlanma adimina tasinmali - simdilik StartDayServer'da (gun basi) calisiyor, bkz. HANDOFF deviation notu.")]
    public AudioClip roosterCrowClip;

    private AudioSource _audioSource;

    [Header("T32: Gun Tamamlama Baglantilari")]
    [Tooltip("Gun tamamlaninca kota kontrolu icin cagrilir (T30).")]
    public QuotaManager quotaManager;
    [Tooltip("Kota basariliysa (veya o gun kota yoksa) acilir. T33'te doldurulacak stub (Assets/Scripts/Market/MarketManager.cs).")]
    public MarketManager marketManager;
    [Tooltip("Kota basarisizsa acilir. T39'da doldurulacak stub (Assets/Scripts/UI/LoseScreenController.cs).")]
    public LoseScreenController loseScreenController;

    [Header("T38: Kazanma Baglantilari")]
    [Tooltip("Gun 18 (final kota) basariyla gecilince acilir (Assets/Scripts/UI/WinScreenController.cs).")]
    public WinScreenController winScreenController;
    [Tooltip("WinScreenController.Show()'a gecirilen istatistik kaynagi (Assets/Scripts/Economy/GameStatsTracker.cs).")]
    public GameStatsTracker gameStatsTracker;


    [Header("T35: Prestij Baglantisi")]
    [Tooltip("Gun basinda bekleyen bonus arac havuzunu tuketmek icin (ConsumeBonusServer).")]
    public PrestigeManager prestigeManager;


    // GDD Bolum 5 - Taban Musteri Sayisi (index 0 = Gun 1 ... index 17 = Gun 18)
    private static readonly int[] BaseVehicleCountPerDay = new int[]
    {
        3, 4, 4, // Gun 1-3
        5, 5, 5, // Gun 4-6
        6, 6, 6, // Gun 7-9
        7, 7, 7, // Gun 10-12
        8, 8, 8, // Gun 13-15
        8, 9, 9  // Gun 16-18
    };

    private const int SoloDayThreshold = 10; // ARCHITECTURE.md Bolum 0: solo modda Gun10+ icin sinirli
    private const int SoloVehicleCap = 6;
    private const int MaxVehiclesPerDay = 10; // GDD Bolum 5: "Maksimum Arac Siniri: 10 arac/gun (bonus dahil)"

    public NetworkVariable<int> CurrentDay = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<float> Timer = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsFreeMode = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>T35: Bugunku gun icin PrestigeManager'dan devralinan bonus arac sayisi (gun basinda
    /// ConsumeBonusServer ile okunur, o gune ozeldir).</summary>
    public NetworkVariable<int> BonusVehiclesToday = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Lobi->Oyun geçişinde state reset: önceki run'dan kalan CurrentDay > 1 ise reset et
        if (IsServer && CurrentDay.Value > 1)
        {
            Debug.Log("[DayCycleManager.OnNetworkSpawn] Scene reload tespit edildi. State sıfırlanıyor.");
            CurrentDay.Value = 1;
            Timer.Value = 0f;
            IsFreeMode.Value = false;
            BonusVehiclesToday.Value = 0;
        }

        IsFreeMode.OnValueChanged += OnFreeModeChanged;

        // BUG DUZELTMESI (kullanici raporu): StartDayServer() burada ARTIK cagrilmiyor.
        // Onceden koşulsuzdu - NGO host StartHost() caginca sahnedeki bu NetworkObject'i
        // otomatik spawn ediyor, yani [Lobi Olustur] tiklanir tiklanmaz oyuncu hala Lobi
        // ekranindayken gun 1 sayaci arka planda basliyordu. Artik BeginGameServer()
        // disaridan (LobbyUI countdown bitince) cagrilana kadar bekleniyor.
    }

    /// <summary>Gercek oyunun baslamasi gerektiginde (lobi countdown'u bitince, LobbyUI
    /// tarafindan) BIR KEZ cagrilir. IsServer degilse veya oyun zaten basladiysa hicbir sey
    /// yapmaz (cift cagriya karsi guvenli).</summary>
    public bool HasGameBegun { get; private set; }

    public void BeginGameServer()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[DayCycleManager] BeginGameServer sadece server'da calisir.");
            return;
        }
        if (HasGameBegun)
        {
            Debug.LogWarning("[DayCycleManager] BeginGameServer zaten cagrilmisti, tekrar baslatilmiyor.");
            return;
        }

        HasGameBegun = true;
        StartDayServer(CurrentDay.Value);
    }

    [ContextMenu("DEBUG: Begin Game NOW (solo test - lobi akisini atla)")]
    private void DebugBeginGameNow()
    {
        if (!IsServer) { Debug.LogWarning("[DayCycleManager] Sadece server'da calisir."); return; }
        BeginGameServer();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        IsFreeMode.OnValueChanged -= OnFreeModeChanged;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (IsFreeMode.Value) return;
        if (Timer.Value <= 0f) return;

        Timer.Value -= Time.deltaTime;
        if (Timer.Value <= 0f)
        {
            Timer.Value = 0f;
            EnterFreeModeServer();
        }
    }

private void StartDayServer(int day)
    {
        Timer.Value = customerWindowSeconds;
        IsFreeMode.Value = false;

        BonusVehiclesToday.Value = (prestigeManager != null) ? prestigeManager.ConsumeBonusServer() : 0;

        // BUG DUZELTMESI (kullanici raporu: Gun 2+ hic arac gelmiyordu). Eski yorum
        // "VehicleSpawner sonraki gunlerde kendi ic mantigiyla surdurur" dogru degildi -
        // VehicleSpawner'da gun-ici tekrar spawn eden bir dongu hic yoktu. Artik HER gun
        // basinda (Gun 1 dahil) BeginDayServer cagriliyor; o gun icin toplam arac sayisini
        // (GetTotalVehicleCountForToday) customerWindowSeconds penceresine yayarak spawn eden
        // gercek dongu VehicleSpawner.Update() icinde calisiyor.
        if (vehicleSpawner != null)
        {
            vehicleSpawner.BeginDayServer(GetTotalVehicleCountForToday(), customerWindowSeconds, day);
        }

        // T65: Horoz sesi - gun basinda tum client'lara reliable ClientRpc ile bildirilir
        // (host dahil, NGO ClientRpc'yi host'ta da local olarak calistirir). T64 eklenince
        // bu cagri Aydinlanma adimina tasinmali (bkz. Header tooltip).
        //
        // GECICI YAMA (02.09.2026, 2-client canli test bulgusu): StartDayServer, gun sonu
        // CompleteDayServer -> AdvanceToNextDayServer zincirinden hemen sonra, ayni frame'de
        // cagriliyor. Bu yuzden rent_deduction sesi (QuotaManager kira kesintisi) ile horoz
        // sesi neredeyse ayni anda calinip ust uste binip anlasilmaz oluyordu. Kalici cozum
        // T64'te (horozu gercekten "Aydinlanma" adimina tasimak) yapilacak - burada sadece
        // kucuk bir gecikme ile iki sesin zaman olarak ayrilmasi saglaniyor.
        StartCoroutine(PlayRoosterCrowDelayed(RoosterCrowDelaySeconds));

        Debug.Log("[DayCycleManager] Gun " + day + " basladi. Pencere: " + customerWindowSeconds +
                   "sn. Bugunku taban arac sayisi: " + GetVehicleCountForDay(day) +
                   " + Prestij bonusu " + BonusVehiclesToday.Value +
                   " = toplam " + GetTotalVehicleCountForToday());
    }

// GECICI YAMA (02.09.2026): rent_deduction ile ayni frame'de calmasini onlemek icin
    // kucuk bir gecikme. Kalici cozum T64'te (Aydinlanma adimina tasima).
    private const float RoosterCrowDelaySeconds = 1.0f;

    private System.Collections.IEnumerator PlayRoosterCrowDelayed(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        PlayRoosterCrowClientRpc();
    }

    [ClientRpc]
    private void PlayRoosterCrowClientRpc()
    {
        if (roosterCrowClip == null)
        {
            Debug.LogWarning("[DayCycleManager] roosterCrowClip atanmamis, horoz sesi calinamadi.");
            return;
        }

        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        _audioSource.PlayOneShot(roosterCrowClip);
    }


    private void EnterFreeModeServer()
    {
        IsFreeMode.Value = true;

        if (vehicleSpawner != null)
        {
            vehicleSpawner.StopAcceptingCustomers();
        }
        else
        {
            Debug.LogWarning("[DayCycleManager] vehicleSpawner atanmamis, spawn durdurulamadi.");
        }

        Debug.Log("[DayCycleManager] 240sn doldu. Serbest Mod acildi.");
    }

    /// <summary>
    /// GDD Bolum 5 taban tablosu + solo-mod override. Prestij bonus araclari (T35) HARIC.
    /// </summary>
    public int GetVehicleCountForDay(int day)
    {
        int clampedDay = Mathf.Clamp(day, 1, BaseVehicleCountPerDay.Length);
        int baseCount = BaseVehicleCountPerDay[clampedDay - 1];

        int playerCount = (NetworkManager.Singleton != null) ? NetworkManager.Singleton.ConnectedClients.Count : 1;
        bool isSolo = playerCount <= 1;

        if (isSolo && clampedDay >= SoloDayThreshold)
        {
            baseCount = Mathf.Min(baseCount, SoloVehicleCap);
        }

        return Mathf.Min(baseCount, MaxVehiclesPerDay);
    }

/// <summary>
    /// T35: GetVehicleCountForDay'in dondurdugu taban+solo-cap degerine, PrestigeManager'dan o
    /// gun icin devralinan bonusu (BonusVehiclesToday) ekler. Ust sinir (GDD: "Maksimum Arac
    /// Sinir: 10 arac/gun (bonus dahil)") bonus eklendikten SONRA tekrar uygulanir - solo modda
    /// Gun10+ icin bu sinir 6'dir (ARCHITECTURE.md Bolum 0).
    /// </summary>
    public int GetTotalVehicleCountForToday()
    {
        int baseCount = GetVehicleCountForDay(CurrentDay.Value);
        int total = baseCount + BonusVehiclesToday.Value;

        int playerCount = (NetworkManager.Singleton != null) ? NetworkManager.Singleton.ConnectedClients.Count : 1;
        bool isSolo = playerCount <= 1;
        int cap = (isSolo && CurrentDay.Value >= SoloDayThreshold) ? SoloVehicleCap : MaxVehiclesPerDay;

        return Mathf.Min(total, cap);
    }


    private void OnFreeModeChanged(bool previous, bool current)
    {
        string who = NetworkManager.Singleton != null ? ("client" + NetworkManager.Singleton.LocalClientId) : "?";
        Debug.Log("[DayCycleManager] (" + who + ") IsFreeMode degisti: " + previous + " -> " + current);
    }

    [ContextMenu("DEBUG: Force Enter Free Mode")]
    private void DebugForceFreeMode()
    {
        if (!IsServer) { Debug.LogWarning("[DayCycleManager] Sadece server'da calisir."); return; }
        Timer.Value = 0f;
        EnterFreeModeServer();
    }

[ContextMenu("DEBUG: Start Next Day RAW (kota kontrolu YOK, Gun+1)")]
    private void DebugStartNextDay()
    {
        if (!IsServer) { Debug.LogWarning("[DayCycleManager] Sadece server'da calisir."); return; }
        StartNextDayServer();
    }

[ContextMenu("DEBUG: Complete Current Day (Kota Kontrolu ile - T32 gercek akis)")]
    private void DebugCompleteDay()
    {
        if (!IsServer) { Debug.LogWarning("[DayCycleManager] Sadece server'da calisir."); return; }
        CompleteDayServer();
    }

    /// <summary>
    /// T32: "[Gunu Tamamla]" butonunun (HUD/Serbest Mod UI, Faz9'da eklenecek) cagiracagi asil akis.
    /// ARCHITECTURE.md "## 4. Veri Akisi": QuotaManager kontrolu -> basariliysa MarketManager ac ->
    /// sonraki gune gec; basarisizsa LoseScreenController tetikle (sonraki gune GECILMEZ).
    /// </summary>
public void CompleteDayServer()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[DayCycleManager] CompleteDayServer sadece server'da calisir.");
            return;
        }

        int dayJustFinished = CurrentDay.Value;

        if (quotaManager == null)
        {
            Debug.LogError("[DayCycleManager] quotaManager atanmamis, gun tamamlanamadi.");
            return;
        }

        quotaManager.CheckQuotaServerRpc(dayJustFinished);

        if (quotaManager.IsBankrupt.Value)
        {
            Debug.Log("[DayCycleManager] Gun " + dayJustFinished + " basarisiz - IFLAS. Sonraki gune GECILMIYOR.");

            if (loseScreenController != null)
            {
                loseScreenController.Show(dayJustFinished);
            }
            else
            {
                Debug.LogWarning("[DayCycleManager] loseScreenController atanmamis (T39 henuz yok) - Iflas ekrani gosterilemedi.");
            }

            return;
        }

        if (quotaManager.IsFinalQuotaDay(dayJustFinished))
        {
            Debug.Log("[DayCycleManager] Gun " + dayJustFinished + " (FINAL kota) basarili! Oyun KAZANILDI.");

            if (winScreenController != null)
            {
                winScreenController.Show(gameStatsTracker);
            }
            else
            {
                Debug.LogWarning("[DayCycleManager] winScreenController atanmamis (T38) - Basari ekrani gosterilemedi.");
            }

            return;
        }

        // KULLANICI ISTEGI: Market'teki KIRMIZI [Gunu Bitir] butonu kaldirildi, islevi TEK
        // KALAN [Sonraki Gune Gec] butonuna eklendi (MarketUI.OnNextDayClicked artik
        // RequestEndDayServerRpc -> buraya cagiriyor). Onceden burada basariliysa
        // marketManager.OpenMarket() cagrilip market TEKRAR aciliyordu (oyuncu ayri bir
        // ikinci tikla gune gecmek zorunda kaliyordu) - artik gerek yok, market zaten
        // bilgisayardan onizleme olarak acilmisti (upgrade alma firsati orada verildi),
        // kota gectiyse DOGRUDAN sonraki gune geciliyor.
        Debug.Log("[DayCycleManager] Gun " + dayJustFinished + " basarili (veya o gun kota yok). Sonraki gune geciliyor.");
        AdvanceToNextDayServer();
    }

    /// <summary>
    /// T33: MarketManager (oyuncu [Sonraki Gune Gec] dedikten sonra) tarafindan cagrilan public
    /// giris noktasi. Onceki StartNextDayServer() private kaldi (DebugStartNextDay hala onu
    /// kullaniyor, kota kontrolu ATLAYARAK ham gun atlama icin).
    /// </summary>
    public void AdvanceToNextDayServer()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[DayCycleManager] AdvanceToNextDayServer sadece server'da calisir.");
            return;
        }
        StartNextDayServer();
    }

    private void StartNextDayServer()
    {
        CurrentDay.Value += 1;
        StartDayServer(CurrentDay.Value);
    }

}
