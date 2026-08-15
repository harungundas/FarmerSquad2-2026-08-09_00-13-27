using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Karakter secim UI'i (ARCHITECTURE.md "## Karakter Seçim UI", TASKS.md T43).
/// Lobi ekraninin (LobbyCanvas/Panel) İÇİNE gomulu bir panel - lobideki herkes birbirinin ne
/// sectigini canli gorebilir.
///
/// 5 sabit satir (Yetiskin/Sisman/Cocuk/Kadin/Yasli). GUNCEL KURAL (kullanici karari): ayni
/// karakteri BIRDEN FAZLA oyuncu secebilir - bu yuzden bir satirin "sahibi" tekil degil, bir
/// LISTE'dir (CharacterSelectionManager.GetOwnersOfCharacter). Satir hicbir zaman kilitlenmez
/// (baskasi secmis olsa da tiklanabilir kalir).
///
/// CharacterSelectionManager'in 10 NetworkVariable'ina (5 slot sahibi + 5 slot karakteri) abone
/// olup herhangi biri degisince tum satirlari yeniden hesaplar (5 satir icin ucuz islem).
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    [Header("Karakter Verisi (sira: Yetiskin, Sisman, Cocuk, Kadin, Yasli)")]
    public CharacterClassData[] characterClassData = new CharacterClassData[5];
    public string[] characterDisplayNames = { "Yetişkin", "Şişman", "Çocuk", "Kadın", "Yaşlı" };

    [Header("Baglantilar")]
    public CharacterSelectionManager selectionManager;
    public CharacterSelectRowUI[] rows = new CharacterSelectRowUI[5];

    private bool subscribed;

    /// <summary>Satir wiring'i burada (Awake DEGIL) - bkz. T43 HANDOFF notu: Awake-zamanli
    /// wiring'in onSelect callback'ini NULL biraktigi gozlemlenmisti. OnEnable panel her
    /// acildiginda (LobbyUI.Show() ile) yeniden calisir, Setup() idempotent.</summary>
    private void OnEnable()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null) continue;
            string stat = BuildStatSummary(i);
            rows[i].Setup(i, characterDisplayNames[i], stat, OnSelectClicked);
        }

        TrySubscribe();
        RefreshAllRows();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        // NetworkManager/selectionManager gec spawn olabilir - abone degilsek her frame denemek
        // ucuz ve basit bir cozum.
        if (!subscribed) TrySubscribe();
    }

    private string BuildStatSummary(int index)
    {
        var data = index < characterClassData.Length ? characterClassData[index] : null;
        if (data == null) return "(veri yok)";
        return $"Hız: {data.walkSpeed:0.#}  Taşıma: {data.carryCapacityLight}  Yemleme: {data.feedDuration:0.#}sn";
    }

    private void TrySubscribe()
    {
        if (subscribed || selectionManager == null) return;

        selectionManager.Slot0Owner.OnValueChanged += OnAnySlotOwnerChanged;
        selectionManager.Slot1Owner.OnValueChanged += OnAnySlotOwnerChanged;
        selectionManager.Slot2Owner.OnValueChanged += OnAnySlotOwnerChanged;
        selectionManager.Slot3Owner.OnValueChanged += OnAnySlotOwnerChanged;
        selectionManager.Slot4Owner.OnValueChanged += OnAnySlotOwnerChanged;

        selectionManager.Slot0Character.OnValueChanged += OnAnySlotCharacterChanged;
        selectionManager.Slot1Character.OnValueChanged += OnAnySlotCharacterChanged;
        selectionManager.Slot2Character.OnValueChanged += OnAnySlotCharacterChanged;
        selectionManager.Slot3Character.OnValueChanged += OnAnySlotCharacterChanged;
        selectionManager.Slot4Character.OnValueChanged += OnAnySlotCharacterChanged;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        // NetworkVariable'lar despawn'da temizlendigi icin burada ayrintili -= yapmiyoruz;
        // subscribed bayragi Update()'te yeniden abone olunmasini engeller/saglar.
        subscribed = false;
    }

    private void OnAnySlotOwnerChanged(ulong a, ulong b) => RefreshAllRows();
    private void OnAnySlotCharacterChanged(int a, int b) => RefreshAllRows();

    public void RefreshAllRows()
    {
        for (int i = 0; i < rows.Length; i++) RefreshRow(i);
    }

    /// <summary>index burada bir KARAKTER TIPI (satir), atama slotu DEGIL. Bu karakteri secmis
    /// olan tum oyunculari (0, 1 veya daha fazla) listeler.</summary>
    private void RefreshRow(int index)
    {
        if (index < 0 || index >= rows.Length || rows[index] == null) return;

        if (selectionManager == null)
        {
            rows[index].SetOccupant("", false, false);
            return;
        }

        var owners = selectionManager.GetOwnersOfCharacter(index);
        ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : CharacterSelectionManager.NoOwner;
        bool isMine = owners.Contains(localId);

        string label = "";
        if (owners.Count > 0)
        {
            var names = new System.Collections.Generic.List<string>(owners.Count);
            foreach (var ownerId in owners)
            {
                names.Add(ownerId == localId ? "Sen" : ("Oyuncu " + ownerId));
            }
            label = string.Join(", ", names);
        }

        // isTakenByOther HER ZAMAN false: ayni karakter birden fazla oyuncu tarafindan
        // secilebildigi icin baskasinin secmis olmasi butonu kilitlemez.
        rows[index].SetOccupant(label, isMine, false);
    }

    private void OnSelectClicked(int index)
    {
        if (selectionManager == null)
        {
            Debug.LogWarning("[CharacterSelectUI] selectionManager atanmamis.");
            return;
        }
        selectionManager.RequestSelectCharacterServerRpc(index);
    }
}
