using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Sunucu-yetkili karakter atama sistemi (T43 kapsaminda, T09'dan beri acik duran
/// "oyuncu-karakter eslestirme eksigi" sorununu da cozer). 5 sabit karakter (yetiskin/sisman/
/// cocuk/kadin/yasli, sahnede zaten NetworkObject'li) icin hangi client'in hangisini sectigini
/// NetworkVariable&lt;ulong&gt; dizisiyle (bos=ulong.MaxValue) tutar.
///
/// Secim yapilinca: (1) bu client'in ONCEDEN sectigi baska bir karakter varsa bosaltilir ve
/// sahipligi sunucuya geri verilir, (2) yeni secilen karakterin NetworkObject sahipligi
/// ChangeOwnership ile bu client'a devredilir - boylece PlayerController.IsOwner o client icin
/// dogru calisir (T09/T42 HANDOFF'larinda tekrarlanan "tum PlayerController'lar IsOwner=True
/// donuyor" sorunu, karakter fiilen secildiginde dogal olarak duzelir).
///
/// UI (CharacterSelectUI.cs) bu NetworkVariable'lara abone olup gosterimi gunceller.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class CharacterSelectionManager : NetworkBehaviour
{
    public const int CharacterCount = 5;
    public const ulong NoOwner = ulong.MaxValue;

    [Header("5 Karakter (sira: Yetiskin, Sisman, Cocuk, Kadin, Yasli - CharacterClassData/GDD sirasi)")]
    public NetworkObject[] characterNetworkObjects = new NetworkObject[CharacterCount];

    public NetworkVariable<ulong> Slot0Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Slot1Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Slot2Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Slot3Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> Slot4Owner = new NetworkVariable<ulong>(NoOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<ulong> GetSlot(int index)
    {
        switch (index)
        {
            case 0: return Slot0Owner;
            case 1: return Slot1Owner;
            case 2: return Slot2Owner;
            case 3: return Slot3Owner;
            case 4: return Slot4Owner;
            default: return null;
        }
    }

    private void SetSlotValue(int index, ulong value)
    {
        switch (index)
        {
            case 0: Slot0Owner.Value = value; break;
            case 1: Slot1Owner.Value = value; break;
            case 2: Slot2Owner.Value = value; break;
            case 3: Slot3Owner.Value = value; break;
            case 4: Slot4Owner.Value = value; break;
        }
    }

    /// <summary>UI, hangi client'in hangi karakteri sectigini okumak icin kullanir (0..4 -> clientId veya NoOwner).</summary>
    public ulong GetOwnerOf(int index)
    {
        var slot = GetSlot(index);
        return slot != null ? slot.Value : NoOwner;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSelectCharacterServerRpc(int characterIndex, ServerRpcParams rpcParams = default)
    {
        if (characterIndex < 0 || characterIndex >= CharacterCount)
        {
            Debug.LogWarning("[CharacterSelectionManager] Gecersiz characterIndex: " + characterIndex);
            return;
        }

        ulong requestingClientId = rpcParams.Receive.SenderClientId;
        ulong currentOwnerOfRequested = GetOwnerOf(characterIndex);

        if (currentOwnerOfRequested != NoOwner && currentOwnerOfRequested != requestingClientId)
        {
            Debug.Log("[CharacterSelectionManager] Slot " + characterIndex + " zaten client" + currentOwnerOfRequested + " tarafindan secilmis, istek reddedildi.");
            return;
        }

        // Bu client'in daha once sectigi baska bir slot varsa bosalt + sahipligi sunucuya geri ver.
        for (int i = 0; i < CharacterCount; i++)
        {
            if (i == characterIndex) continue;
            if (GetOwnerOf(i) == requestingClientId)
            {
                SetSlotValue(i, NoOwner);
                if (characterNetworkObjects[i] != null && characterNetworkObjects[i].IsSpawned)
                {
                    characterNetworkObjects[i].ChangeOwnership(NetworkManager.ServerClientId);
                }
            }
        }

        SetSlotValue(characterIndex, requestingClientId);

        if (characterNetworkObjects[characterIndex] != null && characterNetworkObjects[characterIndex].IsSpawned)
        {
            characterNetworkObjects[characterIndex].ChangeOwnership(requestingClientId);
        }
        else
        {
            Debug.LogWarning("[CharacterSelectionManager] characterNetworkObjects[" + characterIndex + "] atanmamis veya spawn olmamis.");
        }
    }
}
