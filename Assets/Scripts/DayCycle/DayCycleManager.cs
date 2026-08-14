using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ARCHITECTURE.md "## Gün Döngüsü & Zamanlayıcı".
/// Host-authoritative NetworkVariable&lt;float&gt; geri sayım (varsayılan 240sn). Süre dolunca
/// VehicleSpawner'i durdurur (StopAcceptingCustomers) ve Serbest Mod flag'ini (IsFreeMode) açar.
/// Günlük araç sayısı tablosu (GDD Bölüm 5) burada tutulur; solo modda (oyuncu sayısı==1)
/// Gün10+ için 6 ile sınırlanır (ARCHITECTURE.md Bölüm 0 delta tablosu).
///
/// NOT: Prestij bonus araçları (T35 PrestigeManager, henüz yok) GetVehicleCountForDay'in
/// döndürdüğü tabana AYRICA eklenir - bu metod SADECE taban+solo-cap hesaplar.
/// Gerçek gün geçişi / QuotaManager bağlantısı T32'de kurulacak (bu task'ta YOK, kasıtlı).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class DayCycleManager : NetworkBehaviour
{
    [Header("Musteri Gelis Penceresi (GDD Bolum 5: 240sn kurali)")]
    public float customerWindowSeconds = 240f;

    [Header("Bagli Sistemler")]
    [Tooltip("240sn dolunca StopAcceptingCustomers() cagrilir.")]
    public VehicleSpawner vehicleSpawner;

    [Header("T32: Gun Tamamlama Baglantilari")]
    [Tooltip("Gun tamamlaninca kota kontrolu icin cagrilir (T30).")]
    public QuotaManager quotaManager;
    [Tooltip("Kota basariliysa (veya o gun kota yoksa) acilir. T33'te doldurulacak stub (Assets/Scripts/Market/MarketManager.cs).")]
    public MarketManager marketManager;
    [Tooltip("Kota basarisizsa acilir. T39'da doldurulacak stub (Assets/Scripts/UI/LoseScreenController.cs).")]
    public LoseScreenController loseScreenController;


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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        IsFreeMode.OnValueChanged += OnFreeModeChanged;

        if (IsServer)
        {
            StartDayServer(CurrentDay.Value);
        }
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
        Debug.Log("[DayCycleManager] Gun " + day + " basladi. Pencere: " + customerWindowSeconds +
                   "sn. Bugunku taban arac sayisi: " + GetVehicleCountForDay(day));
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

        // QuotaData.TryGetQuota sadece kota gunlerinde (3/6/9/12/15/18) true doner; digerlerinde
        // IsBankrupt HICBIR sekilde degismez (ne true ne de reset) - bu QuotaManager'in (T30) mevcut
        // davranisi, buradan degistirilmiyor.
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

        Debug.Log("[DayCycleManager] Gun " + dayJustFinished + " basarili (veya o gun kota yok). Market aciliyor.");

        if (marketManager != null)
        {
            marketManager.OpenMarket(dayJustFinished);
        }
        else
        {
            Debug.LogWarning("[DayCycleManager] marketManager atanmamis (T33 henuz yok) - market acilamadi.");
        }

        // GECICI DEVIATION (T33'e kadar): MarketManager henuz stub oldugu ve oyuncunun market'te
        // "[Sonraki Gune Gec]" demesini bekleyecek gercek bir mekanizma olmadigi icin, gun gecisi
        // burada DOGRUDAN yapiliyor. T33'te MarketManager kendi butonuyla StartNextDayServer'i
        // (veya bir callback/event'i) tetikleyecek sekilde bu satir KALDIRILMALI.
        StartNextDayServer();
    }

    private void StartNextDayServer()
    {
        CurrentDay.Value += 1;
        StartDayServer(CurrentDay.Value);
    }

}
