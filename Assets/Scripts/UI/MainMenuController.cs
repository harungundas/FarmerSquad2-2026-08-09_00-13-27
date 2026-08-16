using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana Menu (ARCHITECTURE.md "## Ana Menü & Lobi UI" + "## Ortak UI Kiti").
/// [Lobi Oluştur]/[Lobiye Katıl]/[Ayarlar]/[Çıkış] butonları + sağ-altta sürekli görünür
/// Profil kutusu (Profil: etiketi + kullanıcı adı + Kaydet).
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
/// T49 REVIZE (kullanici karari): Ayri bir [Profil] butonu ve modal KALDIRILDI. 5. buton
/// (Cikis) dikey stack'te ekran disina taşıyordu; Profil butonu cikarilinca stack 4 butona
/// dustu ve Cikis tekrar ekran icine girdi. Profil artik Panel'in SAG-ALT kosesinde HER ZAMAN
/// gorunur kucuk bir kutu: "Profil:" etiketi + kullanici adi input + Kaydet butonu - ayri
/// acilip kapanan bir modal degil. Kullanici adi PlayerPrefs'te "FarmerSquad_Username"
/// anahtariyla kalici saklanir. Ilk acilista bos ise "username_{Random 10000-99999}"
/// varsayilani uretilip hemen kaydedilir. CurrentUsername static property, PlayerPrefs ile
/// senkron okur/yazar.
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
    public Button settingsButton;
    public Button quitButton;

    [Header("Ag")]
    [SerializeField] private GameNetworkManager gameNetworkManager;

    [Header("Lobi (T41)")]
    [SerializeField] private LobbyUI lobbyUI;

    [Header("Lobi Listesi (T50)")]
    [SerializeField] private LobbyListUI lobbyListUI;

    [Header("Profil (T49, REVIZE: artik sag-alt kosede SUREKLI gorunur kucuk kutu, modal DEGIL)")]
    [SerializeField] private GameObject profilePanelRoot;
    [SerializeField] private TMPro.TMP_InputField usernameInputField;
    [SerializeField] private Button profilKaydetButton;

    [Header("Ayarlar (opsiyonel - SettingsUI T42'de kurulacak, henuz YOK)")]
    [SerializeField] private GameObject settingsPanelRoot;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        if (createLobbyButton != null) createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        if (joinLobbyButton != null) joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        if (profilKaydetButton != null) profilKaydetButton.onClick.AddListener(OnProfilKaydetClicked);

        // Profil artik ayri bir buton/modal degil - Panel'in sag-alt kosesinde SUREKLI
        // gorunur kucuk bir alan (Profil: etiketi + kullanici adi input + Kaydet). Bu yuzden
        // ProfilePanel her zaman acik kalir, sadece username baslangic degeri yuklenir.
        if (profilePanelRoot != null) profilePanelRoot.SetActive(true);
        if (usernameInputField != null) usernameInputField.text = CurrentUsername;

        // Uygulama acilisinda kullanici adini garanti et (yoksa varsayilan uret+kaydet).
        var ensureLoaded = CurrentUsername;
    }

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

    /// <summary>[Kaydet] (GreenBtn, artik sag-alt kosede SUREKLI gorunur panelde). Input
    /// field'daki metni CurrentUsername'e yazar (bos veya sadece bosluktan olusuyorsa
    /// kaydetmez, eski adi korur). Modal olmadigi icin panel KAPANMAZ - sadece kaydeder.</summary>
    private void OnProfilKaydetClicked()
    {
        if (usernameInputField != null)
        {
            string trimmed = usernameInputField.text.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                CurrentUsername = trimmed;
                usernameInputField.text = CurrentUsername;
            }
        }

        Debug.Log("[MainMenuController] Kullanici adi kaydedildi: " + CurrentUsername);
    }

    /// <summary>[Ayarlar] (GrayBtn). SettingsUI (T42) henuz kurulu degil - settingsPanelRoot
    /// atanmissa onu acar, atanmamissa kasitli stub log basar (icat etmeye gerek yok, T42
    /// kendi panelini kurup burayi baglayacak).</summary>
    private void OnSettingsClicked()
    {
        if (settingsPanelRoot != null)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
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
