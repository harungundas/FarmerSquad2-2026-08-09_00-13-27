using System.Collections.Generic;
using System.Linq;
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
/// tasiyabilmesi GDD'nin orijinal tablosuyla birebir uyumludur (Yetiskin/Cocuk/Kadin/Yasli
/// icin Inek/At dahil her hayvan 1 adet tasinabilir).
///
/// E tusu davranisi baglam-duyarlidir: yakinda alinabilecek (kapasite dolu degilse) bir
/// hayvan varsa TEK E BASISI alma islemidir; aksi halde (kapasite dolu veya yakinda uygun
/// hayvan yoksa) TEK E BASISI tum tasinan hayvanlari birakir.
///
/// Deviation (dokumante edilmis basitlestirme, HayPile/HayCarryState ile tutarli): Hayvanin
/// tasima sirasinda transform.SetParent ile karaktere baglanmasi SADECE LOCAL/sahne-ici bir
/// islemdir, NetworkObject parent-sync/ownership-transfer YAPILMAZ - coklu-client testinde
/// tasima gorseli sadece tasiyan client'ta dogru gorunebilir. Tam network-authoritative tasima
/// (NetworkObject.TrySetParent + ownership) ayri bir polish/network gorevi olarak ele alinmali.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CarryController : MonoBehaviour
{
    [Header("Tasima Ayarlari")]
    [Tooltip("Hayvanin bu mesafe icinde olmasi gerekir. HayCarryState.feedRange=2 ile tutarli.")]
    public float carryRange = 12f;

    private PlayerController playerController;
    private Animator animator;
    private readonly List<AnimalBase> carriedAnimals = new List<AnimalBase>();

    // Tasinan hayvanlar icin basit yerlesim noktalari (sirt/on, Sisman'in 2 hafif hayvani icin yan yana).
    private static readonly Vector3[] CarryOffsets = { new Vector3(-0.3f, 1.3f, 0.4f), new Vector3(0.3f, 1.3f, 0.4f) };

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
    }

    // DEBUG (tasima sorunu teshisi icin eklendi): bir onceki frame'de bulunan hedef, sadece
    // DEGISTIGINDE log basmak icin - her frame log spam'i yapmamak amacli.
    private AnimalBase lastLoggedNearest = null;

    private void Update()
    {
        if (!playerController.IsOwner || !playerController.IsControllable.Value) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        int capacity = playerController.classData != null ? playerController.classData.carryCapacityLight : 1;
        bool isFull = carriedAnimals.Count >= capacity;
        AnimalBase nearest = isFull ? null : FindNearestCarryableAnimal(capacity);

        // DEBUG: hedef degistiginde (yakinda tasinabilir hayvan var/yok oldu) logla.
        if (nearest != lastLoggedNearest)
        {
            if (nearest != null)
                Debug.Log("[CarryController] " + gameObject.name + " - yakinda tasinabilir hayvan bulundu: " + nearest.gameObject.name +
                           " (mesafe: " + Vector3.Distance(transform.position, nearest.transform.position).ToString("0.00") + "m, carryRange: " + carryRange + "m)");
            else
                Debug.Log("[CarryController] " + gameObject.name + " - yakinda tasinabilir hayvan YOK (isFull: " + isFull + ", capacity: " + capacity + "/" + carriedAnimals.Count + ")");
            lastLoggedNearest = nearest;
        }

        // Etkilesim ipucu: sadece bu objenin sahibi (local player) icin goster.
        if (InteractionPromptUI.Instance != null)
        {
            if (nearest != null) InteractionPromptUI.Instance.Show("E - Taşı");
            else if (carriedAnimals.Count == 0) InteractionPromptUI.Instance.Hide();
            // not: carriedAnimals.Count > 0 && nearest == null durumunda asagidaki Drop mantigi
            // calisir, prompt'u "E - Birak" olarak guncellemek istersen buraya eklenebilir (T-sonrasi).
        }

        if (nearest != null)
        {
            if (keyboard.eKey.wasPressedThisFrame)
            {
                PickUp(nearest);
            }
        }
        else if (keyboard.eKey.wasPressedThisFrame)
        {
            // DEBUG: E'ye basildi ama hicbir sey olmuyorsa SEBEBI burada gorunur.
            if (carriedAnimals.Count > 0)
            {
                DropAll();
            }
            else
            {
                Debug.Log("[CarryController] " + gameObject.name + " - E basildi ama tasinacak hayvan yok ve elde de hayvan yok (yapilacak bir sey yok).");
            }
        }
    }

    private AnimalBase FindNearestCarryableAnimal(int capacity)
    {
        bool lightOnly = capacity > 1; // Sadece Sisman (capacity=2) hafif-hayvan kisitina tabi.

        Collider[] hits = Physics.OverlapSphere(transform.position, carryRange);
        AnimalBase nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var animal = hit.GetComponentInParent<AnimalBase>();
            if (animal == null) continue;
            if (carriedAnimals.Contains(animal)) continue; // zaten tasiniyor
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

    private void PickUp(AnimalBase animal)
    {
        int slot = carriedAnimals.Count;
        carriedAnimals.Add(animal);

        animal.transform.SetParent(transform);
        animal.transform.localPosition = CarryOffsets[Mathf.Min(slot, CarryOffsets.Length - 1)];
        animal.transform.localRotation = Quaternion.identity;

        // Bug fix: AnimalIdleWander (T43) her frame transform.position'i (world-space, parent'tan
        // BAGIMSIZ) agil sinirlari icindeki rastgele bir hedefe dogru degistiriyordu. Bu, yukaridaki
        // SetParent/localPosition ile CELISIYOR - tasinan hayvan karakterin sirtindan surekli padoktaki
        // hedef noktaya "kayiyor", tasima gorsel olarak calismiyor gibi gorunuyordu. Tasinirken bu
        // dekoratif katman devre disi birakilir (DropAll() icinde geri acilir).
        var wander = animal.GetComponent<AnimalIdleWander>();
        if (wander != null) wander.enabled = false;

        var rb = animal.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        if (animator != null) animator.SetBool("IsCarrying", true);

        Debug.Log("[CarryController] " + gameObject.name + " hayvani sirtladi: " + animal.gameObject.name +
                   " (" + carriedAnimals.Count + "/" + (playerController.classData != null ? playerController.classData.carryCapacityLight : 1) + ")");
    }

    private void DropAll()
    {
        for (int i = 0; i < carriedAnimals.Count; i++)
        {
            var animal = carriedAnimals[i];
            if (animal == null) continue;

            animal.transform.SetParent(null);
            Vector3 dropOffset = transform.forward * (1f + i * 0.6f);
            animal.transform.position = transform.position + dropOffset;

            var rb = animal.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            var wander = animal.GetComponent<AnimalIdleWander>();
            if (wander != null) wander.enabled = true;
        }

        Debug.Log("[CarryController] " + gameObject.name + " " + carriedAnimals.Count + " hayvani birakti.");
        carriedAnimals.Clear();

        if (animator != null) animator.SetBool("IsCarrying", false);
    }
}
