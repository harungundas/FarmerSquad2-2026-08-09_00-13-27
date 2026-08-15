using UnityEngine;
using TMPro;

/// <summary>
/// Lobi 3D karakter önizleme sistemi (ARCHITECTURE.md "## Networking Foundation" ve
/// "## Karakter Sistemi", TASKS.md T45 — Oturum 1, Network YOK).
///
/// NOT (HANDOFF T45 Deviations'a da yazılacak): Bu dosya daha önce YOKTU - TASKS.md T07
/// "LobbyManager.cs — Steam lobi oluştur/katıl" olarak [x] işaretlenmişti ama script hiç
/// oluşturulmamıştı. Bu oturumda dosya SIFIRDAN, SADECE T45 kapsamıyla (karakter önizleme)
/// oluşturuldu. Steam lobi create/join/kick (T07'nin orijinal kapsamı) burada YOKTUR,
/// T50'de (gerçek network bağlantısı) eklenecek.
///
/// Sorumluluk: 5 lobi slotuna (LobbySlots altındaki Slot0..Slot4) görsel-only karakter
/// önizlemesi + floating isim etiketi yerleştirmek. Oturum 1'de hardcoded "Oyuncu N" isimleri
/// kullanılır (bkz. LobbyUI.cs UpdatePlaceholderSlots - aynı placeholder mantığı).
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("5 Slot Transformu (LobbySlots altında, sırasıyla Slot0..Slot4)")]
    public Transform[] slotTransforms = new Transform[5];

    [Header("5 Karakter Template'i (görsel-only, sıra: Yetişkin, Şişman, Çocuk, Kadın, Yaşlı — LobbyUI.characterNames ile AYNI sıra)")]
    public GameObject[] characterTemplates = new GameObject[5];

    [Header("Floating İsim Ayarları")]
    public Vector3 floatingNameOffset = new Vector3(0f, 2.0f, 0f);
    public float floatingNameFontSize = 4f;

    [Header("Oturum 1 — Network YOK, Start() otomatik hepsini spawn eder")]
    public bool autoSpawnOnStart = true;

    private void Start()
    {
        if (autoSpawnOnStart)
        {
            SpawnAllPreviewCharacters();
        }
    }

    /// <summary>5 slotun tamamına, characterTemplates sırasına göre önizleme karakteri koyar.
    /// İsimler Oturum 1'de hardcoded "Oyuncu 1".."Oyuncu 5" (T46'da gerçek username'e bağlanacak).</summary>
    private void SpawnAllPreviewCharacters()
    {
        int count = Mathf.Min(slotTransforms.Length, characterTemplates.Length);
        for (int i = 0; i < count; i++)
        {
            SpawnCharacterInSlot(i, characterTemplates[i]);
        }
    }

    /// <summary>Verilen slota bir karakter prefabı/template'i instantiate eder, fizik
    /// bileşenlerini görsel-only için devre dışı bırakır ve üstüne floating isim etiketi ekler.
    /// TASKS.md T45 Context'te tanımlanan imza ile birebir aynı.</summary>
    public GameObject SpawnCharacterInSlot(int slotIndex, GameObject characterPrefab)
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

        // Görsel-only: fizik bileşenleri devre dışı (ARCHITECTURE.md T45 Context —
        // "Karakterlerin Rigidbody/Collider disabled yapılacak"). Template'ler zaten editörde
        // devre dışı bırakıldı, burası her ihtimale karşı savunma amaçlı (gerçek bir prefab
        // asset'i bağlanırsa da doğru çalışsın diye).
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

        string displayName = "Oyuncu " + (slotIndex + 1);
        CreateFloatingName(instance.transform, displayName);

        return instance;
    }

    /// <summary>T46: Local oyuncu portre değiştirdiğinde çağrılır. Slotta halihazırda
    /// duran önizleme karakterini (varsa) yok eder, sonra SpawnCharacterInSlot ile yenisini
    /// kurar. Eski karakterin floating name'i FloatingNameFollower.LateUpdate() içinde
    /// target==null olduğunu görüp kendini yok eder (bkz. FloatingNameFollower.cs).</summary>
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

        return SpawnCharacterInSlot(slotIndex, newPrefab);
    }


    /// <summary>Karakterin ~2m üstünde, world-space TextMeshPro ile bağımsız bir isim etiketi
    /// oluşturur (child DEĞİL — bkz. FloatingNameFollower.cs açıklaması, scale sorunlarından
    /// kaçınmak için LateUpdate ile takip eder).</summary>
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
