using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ayarlar ekranı (ARCHITECTURE.md "## Ana Menü & Lobi UI" + "## Ortak UI Kiti", TASKS.md T42).
/// Ses (basit master volume slider), Görüntü (çözünürlük + pencere modu dropdown) ve Kontroller
/// (statik tuş listesi, rebind YOK) sekmeleri. 3 buton ile basit sekme geçişi.
///
/// Ses sistemi GDD'de yok (Kapsam Dışı) — slider yalnızca global AudioListener.volume'a bağlanır,
/// bağlanacağı ayrı bir ses/müzik sistemi henüz mevcut değil. Görüntü sekmesi GERÇEKTEN uygulanır
/// (Screen.SetResolution / Screen.fullScreenMode) — bunlar Unity'nin kendi API'si, ek bir sistem
/// gerektirmiyor.
///
/// MainMenuCanvas'teki [Ayarlar] (GrayBtn, T40) butonu bu script'in bulunduğu panelRoot'u
/// (SettingsCanvas/Panel) doğrudan SetActive(true) ile açar — Ana Menü paneli KAPANMAZ, Ayarlar
/// onun üstüne bindirilmiş bir overlay olarak durur (bkz. MainMenuController.OnSettingsClicked).
/// [Kapat] butonu (GrayBtn) sadece bu paneli kapatır.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panelRoot;

    [Header("Geri Donus (basit toggle)")]
    [SerializeField] private GameObject mainMenuPanelRoot;

    [Header("Sekme Butonları")]
    public Button sesTabButton;
    public Button gorselTabButton;
    public Button kontrollerTabButton;
    public Button closeButton;

    [Header("Sekme Panelleri")]
    public GameObject sesPanel;
    public GameObject gorselPanel;
    public GameObject kontrollerPanel;

    [Header("Ses")]
    public Slider masterVolumeSlider;

    [Header("Görsel")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown windowModeDropdown;

    private Resolution[] availableResolutions;

    private void Awake()
    {
        if (sesTabButton != null) sesTabButton.onClick.AddListener(OnSesTabClicked);
        if (gorselTabButton != null) gorselTabButton.onClick.AddListener(OnGorselTabClicked);
        if (kontrollerTabButton != null) kontrollerTabButton.onClick.AddListener(OnKontrollerTabClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.value = AudioListener.volume;
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        SetupResolutionDropdown();
        SetupWindowModeDropdown();

        ShowTab(0);
    }

    /// <summary>MainMenuController.settingsPanelRoot yerine doğrudan bu metod da kullanılabilir
    /// (panelRoot atanmışsa SetActive(true) ile aynı işi yapar).</summary>
    public void Show()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (mainMenuPanelRoot != null) mainMenuPanelRoot.SetActive(true);
    }

    private void OnSesTabClicked()
    {
        ShowTab(0);
    }

    private void OnGorselTabClicked()
    {
        ShowTab(1);
    }

    private void OnKontrollerTabClicked()
    {
        ShowTab(2);
    }

    /// <summary>index: 0=Ses, 1=Görsel, 2=Kontroller.</summary>
    private void ShowTab(int index)
    {
        if (sesPanel != null) sesPanel.SetActive(index == 0);
        if (gorselPanel != null) gorselPanel.SetActive(index == 1);
        if (kontrollerPanel != null) kontrollerPanel.SetActive(index == 2);
    }

    private void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = value;
    }

    private void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        availableResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentIndex = 0;
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution r = availableResolutions[i];
            options.Add(r.width + " x " + r.height);
            if (r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height)
            {
                currentIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void OnResolutionChanged(int index)
    {
        if (availableResolutions == null || index < 0 || index >= availableResolutions.Length) return;
        Resolution r = availableResolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
    }

    private void SetupWindowModeDropdown()
    {
        if (windowModeDropdown == null) return;

        windowModeDropdown.ClearOptions();
        List<string> options = new List<string>();
        options.Add("Pencereli");
        options.Add("Tam Ekran");
        options.Add("Sınırsız Pencere");
        windowModeDropdown.AddOptions(options);
        windowModeDropdown.value = FullScreenModeToIndex(Screen.fullScreenMode);
        windowModeDropdown.RefreshShownValue();
        windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);
    }

    private int FullScreenModeToIndex(FullScreenMode mode)
    {
        if (mode == FullScreenMode.Windowed) return 0;
        if (mode == FullScreenMode.FullScreenWindow) return 2;
        return 1; // ExclusiveFullScreen / MaximizedWindow
    }

    private void OnWindowModeChanged(int index)
    {
        FullScreenMode mode = FullScreenMode.Windowed;
        if (index == 1) mode = FullScreenMode.ExclusiveFullScreen;
        if (index == 2) mode = FullScreenMode.FullScreenWindow;
        Screen.fullScreenMode = mode;
    }
}
