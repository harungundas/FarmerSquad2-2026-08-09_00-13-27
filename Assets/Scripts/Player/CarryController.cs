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
/// hayvan varsa TEK E BASISI (basili tutmaya GEREK YOK) alma islemidir; aksi halde (kapasite
/// dolu veya yakinda uygun hayvan yoksa) TEK E BASISI tum tasinan hayvanlari birakir.
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
    

    // Tasima yerlesimi (kullanici karari):
    // - Sisman DISI karakterler (kapasite=1): tasidiklari TEK hayvan karakterin SAGINA bakar.
    // - Sisman (kapasite=2): iki hayvan da ONE bakar, sanki iki kolun altinda tasiniyormus gibi
    //   hafifce sagda/solda durur. Sira HER ZAMAN ayni: ilk alinan hayvan (slot 0) SAG kola,
    //   ikinci alinan hayvan (slot 1) SOL kola gelir (carriedAnimals listesine ekleme sirasiyla
    //   birebir eslesir, bu yuzden sira degismez).
    private static readonly Vector3 SingleCarryOffset = new Vector3(0.25f, 1.3f, 0.3f);
    private static readonly Vector3[] TwoCarryOffsets = { new Vector3(0.3f, 1.2f, 0.5f), new Vector3(-0.3f, 1.2f, 0.5f) };
    private static readonly Quaternion SingleCarryRotationOffset = Quaternion.Euler(0f, 90f, 0f); // saga bakar

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
    }

