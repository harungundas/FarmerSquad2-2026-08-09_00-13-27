using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerController tasiyan her karakter objesine eklenir. E tusuyla en yakin
/// tasinabilir hayvani sirtlar, tekrar E ile birakir.
///
/// Kapasite kurali (GDD Bolum 6/12 tasima tablosuyla tutarli): CharacterClassData.
/// carryCapacityLight alani, o sinifin ayni anda tasiyabilecegi TOPLAM hayvan sayisidir.
/// Sadece Sisman (capacity>1, yani 2) SADECE Hafif (Light) hayvan tasiyabilir; digger
/// tum siniflarin (capacity=1) herhangi bir agirlik sinifindan (Hafif VEYA Agir) 1 hayvan
/// tasiyabilmesi GDD'nin orijinal tablosuyla birebir uyumludur.
///
/// AG MIMARISI (kullanici bildirdi: "client'in tasidigi/biraktigi hayvan host'ta / diger
/// client'ta hic gorunmuyordu, teslim de olmuyordu" - KOKLU DUZELTME):
/// Eskiden bu script SADECE MonoBehaviour idi, PickUp/DropAll DOGRUDAN input-sahibi client'ta
/// calisiyor, hayvanin transform.position'ini SADECE O MAKINEDE degistiriyordu - hayvan
/// prefablarinda NetworkTransform da OLMADIGI icin bu degisiklik hicbir baska client'a/host'a
/// YAYILMIYORDU. Artik:
/// 1) Bu sinif NetworkBehaviour'a cevrildi.
/// 2) Girdi tespiti (en yakin hayvan, E tusu) yine SADECE input-sahibi client'ta calisir (UI/
///    prompt icin yerel/duyarli olmasi gerekir) ama gercek SIRTLAMA/BIRAKMA islemini ARTIK
///    SUNUCUYA istekle yaptiriyor (RequestPickUpServerRpc / RequestDropAllServerRpc).
/// 3) Sunucu, tasinan her hayvanin pozisyonunu HER FRAME (FixedUpdate, IsServer gated) tasiyan
///    oyuncunun o anki transform'una gore yeniden hesaplar (SetParent KULLANILMAZ - Netcode
///    spawn edilmis NetworkObject'leri reparent etmeyi sevmiyor, "can only be re-parented
///    after being spawned" uyarisina sebep oluyordu).
/// 4) Hayvan prefablarina eklenen NetworkTransform (server-authoritative, Netcode'un stok
///    NetworkTransform'unun varsayilan davranisi) bu sunucu-yazimlarini OTOMATIK olarak TUM
///    client'lara (tasiyan client dahil) yayar.
/// 5) IsCarrying Animator parametresi de ayni sebepten NetworkVariable ile senkronlanir.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CarryController : NetworkBehaviour
{
    [Header("Tasima Ayarlari")]
    [Tooltip("Hayvanin bu mesafe icinde olmasi gerekir. HayCarryState.feedRange=2 ile tutarli.")]
    public float carryRange = 12f;

    private PlayerController playerController;
    private Animator animator;

    // SUNUCUDA otoriter liste - SADECE sunucunun kendi kopyasi doldurulur/okunur (RPC handler'lar
    // ve FixedUpdate takip donguesu IsServer gated). Client'larin kendi lokal kopyalari bos kalir,
    // bu KASITLI - client'lar kendi UI/prompt kararlarini netCarriedCount/netCarryingHeavy'den verir.
    private readonly List<AnimalBase> carriedAnimals = new List<AnimalBase>();

    // Client-tarafi UI/kapasite kararlari icin senkron edilen ozet bilgi - SADECE sunucu yazar.
    private NetworkVariable<int> netCarriedCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netCarryingHeavy = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // Animator "IsCarrying" bool'u - SADECE sunucu yazar, HERKES (tasiyan dahil) buradan uygular.
    private NetworkVariable<bool> netIsCarryingAnim = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Tasima yerlesimi (kullanici karari):
    private static readonly Vector3 SingleCarryOffset = new Vector3(0.25f, 1.3f, 0.3f);
    private static readonly Vector3[] TwoCarryOffsets = { new Vector3(3.5f, 1.2f, 0.5f), new Vector3(-3.5f, 1.2f, 0.5f) };
    private static readonly Vector3[] TwoCarryDropOffsets = { new Vector3(3.5f, 0f, 2f), new Vector3(-3.5f, 0f, 2f) };
    private static readonly Quaternion SingleCarryRotationOffset = Quaternion.Euler(0f, 90f, 0f);

    /// <summary>KULLANICI ISTEGI (dans ozelligi): PlayerController'in R tusuyla dans
    /// baslatabilmesi icin "eller bos mu" bilgisini disariya acan salt-okunur ozellik.
    /// netCarriedCount zaten SUNUCUdan HERKESE senkronlanan bir NetworkVariable oldugu icin
    /// bu her client'ta (sahibi dahil) dogru sonuc verir.</summary>
    public bool IsHandsEmpty => netCarriedCount.Value == 0;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        netIsCarryingAnim.OnValueChanged += OnCarryingAnimChanged;
        if (animator != null) animator.SetBool("IsCarrying", netIsCarryingAnim.Value);
    }

    public override void OnNetworkDespawn()
    {
        netIsCarryingAnim.OnValueChanged -= OnCarryingAnimChanged;
    }

    private void OnCarryingAnimChanged(bool previous, bool current)
    {
        if (animator != null) animator.SetBool("IsCarrying", current);
    }

    private void Update()
    {
        // Girdi tespiti + prompt: SADECE bu govdenin input-sahibi (yerel oyuncu) client'inda calisir.
        if (!playerController.IsOwner || !playerController.IsControllable.Value) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        int maxLightSlots = playerController.classData != null ? playerController.classData.carryCapacityLight : 1;
        bool carryingHeavy = netCarryingHeavy.Value;
        bool isFull = carryingHeavy || netCarriedCount.Value >= maxLightSlots;
        bool mustBeLight = netCarriedCount.Value > 0;
        AnimalBase nearest = isFull ? null : FindNearestCarryableAnimal(mustBeLight);

        if (nearest != null)
        {
            if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Show(nearest.transform, "E - Bas - Hayvanı Sırtla");

            if (keyboard.eKey.wasPressedThisFrame)
            {
                // KULLANICI BUG RAPORU DUZELTMESI: nearest.NetworkObject henuz SPAWN OLMAMISSA
                // (orn. az once Alim ile spawn edildi, ya da (asil suphe: eski/uyumsuz build)
                // NetworkBehaviour index kaymasi yuzunden referans bozuksa) NetworkObjectReference
                // olusturmak ArgumentException FIRLATIYORDU ("can only be created from spawned
                // NetworkObjects") - bu da E tusuna basinca SESSIZCE hicbir sey olmamasina
                // (hayvan tasinamamasina) sebep oluyordu. Artik once guvenli kontrol ediliyor.
                if (nearest.NetworkObject != null && nearest.NetworkObject.IsSpawned)
                {
                    RequestPickUpServerRpc(new NetworkObjectReference(nearest.NetworkObject));
                }
                else
                {
                    Debug.LogWarning("[CarryController] " + nearest.gameObject.name + " henuz network-spawn olmamis, sirtlanamadi.");
                }
            }
        }
        else if (netCarriedCount.Value > 0)
        {
            if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.ShowTextOnly("E - Bas - Bırak");

            if (keyboard.eKey.wasPressedThisFrame)
            {
                RequestDropAllServerRpc();
            }
        }
        else
        {
            if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Hide();
        }
    }

    /// <summary>SUNUCUDA: tasinan her hayvanin pozisyonunu, tasiyan oyuncunun O ANKI transform'una
    /// gore HER FRAME yeniden hesaplar. SetParent KULLANILMAZ (bkz. sinif basi aciklamasi) - bunun
    /// yerine NetworkTransform (hayvan prefablarina eklendi) bu sunucu-yazimini otomatik yayar.</summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (carriedAnimals.Count == 0) return;

        bool canCarryTwoLight = playerController.classData != null && playerController.classData.carryCapacityLight > 1;

        for (int i = 0; i < carriedAnimals.Count; i++)
        {
            var animal = carriedAnimals[i];
            if (animal == null) continue;

            bool isHeavyAnimal = animal.animalData != null && animal.animalData.weightClass == AnimalWeightClass.Heavy;
            Vector3 offsetLocal;
            Quaternion rotationOffset;
            if (canCarryTwoLight && !isHeavyAnimal)
            {
                offsetLocal = TwoCarryOffsets[Mathf.Min(i, TwoCarryOffsets.Length - 1)];
                rotationOffset = Quaternion.identity;
            }
            else
            {
                offsetLocal = SingleCarryOffset;
                rotationOffset = SingleCarryRotationOffset;
            }

            Vector3 worldOffset = transform.right * offsetLocal.x + transform.up * offsetLocal.y + transform.forward * offsetLocal.z;
            animal.transform.position = transform.position + worldOffset;
            animal.transform.rotation = transform.rotation * rotationOffset;
        }
    }

    private void SnapToGround(Transform animalTransform)
    {
        // BUG DUZELTMESI (kullanici bildirdi): hayvan birakilinca havada asili kaliyordu.
        // Fizige GUVENMEDEN, birakma aninda asagi raycast atip hayvani dogrudan zemin/nesne
        // yuksekligine oturtuyoruz. SADECE SUNUCUDA cagrilir.
        Vector3 origin = animalTransform.position + Vector3.up * 10f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 40f);

        RaycastHit best = default;
        bool found = false;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.transform == animalTransform || hit.transform.IsChildOf(animalTransform)) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            // Trigger collider'lari (citler vb.) zemin adayi saymiyoruz.
            if (hit.collider.isTrigger) continue;

            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                best = hit;
                found = true;
            }
        }

        if (found)
        {
            Vector3 p = animalTransform.position;
            p.y = best.point.y;
            animalTransform.position = p;
        }
        else
        {
            Debug.LogWarning("[CarryController] SnapToGround: " + animalTransform.name + " altinda zemin bulunamadi (raycast bos), pozisyon degistirilmedi.");
        }
    }

    private AnimalBase FindNearestCarryableAnimal(bool lightOnly)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, carryRange);
        AnimalBase nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var animal = hit.GetComponentInParent<AnimalBase>();
            if (animal == null) continue;
            if (animal.IsBeingCarried) continue; // baskasi (veya biz) zaten tasiyor - artik NETWORK genelinde dogru.

            if (lightOnly && animal.animalData != null && animal.animalData.weightClass != AnimalWeightClass.Light) continue;

            float sqrDist = (animal.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = animal;
            }
        }

        return nearest;
    }

    /// <summary>Client, sirtlamak istedigi hayvani bu ServerRpc ile ISTER - gercek mutasyon
    /// SADECE burada, sunucuda gerceklesir. RequireOwnership varsayilan TRUE - sadece bu
    /// govdenin sahibi cagirabilir (baskasinin karakteri adina hayvan sirtlanamaz).</summary>
    [ServerRpc]
    private void RequestPickUpServerRpc(NetworkObjectReference animalRef)
    {
        if (!animalRef.TryGet(out NetworkObject animalNo)) return;
        var animal = animalNo.GetComponent<AnimalBase>();
        if (animal == null) return;
        if (animal.IsBeingCarried) return; // yaris durumu guvenligi: baskasi az once aldi.

        int maxLightSlots = playerController.classData != null ? playerController.classData.carryCapacityLight : 1;
        bool isHeavyAnimal = animal.animalData != null && animal.animalData.weightClass == AnimalWeightClass.Heavy;
        bool carryingHeavyAlready = netCarryingHeavy.Value;
        bool isFull = carryingHeavyAlready || carriedAnimals.Count >= maxLightSlots;
        if (isFull) return;
        if (carriedAnimals.Count > 0 && isHeavyAnimal) return; // 2. slot sadece hafif kabul eder.

        int slot = carriedAnimals.Count;
        carriedAnimals.Add(animal);
        animal.IsBeingCarried = true;

        var rb = animal.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        var controller = animal.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        var carriedColliders = animal.GetComponents<Collider>();
        foreach (var col in carriedColliders) col.enabled = false;

        netCarriedCount.Value = carriedAnimals.Count;
        netCarryingHeavy.Value = isHeavyAnimal || carryingHeavyAlready;
        netIsCarryingAnim.Value = true;

        Debug.Log("[CarryController] (sunucu) " + gameObject.name + " hayvani sirtladi: " + animal.gameObject.name + " (" + carriedAnimals.Count + "/" + maxLightSlots + ")");
    }

    /// <summary>Client, elindekileri birakmak icin bu ServerRpc'yi cagirir - gercek mutasyon
    /// SADECE sunucuda gerceklesir.</summary>
    [ServerRpc]
    private void RequestDropAllServerRpc()
    {
        for (int i = 0; i < carriedAnimals.Count; i++)
        {
            var animal = carriedAnimals[i];
            if (animal == null) continue;

            Vector3 dropOffset;
            if (carriedAnimals.Count > 1)
            {
                Vector3 offsetLocal = TwoCarryDropOffsets[Mathf.Min(i, TwoCarryDropOffsets.Length - 1)];
                dropOffset = transform.right * offsetLocal.x + transform.forward * offsetLocal.z;
            }
            else
            {
                dropOffset = transform.forward * (1f + i * 0.6f);
            }
            animal.transform.position = transform.position + dropOffset;

            var rb = animal.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true; // SnapToGround'a guveniyoruz, fizik motoruna degil (bkz. yorum).
            }

            var controller = animal.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = true;

            var droppedColliders = animal.GetComponents<Collider>();
            foreach (var col in droppedColliders) col.enabled = true;

            SnapToGround(animal.transform);

            animal.IsBeingCarried = false;

            Debug.Log("[CarryController] (sunucu) birakildi: " + animal.gameObject.name + " worldPos=" + animal.transform.position);
        }

        Debug.Log("[CarryController] (sunucu) " + gameObject.name + " " + carriedAnimals.Count + " hayvani birakti.");
        carriedAnimals.Clear();
        netCarriedCount.Value = 0;
        netCarryingHeavy.Value = false;
        netIsCarryingAnim.Value = false;
    }
}
