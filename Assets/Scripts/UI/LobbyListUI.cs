using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [Lobiye Katıl] ekranı (ARCHITECTURE.md "## Ana Menü & Lobi UI" + "## Ortak UI Kiti",
/// TASKS.md T50).
///
/// MainMenuCanvas/Panel/LobbyListPanel içinde, ProfilePanel (T49) ile AYNI modal desen
/// kullanılır: ana menü paneli ARKADA açık kalır, bu panel üstüne biner (HANDOFF.md T49
/// notu — bu desenin korunup korunmayacağı burada netleşti: KORUNDU).
///
/// Liste verisi HER ZAMAN LobbySessionManager.ActiveLobbies'ten okunur (hardcoded DEĞİL —
/// TASKS.md T50 Context: "gerçek veri (hardcoded değil, boş lobiler silinir)"). Satırlar
/// rowTemplate'in runtime Instantiate() klonlarıdır, Refresh() her Show() çağrısında listeyi
/// yeniden çizer.
///
/// Oturum 1'de gerçek 2. bir Unity client'ı yok (bkz. HANDOFF "2-client network testi henüz
/// yapılmadı"), bu yüzden 5/5-dolu ve boş-lobiye-katılma senaryolarını test edebilmek için ilk
/// açılışta LobbySessionManager.CreateFakeLobby ile 2 sahte lobi tohumlanır (T48'in
/// simulatedNotReadyCount deseniyle aynı mantık — gerçek network gelince kaldırılacak).
/// </summary>
public class LobbyListUI : MonoBehaviour
{
    [Header("Panel Kok (bu ekran)")]
    public GameObject panelRoot;

    [Header("Ana Menu Panel Kok (LobbyListPanel bunun ICINDE, T49 modal desenini korur)")]
    public GameObject mainMenuPanelRoot;

    [Header("Liste")]
    public Transform listContent;          // VerticalLayoutGroup'lu container (ListViewport altinda)
    public GameObject rowTemplate;         // sahnede SetActive(false) sablon satir, Instantiate ile cogaltilir
    public TextMeshProUGUI emptyStateText; // "Aktif lobi yok" - liste bossa gorunur
    public TextMeshProUGUI warningText;    // 5/5 dolu uyarisi

    [Header("Ana Menu Donus")]
    public Button anaMenuButton;

    [Header("Baglantilar")]
    public GameNetworkManager gameNetworkManager;
    public LobbyUI lobbyUI;

    [Header("Oturum 1 - Test Tohumu (gercek 2. client yok, bkz. sinif yorumu)")]
    public bool seedDebugLobbiesOnFirstShow = true;
    private static bool hasSeeded = false;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();

/// <summary>ONEMLI: panelRoot BURADA SetActive(false) YAPILMAZ. Sahnede LobbyListPanel zaten
    /// edit-time'da inactive kuruldu (bkz. hiyerarsi kurulumu). Eger panelRoot ilk kez BURADA
    /// false yapilirsa, Show()'un panelRoot.SetActive(true) cagirdigi an GameObject ilk kez
    /// aktiflesiyor olacagindan Unity Awake()'i O ANDA senkron tetikler - Awake icindeki
    /// SetActive(false) da Show()'un SetActive(true)'sunun hemen ardindan calisip paneli tekrar
    /// kapatir (bu bug T50 test asamasinda yakalandi ve duzeltildi). rowTemplate icin sorun
    /// yok cunku o zaten ListContent altinda kalici inactive bir sablon.</summary>
    private void Awake()
    {
        if (rowTemplate != null) rowTemplate.SetActive(false);
        if (anaMenuButton != null) anaMenuButton.onClick.AddListener(OnAnaMenuClicked);
    }

    /// <summary>MainMenuController.OnJoinLobbyClicked() tarafından çağrılır. Ana menü panelini
    /// açık tutar (T49 modal deseni), liste panelini açar ve LobbySessionManager.ActiveLobbies'i
    /// güncel haliyle çizer.</summary>
    public void Show()
    {
        if (seedDebugLobbiesOnFirstShow && !hasSeeded)
        {
            hasSeeded = true;
            LobbySessionManager.CreateFakeLobby("Ayse", LobbySessionManager.MaxPlayersPerLobby); // 5/5 DOLU senaryo
            LobbySessionManager.CreateFakeLobby("Mehmet", 2); // bos yeri olan senaryo
        }

        if (mainMenuPanelRoot != null) mainMenuPanelRoot.SetActive(true);
        if (panelRoot != null) panelRoot.SetActive(true);
        if (warningText != null) warningText.text = "";

        Refresh();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Mevcut satirlari yok edip LobbySessionManager.ActiveLobbies'e gore yeniden
    /// cizer. Liste bossa emptyStateText gosterilir.</summary>
    private void Refresh()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] != null) Destroy(spawnedRows[i]);
        }
        spawnedRows.Clear();

        bool hasAny = LobbySessionManager.ActiveLobbies.Count > 0;
        if (emptyStateText != null) emptyStateText.gameObject.SetActive(!hasAny);

        if (rowTemplate == null || listContent == null)
        {
            Debug.LogWarning("[LobbyListUI] rowTemplate veya listContent atanmamis, liste cizilemedi.");
            return;
        }

        for (int i = 0; i < LobbySessionManager.ActiveLobbies.Count; i++)
        {
            LobbySessionManager.LobbyInfo info = LobbySessionManager.ActiveLobbies[i];
            GameObject row = Instantiate(rowTemplate, listContent);
            row.SetActive(true);
            row.name = "LobbyRow_" + info.lobbyCode;

            bool isFull = info.playerCount >= LobbySessionManager.MaxPlayersPerLobby;

            TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = "Kod: " + info.lobbyCode + "   (" + info.playerCount + "/" + LobbySessionManager.MaxPlayersPerLobby + ")" + (isFull ? "  - DOLU" : "");
            }

            Button button = row.GetComponent<Button>();
            if (button != null)
            {
                int capturedCode = info.lobbyCode;
                button.onClick.AddListener(() => OnLobbyRowClicked(capturedCode));
            }

            spawnedRows.Add(row);
        }
    }

    /// <summary>Bir lobi satirina tiklaninca cagirilir. Lobi doluysa (5/5) katilmaz, sadece
    /// uyari metni gosterir (TASKS.md T50 Test: "5/5 lobiye tiklaninca uyari aliyor mu").
    /// Bos lobiyse LobbySessionManager.JoinLobby ile katilir, StartClient() cagirir ve
    /// LobbyUI'yi gercek lobi koduyla acar.</summary>
    private void OnLobbyRowClicked(int code)
    {
        bool success = LobbySessionManager.JoinLobby(code, out LobbySessionManager.LobbyInfo info);

        if (!success)
        {
            if (warningText != null)
            {
                warningText.text = (info.lobbyCode == code)
                    ? (info.playerCount + "/" + LobbySessionManager.MaxPlayersPerLobby + " - Bu lobi DOLU, katilamazsin.")
                    : "Bu lobi artik mevcut degil.";
            }
            Debug.Log("[LobbyListUI] Katilim reddedildi (dolu/gecersiz), kod: " + code);
            Refresh();
            return;
        }

        if (warningText != null) warningText.text = "";

        if (gameNetworkManager != null)
        {
            gameNetworkManager.StartClient();
        }
        else
        {
            Debug.LogError("[LobbyListUI] gameNetworkManager atanmamis.");
        }

        Hide();
        if (mainMenuPanelRoot != null) mainMenuPanelRoot.SetActive(false);

        if (lobbyUI != null)
        {
            lobbyUI.Show(info.lobbyCode);
        }
        else
        {
            Debug.LogWarning("[LobbyListUI] lobbyUI atanmamis, lobi sahnesi acilamadi.");
        }

        Debug.Log("[LobbyListUI] Lobiye katildi, kod: " + info.lobbyCode + " (" + info.playerCount + "/" + LobbySessionManager.MaxPlayersPerLobby + ")");
    }

    /// <summary>[Ana Menü]. Liste panelini kapatir; ana menu paneli zaten acik kaldigi icin
    /// (T49 modal deseni) ekstra bir sey yapmaya gerek yok.</summary>
    private void OnAnaMenuClicked()
    {
        Hide();
        Debug.Log("[LobbyListUI] Ana Menu'ye donuldu.");
    }
}
