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

    [Header("Test Siparisi (T21 OrderData ile, orn. 2 Tavuk Satis)")]
    public AnimalData testAnimalData;
    public int testOrderCount = 2;
    public OrderDirection testOrderDirection = OrderDirection.Satis;

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

        if (!IsServer) return; // Do: "NetworkObject olarak spawn (host authoritative)"

        SpawnTestVehicle();
    }

    private Transform[] QueueWaypoints => new[] { standFrontPoint, queueSlot1, queueSlot2, queueSlot3 };

    private void SpawnTestVehicle()
    {
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

        // T21'deki OrderData constructor'i kullanilarak sabit test siparisi olusturulur.
        // Siparis-arac baglantisi (hangi aracin hangi siparisi tasidigi) T24 Pazarlik
        // Sistemi'nde kurulacak - simdilik sadece dogrulama amacli Debug.Log.
        var testOrder = new OrderData(AnimalSpecies.Chicken, testOrderCount, testOrderDirection, testAnimalData);
        customerVehicle.CurrentOrder = testOrder; // T26: StandInteraction'in okuyabilmesi icin araca kaydedilir
        Debug.Log("[VehicleSpawner] Test araci spawn edildi: " + vehicleInstance.name +
                   " | Siparis: " + testOrder.count + "x " + testOrder.species +
                   " (" + testOrder.direction + ") basePrice=" + testOrder.basePrice);
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
    /// CustomerVehicle, despawn noktasina ulastiginda bunu cagirir. Kuyruktan cikarilir,
    /// NetworkObject despawn edilir, ve kalan kuyruk bir slot one kaydirilir.
    /// </summary>
    public void OnVehicleDespawning(CustomerVehicle vehicle)
    {
        queue.Remove(vehicle);

        var no = vehicle.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned) no.Despawn();
        else Destroy(vehicle.gameObject);

        RefreshQueueTargets();
    }
}
