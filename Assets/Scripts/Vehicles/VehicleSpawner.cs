using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Musteri araci spawn + kuyruk yonetim sistemi.
///
/// T22 (iskelet): sabit spawn noktasindan tek bir test aracini host-authoritative olarak
/// spawn eder. T23 EKLENTISI (kullanici talebiyle, gercek waypoint'ler kullanilarak):
/// kuyruk sirasi yonetimi eklendi - StandFront (musteriye en yakin, hizmet noktasi) +
/// 3 kuyruk slotu (Queue_Slot1/2/3, StandFront'a yakinlik sirasina gore). Yol dogrusal
/// oldugu icin (tum noktalar ayni X ekseninde, sadece Z degisir) NavMesh GEREKMEDI,
/// CustomerVehicle.cs basit Vector3.MoveTowards kullaniyor.
///
/// KUYRUK MANTIGI: queue listesi index0=StandFront isgal eden arac, index1=Queue_Slot1,
/// index2=Queue_Slot2, index3=Queue_Slot3 seklinde ORDERLI tutulur. Yeni arac spawn
/// olunca kuyruğun sonuna eklenir (bos ilk slota yonlendirilir). Bir arac StandFront'ta
/// islemini tamamlayip (yer tutucu: standDwellTime) despawn noktasina gidip yok olunca,
/// kuyruktaki HERKES bir slot ONE kaydirilir (RefreshQueueTargets).
///
/// 4'TEN FAZLA ARAC (dokumante edilmis basitlestirme): Su an sadece 4 isimli slot var
/// (Stand+3 Queue). Kuyrukta 4'ten fazla arac varsa, 5. ve sonrasi TUMU Queue_Slot3'u
/// hedefler (ust uste binebilirler, gorsel kusur) - gercek gunluk arac sayisi Faz 7'de
/// baglanana kadar bu bir sorun degil (T22 zaten "sadece 1 test araci" spawn ediyor).
/// </summary>
public class VehicleSpawner : NetworkBehaviour
{
    [Header("Spawn (T22 iskelet: tek araclik sabit test)")]
    [Tooltip("Assets/cars/blue_truck.prefab (NetworkObject eklenmis, DefaultNetworkPrefabs.asset listesine kayitli).")]
    public GameObject truckPrefab;

    [Tooltip("blue_truck.glb ham modeli sahnenin genel olcegine gore kucuk kaliyor - gorsel dogrulama sonrasi ayarlanabilir.")]
    public float spawnScale = 10f;

    [Header("Test Siparisi (T21 OrderData ile, orn. 2 Tavuk Satis) - SADECE GUN 1")]
    public AnimalData testAnimalData;
    public int testOrderCount = 2;
    public OrderDirection testOrderDirection = OrderDirection.Satis;

    [Header("Rastgele Siparis Havuzu (Gun 2+, kullanici raporu duzeltmesi)")]
    [Tooltip("5 hayvan turunun AnimalData asset'leri (Assets/Data/Animals/). Inspector'da atanmali.")]
    public AnimalData[] allAnimalData;

    [Header("Kuyruk Waypoint'leri (T23 - kullanicinin sahnede yerlestirdigi noktalarla eslesir)")]
    [Tooltip("Sahnedeki 'spawn_olma_noktası' objesi.")]
    public Transform spawnPoint;
    [Tooltip("Sahnedeki 'yok_olma_noktası' objesi.")]
    public Transform despawnPoint;
    [Tooltip("Musteriye en yakin, hizmet noktasi.")]
    public Transform standFrontPoint;
    public Transform queueSlot1;
    public Transform queueSlot2;
    public Transform queueSlot3;

    
    [Header("Gun Dongusu Baglantisi (T31)")]
    [Tooltip("DayCycleManager 240sn timer'i dolunca StopAcceptingCustomers() cagirir; false ise yeni arac spawn edilmez.")]
    public bool IsAcceptingCustomers = true;

    [Header("Spawn Gecikmesi (kullanici duzeltmesi: araclar ayni anda spawn olmasin)")]
    [Tooltip("Bos slot doldururken art arda spawn edilen araclar arasinda beklenecek belirgin sure (saniye). Gunun ilk aracinda bu bekleme YOK, sadece 2. ve sonraki araclar arasinda uygulanir.")]
    public float spawnStaggerDelaySeconds = 2.5f;

