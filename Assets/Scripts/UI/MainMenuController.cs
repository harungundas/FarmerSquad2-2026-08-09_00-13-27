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

        // BUG DUZELTMESI (kullanici raporu): LobbyCanvas/Panel sahnede yanlislikla aktif
        // kaydedilmisti, bu da oyun Ana Menu'deyken Lobi ekraninin gorunmesine ve Ayarlar dahil
        // diger panellerin ustunu kapatmasina neden oluyordu. Sahne dosyasinin kaydedilmis
        // aktiflik durumuna guvenmek yerine, Ana Menu acilirken diger tum ust-seviye ekranlarin
        // KAPALI oldugu burada da ayrica garanti edilir (LobbyUI/SettingsUI kendi Awake'lerinde
        // de kendilerini kapatiyor - bu ekstra bir savunma katmani, T40/T41/T42 arasi sira
        // garantisi olmadigi icin).
        if (lobbyUI != null) lobbyUI.Hide();
        if (settingsPanelRoot != null) settingsPanelRoot.SetActive(false);

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
    /// <summary>KULLANICI KARARI (Steamworks YOK, NetworkManager gercek LAN host/client
    /// baglantisini yonetsin): sahte LobbySessionManager.CreateLobby() KALDIRILDI - artik
    /// dogrudan gameNetworkManager.StartHost() cagriliyor (tum ag arayuzlerinde dinlemeye
    /// baslar), LobbyUI.Show() gercek NetworkManager.IsHost durumuna gore host'un LAN IP'sini
    /// gosterir (arkadasina soyleyecegi adres).</summary>
    private void OnCreateLobbyClicked()
    {
        if (gameNetworkManager == null)
        {
            Debug.LogError("[MainMenuController] gameNetworkManager atanmamis.");
            return;
        }

        bool started = gameNetworkManager.StartHost();
        if (!started)
        {
            Debug.LogError("[MainMenuController] Host baslatilamadi.");
            return;
        }

        if (panelRoot != null) panelRoot.SetActive(false);
        ShowLobbyUI(0);
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
        // BUG DUZELTMESI (kullanici bildirdi: Ayarlar'i Ana Menu'den acip Geri'ye basinca
        // doğrudan oyuna atiyordu): bu metod T42 oncesi yazilmis bir stub'du, settingsPanelRoot'u
        // DOGRUDAN SetActive(true) yapiyordu - SettingsUI.Show() UZERINDEN GECMIYORDU. SettingsUI
        // artik Hide()'da "Show() aninda Ana Menu acik miydi" bilgisini kullanarak geri donuyor
        // (bkz. SettingsUI.cs) - bu metod Show()'u ATLADIGI icin o bilgi hic guncellenmiyordu ve
        // Hide() Ana Menu'yu YANLIŞLIKLA kapali sanıp tekrar acmiyordu. Artik SettingsUI.Show()
        // uzerinden aciliyor, boylece dogru state takip ediliyor.
        if (settingsPanelRoot != null)
        {
            // NOT: SettingsUI komponenti settingsPanelRoot'un KENDISINDE degil, PARENT'inda
            // (SettingsCanvas) - GetComponent yerine GetComponentInParent kullanilmali.
            var settingsUI = settingsPanelRoot.GetComponentInParent<SettingsUI>();
            if (settingsUI != null)
            {
                settingsUI.Show();
            }
            else
            {
                // SettingsUI komponenti bulunamadi (beklenmedik durum) - eski stub davranisina dus.
                if (panelRoot != null) panelRoot.SetActive(false);
                settingsPanelRoot.SetActive(true);
            }
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
