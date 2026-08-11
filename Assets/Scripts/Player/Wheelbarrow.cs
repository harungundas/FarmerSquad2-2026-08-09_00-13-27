using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// El Arabasi (Wheelbarrow). Assets/cars/el_arabası.glb modelinden sahneye yerlestirilen
/// prefab/obje'ye eklenir. ISKELET (T20): satin alinana kadar devre disi (isPurchased=false),
/// gercek satin alma baglantisi T33'te MarketManager ile kurulacak - simdilik Inspector'dan
/// elle true yapilip test edilebilir.
///
/// KAPASITE (ARCHITECTURE.md Bolum 0 deltasi - GDD'nin orijinal "3 hafif/2 orta/1 agir"
/// DEGIL, orta agirlik sinifi kalktigi icin): 3 HAFIF (Light) VEYA 1 AGIR (Heavy) hayvan.
/// Karisik yukleme YOK - ilk yuklenen hayvanin agirlik sinifi kilitlenir (lockedWeightClass),
/// farkli sinif hayvan bosaltilana kadar yuklenemez.
///
/// KULLANIM: Oyuncu El Arabasina yaklasip E'ye TEK basisla itmeye baslar/birakir (surekli
/// basili degil, toggle). Iterken PlayerController.pushSpeedMultiplier=0.5 uygulanir (hiz
/// %50 duser, CarryController'daki tasima ile ayni anda da olabilir - ikisi bagimsiz).
/// Iterken/durup dururken yakindaki serbest (baskasinin tasimadigi) hayvanlar otomatik
/// El Arabasina yuklenir (CarryController'daki gibi elle E-tetiklemeli "sec/tasi" degil,
/// bu iskelet task'ta basitlik icin otomatik-yakinlik yuklemesi tercih edildi).
///
/// Deviation (dokumante edilmis basitlestirme, CarryController/HayPile ile tutarli):
/// Hayvan/Wheelbarrow parent'lama SADECE LOCAL islemdir, NetworkObject parent-sync
/// YAPILMAZ - coklu-client'ta gorsel sadece iten client'ta dogru gorunur. Teslimat alanina
/// tasima ve UnloadAll baglantisi (DeliveryZoneDetector ile) ayri bir sonraki task'ta kurulacak.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Wheelbarrow : MonoBehaviour
{
    [Header("Satin Alma (T33'te MarketManager baglanacak)")]
    [Tooltip("Satin alinmadan El Arabasi kullanilamaz (Do: 'Satin alinana kadar devre disi'). Simdilik test icin Inspector'dan elle acilabilir.")]
    public bool isPurchased = false;

    [Header("Kapasite (ARCHITECTURE.md Bolum 0: 3 hafif VEYA 1 agir)")]
    public int lightCapacity = 3;
    public int heavyCapacity = 1;

    [Header("Etkilesim")]
    [Tooltip("Oyuncunun El Arabasini itmeye baslayabilecegi / hayvan yukleyebilecegi mesafe. CarryController.carryRange=2 ile tutarli.")]
    public float interactRange = 2f;
    [Tooltip("Iterken uygulanan hiz carpani. GDD/ARCHITECTURE: hiz %50 duser.")]
    public float pushSpeedMultiplier = 0.5f;
    [Header("Iterken Sabit Yerel Transform (Play modunda elle bulunup dogrulanmis degerler - kullanici tarafindan verildi, eski otomatik parent-scale telafisi/pushOffset hesaplamasi yanlis gorunuyordu)")]
    [Tooltip("El Arabasinin oyuncuya parent olunca alacagi SABIT yerel pozisyon.")]
    public Vector3 pushLocalPosition = new Vector3(0f, -20f, 200f);
    [Tooltip("El Arabasinin oyuncuya parent olunca alacagi SABIT yerel rotasyon (Euler).")]
    public Vector3 pushLocalEulerRotation = new Vector3(-90f, 0f, 0f);
    [Tooltip("El Arabasinin oyuncuya parent olunca alacagi SABIT yerel olcek.")]
    public Vector3 pushLocalScale = new Vector3(30f, 30f, 30f);

    private readonly List<AnimalBase> loadedAnimals = new List<AnimalBase>();
    private AnimalWeightClass? lockedWeightClass = null; // ilk yuklenen hayvanin sinifina kilitlenir

    private PlayerController pushingPlayer;
    private Transform originalParent;
    private Quaternion originalLocalRotation; // sahnede dururken DOGRU gorunen rotasyon (orn. .glb model-eksen duzeltmesi, (270,0,0) gibi olabilir) - Quaternion.identity'ye GUVENME
    private Collider wheelbarrowCollider; // yerde SOLID (carpilabilir), tasinirken TRIGGER (oyuncuyu itmesin - bkz. StartPushing/StopPushing)
    private Vector3 originalLocalScale; // oyuncuya parent olunca (oyuncu scale'i cok kucuk, orn. 0.08) localScale/localPosition OTOMATIK kuculur - telafi icin sakliyoruz

    private static readonly Vector3[] LoadOffsets =
    {
        new Vector3(-0.3f, 0.5f, 0.3f),
        new Vector3(0.3f, 0.5f, 0.3f),
        new Vector3(0f, 0.5f, -0.3f),
    };

    private void Awake()
    {
        originalParent = transform.parent;
        originalLocalRotation = transform.localRotation;
        wheelbarrowCollider = GetComponent<Collider>();
        originalLocalScale = transform.localScale;
    }

    private void Update()
    {
        if (!isPurchased) return; // Do: "Satin alinana kadar devre disi"

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (pushingPlayer != null)
        {
            if (keyboard.eKey.wasPressedThisFrame)
            {
                StopPushing();
                return;
            }

            TryAutoLoadNearbyAnimal();
            return;
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            PlayerController nearest = FindNearestOwnerPlayer();
            if (nearest != null)
            {
                StartPushing(nearest);
            }
        }
        else
        {
            TryAutoLoadNearbyAnimal();
        }
    }

    private PlayerController FindNearestOwnerPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange);
        PlayerController nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var pc = hit.GetComponentInParent<PlayerController>();
            if (pc == null || !pc.IsOwner) continue;

            float sqrDist = (pc.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = pc;
            }
        }

        return nearest;
    }

