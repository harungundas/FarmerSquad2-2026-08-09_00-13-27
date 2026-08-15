using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana Menu (ARCHITECTURE.md "## Ana Menü & Lobi UI" + "## Ortak UI Kiti").
/// [Lobi Oluştur]/[Lobiye Katıl]/[Ayarlar]/[Çıkış] butonları.
///
/// SAPMA (bkz. HANDOFF.md T40 notu, dogrulandi): TASKS.md T40 "LobbyManager (T07) ile baglanti"
/// diyor, ama T07 GERCEKTEN atlanmis - Assets/Scripts/Networking/LobbyManager.cs YOK, sadece
/// GameNetworkManager.cs var (StartHost()/StartClient(), gecici UTP transport). Bu yuzden
/// [Lobi Oluştur]=StartHost(), [Lobiye Katıl]=StartClient() olarak baglandi - gercek bir Steam
/// lobi kodu uretimi/girisi YOK (T41 veya gercek T07'ye ertelendi).
///
/// Tek sahne projesi (Build Settings'te sadece SampleScene var). Diger ekranlarla (HUDCanvas,
/// WinScreenCanvas, LoseScreenController, MarketCanvas) AYNI desen: bu panel de kendi
/// Canvas'inda, sahne gecisi degil panelRoot.SetActive(false/true) ile acilip kapanir.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Baglantilar")]
    public GameObject panelRoot;
    public Button createLobbyButton;
    public Button joinLobbyButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Ag")]
    [SerializeField] private GameNetworkManager gameNetworkManager;

    [Header("Ayarlar (opsiyonel - SettingsUI T42'de kurulacak, henuz YOK)")]
    [SerializeField] private GameObject settingsPanelRoot;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        if (createLobbyButton != null) createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        if (joinLobbyButton != null) joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
    }

    /// <summary>[Lobi Oluştur] (BlueBtn). GameNetworkManager.StartHost() cagirir (gercek Steam
    /// lobi kodu yok - bkz. sinif yorumu). Basarili baslatma sonrasi menu paneli kapanir.</summary>
    private void OnCreateLobbyClicked()
    {
        if (gameNetworkManager == null)
        {
            Debug.LogError("[MainMenuController] gameNetworkManager atanmamis.");
            return;
        }

        gameNetworkManager.StartHost();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>[Lobiye Katıl] (BlueBtn). GameNetworkManager.StartClient() cagirir - lobi kodu
    /// girisi YOK, NetworkManager'in Inspector'daki varsayilan UTP adresine baglanir (bkz. sinif
    /// yorumu).</summary>
    private void OnJoinLobbyClicked()
    {
        if (gameNetworkManager == null)
        {
            Debug.LogError("[MainMenuController] gameNetworkManager atanmamis.");
            return;
        }

        gameNetworkManager.StartClient();
        if (panelRoot != null) panelRoot.SetActive(false);
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
