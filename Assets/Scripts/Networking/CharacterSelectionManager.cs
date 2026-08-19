using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Sunucu-yetkili karakter atama sistemi (T43'ten devam, kullanici karari ile guncellendi).
///
/// ESKI DAVRANIS (T43): 5 karakter = 5 slot, her slotu SADECE bir client alabiliyordu, bos
/// kalan client karaktersiz kaliyordu.
///
/// YENI DAVRANIS (kullanici karari):
/// 1) Lobiye giren HER oyuncu HICBIR ZAMAN bos kalmaz - varsayilan olarak Yetiskin'e
///    (characterIndex=0) atanir.
/// 2) Ayni karakter birden fazla oyuncu tarafindan secilebilir (zorunlu benzersizlik yok).
///
/// Bunu desteklemek icin model degisti: eskiden "slot = karakter tipi" idi, simdi
/// "slot = oyuncu atama yeri" (en fazla 5 - GDD oyuncu limiti). Her atama slotu bir
/// (owner clientId, secilen characterIndex) ciftini tutar.
///
/// Fiziksel govde kurali: bir karakter tipinin SAHNEDEKI orijinal objesi (characterNetworkObjects)
/// bos ise ona sahiplik devredilir (eski davranisla ayni, ilk secen icin degisiklik yok). O tip
/// zaten baskasindaysa, characterPrefabs'tan calisma-zamaninda yeni bir kopya spawn edilip
/// sahiplik direkt yeni client'a verilir - boylece ayni karakteri secen herkesin kendi kontrol
/// edebildigi ayri bir govdesi olur.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class CharacterSelectionManager : NetworkBehaviour
{
    public const int CharacterCount = 5;
    public const int AssignmentSlotCount = 5;
    public const ulong NoOwner = ulong.MaxValue;
    public const int DefaultCharacterIndex = 0; // Yetiskin

    [Header("5 Karakter Sablonu (sahnedeki orijinal objeler, sira: Yetiskin, Sisman, Cocuk, Kadin, Yasli)")]
    public NetworkObject[] characterNetworkObjects = new NetworkObject[CharacterCount];

    [Header("Ayni karakter ikinci/ucuncu kez secilince kopyalanacak prefablar (sablonla AYNI sira)")]
    public GameObject[] characterPrefabs = new GameObject[CharacterCount];

    // Atama slotlari: slot index bir OYUNCU yeri (karakter TIPI degil). Owner=NoOwner ise slot bos.
    public NetworkVariable<ulong> Slot0Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Slot1Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Slot2Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Slot3Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Slot4Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Slot0Character = new NetworkVariable<int>(DefaultCharacterIndex, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Slot1Character = new NetworkVariable<int>(DefaultCharacterIndex, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Slot2Character = new NetworkVariable<int>(DefaultCharacterIndex, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Slot3Character = new NetworkVariable<int>(DefaultCharacterIndex, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Slot4Character = new NetworkVariable<int>(DefaultCharacterIndex, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Server-only, network'e gitmez: hangi client'in fiziksel govdesi hangi GameObject.
    private readonly Dictionary<ulong, GameObject> bodyByClient = new Dictionary<ulong, GameObject>();
    // Server-only: karakter sablonu (0..4) su an hangi client'a ait (NoOwner = bos/sunucuda).
    private readonly ulong[] templateOwner = new ulong[CharacterCount];

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        for (int i = 0; i < CharacterCount; i++) templateOwner[i] = NoOwner;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Bu obje spawn olmadan once zaten baglanmis client'lar varsa (ornegin host) onlari da isle.
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            OnClientConnected(client.ClientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    /// <summary>Yeni baglanan (veya bu obje spawn olmadan once zaten bagli) her client, bos bir
    /// atama slotuna varsayilan karakter (Yetiskin) ile atanir - boylece hicbir zaman
    /// karaktersiz kalmaz. Cift cagrilirsa (guvenlik icin) no-op olur.</summary>
    private void OnClientConnected(ulong clientId)
    {
        if (FindAssignmentSlotOf(clientId) >= 0) return;

        int slot = FindFreeAssignmentSlot();
        if (slot < 0)
        {
            Debug.LogWarning("[CharacterSelectionManager] Bos atama slotu yok, client" + clientId + " icin karakter atanamadi.");
            return;
        }

        SetSlotOwner(slot, clientId);
        SetSlotCharacter(slot, DefaultCharacterIndex);
        AssignBody(clientId, DefaultCharacterIndex);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        ReleaseBody(clientId);

        int slot = FindAssignmentSlotOf(clientId);
        if (slot >= 0)
        {
            SetSlotOwner(slot, NoOwner);
            SetSlotCharacter(slot, DefaultCharacterIndex);
        }
    }

    /// <summary>UI, secim butonuna tiklaninca cagirir. Ayni karakter birden fazla client
    /// tarafindan secilebilir - baskasinin secmis olmasi artik REDDETME sebebi degil.</summary>
[ServerRpc(RequireOwnership = false)]
    public void RequestSelectCharacterServerRpc(int characterIndex, ServerRpcParams rpcParams = default)
    {
        if (characterIndex < 0 || characterIndex >= CharacterCount)
        {
            Debug.LogWarning("[CharacterSelectionManager] Gecersiz characterIndex: " + characterIndex);
            return;
        }

        ulong requestingClientId = rpcParams.Receive.SenderClientId;
        int slot = FindAssignmentSlotOf(requestingClientId);
        if (slot < 0)
        {
            Debug.LogWarning("[CharacterSelectionManager] client" + requestingClientId + " icin atama slotu bulunamadi (henuz OnClientConnected islenmemis olabilir).");
            return;
        }

        Debug.Log("[CharacterSelectionManager] client" + requestingClientId + " karakter secimi istedi: index " + characterIndex + " (slot " + slot + ")");

        if (GetSlotCharacter(slot) == characterIndex)
        {
            Debug.Log("[CharacterSelectionManager] client" + requestingClientId + " zaten bu karakteri (index " + characterIndex + ") secmis durumda, no-op.");
            return; // zaten bu karakter secili, no-op
        }

        ReleaseBody(requestingClientId);
        SetSlotCharacter(slot, characterIndex);
        AssignBody(requestingClientId, characterIndex);
    }

    // ---------------- Govde (fiziksel karakter objesi) atama ----------------

    /// <summary>Sablon (characterNetworkObjects[characterIndex]) bossa sahiplik dogrudan ona
    /// devredilir (ilk secen icin eski davranisla ayni). Doluysa characterPrefabs'tan yeni bir
    /// kopya calisma-zamaninda spawn edilip sahiplik yeni client'a verilir.</summary>
    private void AssignBody(ulong clientId, int characterIndex)
    {
        if (!IsServer) return;

        GameObject body;

        if (templateOwner[characterIndex] == NoOwner)
        {
            var template = characterNetworkObjects[characterIndex];
            if (template == null)
            {
                Debug.LogWarning("[CharacterSelectionManager] characterNetworkObjects[" + characterIndex + "] atanmamis.");
                return;
            }
            templateOwner[characterIndex] = clientId;
            if (template.IsSpawned) template.ChangeOwnership(clientId);
            body = template.gameObject;
        }
        else
        {
            var prefab = characterPrefabs[characterIndex];
            if (prefab == null)
            {
                Debug.LogWarning("[CharacterSelectionManager] characterPrefabs[" + characterIndex + "] atanmamis, ayni karakterden ikinci kopya spawn edilemedi.");
                return;
            }

            var template = characterNetworkObjects[characterIndex];
            Vector3 basePos = template != null ? template.transform.position : Vector3.zero;
            Quaternion baseRot = template != null ? template.transform.rotation : Quaternion.identity;
            int copyNumber = CountBodiesOfCharacter(characterIndex); // 1, 2, 3... (0 = sablonun kendisi zaten dolu)
            Vector3 spawnPos = basePos + Vector3.forward * 3f * copyNumber;

            var instance = Instantiate(prefab, spawnPos, baseRot);
            var netObj = instance.GetComponent<NetworkObject>();
            netObj.SpawnWithOwnership(clientId);
            body = instance;
        }

        // Host-mode guvenlik notu: sunucuya ait (henuz atanmamis) sahne objelerinin
        // OwnerClientId'si de 0'dir, host'un kendi LocalClientId'si de 0 - IsOwner TEK
        // BASINA guvenilmez. Bu yuzden govde gercekten bu client'a devredilirken
        // PlayerController.IsControllable acikca true yapiliyor (Update() bunu kontrol eder).
        var assignedPc = body.GetComponent<PlayerController>();
        if (assignedPc != null) assignedPc.IsControllable.Value = true;

        Debug.Log("[CharacterSelectionManager] client" + clientId + " icin govde atandi -> karakter index " + characterIndex + " (" + body.name + ")");

        bodyByClient[clientId] = body;
    }

    /// <summary>Client'in su anki govdesini birakir: sablonsa sahiplik sunucuya geri verilir,
    /// runtime kopyaysa despawn+destroy edilir.</summary>
    private void ReleaseBody(ulong clientId)
    {
        if (!IsServer) return;
        if (!bodyByClient.TryGetValue(clientId, out var body) || body == null)
        {
            bodyByClient.Remove(clientId);
            return;
        }

        int templateIndex = System.Array.FindIndex(characterNetworkObjects, t => t != null && t.gameObject == body);
        var netObj = body.GetComponent<NetworkObject>();

        // Govde birakilirken kontrol yetkisi de acikca kapatilir - aksi halde sablon sunucuya
        // geri dondugunde IsControllable=true takili kalir ve host-mode'da yeniden kontrol edilebilir olur.
        var releasedPc = body.GetComponent<PlayerController>();
        if (releasedPc != null) releasedPc.IsControllable.Value = false;

        if (templateIndex >= 0)
        {
            templateOwner[templateIndex] = NoOwner;
            if (netObj != null && netObj.IsSpawned) netObj.ChangeOwnership(NetworkManager.ServerClientId);
        }
        else if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }

        bodyByClient.Remove(clientId);
    }

    private int CountBodiesOfCharacter(int characterIndex)
    {
        int count = 0;
        for (int i = 0; i < AssignmentSlotCount; i++)
        {
            if (GetSlotOwner(i) != NoOwner && GetSlotCharacter(i) == characterIndex) count++;
        }
        return count;
    }

    // ---------------- Atama slotu yardimcilari ----------------

    private int FindFreeAssignmentSlot()
    {
        for (int i = 0; i < AssignmentSlotCount; i++) if (GetSlotOwner(i) == NoOwner) return i;
        return -1;
    }

    private int FindAssignmentSlotOf(ulong clientId)
    {
        for (int i = 0; i < AssignmentSlotCount; i++) if (GetSlotOwner(i) == clientId) return i;
        return -1;
    }

    public ulong GetSlotOwner(int slot)
    {
        switch (slot)
        {
            case 0: return Slot0Owner.Value;
            case 1: return Slot1Owner.Value;
            case 2: return Slot2Owner.Value;
            case 3: return Slot3Owner.Value;
            case 4: return Slot4Owner.Value;
            default: return NoOwner;
        }
    }

    public int GetSlotCharacter(int slot)
    {
        switch (slot)
        {
            case 0: return Slot0Character.Value;
            case 1: return Slot1Character.Value;
            case 2: return Slot2Character.Value;
            case 3: return Slot3Character.Value;
            case 4: return Slot4Character.Value;
            default: return DefaultCharacterIndex;
        }
    }

    private void SetSlotOwner(int slot, ulong value)
    {
        switch (slot)
        {
            case 0: Slot0Owner.Value = value; break;
            case 1: Slot1Owner.Value = value; break;
            case 2: Slot2Owner.Value = value; break;
            case 3: Slot3Owner.Value = value; break;
            case 4: Slot4Owner.Value = value; break;
        }
    }

    private void SetSlotCharacter(int slot, int value)
    {
        switch (slot)
        {
            case 0: Slot0Character.Value = value; break;
            case 1: Slot1Character.Value = value; break;
            case 2: Slot2Character.Value = value; break;
            case 3: Slot3Character.Value = value; break;
            case 4: Slot4Character.Value = value; break;
        }
    }

    /// <summary>UI: verilen karakter indexini su an secmis olan tum client'larin clientId
    /// listesini dondurur (bos ise bos liste - artik "tek sahip" kavrami yok).</summary>
    public List<ulong> GetOwnersOfCharacter(int characterIndex)
    {
        var result = new List<ulong>();
        for (int i = 0; i < AssignmentSlotCount; i++)
        {
            ulong owner = GetSlotOwner(i);
            if (owner != NoOwner && GetSlotCharacter(i) == characterIndex) result.Add(owner);
        }
        return result;
    }
}
