using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// `PlayerController` tasiyan her karakter objesine eklenir (bkz. ARCHITECTURE.md
/// "## Besleme (Saman) Sistemi"). Oyuncu elinde saman tasirken (isCarryingHay=true,
/// bkz. HayPile.cs / T17) bir hayvana yaklasip E tusunu CharacterClassData.feedDuration
/// kadar basili tutunca AnimalHunger.Feed() cagrilir (aclik 100'e reset), saman tukenir
/// (isCarryingHay=false).
///
/// Kadin karakteri feedDuration=2.0sn, Yasli=5.0sn (T03'teki CharacterClassData degerleri,
/// PlayerController.classData uzerinden okunur - burada sabit deger YAZILMAZ).
///
/// Yaklasma tespiti: HayPile.cs'deki (T17) SphereCollider-trigger pattern'inin AKSINE,
/// hayvan prefablarinda (orn. Cow) zaten trigger OLMAYAN fiziksel BoxCollider var (T13) -
/// oraya ikinci bir trigger collider eklemek prefab'lari degistirmeyi gerektirir. Bunun
/// yerine her frame Physics.OverlapSphere ile en yakin AnimalBase aranir. TASKS.md T18
/// Context'i spesifik bir teknik zorunlu kilmiyor, sadece "hayvana yaklasip E" diyor.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class HayCarryState : MonoBehaviour
{
    [Header("Besleme Ayarlari")]
    [Tooltip("Hayvanin bu mesafe icinde olmasi gerekir (metre). Sayisal deger GDD'de verilmemis, HayPile.cs'deki triggerRadius=2 ile tutarli secildi.")]
    public float feedRange = 12f;

    private PlayerController playerController;
    private AnimalBase targetAnimal;
    private float holdTimer = 0f;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

private void Update()
    {
        if (!playerController.IsOwner || !playerController.IsControllable.Value) return;

        if (!playerController.isCarryingHay)
        {
            targetAnimal = null;
            holdTimer = 0f;
            return;
        }

        targetAnimal = FindNearestAnimal();

        var keyboard = Keyboard.current;
        if (keyboard == null || targetAnimal == null)
        {
            holdTimer = 0f;
            if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Hide();
            return;
        }

        if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Show(targetAnimal.transform, "E - Basılı Tut - Besle");

        if (keyboard.eKey.isPressed)
        {
            holdTimer += Time.deltaTime;

            float baseFeedDuration = playerController.classData != null ? playerController.classData.feedDuration : 4f;
            // T61: Takim Cesitliligi Bonusu - CharacterSelectionManager server-authoritative
            // NetworkVariable'i uzerinden okunur (4+ farkli sinif, hicbiri tekrarsizsa 0.92,
            // aksi halde 1.0). Kadin'in kendi -%50 bonusunun UZERINE carpimsal biner (kullanici
            // karari, TASKS.md T61 Context).
            float teamMultiplier = CharacterSelectionManager.Instance != null
                ? CharacterSelectionManager.Instance.TeamFeedDurationMultiplier.Value
                : 1f;
            float feedDuration = baseFeedDuration * teamMultiplier;
            if (holdTimer >= feedDuration)
            {
                FeedAnimal(targetAnimal);
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    private AnimalBase FindNearestAnimal()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, feedRange);
        AnimalBase nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var animal = hit.GetComponentInParent<AnimalBase>();
            if (animal == null) continue;

            float sqrDist = (animal.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = animal;
            }
        }

        return nearest;
    }

private void FeedAnimal(AnimalBase animal)
    {
        var hunger = animal.GetComponent<AnimalHunger>();
        if (hunger != null)
        {
            hunger.Feed();
        }
        else
        {
            // AnimalHunger henuz eklenmemis bir hayvan olursa (olmamali, T13/T14 kapsami) - guvenlik agi.
            animal.hunger = 100f;
        }

        playerController.isCarryingHay = false;
        holdTimer = 0f;
        Debug.Log("[HayCarryState] " + gameObject.name + " hayvani besledi: " + animal.gameObject.name + " - aclik 100'e resetlendi.");
        targetAnimal = null;

        if (InteractionIndicator.Instance != null) InteractionIndicator.Instance.Hide();
    }
}