private void Update()
    {
        if (!playerController.IsOwner || !playerController.IsControllable.Value) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        int capacity = playerController.classData != null ? playerController.classData.carryCapacityLight : 1;
        bool isFull = carriedAnimals.Count >= capacity;
        AnimalBase nearest = isFull ? null : FindNearestCarryableAnimal(capacity);

        // BUG DUZELTMESI (kullanici karari): eskiden E BASILI TUTMAK gerekiyordu (pickupHoldDuration).
        // Artik TEK E BASISI yeterli - hem alma hem birakma icin wasPressedThisFrame kullanilir.
        if (nearest != null)
        {
            if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Show(nearest.transform, "E - Bas - Hayvanı Sırtla");

            if (keyboard.eKey.wasPressedThisFrame)
            {
                PickUp(nearest);
            }
        }
        else if (carriedAnimals.Count > 0)
        {
            if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.ShowTextOnly("E - Bas - Bırak");

            if (keyboard.eKey.wasPressedThisFrame)
            {
                DropAll();
            }
        }
        else
        {
            if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Hide();
        }
    }

    private void SnapToGround(Transform animalTransform)
    {
        // BUG DUZELTMESI (kullanici bildirdi): hayvan birakilinca havada asili kaliyordu.
        // Sebep: 5 hayvan turu 4 farkli asset paketinden geliyor ve fizik kurulumlari
        // TUTARSIZ - Cow/Goat Rigidbody.useGravity=false, Horse/Chicken Rigidbody YERINE
        // sadece CharacterController var (CharacterController kendiliginden yercekimi
        // UYGULAMAZ), Sheep'te ikisi de yok. isKinematic=false bu yuzden guvenilir dusme
        // saglamiyordu. Fizige GUVENMEDEN, birakma aninda asagi raycast atip hayvani
        // dogrudan zemin/nesne yuksekligine oturtuyoruz.
        Vector3 origin = animalTransform.position + Vector3.up * 10f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 40f);

        RaycastHit best = default;
        bool found = false;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.transform == animalTransform || hit.transform.IsChildOf(animalTransform)) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            // BUG DUZELTMESI (hayvan havada asili kaliyor sorunu): citler gibi bazi objelerin
            // trigger collider'i var (orn. PenStockEntryDetector icin) ve bu trigger, gercek
            // zeminden DAHA YUKARIDA bir yukseklikte hit veriyordu - raycast ilk (en yakin) hit'i
            // "zemin" saniyordu ve hayvan o trigger'in yuksekligine oturup havada kalmis gibi
            // gorunuyordu. Trigger collider'lari zemin adayi olarak SAYMIYORUZ.
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

        // BUG DUZELTMESI: AnimalIdleWander bu bayragi gorunce kendi hareketini durdurur -
        // aksi halde asagidaki SetParent/localPosition atamasi her frame ezilip hayvan
        // gorunurde eski yerinde kalmaya devam ediyordu.
        animal.IsBeingCarried = true;

        animal.transform.SetParent(transform);

        // BUG DUZELTMESI: eskiden buraya "animal.transform.localPosition = CarryOffsets[...]"
        // yaziliyordu. Bu LOCAL bir deger oldugu icin karakterin transform.localScale'ine
        // (orn. 0.08) BOLUNEREK yorumlaniyordu - yani 1.3 birimlik "sirtta tasima" ofseti
        // gercek dunyada sadece ~0.1m'ye denk geliyordu, hayvan neredeyse karakterin ayaklarinin
        // icinde/zeminin altinda kalip gorunmez oluyordu (bazi govde kopyalarinda ise TERSI olup
        // haritada uzaga savruluyordu - govde/prefab olcegi 0.08'den FARKLIYSA ayni sayilar cok
        // buyuk bir dunya-uzayi ofsetine karsilik geliyordu). Duzeltme: ofseti karakterin KENDI
        // yon vektorleriyle (right/up/forward - bunlar olcekten BAGIMSIZDIR) DUNYA UZAYINDA
        // hesaplayip animal.transform.position'a dogrudan world-space olarak yaziyoruz. Boylece
        // sonuc, karakterin scale'i ne olursa olsun (0.08, 1, veya baska bir govde kopyasinda
        // farkli bir deger) HER ZAMAN ayni gercek-dunya mesafesinde (~0.3-1.3m) kalir.
        int capacityForRotation = playerController.classData != null ? playerController.classData.carryCapacityLight : 1;
        bool isTwoCarry = capacityForRotation > 1; // sadece Sisman

        Vector3 offsetLocal;
        Quaternion rotationOffset;
        if (isTwoCarry)
        {
            offsetLocal = TwoCarryOffsets[Mathf.Min(slot, TwoCarryOffsets.Length - 1)];
            rotationOffset = Quaternion.identity; // Sisman'da hayvanlar ONE bakar
        }
        else
        {
            offsetLocal = SingleCarryOffset;
            rotationOffset = SingleCarryRotationOffset; // digger karakterlerde hayvan SAGA bakar
        }

        Vector3 worldOffset = transform.right * offsetLocal.x + transform.up * offsetLocal.y + transform.forward * offsetLocal.z;
        animal.transform.position = transform.position + worldOffset;
        animal.transform.rotation = transform.rotation * rotationOffset;

        var rb = animal.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // BUG DUZELTMESI: hayvan sirtlanmadan once fizik motorundan kalma bir hiz (velocity)
            // tasiyor olabilirdi (dusme, itilme, vb.) - bu hiz Rigidbody.isKinematic=true iken
            // gizli kaliyor ve DropAll()'da isKinematic=false yapilinca aninda geri uygulaniyordu,
            // bu da "birakilan hayvanin yavasca ucarak uzaklasmasi" bug'ina sebep oluyordu.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        var controller = animal.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        // BUG DUZELTMESI (kullanici bildirdi: "tuslara dokunmadan hareket ediyorum"): CharacterController
        // disable edilse de hayvanin gercek Collider'i (BoxCollider/CapsuleCollider vb.) ACIK kaliyordu.
        // Kinematik hale gelen hayvan karaktere ~0.3-1.3m offsetle parent'landiginda bu collider,
        // karakterin KENDI collider'iyla ic ice giriyor - fizik motoru her FixedUpdate'te ayristirma
        // kuvveti uygulayip karakteri tus basilmadan kaydiriyordu. Tasima suresince TUM Collider'lari
        // kapatiyoruz (GetComponents COKLU - bazi govdelerde birden fazla collider olabilir).
        var carriedColliders = animal.GetComponents<Collider>();
        foreach (var col in carriedColliders) col.enabled = false;

        if (animator != null) animator.SetBool("IsCarrying", true);

        Debug.Log("[CarryController] " + gameObject.name + " hayvani sirtladi: " + animal.gameObject.name +
                   " (" + carriedAnimals.Count + "/" + (playerController.classData != null ? playerController.classData.carryCapacityLight : 1) + ")" +
                   " localPos=" + animal.transform.localPosition + " worldPos=" + animal.transform.position);
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
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
            }

            var controller = animal.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = true;

            // PickUp()'ta kapatilan Collider'lari geri aciyoruz - SnapToGround'dan ONCE acmiyoruz
            // ki raycast hala kendi collider'ina çarpip yanlis zemin bulmasin (SnapToGround zaten
            // kendi transform'unu/child'larini disliyor ama erken acmak gereksiz risk).
            var droppedColliders = animal.GetComponents<Collider>();
            foreach (var col in droppedColliders) col.enabled = true;

            // BUG DUZELTMESI (kullanici bildirdi, "havada asili kaliyor"): isKinematic=false /
            // CharacterController.enabled=true fizigin hayvani zemine dusurecegini VARSAYIYORDU
            // ama asset paketleri arasi tutarsiz kurulum yuzunden bu garanti degildi (bkz.
            // SnapToGround yorum bloğu). Raycast ile dogrudan zemine oturtuyoruz.
            SnapToGround(animal.transform);

            animal.IsBeingCarried = false;

            Debug.Log("[CarryController] birakildi: " + animal.gameObject.name + " worldPos=" + animal.transform.position);
        }

        Debug.Log("[CarryController] " + gameObject.name + " " + carriedAnimals.Count + " hayvani birakti.");
        carriedAnimals.Clear();

        if (animator != null) animator.SetBool("IsCarrying", false);
    }
}
