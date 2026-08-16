using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana Menu (ARCHITECTURE.md "## Ana Menü & Lobi UI" + "## Ortak UI Kiti").
/// [Lobi Oluştur]/[Lobiye Katıl]/[Profil]/[Ayarlar]/[Çıkış] butonları.
///
/// SAPMA (bkz. HANDOFF.md T40 notu, dogrulandi): TASKS.md T40 "LobbyManager (T07) ile baglanti"
/// diyor, ama T07 GERCEKTEN atlanmis - Assets/Scripts/Networking/LobbyManager.cs YOK, sadece
/// GameNetworkManager.cs var (StartHost()/StartClient(), gecici UTP transport). Bu yuzden
/// [Lobi Oluştur]=StartHost(), [Lobiye Katıl]=StartClient() olarak baglandi - gercek bir Steam
/// lobi kodu uretimi/girisi YOK (T41 veya gercek T07'ye ertelendi).
///
/// T41 EKLEMESI: StartHost()/StartClient() basarili baslatma sonrasi artik LobbyUI.Show()
/// cagriliyor (oyuncu listesi + host-only kick gostermek icin). lobbyUI atanmamissa (henuz
/// kurulmamis bir sahnede) stub log basar, hata firlatmaz.
///
/// T49 EKLEMESI: [Profil] butonu -> ProfilePanel modal'i acar (Panel'in cocugu, ForceStartModalPanel
/// ile ayni desen: merkezde sabit oranli anchor'li kutu). Kullanici adi PlayerPrefs'te
/// "FarmerSquad_Username" anahtariyla kalici saklanir. Ilk acilista bos ise
/// "username_{Random 10000-99999}" varsayilani uretilip hemen kaydedilir. CurrentUsername
/// static property, PlayerPrefs ile senkron okur/yazar (TASKS.md T49 context: "public static
/// string, initialization sadece ilk kez" - PlayerPrefs backing ile gercek kalicilik saglanir,
/// boylece sonraki oyun acilislarinda da hatirlanir).
///
/// Tek sahne projesi (Build Settings'te sadece SampleScene var). Diger ekranlarla (HUDCanvas,
/// WinScreenCanvas, LoseScreenController, MarketCanvas) AYNI desen: bu panel de kendi
/// Canvas'inda, sahne gecisi degil panelRoot.SetActive(false/true) ile acilip kapanir.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const string UsernamePrefsKey = "FarmerSquad_Username";

    private static string _currentUsername;

    /// <summary>Kalici kullanici adi. Ilk okumada PlayerPrefs'ten yuklenir, bossa varsayilan
    /// uretilip kaydedilir. Baska scriptler (orn. T45 floating name) bunu okuyabilir.</summary>
    public static string CurrentUsername
    {
        get
        {
            if (string.IsNullOrEmpty(_currentUsername))
            {
                _currentUsername = PlayerPrefs.GetString(UsernamePrefsKey, string.Empty);
                if (string.IsNullOrEmpty(_currentUsername))
                {
                    _currentUsername = "username_" + Random.Range(10000, 99999);
                    PlayerPrefs.SetString(UsernamePrefsKey, _currentUsername);
                    PlayerPrefs.Save();
                }
            }
            return _currentUsername;
        }
        private set
        {
            _currentUsername = value;
            PlayerPrefs.SetString(UsernamePrefsKey, _currentUsername);
            PlayerPrefs.Save();
        }
    }

    [Header("Baglantilar")]
    public GameObject panelRoot;
    public Button createLobbyButton;
    public Button joinLobbyButton;
    public Button profilButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Ag")]
    [SerializeField] private GameNetworkManager gameNetworkManager;

    [Header("Lobi (T41)")]
    [SerializeField] private LobbyUI lobbyUI;

    [Header("Lobi Listesi (T50)")]
    [SerializeField] private LobbyListUI lobbyListUI;


    [Header("Profil (T49)")]
    [SerializeField] private GameObject profilePanelRoot;
    [SerializeField] private TMPro.TMP_InputField usernameInputField;
    [SerializeField] private Button profilKaydetButton;
    [SerializeField] private Button profilGeriButton;

    [Header("Ayarlar (opsiyonel - SettingsUI T42'de kurulacak, henuz YOK)")]
    [SerializeField] private GameObject settingsPanelRoot;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        if (profilePanelRoot != null) profilePanelRoot.SetActive(false);

        if (createLobbyButton != null) createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        if (joinLobbyButton != null) joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
        if (profilButton != null) profilButton.onClick.AddListener(OnProfilClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        if (profilKaydetButton != null) profilKaydetButton.onClick.AddListener(OnProfilKaydetClicked);
        if (profilGeriButton != null) profilGeriButton.onClick.AddListener(OnProfilGeriClicked);

        // Uygulama acilisinda kullanici adini garanti et (yoksa varsayilan uret+kaydet).
        var ensureLoaded = CurrentUsername;
    }

    /// <summary>[Lobi Oluştur] (BlueBtn). GameNetworkManager.StartHost() cagirir (gercek Steam
    /// lobi kodu yok - bkz. sinif yorumu). Basarili baslatma sonrasi menu paneli kapanir ve
    /// LobbyUI acilir.</summary>
