using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ayarlar ekranı (ARCHITECTURE.md "## Ana Menü & Lobi UI" + "## Ortak UI Kiti", TASKS.md T42).
/// Ses (Müzik + Efekt SFX olarak AYRI iki hacim slider'ı), Görüntü (çözünürlük + pencere modu
/// dropdown) ve Kontroller (statik tuş listesi, rebind YOK) sekmeleri. 3 buton ile basit sekme
/// geçişi.
///
/// Ses sistemi artık mevcut (kullanici talebi, "## Ses (Audio) Sistemi" - Assets/Scripts/Audio/
/// AudioManager.cs): Müzik slider'ı AudioManager.SetMusicVolume'a, Efekt slider'ı
/// AudioManager.SetSFXVolume'a bağlanır - ikisi TAMAMEN BAĞIMSIZ, PlayerPrefs'te ayrı ayrı
/// kalıcıdır. Görüntü sekmesi GERÇEKTEN uygulanır (Screen.SetResolution /
/// Screen.fullScreenMode) — bunlar Unity'nin kendi API'si, ek bir sistem gerektirmiyor.
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

    [Header("Ses (Muzik ve Efekt - AYRI kanallar, AudioManager.cs)")]
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Görsel")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown windowModeDropdown;

    private Resolution[] availableResolutions;

    private void Awake()
    {
        // BUG DUZELTMESI (kullanici raporu): baska panellerin (orn. LobbyCanvas) sahnede
        // yanlislikla aktif kaydedilmesi, bu panelin de kendi baslangic gorunurlugune
        // guvenilemeyecegini gosterdi. Awake artik kendi kapaliligini garanti eder.
        if (panelRoot != null) panelRoot.SetActive(false);

        if (sesTabButton != null) sesTabButton.onClick.AddListener(OnSesTabClicked);
        if (gorselTabButton != null) gorselTabButton.onClick.AddListener(OnGorselTabClicked);
        if (kontrollerTabButton != null) kontrollerTabButton.onClick.AddListener(OnKontrollerTabClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.value = AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 1f;
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.value = AudioManager.Instance != null ? AudioManager.Instance.SFXVolume : 1f;
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        SetupResolutionDropdown();
        SetupWindowModeDropdown();

        ShowTab(0);
    }

    /// <summary>MainMenuController.settingsPanelRoot yerine doğrudan bu metod da kullanılabilir
    /// (panelRoot atanmışsa SetActive(true) ile aynı işi yapar).</summary>
    // BUG DUZELTMESI (HUD'a Ayarlar butonu eklenirken fark edildi): Hide() eskiden
    // mainMenuPanelRoot'u KOSULSUZ tekrar aciyordu - bu sadece Ana Menu'den acilinca dogruydu.
    // Artik Show() aninda mainMenuPanelRoot'un o anki durumu hatirlaniyor, Hide() da AYNI duruma
    // donuyor - boylece oyun ici HUD'dan acilinca (mainMenuPanelRoot zaten kapaliyken) kapatinca
    // Ana Menu oyunun ustune binmiyor; Ana Menu'den acilinca (eskisi gibi) davranis degismiyor.
    private bool mainMenuWasActiveBeforeShow;

    public void Show()
    {
        if (mainMenuPanelRoot != null) mainMenuWasActiveBeforeShow = mainMenuPanelRoot.activeSelf;
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (mainMenuPanelRoot != null) mainMenuPanelRoot.SetActive(mainMenuWasActiveBeforeShow);
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

    private void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(value);
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
