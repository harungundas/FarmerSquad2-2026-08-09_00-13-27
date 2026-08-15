using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Karakter secim UI'i (ARCHITECTURE.md "## Karakter Seçim UI", TASKS.md T43).
/// KULLANICI KARARI (bu HANDOFF/sohbette netlestirildi): ayri bir tam-ekran ekran DEGIL,
/// Lobi ekraninin (LobbyCanvas/Panel) İÇİNE gomulu bir panel - boylece lobideki herkes
/// birbirinin ne sectigini canli gorebilir.
///
/// 5 sabit satir (Yetiskin/Sisman/Cocuk/Kadin/Yasli - CharacterSelectionManager'daki slot
/// sirasiyla AYNI), her biri CharacterClassData'dan okunan kisa bir stat ozeti gosterir.
/// CharacterSelectionManager'in 5 NetworkVariable'ina abone olup canli guncellenir.
///
/// GELECEK POLISH FIKRI (kullanici notu, bu gorevin kapsami DEGIL): Karakterlerin 3D
/// modellerinin PUBG tarzi bir onizleme kamerasi/RenderTexture ile lobide gorunmesi -
/// ayri bir kamera + RenderTexture kurulumu gerektirir, ileride ele alinabilir.
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

    private void Awake()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null) continue;
            int captured = i;
            string stat = BuildStatSummary(i);
            rows[i].Setup(i, characterDisplayNames[i], stat, OnSelectClicked);
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        RefreshAllRows();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        // NetworkManager/selectionManager gec spawn olabilir (host baslatilmadan once panel
        // acilmis olabilir) - abone degilsek her frame denemek ucuz ve basit bir cozum.
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
        selectionManager.Slot0Owner.OnValueChanged += (a, b) => RefreshRow(0);
        selectionManager.Slot1Owner.OnValueChanged += (a, b) => RefreshRow(1);
        selectionManager.Slot2Owner.OnValueChanged += (a, b) => RefreshRow(2);
        selectionManager.Slot3Owner.OnValueChanged += (a, b) => RefreshRow(3);
        selectionManager.Slot4Owner.OnValueChanged += (a, b) => RefreshRow(4);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        // NetworkVariable'lar despawn'da temizlendigi icin burada ayrintili -= yapmiyoruz;
        // subscribed bayragi Update()'te yeniden abone olunmasini engeller/saglar.
        subscribed = false;
    }

    public void RefreshAllRows()
    {
        for (int i = 0; i < rows.Length; i++) RefreshRow(i);
    }

    private void RefreshRow(int index)
    {
        if (index < 0 || index >= rows.Length || rows[index] == null) return;

        ulong ownerId = selectionManager != null ? selectionManager.GetOwnerOf(index) : CharacterSelectionManager.NoOwner;
        bool taken = ownerId != CharacterSelectionManager.NoOwner;
        bool isMine = taken && NetworkManager.Singleton != null && ownerId == NetworkManager.Singleton.LocalClientId;

        string label = "";
        if (taken)
        {
            label = isMine ? "Sen" : ("Oyuncu " + ownerId);
        }

        rows[index].SetOccupant(label, isMine, taken && !isMine);
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