/// <summary>[Lobi Oluştur] (BlueBtn). LobbySessionManager.CreateLobby() ile GERÇEK (hardcoded
    /// olmayan) 4 haneli lobi kodu üretir (TASKS.md T50), sonra GameNetworkManager.StartHost()
    /// çağırır. Menü paneli kapanır, LobbyUI üretilen gerçek kodla açılır.</summary>
    private void OnCreateLobbyClicked()
    {
        if (gameNetworkManager == null)
        {
            Debug.LogError("[MainMenuController] gameNetworkManager atanmamis.");
            return;
        }

        LobbySessionManager.LobbyInfo info = LobbySessionManager.CreateLobby(CurrentUsername);

        gameNetworkManager.StartHost();
        if (panelRoot != null) panelRoot.SetActive(false);
        ShowLobbyUI(info.lobbyCode);
    }

    /// <summary>[Lobiye Katıl] (BlueBtn). GameNetworkManager.StartClient() cagirir - lobi kodu
    /// girisi YOK, NetworkManager'in Inspector'daki varsayilan UTP adresine baglanir (bkz. sinif
    /// yorumu). Basarili baslatma sonrasi menu paneli kapanir ve LobbyUI acilir.</summary>
/// <summary>[Lobiye Katıl] (BlueBtn). T50 ONCESI dogrudan StartClient() cagiriyordu; ARTIK
    /// LobbyListUI'yi acar (gercek ActiveLobbies verisiyle) - gercek katilim, secilen satira
    /// tiklaninca LobbyListUI.OnLobbyRowClicked() icinde StartClient() ile tetiklenir.</summary>
    private void OnJoinLobbyClicked()
    {
        if (lobbyListUI == null)
        {
            Debug.LogError("[MainMenuController] lobbyListUI atanmamis.");
            return;
        }

        lobbyListUI.Show();
    }

    /// <summary>lobbyUI atanmissa Show() cagirir; atanmamissa (henuz kurulmamis bir sahnede)
    /// sessizce stub log basar, hata firlatmaz.</summary>
/// <summary>lobbyUI atanmissa Show(code) cagirir (gercek lobi koduyla - T50); atanmamissa
    /// (henuz kurulmamis bir sahnede) sessizce stub log basar, hata firlatmaz.</summary>
    private void ShowLobbyUI(int lobbyCode)
    {
        if (lobbyUI != null)
        {
            lobbyUI.Show(lobbyCode);
        }
        else
        {
            Debug.Log("[MainMenuController] lobbyUI atanmamis - LobbyUI (T41) henuz baglanmamis.");
        }
    }

    /// <summary>[Profil] (GrayBtn). ProfilePanel modal'ini acar, input field'i mevcut
    /// CurrentUsername ile doldurur. Ana menu panel'i ARKADA acik kalir (ForceStartModalPanel
    /// ile ayni desen - modal ustte, alttaki panel kapanmaz).</summary>
    private void OnProfilClicked()
    {
        if (profilePanelRoot == null)
        {
            Debug.LogError("[MainMenuController] profilePanelRoot atanmamis.");
            return;
        }

        if (usernameInputField != null) usernameInputField.text = CurrentUsername;
        profilePanelRoot.SetActive(true);
    }

    /// <summary>[Kaydet] (GreenBtn). Input field'daki metni CurrentUsername'e yazar (bos veya
    /// sadece boslukdan olusuyorsa kaydetmez, eski adi korur) ve modal'i kapatir.</summary>
    private void OnProfilKaydetClicked()
    {
        if (usernameInputField != null)
        {
            string trimmed = usernameInputField.text.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                CurrentUsername = trimmed;
            }
        }

        if (profilePanelRoot != null) profilePanelRoot.SetActive(false);
    }

    /// <summary>[Geri] (GrayBtn). Degisiklikleri KAYDETMEDEN modal'i kapatir.</summary>
    private void OnProfilGeriClicked()
    {
        if (profilePanelRoot != null) profilePanelRoot.SetActive(false);
    }

    /// <summary>[Ayarlar] (GrayBtn). SettingsUI (T42) henuz kurulu degil - settingsPanelRoot
    /// atanmissa onu acar, atanmamissa kasitli stub log basar (icat etmeye gerek yok, T42
    /// kendi panelini kurup burayi baglayacak).</summary>
    private void OnSettingsClicked()
    {
        if (settingsPanelRoot != null)
        {
            settingsPanelRoot.SetActive(true);
        }
        else
        {
            Debug.Log("[MainMenuController] Ayarlar tiklandi - SettingsUI (T42) henuz kurulu degil.");
        }
    }

    /// <summary>[Çıkış] (RedBtn). Build'de uygulamayi kapatir; Unity Editor'de Play modunu
    /// durdurur (boylece Play modunda test edilebilir).</summary>
    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
