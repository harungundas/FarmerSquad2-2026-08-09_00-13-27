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


    [Header("Jump")]
    public float jumpUpForce = 6f;
    public float groundCheckDistance = 1.0f;

    [Header("Animation Locomotion Levels (gercek hizdan bagimsiz, BlendTree esikleriyle AYNI olmali)")]
    public float walkAnimSpeed = 16f;
    public float runAnimSpeed = 28f;

    private Rigidbody rb;
    private Animator animator;
    private bool wasRunning = false;
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
        float actualSpeed = isSprinting ? baseSpeed * sprintMultiplier : baseSpeed;

        float h = 0f;
        float v = 0f;
        if (keyboard.aKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed) h += 1f;
        if (keyboard.sKey.isPressed) v -= 1f;
        if (keyboard.wKey.isPressed) v += 1f;

        Vector3 inputDir = new Vector3(h, 0f, v);
        bool isMoving = inputDir.sqrMagnitude > 0.0001f;
        float animSpeed = 0f;

        if (isMoving)
        {
            inputDir.Normalize();

            var cam = Camera.main;
            Vector3 moveDir;
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

            Vector3 newPosition = rb.position + moveDir * actualSpeed * Time.deltaTime;
            rb.MovePosition(newPosition);
            transform.forward = moveDir;

            animSpeed = isSprinting ? runAnimSpeed : walkAnimSpeed;
        }

        if (keyboard.spaceKey.wasPressedThisFrame && IsGrounded())
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpUpForce, rb.linearVelocity.z);
            if (animator != null) animator.SetTrigger("JumpTrigger");
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", animSpeed);

            bool isCarrying = animator.GetBool("IsCarrying");
            bool isRunning = isMoving && isSprinting;

            if (!isCarrying)
            {
                if (isRunning && !wasRunning) animator.SetTrigger("RunEnter");
                else if (!isRunning && wasRunning) animator.SetTrigger("RunExit");
            }
            wasRunning = isRunning;

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
