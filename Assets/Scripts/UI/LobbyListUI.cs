using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// [Lobiye Katıl] ekranı (ARCHITECTURE.md "## Ana Menü & Lobi UI").
///
/// KULLANICI KARARI (Steamworks ISTEMIYORUM, NetworkManager kendi basina host/client
/// yonetsin, LAN yeterli): Bu ekran ARTIK lobi KODU degil, hostun LAN IP adresini ister.
/// Eskiden LobbySessionManager.JoinLobby(code,...) ile sahte bir listede arama yapiyordu -
/// bu liste iki ayri bilgisayarin surecleri arasinda hic paylasilmadigi icin katilim HER
/// ZAMAN basarisiz oluyordu. Artik dogrudan GameNetworkManager.StartClient(ip) cagrilir,
/// basari/basarisizlik GERCEK NetworkManager olaylarindan (OnClientConnectedCallback /
/// OnClientDisconnectCallback) anlasilir.
/// </summary>
public class LobbyListUI : MonoBehaviour
{
    [Header("Panel Kok (bu ekran)")]
    public GameObject panelRoot;

    [Header("Ana Menu Panel Kok (LobbyListPanel bunun ICINDE, T49 modal desenini korur)")]
    public GameObject mainMenuPanelRoot;

    [Header("Host IP Adresi ile Katilim")]
    [Tooltip("Placeholder/label metni Inspector'dan 'Lobi Kodu' yerine 'Host IP Adresi' olarak guncellenmeli - kod tarafinda zorunlu degil, sadece kullanici deneyimi icin.")]
    public TMP_InputField codeInputField;
    public Button joinByCodeButton;
    public TextMeshProUGUI warningText;

    [Header("Ana Menu Donus")]
    public Button anaMenuButton;

    [Header("Baglantilar")]
    public GameNetworkManager gameNetworkManager;
    public LobbyUI lobbyUI;

    private const float ConnectTimeoutSeconds = 8f;
    private Coroutine connectTimeoutCoroutine;
    private bool connectCallbacksHooked = false;

    private void Awake()
    {
        if (anaMenuButton != null) anaMenuButton.onClick.AddListener(OnAnaMenuClicked);
        if (joinByCodeButton != null) joinByCodeButton.onClick.AddListener(OnJoinClicked);
    }

    public void Show()
    {
        if (mainMenuPanelRoot != null) mainMenuPanelRoot.SetActive(true);
        if (panelRoot != null) panelRoot.SetActive(true);
        if (warningText != null) warningText.text = "";
        if (codeInputField != null) codeInputField.text = "";
        if (joinByCodeButton != null) joinByCodeButton.interactable = true;
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>[Lobiye Katıl]. Input alanindaki METNI (artik sayisal kod DEGIL, IP adresi -
    /// orn "192.168.1.5") okur, GameNetworkManager.StartClient(ip) ile gercek Netcode
    /// baglanti girisimi baslatir. Sonuc NetworkManager'in kendi callback'leriyle asenkron
    /// olarak belirlenir (bkz. OnConnectedToHost / OnDisconnectedFromHost / timeout).</summary>
    private void OnJoinClicked()
    {
        if (codeInputField == null || string.IsNullOrWhiteSpace(codeInputField.text))
        {
            if (warningText != null) warningText.text = "Host'un IP adresini girin (örn: 192.168.1.5).";
            return;
        }

        if (gameNetworkManager == null)
        {
            Debug.LogError("[LobbyListUI] gameNetworkManager atanmamis.");
            return;
        }

        string ip = codeInputField.text.Trim();

        if (warningText != null) warningText.text = "Bağlanılıyor...";
        if (joinByCodeButton != null) joinByCodeButton.interactable = false;

        bool started = gameNetworkManager.StartClient(ip);
        if (!started)
        {
            if (warningText != null) warningText.text = "Bağlantı başlatılamadı.";
            if (joinByCodeButton != null) joinByCodeButton.interactable = true;
            return;
        }

        if (NetworkManager.Singleton != null && !connectCallbacksHooked)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnConnectedToHost;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnectedFromHost;
            connectCallbacksHooked = true;
        }

        if (connectTimeoutCoroutine != null) StopCoroutine(connectTimeoutCoroutine);
        connectTimeoutCoroutine = StartCoroutine(ConnectTimeoutWatch());
    }

    /// <summary>ConnectTimeoutSeconds icinde baglanti kurulamazsa (host kapali, yanlis IP,
    /// firewall vb.) girisimi iptal edip kullaniciyi bilgilendirir.</summary>
    private IEnumerator ConnectTimeoutWatch()
    {
        yield return new WaitForSeconds(ConnectTimeoutSeconds);

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsConnectedClient)
        {
            NetworkManager.Singleton.Shutdown();
            CleanupConnectionAttempt();
            if (warningText != null) warningText.text = "Bağlanılamadı. IP'yi, aynı ağda olduğunuzu ve arkadaşının host başlattığını kontrol et.";
        }
    }

    /// <summary>Gercekten bize ait client baglaninca (host tarafinda BASKA oyuncular da
    /// baglanabilir, bu yuzden clientId kontrolu sart) LobbyUI'yi acar.</summary>
    private void OnConnectedToHost(ulong clientId)
    {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId) return;

        CleanupConnectionAttempt();
        if (warningText != null) warningText.text = "";

        Hide();
        if (mainMenuPanelRoot != null) mainMenuPanelRoot.SetActive(false);

        if (lobbyUI != null)
        {
            lobbyUI.Show(0); // gercek baglantida sayisal "lobi kodu" kavraminin onemi yok
        }
        else
        {
            Debug.LogWarning("[LobbyListUI] lobbyUI atanmamis, lobi ekrani acilamadi.");
        }

        Debug.Log("[LobbyListUI] Host'a basariyla baglanildi.");
    }

    /// <summary>Baglanti GIRISIMI sirasinda (henuz gercekten baglanmadan) kopma/red olursa
    /// tetiklenir - zaten baglanmis bir oyuncunun normal ayrilmasiyla KARISTIRILMAMALI.</summary>
    private void OnDisconnectedFromHost(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient) return;

        CleanupConnectionAttempt();
        if (warningText != null) warningText.text = "Bağlantı reddedildi/koptu. IP'yi kontrol et.";
    }

    private void CleanupConnectionAttempt()
    {
        if (connectTimeoutCoroutine != null)
        {
            StopCoroutine(connectTimeoutCoroutine);
            connectTimeoutCoroutine = null;
        }

        if (NetworkManager.Singleton != null && connectCallbacksHooked)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnConnectedToHost;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnectedFromHost;
        }
        connectCallbacksHooked = false;

        if (joinByCodeButton != null) joinByCodeButton.interactable = true;
    }

    private void OnAnaMenuClicked()
    {
        Hide();
        Debug.Log("[LobbyListUI] Ana Menu'ye donuldu.");
    }
}
