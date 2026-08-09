using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Temel karakter hareketi (WASD) ve network senkronu.
/// Hareket sadece IsOwner olan client tarafindan islenir; NetworkTransform pozisyonu senkronlar.
/// Hiz, CharacterClassData.walkSpeed'den okunur.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [Header("Character Data")]
    public CharacterClassData classData;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

private void Update()
    {
        if (!IsOwner) return;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        float h = 0f;
        float v = 0f;
        if (keyboard.aKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed) h += 1f;
        if (keyboard.sKey.isPressed) v -= 1f;
        if (keyboard.wKey.isPressed) v += 1f;

        Vector3 inputDir = new Vector3(h, 0f, v);
        if (inputDir.sqrMagnitude < 0.0001f) return;

        inputDir.Normalize();
        float speed = classData != null ? classData.walkSpeed : 5f;

        Vector3 newPosition = rb.position + inputDir * speed * Time.deltaTime;
        rb.MovePosition(newPosition);

        transform.forward = inputDir;
    }
}
