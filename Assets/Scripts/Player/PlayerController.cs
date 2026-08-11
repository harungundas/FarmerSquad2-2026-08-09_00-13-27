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

    [Header("Feeding (T17 placeholder - tam mantik T18 HayCarryState.cs'de)")]
    public bool isCarryingHay = false;

    [Header("Sprint")]
    public float sprintMultiplier = 1.6f;

    [Header("El Arabasi (Wheelbarrow.cs tarafindan disaridan set edilir, T20)")]
    [Tooltip("El Arabasini iterken 0.5 (hiz %50 duser), aksi halde 1. Wheelbarrow.cs disaridan yazar.")]
    public float pushSpeedMultiplier = 1f;



    [Header("Jump")]
    public float jumpUpForce = 6f;
    public float groundCheckDistance = 1.0f;

    [Header("Animation Locomotion Levels (gercek hizdan bagimsiz, BlendTree esikleriyle AYNI olmali)")]
    public float walkAnimSpeed = 16f;
    public float runAnimSpeed = 28f;

    private Rigidbody rb;
    private Animator animator;
    private bool wasMovingWhileCarrying = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance + 0.1f);
    }

private void Update()
    {
        if (!IsOwner) return;

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

        if (keyboard.spaceKey.wasPressedThisFrame && IsGrounded())
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpUpForce, rb.linearVelocity.z);
            if (animator != null) animator.SetTrigger("JumpTrigger");
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", animSpeed);

            bool isCarrying = animator.GetBool("IsCarrying");

            if (isCarrying)
            {
                if (!isMoving && wasMovingWhileCarrying) animator.SetTrigger("CarryWalkStop");
                wasMovingWhileCarrying = isMoving;
            }
            else
            {
                wasMovingWhileCarrying = false;
            }
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
