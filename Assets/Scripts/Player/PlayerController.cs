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

    [Header("Animasyon Senkronu (BUG DUZELTMESI: kullanici bildirdi - diger client'ta hicbir\n    animasyon gorunmuyordu, sanki sadece Idle varmis gibi)")]
    [Tooltip("Eskiden animator.SetFloat/SetTrigger SADECE IsOwner iken (bu Update() zaten IsOwner\n    gated) cagriliyordu - yani SADECE sahibinin KENDI ekraninda dogru animasyon oynuyordu,\n    diger butun client'lar/host o karakteri hep varsayilan (Idle) Animator durumunda goruyordu\n    (NetworkTransform sadece POZISYONU senkronluyor, Animator parametrelerini DEGIL). Cozum:\n    sahibi bu degerleri NetworkVariable'a yazar (Owner-writable), HERKES (sahibi dahil) bunlari\n    asagidaki LateUpdate'te KENDI Animator'ina uygular.")]
    private NetworkVariable<float> netAnimSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> netJumpTick = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Dans (kullanici istegi): R tusu, sadece eller bosken (CarryController.IsHandsEmpty)")]
    private NetworkVariable<bool> netIsDancing = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private CarryController carryController;

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
        carryController = GetComponent<CarryController>();
    }

    public override void OnNetworkSpawn()
    {
        // Bu govde henuz bir oyuncuya ATANMAMISSA (CharacterSelectionManager.IsControllable'i
        // hala varsayilan false ise) haritada hareketsiz beklemesin - gorunmez ve carpismasiz
        // kalsin, atandigi an (IsControllable=true) geri gorunur/carpisir hale gelsin.
        ApplyBodyVisibility(IsControllable.Value);
        IsControllable.OnValueChanged += OnControllableChanged;
        netJumpTick.OnValueChanged += OnJumpTickChanged;
    }

    public override void OnNetworkDespawn()
    {
        IsControllable.OnValueChanged -= OnControllableChanged;
        netJumpTick.OnValueChanged -= OnJumpTickChanged;
    }

    /// <summary>HERKESTE (sahibi dahil) tetiklenir - JumpTrigger animasyonunu senkron oynatir.</summary>
    private void OnJumpTickChanged(int previous, int current)
    {
        if (animator != null) animator.SetTrigger("JumpTrigger");
    }

    /// <summary>HERKESTE calisir (IsOwner GATE'i YOK - bu bilincli, Speed parametresi HERKESTE
    /// ayni sekilde uygulanmali ki uzaktaki gozlemciler de dogru animasyonu gorsun).</summary>
    private void LateUpdate()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", netAnimSpeed.Value);
            animator.SetBool("IsDancing", netIsDancing.Value);
        }
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
            // KULLANICI BUG RAPORU DUZELTMESI (yan urun): eskiden ONCE isKinematic=true
            // yapiliyor, SONRA linearVelocity/angularVelocity sifirlaniyordu - Unity zaten
            // kinematik olan bir body'de velocity set edilmesine izin vermiyor, bu yuzden
            // konsolda "Setting linear/angular velocity of a kinematic body is not supported"
            // spam'i olusuyordu. Simdi SIRA DUZELTILDI: once velocity sifirlanir, SONRA kinematik
            // yapilir - hicbir uyari uretmez.
            if (visible == false)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = !visible;
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

        // KULLANICI ISTEGI: R tusu ile dans, SADECE eller BOSKEN (CarryController.IsHandsEmpty)
        // baslatilabilir/durdurulabilir (toggle). Hareket etmeye baslarsa (WASD) dans otomatik
        // iptal olur - Dance state'i yuruyus/kosu Speed parametresini kullanmiyor, bu yuzden
        // dans ederken hareket etmek animasyon acisindan anlamsiz kalirdi. Eller sonradan
        // dolarsa (elindekiyle dans ederken E ile hayvan alinirsa) da guvenlik icin iptal edilir.
        bool handsEmpty = carryController == null || carryController.IsHandsEmpty;

        if (keyboard.rKey.wasPressedThisFrame && handsEmpty)
        {
            netIsDancing.Value = !netIsDancing.Value;
        }

        if (netIsDancing.Value && (isMoving || !handsEmpty))
        {
            netIsDancing.Value = false;
        }

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
            if (animator != null) animator.SetTrigger("JumpTrigger"); // aninda yerel geri bildirim (gecikmesiz)
            netJumpTick.Value++; // digger client'lara/host'a yay
        }

        // BUG DUZELTMESI (diger client'ta animasyon gorunmuyordu): eskiden burada DOGRUDAN
        // animator.SetFloat("Speed", animSpeed) cagriliyordu - bu SADECE sahibinin kendi ekraninda
        // isliyordu (yukaridaki IsOwner erken-return'u yuzunden bu Update() zaten sadece sahipte
        // calisiyor). Artik degeri NetworkVariable'a yaziyoruz - LateUpdate() (IsOwner gate'i
        // OLMADAN, herkeste calisir) buradan okuyup KENDI Animator'ina uygular, boylece uzaktaki
        // gozlemciler de dogru yuruyus/kosma animasyonunu gorur.
        netAnimSpeed.Value = animSpeed;
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
