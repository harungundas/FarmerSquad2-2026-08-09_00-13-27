using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Temel karakter hareketi (WASD, kamera-relative), Shift ile sprint, Space ile zipla,
/// ve network senkronu. Hareket sadece IsOwner olan client tarafindan islenir.
///
/// Animasyon tasarimi: Animator'a beslenen "Speed" parametresi GERCEK fiziksel hizi degil,
/// SABIT bir "lokomasyon seviyesi" gosterir (0=Idle, WalkAnimSpeed=Walk, RunAnimSpeed=Run).
/// Bu sayede karakterler arasi farkli walkSpeed degerleri (16-25 araligi) animasyon
/// karismasina sebep olmaz - Run animasyonu SADECE Shift (sprint) basiliyken devreye girer.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [Header("Character Data")]
    public CharacterClassData classData;

    [Header("Ownership Gate (CharacterSelectionManager tarafindan yazilir)")]
    [Tooltip("IsOwner tek basina yeterli DEGIL: host modunda, sunucuya ait (henuz kimseye atanmamis) sahne objelerinin OwnerClientId'si de 0'dir - host'un kendi LocalClientId'si de 0 oldugu icin IsOwner yanlislikla true doner. Bu yuzden CharacterSelectionManager, bir govdeyi bir client'a ATADIGI zaman bu bayragi acikca true yapar, biraktiginda false yapar.")]
    public NetworkVariable<bool> IsControllable = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Feeding (T17 placeholder - tam mantik T18 HayCarryState.cs'de)")]
    public bool isCarryingHay = false;

    [Header("Sprint")]
    public float sprintMultiplier = 1.6f;

    [Header("El Arabasi (Wheelbarrow.cs tarafindan disaridan set edilir, T20)")]
    [Tooltip("El Arabasini iterken 0.5 (hiz %50 duser), aksi halde 1. Wheelbarrow.cs disaridan yazar.")]
    public float pushSpeedMultiplier = 1f;

    [Header("Jump")]
    public float jumpUpForce = 53f;
    public float groundCheckDistance = 1.0f;
    [Tooltip("Kullanici karari (2.revizyon): sadece dusus fazini hizlandirmak yetersiz kaldi (cikis fazi hala yavasti, toplam sure yeterince kismiyordu). Artik CIKIS+INIS ikisinde birden uygulaniyor - yukseklik AYNI (~8m) kalsin diye jumpUpForce da orantili artirildi (20->53). 1 = normal (kapali).")]
    public float airGravityMultiplier = 7f;

    [Header("Animation Locomotion Levels (gercek hizdan bagimsiz, BlendTree esikleriyle AYNI olmali)")]
    public float walkAnimSpeed = 16f;
    public float runAnimSpeed = 28f;

    private Rigidbody rb;
    private Animator animator;
    private Renderer[] bodyRenderers;
    private Collider[] bodyColliders;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        bodyRenderers = GetComponentsInChildren<Renderer>(true);
        bodyColliders = GetComponentsInChildren<Collider>(true);
    }

    public override void OnNetworkSpawn()
    {
        // Bu govde henuz bir oyuncuya ATANMAMISSA (CharacterSelectionManager.IsControllable'i
        // hala varsayilan false ise) haritada hareketsiz beklemesin - gorunmez ve carpismasiz
        // kalsin, atandigi an (IsControllable=true) geri gorunur/carpisir hale gelsin.
        ApplyBodyVisibility(IsControllable.Value);
        IsControllable.OnValueChanged += OnControllableChanged;
    }

    public override void OnNetworkDespawn()
    {
        IsControllable.OnValueChanged -= OnControllableChanged;
    }

    private void OnControllableChanged(bool previousValue, bool newValue)
    {
        ApplyBodyVisibility(newValue);
    }

    private void ApplyBodyVisibility(bool visible)
    {
        if (bodyRenderers != null)
            foreach (var r in bodyRenderers) if (r != null) r.enabled = visible;

        if (bodyColliders != null)
            foreach (var c in bodyColliders) if (c != null) c.enabled = visible;

        if (rb != null)
        {
            // Gorunmezken fizige tabi olmasin (dusmesin/itilmesin), gorunur olunca normal
            // (yer cekimine tabi, non-kinematic) davranisina doner - Update()'teki linearVelocity
            // kontrolu bunu zaten non-kinematic bekliyor.
            rb.isKinematic = !visible;
            if (visible == false)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (animator != null) animator.enabled = visible;
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance + 0.1f);
    }

    private void Update()
    {
        if (!IsOwner || !IsControllable.Value) return;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        bool isSprinting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        float baseSpeed = classData != null ? classData.walkSpeed : 5f;
        float actualSpeed = (isSprinting ? baseSpeed * sprintMultiplier : baseSpeed) * pushSpeedMultiplier;

        float h = 0f;
        float v = 0f;
        if (keyboard.aKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed) h += 1f;
        if (keyboard.sKey.isPressed) v -= 1f;
        if (keyboard.wKey.isPressed) v += 1f;

        Vector3 inputDir = new Vector3(h, 0f, v);
        bool isMoving = inputDir.sqrMagnitude > 0.0001f;
        float animSpeed = 0f;
        Vector3 moveDir = Vector3.zero;

        if (isMoving)
        {
            inputDir.Normalize();

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 camForward = cam.transform.forward;
                Vector3 camRight = cam.transform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();
                moveDir = camForward * v + camRight * h;
                moveDir.Normalize(); // DUZELTME: caprazda (h=1,v=1) normalize edilmeden uzunluk sqrt(2) oluyordu, hiz %41 fazlaydi.
            }
            else
            {
                moveDir = inputDir;
            }

            transform.forward = moveDir;
            animSpeed = isSprinting ? runAnimSpeed : walkAnimSpeed;
        }

        // Rigidbody NON-KINEMATIC (yercekimi/ziplama icin isKinematic=false birakildi) - bu yuzden
        // MovePosition YERINE dogrudan linearVelocity kontrolu kullaniliyor. MovePosition, kinematik
        // OLMAYAN bir Rigidbody'de fizik motorunun carpisma/ornusme durumunda beklenmedik bir "tepki
        // hizi" (residual velocity) uretmesine sebep olabiliyordu - bu hiz input birakildiginda HICBIR
        // YERDE sifirlanmiyordu, bu da "tusu birakinca ters yonde kaymaya devam etme" bug'inin sebebiydi.
        // Y bileseni (yercekimi/ziplama, asagidaki Space-kontrolunce ayrica set edilir) KORUNUYOR,
        // sadece X/Z (yatay hareket) HER FRAME ACIKCA set ediliyor (hareket yoksa da sifirlaniyor).
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 horizontalVelocity = moveDir * actualSpeed;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);

        // Kullanici karari (2. revizyon): havada gecirilen sureyi kissaltmak icin ARTIK hem
        // cikis hem inis fazinda ekstra yercekimi uygulaniyor (SADECE dususte uygulamak yetersiz
        // kalmisti, cikis hala yavasti). Yukseklik AYNI kalsin diye jumpUpForce buna orantili
        // (20 -> 53) artirildi - bkz. Space kontrolu asagida.
        if (!IsGrounded())
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (airGravityMultiplier - 1f) * Time.deltaTime;
        }

        if (keyboard.spaceKey.wasPressedThisFrame && IsGrounded())
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpUpForce, rb.linearVelocity.z);
            if (animator != null) animator.SetTrigger("JumpTrigger");
        }

        if (animator != null)
        {
            // BUG DUZELTMESI (kullanici bildirdi: tasirken durunca ~4-5sn animasyon kilitleniyordu):
            // CarryWalkStop tetiklenince gecilen WalkStop_Carrying state'inin kullandigi klip
            // (WalkToStopCarring.fbx) Mixamo'nun TAM ham kaydini kullaniyor (147 frame, ~4.9sn),
            // kirpilmamis. Bu, hasExitTime=0.9 ile carpilinca ~4.4 saniyelik bir "kilitlenme"
            // hissi yaratiyordu (tuslara basilsa da animasyon degismiyordu). CarryingBlendTree
            // zaten Speed=0 esiginde ayri bir tasima-idle pozu iceriyor (dogrulandi), bu yuzden
            // ayri bir "walk stop" gecis state'ine ihtiyac yok - trigger artik ATILMIYOR,
            // WalkStop_Carrying state'i erisilemez (zararsiz) kaliyor. Klip ileride dogru araliga
            // kirpilirse (Unity Animation penceresinde gorsel kontrolle), bu blok geri acilabilir.
            animator.SetFloat("Speed", animSpeed);
        }
    }

    /// <summary>
    /// Disaridan (orn. debug/context menu) zipla animasyonunu + fizigini tetiklemek icin.
    /// </summary>
    public void TriggerJump()
    {
        if (IsGrounded())
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpUpForce, rb.linearVelocity.z);
        }
        if (animator != null)
        {
            animator.SetTrigger("JumpTrigger");
        }
    }
}
