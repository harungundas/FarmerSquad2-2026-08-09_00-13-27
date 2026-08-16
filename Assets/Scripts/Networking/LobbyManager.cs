using UnityEngine;
using TMPro;

/// <summary>
/// Lobi 3D karakter önizleme sistemi (ARCHITECTURE.md "## Networking Foundation" ve
/// "## Karakter Sistemi", TASKS.md T45).
///
/// Sorumluluk: LobbyUI açıldığında, sadece local oyuncu (slot 0) spawn olur ve username'i
/// floating isim olarak gösterilir. Diğer oyuncular network'ten gelince (T50) onlar eklenecek.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("5 Slot Transformu (LobbySlots altında, sırasıyla Slot0..Slot4)")]
    public Transform[] slotTransforms = new Transform[5];

    [Header("5 Karakter Template'i (görsel-only, sıra: Yetişkin, Şişman, Çocuk, Kadın, Yaşlı)")]
    public GameObject[] characterTemplates = new GameObject[5];

    [Header("Floating İsim Ayarları")]
    public Vector3 floatingNameOffset = new Vector3(0f, 2.0f, 0f);
    public float floatingNameFontSize = 4f;

    [Header("Oturum 1 — Network YOK, Start() sadece slot 0'a (local) spawn eder")]
    public bool autoSpawnOnStart = true;

    private void Start()
    {
        if (autoSpawnOnStart)
        {
            SpawnLocalPlayerPreview();
        }
    }

    /// <summary>Sadece slot 0'a (local oyuncu) spawn et. Username'i MainMenuController'dan al.</summary>
    private void SpawnLocalPlayerPreview()
    {
        if (slotTransforms.Length > 0 && characterTemplates.Length > 0 && slotTransforms[0] != null && characterTemplates[0] != null)
        {
            SpawnCharacterInSlot(0, characterTemplates[0], MainMenuController.CurrentUsername);
        }
        else
        {
            Debug.LogWarning("[LobbyManager] Slot 0 veya characterTemplates[0] eksik.");
        }
    }

    /// <summary>Verilen slota bir karakter prefabı instantiate eder, görsel-only ayarlaması
    /// yapar ve floating isim etiketi ekler.</summary>
    public GameObject SpawnCharacterInSlot(int slotIndex, GameObject characterPrefab, string displayName)
    {
        if (characterPrefab == null)
        {
            Debug.LogWarning("[LobbyManager] characterPrefab boş, slot " + slotIndex + " atlanıyor.");
            return null;
        }

        if (slotTransforms == null || slotIndex < 0 || slotIndex >= slotTransforms.Length || slotTransforms[slotIndex] == null)
        {
            Debug.LogWarning("[LobbyManager] Geçersiz slotIndex: " + slotIndex);
            return null;
        }

        Transform slot = slotTransforms[slotIndex];

        GameObject instance = Instantiate(characterPrefab, slot.position, slot.rotation, slot);
        instance.SetActive(true);
        instance.name = "PreviewCharacter_Slot" + slotIndex;

        // Görsel-only: fizik bileşenleri devre dışı
        foreach (Rigidbody rb in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
        }
        foreach (Collider col in instance.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        CreateFloatingName(instance.transform, displayName);

        return instance;
    }

    /// <summary>T46: Local oyuncu portre değiştirdiğinde çağrılır.</summary>
    public GameObject ReplaceCharacterInSlot(int slotIndex, GameObject newPrefab)
    {
        if (slotTransforms == null || slotIndex < 0 || slotIndex >= slotTransforms.Length || slotTransforms[slotIndex] == null)
        {
            Debug.LogWarning("[LobbyManager] Geçersiz slotIndex (ReplaceCharacterInSlot): " + slotIndex);
            return null;
        }

        Transform slot = slotTransforms[slotIndex];
        for (int i = slot.childCount - 1; i >= 0; i--)
        {
            Destroy(slot.GetChild(i).gameObject);
        }

        return SpawnCharacterInSlot(slotIndex, newPrefab, MainMenuController.CurrentUsername);
    }

    /// <summary>Karakterin üstünde world-space TMP ile bağımsız isim etiketi oluşturur.</summary>
    private void CreateFloatingName(Transform target, string displayName)
    {
        GameObject nameGO = new GameObject("FloatingName_" + displayName);
        nameGO.transform.position = target.position + floatingNameOffset;
        nameGO.transform.rotation = target.rotation;

        TextMeshPro tmp = nameGO.AddComponent<TextMeshPro>();
        tmp.text = displayName;
        tmp.fontSize = floatingNameFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;

        FloatingNameFollower follower = nameGO.AddComponent<FloatingNameFollower>();
        follower.target = target;
        follower.worldOffset = floatingNameOffset;
    }
}