private void StartPushing(PlayerController player)
    {
        pushingPlayer = player;
        player.pushSpeedMultiplier = pushSpeedMultiplier;

        var playerAnimator = player.GetComponent<Animator>();
        if (playerAnimator != null) playerAnimator.SetBool("IsPushingWheelbarrow", true);

        transform.SetParent(player.transform);

        // Onceki otomatik parent-scale telafisi (lossyScale'e gore hesaplanan localScale/
        // TransformDirection ile hesaplanan pozisyon) Play modunda YANLIS gorunuyordu.
        // Kullanici Play modunda elle deneyerek DOGRU gorunen sabit yerel degerleri buldu
        // (pushLocalPosition/pushLocalEulerRotation/pushLocalScale) - artik hesaplama
        // yapilmiyor, dogrudan bu sabit degerler uygulaniyor.
        transform.localPosition = pushLocalPosition;
        transform.localRotation = Quaternion.Euler(pushLocalEulerRotation);
        transform.localScale = pushLocalScale;

        // KRITIK: tasinirken TRIGGER yap. El Arabasinin SOLID (non-trigger) BoxCollider'i +
        // kendi (kinematic) Rigidbody'si, oyuncuya parent olunca oyuncunun kendi dinamik
        // Rigidbody'siyle surekli carpisip birbirini itmeye calisiyordu (parent bagi onlari
        // hicbir zaman gercekten ayiramadigi icin hiz katlanarak artan, durdurulamayan bir
        // "runaway" harekete sebep oluyordu). Trigger'da fiziksel itme sifirlanir, sadece
        // OverlapSphere ile yapilan yakinlik tespiti (TryAutoLoadNearbyAnimal/FindNearestOwnerPlayer)
        // etkilenmeden calismaya devam eder.
        if (wheelbarrowCollider != null) wheelbarrowCollider.isTrigger = true;

        Debug.Log("[Wheelbarrow] " + player.gameObject.name + " El Arabasini itmeye basladi (hiz x" + pushSpeedMultiplier + ").");
    }

    private void StopPushing()
    {
        if (pushingPlayer != null)
        {
            pushingPlayer.pushSpeedMultiplier = 1f;

            var playerAnimator = pushingPlayer.GetComponent<Animator>();
            if (playerAnimator != null) playerAnimator.SetBool("IsPushingWheelbarrow", false);
        }

        transform.SetParent(originalParent);
        transform.localRotation = originalLocalRotation; // birakinca da dogru duruma donsun (onceki bug: hicbir zaman geri yuklenmiyordu)
        if (wheelbarrowCollider != null) wheelbarrowCollider.isTrigger = false; // yerde dururken tekrar SOLID (carpilabilir) olsun
        transform.localScale = originalLocalScale; // parent-scale telafisinden (StartPushing'teki) once ki orijinal olcege don
        pushingPlayer = null;

        Debug.Log("[Wheelbarrow] El Arabasi birakildi.");
    }

    private void TryAutoLoadNearbyAnimal()
    {
        if (IsFull()) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange);
        foreach (var hit in hits)
        {
            var animal = hit.GetComponentInParent<AnimalBase>();
            if (animal == null || loadedAnimals.Contains(animal)) continue;
            if (animal.transform.parent != null && animal.transform.parent != transform) continue; // baskasi (CarryController) tasiyor

            if (CanLoad(animal))
            {
                Load(animal);
                return; // bir frame'de tek hayvan yuklenir
            }
        }
    }

    private bool CanLoad(AnimalBase animal)
    {
        if (animal.animalData == null) return false;
        var wc = animal.animalData.weightClass;

        if (wc == AnimalWeightClass.Heavy)
        {
            if (lockedWeightClass == AnimalWeightClass.Light) return false; // hafiflerle dolmaya baslanmis
            return loadedAnimals.Count < heavyCapacity;
        }
        else
        {
            if (lockedWeightClass == AnimalWeightClass.Heavy) return false; // agir zaten yuklu
            return loadedAnimals.Count < lightCapacity;
        }
    }

    private void Load(AnimalBase animal)
    {
        lockedWeightClass = animal.animalData.weightClass;
        int slot = loadedAnimals.Count;
        loadedAnimals.Add(animal);

        animal.transform.SetParent(transform);
        animal.transform.localPosition = LoadOffsets[Mathf.Min(slot, LoadOffsets.Length - 1)];
        animal.transform.localRotation = Quaternion.identity;

        var rb = animal.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        int cap = lockedWeightClass == AnimalWeightClass.Heavy ? heavyCapacity : lightCapacity;
        Debug.Log("[Wheelbarrow] Hayvan yuklendi: " + animal.gameObject.name + " (" + loadedAnimals.Count + "/" + cap + ")");
    }

    private bool IsFull()
    {
        if (lockedWeightClass == AnimalWeightClass.Heavy) return loadedAnimals.Count >= heavyCapacity;
        if (lockedWeightClass == AnimalWeightClass.Light) return loadedAnimals.Count >= lightCapacity;
        return false;
    }

    /// <summary>
    /// Test/debug icin: yuklu tum hayvanlari bosaltir. Teslimat alanina resmi baglanti
    /// (DeliveryZoneDetector ile) sonraki bir task'ta kurulacak - bu iskelet task'ta
    /// sadece manuel/context-menu ile cagrilabilir bir bosaltma yeterli.
    /// </summary>
    [ContextMenu("Debug: Unload All")]
    public void UnloadAll()
    {
        for (int i = 0; i < loadedAnimals.Count; i++)
        {
            var animal = loadedAnimals[i];
            if (animal == null) continue;

            animal.transform.SetParent(null);
            animal.transform.position = transform.position + transform.forward * (1f + i * 0.5f);

            var rb = animal.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
        }
        loadedAnimals.Clear();
        lockedWeightClass = null;

        Debug.Log("[Wheelbarrow] Tum hayvanlar bosaltildi.");
    }
}
