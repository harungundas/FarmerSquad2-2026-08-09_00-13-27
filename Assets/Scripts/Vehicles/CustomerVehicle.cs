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
    public float maxSpeed = 15f;
    [Tooltip("Ease egrisinde en yavas anda bile hizin maxSpeed'e orani (0 = tam durur, ease geçişi cok yavas olur).")]
    public float minSpeedFraction = 0.2f;
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
    private bool isHeadingToDespawn = false;

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
            return; // Sonsuza kadar bekler, ResolveOrder() cagrilana kadar hicbir zamanlayici YOK.
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
            return;
        }

        // Kuyruk slotlarindan birine (StandFront disinda) ulasildi - spawner'in
        // RefreshQueueTargets'i zaten periyodik olarak hedefi guncelleyecek.
    }

    private void SetMovingAnim(bool isMoving)
    {
        if (animator == null) return;
        animator.SetBool("IsMoving", isMoving);
    }
}
