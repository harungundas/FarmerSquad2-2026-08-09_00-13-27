using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// `samanlar` altindaki 5 saman yiginindan her birine eklenir.
/// Oyuncu (local/IsOwner) yakinken E tusunu pickupHoldDuration (varsayilan 1sn)
/// basili tutunca saman alinir: PlayerController.isCarryingHay true olur,
/// yigin gorsel olarak kuculup kaybolur.
///
/// Kaynak T17 karari geregi TUKENMEYEN basit versiyon: respawn yok, kullanim
/// sayaci yok - alinan yigin bir kez kaybolur, geri gelmez (playtest'te
/// degistirilebilir, bkz. TASKS.md T17 Context).
///
/// Not (deviation, bkz. HANDOFF.md): Bu script NetworkBehaviour DEGIL, sadece
/// local MonoBehaviour. Saman alma islemi digger client'lara sync EDILMIYOR
/// (obje sadece alan oyuncunun ekraninda degil, TUM client'larda ayni objeyi
/// gorecegi icin aslinda herkeste kaybolacak - SetActive(false) sahne objesi
/// uzerinde calisir, network senkron degil). Coklu oyunculu testte ayri bir
/// bug/deviation olarak not edilmeli.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class HayPile : MonoBehaviour
{
    [Header("Etkilesim")]
    public float pickupHoldDuration = 1f;
    public float triggerRadius = 2f;
    public float shrinkDuration = 0.25f;

    private PlayerController playerInRange;
    private float holdTimer = 0f;
    private bool taken = false;

    private void Awake()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = triggerRadius;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (taken) return;
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc.IsOwner)
        {
            playerInRange = pc;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc == playerInRange)
        {
            playerInRange = null;
            holdTimer = 0f;
        }
    }

private void Update()
    {
        if (taken || playerInRange == null)
        {
            if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Hide();
            return;
        }

        if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Show(transform, "E - Basılı Tut - Yem Al");

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.eKey.isPressed)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= pickupHoldDuration)
            {
                PickUp(playerInRange);
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

private void PickUp(PlayerController player)
    {
        taken = true;
        player.isCarryingHay = true;
        Debug.Log("[HayPile] Saman alindi: " + gameObject.name + " - oyuncu saman tasiyor durumuna gecti.");
        if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Hide();
        StartCoroutine(ShrinkAndDisable());
    }

    private System.Collections.IEnumerator ShrinkAndDisable()
    {
        Vector3 startScale = transform.localScale;
        float t = 0f;
        while (t < shrinkDuration)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / shrinkDuration);
            transform.localScale = startScale * k;
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
