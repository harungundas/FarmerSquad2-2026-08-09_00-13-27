using UnityEngine;
using Unity.Netcode;

/// <summary>
/// PLAN.md §3.7 / TASKS.md T44: Agildaki hayvanlara dekoratif bir canlilik katmani ekler.
/// NavMesh KULLANILMAZ. 8-15sn rastgele bekleme sonrasi, ownerPen'deki PenManager.bounds
/// icinde rastgele bir hedef nokta secilir ve karaktere sabit 0.3-0.5 m/s hizla
/// Vector3.MoveTowards ile gidilir. Bu SADECE gorsel bir katmandir - aclik, satis, teslimat
/// mekaniklerine HICBIR etkisi yoktur (ARCHITECTURE.md "## 5. Kapsam Dışı").
/// AnimalBase ile ayni objeye eklenir (AnimalHunger.cs ile ayni desen).
/// </summary>
[RequireComponent(typeof(AnimalBase))]
public class AnimalIdleWander : NetworkBehaviour
{
    [Header("Bekleme Suresi (sn)")]
    [Tooltip("PLAN.md §3.7: 8-15sn rastgele bekleme.")]
    public float minWaitSeconds = 8f;
    public float maxWaitSeconds = 15f;

    [Header("Hareket Hizi (m/s)")]
    [Tooltip("PLAN.md §3.7: cok yavas hiz, 0.3-0.5 m/s.")]
    public float minMoveSpeed = 0.3f;
    public float maxMoveSpeed = 0.5f;

    private enum WanderState { Waiting, Moving }

    private AnimalBase animalBase;
    private WanderState state = WanderState.Waiting;
    private float waitTimer;
    private Vector3 targetPoint;
    private float moveSpeed;

    private void Awake()
    {
        animalBase = GetComponent<AnimalBase>();
    }

    private void Start()
    {
        waitTimer = Random.Range(minWaitSeconds, maxWaitSeconds);
    }

    private void Update()
    {
        // Host authoritative: sadece server hareket ettirir - AnimalHunger.cs (T14) ile ayni desen.
        // NOT: pozisyon su an NetworkTransform ile senkron edilmiyor (hayvan prefablarinda yok) -
        // coklu-client'ta client'larin bu hareketi gormesi icin ileride NetworkTransform eklenmesi
        // gerekebilir (T44 kapsami disi, bu dekoratif katman sadece host/tek-oyunculu acidan
        // test edildi).
        if (!IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return;
        }

        // BUG DUZELTMESI: hayvan bir oyuncu tarafindan tasiniyorsa (CarryController.PickUp)
        // burasi HICBIR SEY yapmamali - aksi halde 'Moving' durumundaki bir hayvan, tasiniyor
        // olsa bile kendi agil-ici hedef noktasina yurumeye devam ediyordu, bu da CarryController'in
        // her frame ayarladigi parent-offset pozisyonunu eziyordu ("elim bos, tavuk yerde kaldi" bug'i).
        if (animalBase.IsBeingCarried)
        {
            return;
        }

        switch (state)
        {
            case WanderState.Waiting:
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f)
                {
                    if (TryPickTargetPoint())
                    {
                        moveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);
                        state = WanderState.Moving;
                    }
                    else
                    {
                        // ownerPen/bounds henuz atanmamissa (orn. Instantiate anindan hemen sonra),
                        // exception atmadan tekrar beklemeye gec.
                        waitTimer = Random.Range(minWaitSeconds, maxWaitSeconds);
                    }
                }
                break;

            case WanderState.Moving:
                transform.position = Vector3.MoveTowards(transform.position, targetPoint, moveSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, targetPoint) <= 0.01f)
                {
                    state = WanderState.Waiting;
                    waitTimer = Random.Range(minWaitSeconds, maxWaitSeconds);
                }
                break;
        }
    }

    /// <summary>
    /// PenManager.GetRandomPointInBounds() (T15'te yazildi) ile ayni bounds referansini kullanir.
    /// </summary>
    private bool TryPickTargetPoint()
    {
        if (animalBase.ownerPen == null)
        {
            return false;
        }

        PenManager penManager = animalBase.ownerPen.GetComponent<PenManager>();
        if (penManager == null || penManager.bounds == null)
        {
            return false;
        }

        targetPoint = penManager.GetRandomPointInBounds();
        return true;
    }
}
