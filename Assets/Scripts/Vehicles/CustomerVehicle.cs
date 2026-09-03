using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Musteri araci: kendi kuyruk sirasindaki hedef noktaya duz-hat hareket eder (yol
/// dogrusal oldugu icin NavMesh gerekmiyor, basit Vector3.MoveTowards - ARCHITECTURE.md
/// T23 notundaki "basit lerp" secenegi kullanildi).
///
/// HIZ EGRISI: Sabit hiz DEGIL, her "leg" (bir SetTarget cagrisindan sonraki tek yolculuk)
/// icin ease-in/ease-out (yavas basla -> hizlan -> yavaslayarak var) sinus egrisi kullanilir,
/// gorsel gerceklik icin (kullanici talebi).
///
/// STAND BEKLEME: standDwellTime YOK - arac StandFront'a ulasinca SONSUZA KADAR bekler,
/// ancak disaridan ResolveOrder() cagrilana kadar (gercek pazarlik/teslimat sonucu, T24-27
/// baglanacak; basarili/basarisiz farketmez, herhangi bir sonuc gelmeden arac AYRILMAZ -
/// kullanici acikca "test icin bile olsa boyle olmasin" dedi, sahte zaman-asimi YOK).
/// [ContextMenu] ile GELISTIRICI test amacli manuel tetikleme mumkun (otomatik degil).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class CustomerVehicle : NetworkBehaviour
{
    [Header("Hareket")]
    public float maxSpeed = 50f; // T26 test sonrasi ayarlandi: spawn->stand mesafesi ~387 birim, 15f/s ile 25+ sn suruyordu (kullanici "cok yavas" dedi).
    [Tooltip("Ease egrisinde en yavas anda bile hizin maxSpeed'e orani (0 = tam durur, ease geçişi cok yavas olur).")]
    public float minSpeedFraction = 0.35f; // ease-in/out baslangic/bitis hizi artirildi, sarkma hissi azaltmak icin
    public float arrivalThreshold = 0.5f;

    [Header("Model Yon Duzeltmesi")]
    [Tooltip("Modelin kendi 'on' yonu ile hareket yonu arasindaki fark. Model ters gidiyor gibi gorunuyorsa 180 dene.")]
    public float modelForwardYOffset = 180f;

    private VehicleSpawner spawner;
    private Animator animator;
    private Transform currentTarget;
    private Vector3 legStartPosition;
    private float legTotalDistance;
    private bool isWaitingAtStand = false;

    /// <summary>
    /// T26 StandInteraction icin eklendi: T22/T23'te OrderData sadece VehicleSpawner icinde
    /// hesaplanip LOGLANIYORDU, hicbir yerde SAKLANMIYORDU. StandInteraction'in gercek
    /// bekleyen musterinin siparisini okuyabilmesi icin VehicleSpawner.SpawnTestVehicle()
    /// bu alani dogrudan set eder.
    /// </summary>
    public OrderData CurrentOrder;

    /// <summary>T26: StandInteraction'in "StandFront'ta bekleyen arac var mi" kontrolu icin.</summary>
    public bool IsWaitingAtStand => isWaitingAtStand;
    private bool isHeadingToDespawn = false;

    // ---- T58 - Ozel Musteri ----
    [Header("T58 - Ozel Musteri")]
    [Tooltip("VehicleSpawner tarafindan spawn aninda ayarlanir. true ise bu arac normalden %50 fazla odeyen ozel musteridir ve kisa bir kabul penceresi vardir.")]
    public bool IsSpecial = false;
    [Tooltip("Ozel musterinin StandFront'a ulastiktan sonra bekleyecegi maksimum sure saniye. Sadece IsSpecial=true iken kullanilir - normal musteriler icin hicbir zamanlayici YOK spec geregi, sinif yorumuna bkz.")]
    public float specialAcceptWindowSeconds = 37.5f;

    private float specialWindowTimer = 0f;
    private bool specialWindowActive = false;

    /// <summary>T58: Ozel musteri kacirildiginda (kabul penceresi dolup arac kendiliginden
    /// ayrildiginda) VehicleSpawner'a bildirim - ayri, cezasiz sayaci artirmasi icin.
    /// Normal ResolveOrder() (oyuncu tarafindan sonuclandirilan islem) BU YOLU KULLANMAZ.</summary>
    public System.Action<CustomerVehicle> OnSpecialCustomerMissed;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    public void Initialize(VehicleSpawner ownerSpawner)
    {
        spawner = ownerSpawner;
    }

    public void SetTarget(Transform waypoint)
    {
        currentTarget = waypoint;
        isWaitingAtStand = false;
        legStartPosition = transform.position;
        legTotalDistance = waypoint != null ? Vector3.Distance(legStartPosition, waypoint.position) : 0f;
    }

    /// <summary>
    /// Gercek pazarlik/teslimat sistemi (T24-27) tarafindan, siparis SONUCU (basarili veya
    /// basarisiz, farketmez) belli olunca cagrilmalidir. Cagrilana kadar arac StandFront'ta
    /// SONSUZA KADAR bekler.
    /// </summary>
    public void ResolveOrder()
    {
        if (!isWaitingAtStand) return;
        isWaitingAtStand = false;
        isHeadingToDespawn = true;

        // KULLANICI DUZELTMESI: kuyruktakilerin ilerlemesi bu aracin fiilen haritadan
        // silinmesini (despawn noktasina ulasmasini) BEKLEMEMELI - standdan ayrilma ANINDA
        // spawner'a haber verilir, kuyruk hemen bir slot one kayar.
        spawner.OnVehicleLeavingStandServer(this);

        SetTarget(spawner.DespawnPoint);
    }

    [ContextMenu("DEBUG: Force Resolve Order")]
    private void DebugForceResolveOrder()
    {
        ResolveOrder();
    }

    private void Update()
    {
        if (!IsServer) return; // Host-authoritative hareket

        if (isWaitingAtStand)
        {
            SetMovingAnim(false);

            // T58: SADECE ozel musteri icin kisa bir kabul penceresi zamanlayicisi calisir.
            // Normal musteriler icin hicbir zamanlayici YOK (mevcut davranis - sinif yorumuna bkz).
            if (IsSpecial && specialWindowActive)
            {
                specialWindowTimer -= Time.deltaTime;
                if (specialWindowTimer <= 0f)
                {
                    specialWindowActive = false;
                    HandleSpecialCustomerMissed();
                }
            }

            return; // Normalde sonsuza kadar bekler, ResolveOrder() cagrilana kadar.
        }

        if (currentTarget == null)
        {
            SetMovingAnim(false);
            return;
        }

        float remaining = Vector3.Distance(transform.position, currentTarget.position);
        if (remaining <= arrivalThreshold)
        {
            SetMovingAnim(false);
            OnArrived();
            return;
        }

        float progress = legTotalDistance > 0.01f ? Mathf.Clamp01(1f - remaining / legTotalDistance) : 1f;
        float easeFactor = Mathf.Sin(progress * Mathf.PI); // 0 basta/sonda, 1 ortada
        float currentSpeed = maxSpeed * Mathf.Lerp(minSpeedFraction, 1f, easeFactor);

        Vector3 dir = (currentTarget.position - transform.position).normalized;
        transform.position = Vector3.MoveTowards(transform.position, currentTarget.position, currentSpeed * Time.deltaTime);

        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, modelForwardYOffset, 0f);
        }

        SetMovingAnim(true);
    }

    private void OnArrived()
    {
        if (isHeadingToDespawn)
        {
            spawner.OnVehicleDespawning(this);
            return;
        }

        if (currentTarget == spawner.StandFrontPoint)
        {
            isWaitingAtStand = true;
            PlayHornClientRpc(); // Kullanici talebi: standa giris yapan arac TEK BIR KEZ korna calar.

            // T58: StandFront'a ulasan arac ozelse, burada kabul penceresi zamanlayicisi baslar.
            if (IsSpecial)
            {
                specialWindowTimer = specialAcceptWindowSeconds;
                specialWindowActive = true;
            }

            return;
        }

        // Kuyruk slotlarindan birine (StandFront disinda) ulasildi - spawner'in
        // RefreshQueueTargets'i zaten periyodik olarak hedefi guncelleyecek.
    }

    /// <summary>T58: Ozel musterinin kabul penceresi dolup arac kendiliginden ayrildiginda
    /// cagrilir. Normal ResolveOrder() akisindan TAMAMEN AYRI: Streak sayacini (T59) KIRMAZ,
    /// sadece ayri/cezasiz OnSpecialCustomerMissed action'ini tetikler (VehicleSpawner bu
    /// aboneligi kullanarak kendi missedSpecialCustomers sayacini artirir - bkz. TASKS.md T58).
    /// Aracin kendisi normal bir ResolveOrder() gibi degil, direkt despawn'a yonlendirilir
    /// (siparis sonuclanmadi, sadece kacirildi - kuyruk spawner tarafindan ayni sekilde
    /// guncellenir).</summary>
    private void HandleSpecialCustomerMissed()
    {
        if (!isWaitingAtStand) return;
        isWaitingAtStand = false;
        isHeadingToDespawn = true;

        OnSpecialCustomerMissed?.Invoke(this);

        spawner.OnVehicleLeavingStandServer(this);
        SetTarget(spawner.DespawnPoint);
    }

    [ClientRpc]
    private void PlayHornClientRpc()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.carHornClip);
        }
    }

    private void SetMovingAnim(bool isMoving)
    {
        if (animator == null) return;
        animator.SetBool("IsMoving", isMoving);
    }
}
