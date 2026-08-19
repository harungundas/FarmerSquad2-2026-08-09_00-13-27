using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [Lobiye Katıl] ekranı (ARCHITECTURE.md "## Ana Menü & Lobi UI" + "## Ortak UI Kiti",
/// TASKS.md T50).
///
/// MainMenuCanvas/Panel/LobbyListPanel içinde, ProfilePanel (T49) ile AYNI modal desen
/// kullanılır: ana menü paneli ARKADA açık kalır, bu panel üstüne biner.
///
/// BUG DUZELTMESI (kullanici istegi): Onceki surumde bu ekran LobbySessionManager.ActiveLobbies
/// listesini satir satir gosteriyordu. Artik liste YOK - sadece tek bir lobi kodu input alani +
/// [Lobiye Katil] butonu var. Kullanici kodu girer, OnJoinByCodeClicked() dogrudan
/// LobbySessionManager.JoinLobby(code, ...) ile katilmayi dener.
/// </summary>
public class LobbyListUI : MonoBehaviour
{
    [Header("Panel Kok (bu ekran)")]
    public GameObject panelRoot;

    [Header("Ana Menu Panel Kok (LobbyListPanel bunun ICINDE, T49 modal desenini korur)")]
    public GameObject mainMenuPanelRoot;

    [Header("Lobi Kodu ile Katilim (liste yerine)")]
    public TMP_InputField codeInputField;
    public Button joinByCodeButton;
    public TextMeshProUGUI warningText;    // gecersiz/dolu kod uyarisi

    [Header("Ana Menu Donus")]
    public Button anaMenuButton;

    [Header("Baglantilar")]
    public GameNetworkManager gameNetworkManager;
    public LobbyUI lobbyUI;

/// <summary>ONEMLI: panelRoot BURADA SetActive(false) YAPILMAZ. Sahnede LobbyListPanel zaten
    /// edit-time'da inactive kuruldu (bkz. hiyerarsi kurulumu). Eger panelRoot ilk kez BURADA
    /// false yapilirsa, Show()'un panelRoot.SetActive(true) cagirdigi an GameObject ilk kez
    /// aktiflesiyor olacagindan Unity Awake()'i O ANDA senkron tetikler - Awake icindeki
    /// SetActive(false) da Show()'un SetActive(true)'sunun hemen ardindan calisip paneli tekrar
    /// kapatir (bu bug T50 test asamasinda yakalandi ve duzeltildi). rowTemplate icin sorun
    /// yok cunku o zaten ListContent altinda kalici inactive bir sablon.</summary>
private void Awake()
    {
        // DUZELTME (kullanici raporu - cift tiklama bug'i): panelRoot bu script'in kendi
        // GameObject'i (LobbyListPanel) oldugu ve sahnede zaten inactive kuruldugu icin,
        // burada tekrar SetActive(false) cagirmak Show()'un ilk SetActive(true)'sinin
        // TETIKLEDIGI senkron Awake cagrisi icinde paneli hemen kapatiyordu (ilk tiklama
        // etkisiz gorunuyordu, ikinci tiklamada Awake tekrar calismadigindan calisiyordu).
        // panelRoot.SetActive(false) satiri KASITLI olarak buradan kaldirildi.
        if (anaMenuButton != null) anaMenuButton.onClick.AddListener(OnAnaMenuClicked);
        if (joinByCodeButton != null) joinByCodeButton.onClick.AddListener(OnJoinByCodeClicked);
    }

    /// <summary>MainMenuController.OnJoinLobbyClicked() tarafindan cagirilir. Ana menu panelini
    /// acik tutar (T49 modal deseni), kod giris panelini acar. BUG DUZELTMESI (kullanici
    /// istegi): liste yerine sadece lobi kodu input'u istenir.</summary>
    public void Show()
    {
        if (mainMenuPanelRoot != null) mainMenuPanelRoot.SetActive(true);
        if (panelRoot != null) panelRoot.SetActive(true);
        if (warningText != null) warningText.text = "";
        if (codeInputField != null) codeInputField.text = "";
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

/// <summary>[Lobiye Katil]. Input alanindaki kodu okur, LobbySessionManager.JoinLobby ile
    /// katilmaya calisir. Kod bos/gecersizse, lobi doluysa veya yoksa warningText'te sebep
    /// gosterilir. Basarili olursa StartClient() cagirir ve LobbyUI'yi gercek lobi koduyla acar.</summary>
    private void OnJoinByCodeClicked()
    {
        if (codeInputField == null || string.IsNullOrEmpty(codeInputField.text) || !int.TryParse(codeInputField.text, out int code))
        {
            if (warningText != null) warningText.text = "Gecerli bir lobi kodu girin.";
            return;
        }

        bool success = LobbySessionManager.JoinLobby(code, out LobbySessionManager.LobbyInfo info);

        if (!success)
        {
            if (warningText != null)
            {
                warningText.text = (info.lobbyCode == code)
                    ? (info.playerCount + "/" + LobbySessionManager.MaxPlayersPerLobby + " - Bu lobi DOLU, katilamazsin.")
                    : "Bu kodda bir lobi bulunamadi.";
            }
            Debug.Log("[LobbyListUI] Katilim reddedildi (dolu/gecersiz), kod: " + code);
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
