using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Her hayvan instance'ina eklenir. Hangi ağıla ait oldugunu (ownerPen) ve
/// mevcut acllik degerini (hunger) tutar. Tur/fiyat/agirlik bilgisi AnimalData'dan okunur.
/// </summary>
public class AnimalBase : NetworkBehaviour
{
    [Header("Data")]
    public AnimalData animalData;

    [Header("Runtime State")]
    public Transform ownerPen;
    public float hunger = 100f;

    [Header("Agil Stok Durumu (PenStockEntryDetector tarafindan yazilir)")]
    [Tooltip("true ise bu hayvan zaten PenManager stok sayacina eklenmis demektir - tekrar agila girip cikinca ikinci kez sayilmasin diye.")]
    public bool countedInStock = false;

    // BUG DUZELTMESI (kullanici bildirdi: "client'in tasidigi/biraktigi hayvan host'ta gorunmuyordu,
    // teslim de olmuyordu"): Bu alan eskiden DUZ bir bool idi - sadece PickUp/DropAll'i CALISTIRAN
    // makinede (eskiden: SADECE tasiyan client'in kendi lokal kopyasinda) dogru degeri aliyordu.
    // Sunucunun KENDI kopyasi hicbir zaman guncellenmiyordu, bu yuzden AnimalIdleWander (IsServer
    // gated) hayvanin tasindigini hic bilmiyor, dolasmaya devam ediyordu - ayrica PenManager/
    // DeliveryZoneDetector gibi sunucu-yetkili sistemler de tasima durumunu goremiyordu.
    // NetworkVariable'a cevirip SADECE SUNUCUNUN yazmasina izin veriyoruz (CarryController artik
    // pickup/drop'u ServerRpc icinde, yani IsServer=true iken cagiriyor) - herkes okuyabilir.
    private NetworkVariable<bool> netIsBeingCarried = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>true iken bu hayvan bir oyuncu tarafindan sirtlanmis demektir - AnimalIdleWander bu
    /// sure boyunca hareketini DURDURUR. Artik NetworkVariable-destekli: SADECE sunucu (CarryController'in
    /// ServerRpc'leri icinden) set etmeli, herkes okuyabilir.</summary>
    public bool IsBeingCarried
    {
        get => netIsBeingCarried.Value;
        set => netIsBeingCarried.Value = value;
    }

    private Rigidbody rb;

    // BUG DUZELTMESI: Koyun ve Keci prefablarinda (UrsaAnimation paketi) Animator.applyRootMotion
    // ACIK geliyordu. Hayvanlarin TUM hareketi (AnimalIdleWander + CarryController) elle/scripted
    // yapiliyor (ARCHITECTURE.md: "NavMesh YOK") - Animator'in kendi root-motion'i buna paralel
    // olarak her frame transform.position'i degistirip scripted konumun ustune biniyordu. Kalici
    // cozum: her hayvan icin Animator varsa applyRootMotion'i acilista zorla kapat.
    private void Awake()
    {
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// KULLANICI BUG RAPORU DUZELTMESI: "client hayvan tasirken kendi kendine kayiyor/hiz
    /// kazaniyor". Kok neden: NetworkTransform SADECE pozisyon/rotasyonu senkronlar,
    /// Rigidbody.isKinematic gibi fizik ozelliklerini DEGIL. CarryController.RequestPickUpServerRpc/
    /// RequestDropAllServerRpc SADECE SUNUCUNUN kendi Rigidbody kopyasini kinematik yapiyordu -
    /// her CLIENT'in kendi LOKAL Rigidbody kopyasi ise non-kinematic KALIYOR, yani her client
    /// kendi fizik motoruyla (yercekimi, carpisma tepkileri) o hayvani BAGIMSIZ simule etmeye
    /// devam ediyordu. Sonuc: sunucunun "tasiyanin eline gore" yazdigi pozisyon ile client'in
    /// KENDI fizik motorunun ürettigi hareket CEKISIYORDU - hayvan client ekraninda kendiliginden
    /// kayiyor/hiz kazaniyor gibi gorunuyordu.
    /// DUZELTME: netIsBeingCarried degisince HERKES (host dahil, sunucudakiyle AYNI deger zaten
    /// idempotent) kendi LOKAL Rigidbody'sini bu duruma gore kinematik yapar/serbest birakir -
    /// artik hicbir client'in kendi fizigi tasima sirasinda devrede olmuyor.
    ///
    /// 2. TUR (kullanici bildirdi: "hayvanlar hala client'i suruklüyor"): yukaridaki fix sadece
    /// Rigidbody.isKinematic'i kapsiyordu. CarryController'in Collider.enabled/CharacterController.
    /// enabled degisiklikleri de AYNI sekilde SADECE sunucuda uygulaniyordu - client'in ekraninda
    /// hayvanin collider'i tasima boyunca ACIK kaliyordu. Bu collider, karakterin govdesine bitisik
    /// (SingleCarryOffset) surekli yeniden konumlandirilirken, kinematik olsa bile client'in KENDI
    /// (non-kinematic) karakterine fiziksel itme uyguluyordu - "hayvan client'i suruklüyor" buydu.
    /// Artik collider/CharacterController durumu da bu tek merkezi yerden HERKESE uygulaniyor.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        netIsBeingCarried.OnValueChanged += OnCarriedStateChanged;
        ApplyCarriedPhysicsState(netIsBeingCarried.Value);
    }

    public override void OnNetworkDespawn()
    {
        netIsBeingCarried.OnValueChanged -= OnCarriedStateChanged;
    }

    private void OnCarriedStateChanged(bool previous, bool current)
    {
        ApplyCarriedPhysicsState(current);
    }

    private void ApplyCarriedPhysicsState(bool carried)
    {
        if (rb != null)
        {
            if (carried)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = false;
            }
        }

        var colliders = GetComponents<Collider>();
        foreach (var col in colliders) col.enabled = !carried;

        var charController = GetComponent<CharacterController>();
        if (charController != null) charController.enabled = !carried;
    }
}