    /// <summary>DayCycleManager (T31) 240sn dolunca bunu cagirir.</summary>
public void StopAcceptingCustomers()
    {
        IsAcceptingCustomers = false;
        Debug.Log("[VehicleSpawner] Musteri gelisi durduruldu (DayCycleManager tarafindan).");
    }
public Transform DespawnPoint => despawnPoint;
    public Transform StandFrontPoint => standFrontPoint;

    private readonly List<CustomerVehicle> queue = new List<CustomerVehicle>();

    /// <summary>T26 StandInteraction icin: StandFront'ta bekleyen (varsa) araci dondurur, yoksa null.</summary>
    public CustomerVehicle GetVehicleWaitingAtStand()
    {
        foreach (var v in queue)
        {
            if (v != null && v.IsWaitingAtStand) return v;
        }
        return null;
    }

public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // BUG DUZELTMESI (kullanici raporu): SpawnTestVehicle() burada ARTIK otomatik
        // cagrilmiyor. Onceden koşulsuzdu - NGO host StartHost() caginca bu NetworkObject
        // otomatik spawn ediyor, yani [Lobi Olustur] tiklanir tiklanmaz oyuncu hala Lobi
        // ekranindayken "2x Tavuk siparisi geldi" uyarisi cikiyordu. Artik BeginSpawning()
        // disaridan (DayCycleManager.BeginGameServer -> gercek gun 1 baslangici) cagrilana
        // kadar bekleniyor.
    }

    // BUG DUZELTMESI (kullanici raporu, Gun 2+ hic arac gelmiyordu): Eski yorum
    // "VehicleSpawner sonraki gunlerde kendi ic mantigiyla spawn dongusunu surdurur"
    // YANLISTI - burada gun-ici tekrar spawn eden hicbir dongu yoktu, sadece T22'nin tek
    // seferlik SpawnTestVehicle() metodu vardi. Artik gercek gunluk dongu burada: BeginDayServer
    // HER gun basi (Gun 1 dahil) DayCycleManager tarafindan cagrilir, o gun icin toplam arac
    // sayisini (GetTotalVehicleCountForToday) customerWindowSeconds penceresine esit araliklarla
    // yayar. Update() bu araligi sayip sirayla SpawnTestVehicle() cagirir.
    private int vehiclesRemainingToday = 0;
    private int currentDayForSpawning = 1;

    /// <summary>DayCycleManager.StartDayServer() tarafindan HER gun basi (Gun 1 dahil) cagrilir.
    /// KULLANICI DUZELTMESI (eski zamana-yayma davranisi BUG'di, boyle istenmemisti): Araclar
    /// artik windowSeconds penceresine esit araliklarla YAYILMIYOR. Gun basinda musait kuyruk
    /// slotu (StandFront + 3 Queue = 4 slot) oldugu surece, bugunku toplam arac sayisindan
    /// (en fazla 4 tanesi) ANINDA spawn edilir. Kalan araclar, oyuncu bir araci
    /// bitirip (ResolveOrder -> despawn) slot bosalttikca BIRER BIRER (FillAvailableSlotsServer
    /// araciligiyla) spawn edilir - zamanlayiciya bagli DEGIL.
    /// windowSeconds parametresi artik spawn araligi icin KULLANILMIYOR (DayCycleManager hala
    /// gecirir, sadece log icin saklanir) - musteri gelis penceresinin KAPANMASI
    /// (IsAcceptingCustomers=false, DayCycleManager'in 240/180sn zamanlayicisi) ayri bir
    /// mekanizma olarak AYNEN calismaya devam ediyor; sadece spawn ARALIGI artik ona bagli degil.</summary>
    public void BeginDayServer(int vehicleCountForToday, float windowSeconds, int currentDay)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[VehicleSpawner] BeginDayServer sadece server'da calisir.");
            return;
        }

        IsAcceptingCustomers = true;
        vehiclesRemainingToday = Mathf.Max(0, vehicleCountForToday);
        currentDayForSpawning = currentDay;

        Debug.Log("[VehicleSpawner] Gun " + currentDay + " basladi, bugun toplam " + vehiclesRemainingToday + " arac. Musait slotlar aninda dolduruluyor, kalanlar slot bosaldikca gelecek.");

        FillAvailableSlotsServer();
    }

    /// <summary>Kuyrukta (StandFront + 3 Queue = 4 slot) bos yer var VE bugun icin hala arac
    /// hakki kaldiysa SpawnTestVehicle() cagirir - dolana veya hak bitene kadar tekrarlar.
    /// Gun basinda (BeginDayServer) ve bir arac standdan ayrilip slot bosaldiginda
    /// (OnVehicleLeavingStandServer) cagrilir.
    /// KULLANICI DUZELTMESI: Araclar artik AYNI ANDA (tek frame'de) spawn olmuyor - art arda
    /// gelen spawn'lar arasinda spawnStaggerDelaySeconds kadar BELIRGIN bir gecikme var (coroutine
    /// ile). Zaten calisan bir doldurma coroutine'i varsa (isFillingSlots) tekrar baslatilmaz;
    /// coroutine kendi ici dongusunde kosullari her adimda tekrar kontrol eder.</summary>
    private bool isFillingSlots = false;

    private void FillAvailableSlotsServer()
    {
        if (isFillingSlots) return;
        StartCoroutine(FillAvailableSlotsCoroutine());
    }

    private IEnumerator FillAvailableSlotsCoroutine()
    {
        isFillingSlots = true;
        int maxSlots = QueueWaypoints.Length;
        bool spawnedAtLeastOnce = false;

        while (IsAcceptingCustomers && vehiclesRemainingToday > 0 && queue.Count < maxSlots)
        {
            if (spawnedAtLeastOnce)
            {
                yield return new WaitForSeconds(spawnStaggerDelaySeconds);

                // Bekleme sirasinda kosullar degismis olabilir (gun bitmis, slotlar dolmus vs.) - tekrar kontrol et.
                if (!IsAcceptingCustomers || vehiclesRemainingToday <= 0 || queue.Count >= maxSlots) break;
            }

            SpawnTestVehicle();
            vehiclesRemainingToday--;
            spawnedAtLeastOnce = true;
        }

        isFillingSlots = false;
    }

    private Transform[] QueueWaypoints => new[] { standFrontPoint, queueSlot1, queueSlot2, queueSlot3 };

    private void SpawnTestVehicle()
    {
        if (!IsAcceptingCustomers)
        {
            Debug.Log("[VehicleSpawner] IsAcceptingCustomers=false, spawn iptal (Serbest Mod).");
            return;
        }

        if (truckPrefab == null)
        {
            Debug.LogError("[VehicleSpawner] truckPrefab atanmamis, spawn iptal.");
            return;
        }
        if (spawnPoint == null)
        {
            Debug.LogError("[VehicleSpawner] spawnPoint atanmamis, spawn iptal.");
            return;
        }

        GameObject vehicleInstance = Instantiate(truckPrefab, spawnPoint.position, Quaternion.identity);
        vehicleInstance.transform.localScale = Vector3.one * spawnScale;

        var networkObject = vehicleInstance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("[VehicleSpawner] truckPrefab'da NetworkObject yok, spawn iptal.");
            Destroy(vehicleInstance);
            return;
        }

        var customerVehicle = vehicleInstance.GetComponent<CustomerVehicle>();
        if (customerVehicle == null) customerVehicle = vehicleInstance.AddComponent<CustomerVehicle>();
        customerVehicle.Initialize(this);

        networkObject.Spawn();

        queue.Add(customerVehicle);
        RefreshQueueTargets();

        // BUG DUZELTMESI (kullanici raporu: hep "2 Tavuk" siparisi geliyordu). Gun 1'de
        // GDD/ARCHITECTURE geregi sabit test siparisi ("2 Tavuk Satis") KORUNUR (oyuncunun
        // ilk gunu ogrenmesi icin ongorulen deger), Gun 2+'da GenerateRandomOrder() ile
        // tur/adet/yon HER seferinde rastgele secilir. Siparis-arac baglantisi (hangi
        // aracin hangi siparisi tasidigi) T24 Pazarlik Sistemi'nde kurulacak.
        var testOrder = (currentDayForSpawning <= 1)
            ? new OrderData(AnimalSpecies.Chicken, testOrderCount, testOrderDirection, testAnimalData)
            : GenerateRandomOrder();
        customerVehicle.CurrentOrder = testOrder; // T26: StandInteraction'in okuyabilmesi icin araca kaydedilir
        Debug.Log("[VehicleSpawner] Test araci spawn edildi: " + vehicleInstance.name +
                   " | Siparis: " + testOrder.count + "x " + testOrder.species +
                   " (" + testOrder.direction + ") basePrice=" + testOrder.basePrice);

        NotifyVehicleArrivedClientRpc(testOrder.species, testOrder.count, testOrder.direction);

    }

    /// <summary>Gun 2+ icin rastgele siparis uretir: allAnimalData'dan rastgele bir tur, 1-3
    /// arasi rastgele adet, %50 Satis/%50 Alim rastgele yon. allAnimalData bos/atanmamissa
    /// guvenli sekilde sabit test siparisine (2x Tavuk Satis) geri doner.</summary>
    private OrderData GenerateRandomOrder()
    {
        if (allAnimalData == null || allAnimalData.Length == 0)
        {
            Debug.LogWarning("[VehicleSpawner] allAnimalData atanmamis/bos, sabit test siparisine donuluyor.");
            return new OrderData(AnimalSpecies.Chicken, testOrderCount, testOrderDirection, testAnimalData);
        }

        var randomAnimal = allAnimalData[Random.Range(0, allAnimalData.Length)];

        // BUG DUZELTMESI (kullanici raporu: 2 Inek/2 At siparisi gelip tasima imkansiz hale
        // geliyordu). Agir hayvanlarin (Inek/At) tasima kapasitesi karakter basina 1'dir
        // (El Arabasi bile 1 agir tasir, ARCHITECTURE.md "## Tasima & Etkilesim"), bu yuzden
        // Agir siniftaki siparisler HER ZAMAN 1 adetle sinirlanir. Hafif hayvanlar (Tavuk/
        // Koyun/Keci) 1-3 arasi rastgele kalir.
        int randomCount = (randomAnimal.weightClass == AnimalWeightClass.Heavy) ? 1 : Random.Range(1, 4);
        OrderDirection randomDirection = (Random.value < 0.5f) ? OrderDirection.Satis : OrderDirection.Alim;

        return new OrderData(randomAnimal.species, randomCount, randomDirection, randomAnimal);
    }

    private void RefreshQueueTargets()
    {
        var waypoints = QueueWaypoints;
        for (int i = 0; i < queue.Count; i++)
        {
            Transform target = i < waypoints.Length ? waypoints[i] : queueSlot3;
            queue[i].SetTarget(target);
        }
    }

    /// <summary>
    /// KULLANICI DUZELTMESI: Standdan ayrilan aracin ARDINDAN kuyruktakilerin ilerlemesi icin
    /// aracin haritadan tamamen yok olmasini (despawn) beklemeye gerek YOK. Bu yuzden kuyruktan
    /// cikarma + RefreshQueueTargets + yeni arac doldurma islemleri artik BURADA degil,
    /// CustomerVehicle.ResolveOrder() standdan ayrilma anini bildirdiginde
    /// (OnVehicleLeavingStandServer) yapiliyor. Arac bu noktaya (despawn noktasina) fiilen
    /// ULASTIGINDA ise sadece NetworkObject'i gercekten yok etmek gerekiyor - kuyruk zaten
    /// cok once (arac standdan ayrilir ayrilmaz) guncellenmisti.
    /// </summary>
    public void OnVehicleDespawning(CustomerVehicle vehicle)
    {
        // Guvenlik agi: normalde OnVehicleLeavingStandServer'da zaten cikarilmis olur (idempotent).
        queue.Remove(vehicle);

        var no = vehicle.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned) no.Despawn();
        else Destroy(vehicle.gameObject);
    }

    /// <summary>
    /// KULLANICI DUZELTMESI: CustomerVehicle.ResolveOrder() cagrildigi (arac standdan ayrilip
    /// despawn noktasina yonlendigi) AN cagrilir - aracin despawn noktasina fiilen ULASMASINI
    /// veya haritadan silinmesini BEKLEMEZ. Araci kuyruktan hemen cikarir, kalan kuyruk bir
    /// slot one kayar (RefreshQueueTargets) ve bugun icin arac hakki kaldiysa yeni bir arac
    /// (belirgin gecikmeyle) spawn siraligina alinir.
    /// </summary>
    public void OnVehicleLeavingStandServer(CustomerVehicle vehicle)
    {
        queue.Remove(vehicle);
        RefreshQueueTargets();
        FillAvailableSlotsServer();
    }



    /// <summary>T37: Sunucu tarafinda spawn tamamlaninca tum client'lara "Arac geldi!" HUD uyarisini tetikler.</summary>
    [ClientRpc]
    private void NotifyVehicleArrivedClientRpc(AnimalSpecies species, int count, OrderDirection direction)
    {
        if (HUDController.Instance != null)
        {
            HUDController.Instance.ShowVehicleArrivedAlert(species, count, direction);
        }
    }
}
