using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lobi ekranı ana kontrolcüsü (ARCHITECTURE.md "## Ana Menü & Lobi UI" + "## Ortak UI Kiti",
/// TASKS.md T44 — Canvas Layout, Oturum 1: network YOK, sadece local/statik UI iskeleti).
///
/// SIFIRDAN YENİDEN KURULDU (bkz. HANDOFF.md "T44 REDDEDİLDİ"): önceki deneme 5 karakter
/// portre butonunu T43'ün CharacterSelectionManager sistemine devretmişti, ama o sistemde
/// hiçbir görsel buton yoktu (sadece NetworkObject). Kullanıcı bunu kabul etmedi.
/// Bu sürümde 5 portre butonu doğrudan bu Canvas'ın İÇİNDE, ekranın ALT kısmında,
/// bu script tarafından yönetilir. Karakter seçiminin network'e bağlanması T46'da yapılacak.
///
/// Bu oturumda (T44) sadece UI iskeleti kurulur: alanlar public, buton tıklamaları Debug.Log
/// ile doğrulanır. Gerçek Ready/Countdown/Start/Leave mantığı T46-T48'de eklenecek.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("Panel Kok (MainMenuController.Show()/Hide() icin)")]
    public GameObject panelRoot;

    [Header("Ust Bilgi")]
    public TextMeshProUGUI lobbyCodeText;
    public TextMeshProUGUI playerCountText;

    [Header("Ortada - 5 Oyuncu Slotu (placeholder isim, 3D karakter + floating name T45'te)")]
    public TextMeshProUGUI[] slotNameTexts = new TextMeshProUGUI[5];

    [Header("Alt - 5 Karakter Portre Butonu (ZORUNLU, her zaman gorunur)")]
    public Button[] characterPortraitButtons = new Button[5];
    public Image[] characterPortraitBackgrounds = new Image[5];
    public string[] characterNames = new string[] { "Yetiskin", "Sisman", "Cocuk", "Kadin", "Yasli" };

    [Header("Sag Ust - Ready (GreenBtn, mantik T47'de)")]
    public Button readyButton;
    public TextMeshProUGUI readyButtonText;

    [Header("Sag Alt - Start Game (BlueBtn, host-only, mantik T48'de)")]
    public Button startGameButton;

    [Header("Sol Alt - Leave (RedBtn)")]
    public Button leaveButton;

    [Header("Ortada Sag - Countdown (mantik T47'de)")]
    public TextMeshProUGUI countdownText;

    private int lobbyCode;
    private int selectedCharacterIndex = 0;

    private static readonly Color SelectedColor = new Color(1f, 0.92f, 0.55f, 1f);
    private static readonly Color UnselectedColor = Color.white;
    private static readonly Color LockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("T46 - Local Karakter Degisimi (LobbyManager.ReplaceCharacterInSlot cagirir)")]
    public LobbyManager lobbyManager;
    public int localSlotIndex = 0; // Oyuncu 1 = sen (hardcoded, T49'da client ID'ye baglanacak)

    private bool isSelectionLocked = false;
    private bool isReady = false;


    private void Awake()
    {
        WireButtons();
        WireCharacterPortraits();
    }

    /// <summary>MainMenuController.OnCreateLobbyClicked/OnJoinLobbyClicked sonrasi cagirir.
    /// Lobi kodunu (rakam-only, T50'de gercek host tarafinda ayni yontemle uretilecek) ve
    /// baslangic degerlerini ayarlar.</summary>
    public void Show()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        GenerateLobbyCode();
        UpdatePlaceholderSlots();
        SelectCharacter(0);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>4 haneli rakam-only lobi kodu (harf YOK). PLAN.md/HANDOFF gereği
    /// Random.Range(1000, 9999).</summary>
    private void GenerateLobbyCode()
    {
        lobbyCode = Random.Range(1000, 9999);
        if (lobbyCodeText != null) lobbyCodeText.text = lobbyCode.ToString();
        if (playerCountText != null) playerCountText.text = "1/5";
        if (countdownText != null) countdownText.text = "-";
    }

    /// <summary>Oturum 1'de gercek oyuncu verisi yok - sabit "Oyuncu N" placeholder metni
    /// gosterir. T45'te 3D karakter + floating name bunun yerini alacak.</summary>
    private void UpdatePlaceholderSlots()
    {
        for (int i = 0; i < slotNameTexts.Length; i++)
        {
            if (slotNameTexts[i] != null) slotNameTexts[i].text = "Oyuncu " + (i + 1);
        }
    }

    private void WireButtons()
    {
        if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
        if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGameClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
    }

        /// <summary>5 portre butonuna tiklama dinleyicisi baglar. Her buton kendi indexini
    /// (0-4, characterNames sirasiyla) kapatir (closure).</summary>
    private void WireCharacterPortraits()
    {
        for (int i = 0; i < characterPortraitButtons.Length; i++)
        {
            int capturedIndex = i;
            if (characterPortraitButtons[i] != null)
            {
                characterPortraitButtons[i].onClick.AddListener(() => OnCharacterSelected(capturedIndex));
            }
        }
    }

    /// <summary>T46: portre tiklaninca cagirilir. Secili karakteri gunceller, gorseli
    /// vurgular ve local slot'taki (localSlotIndex) 3D karakteri yeni prefab ile degistirir.
    /// Kilitliyken (Ready sonrasi) hicbir sey yapmaz - buton zaten interactable=false olur,
    /// bu kontrol savunma amacli ekstra guvenlik.</summary>
    public void OnCharacterSelected(int charIndex)
    {
        if (isSelectionLocked) return;

        SelectCharacter(charIndex);

        string charName = (charIndex >= 0 && charIndex < characterNames.Length) ? characterNames[charIndex] : charIndex.ToString();
        Debug.Log("[LobbyUI] Karakter secildi: " + charName + " (index " + charIndex + ")");

        if (lobbyManager != null && lobbyManager.characterTemplates != null &&
            charIndex >= 0 && charIndex < lobbyManager.characterTemplates.Length)
        {
            lobbyManager.ReplaceCharacterInSlot(localSlotIndex, lobbyManager.characterTemplates[charIndex]);
        }
        else
        {
            Debug.LogWarning("[LobbyUI] lobbyManager veya characterTemplates atanmamis, 3D model degistirilemedi.");
        }
    }




    /// <summary>Secili portrenin gorsel vurgusunu gunceller (active state). T46'da Ready
    /// sonrasi kilitleme mantigi bu metodun uzerine eklenecek.</summary>
    private void SelectCharacter(int index)
    {
        selectedCharacterIndex = index;
        for (int i = 0; i < characterPortraitBackgrounds.Length; i++)
        {
            if (characterPortraitBackgrounds[i] == null) continue;
            characterPortraitBackgrounds[i].color = (i == index) ? SelectedColor : UnselectedColor;
        }
    }

    /// <summary>T46: Ready basilinca (true) portre butonlarini gri + tiklanamaz yapar,
    /// "Hazir Degil" basilinca (false) tekrar acar. Secili karakterin vurgusu locked=false
    /// olunca SelectCharacter ile geri yuklenir.</summary>
    public void SetCharacterSelectionLocked(bool locked)
    {
        isSelectionLocked = locked;

        for (int i = 0; i < characterPortraitButtons.Length; i++)
        {
            if (characterPortraitButtons[i] != null) characterPortraitButtons[i].interactable = !locked;
        }

        if (locked)
        {
            for (int i = 0; i < characterPortraitBackgrounds.Length; i++)
            {
                if (characterPortraitBackgrounds[i] != null) characterPortraitBackgrounds[i].color = LockedColor;
            }
        }
        else
        {
            SelectCharacter(selectedCharacterIndex);
        }
    }


    /// <summary>T46 kapsaminda sadece local-only kilit toggle'i. Gercek "herkes ready mi"
    /// kontrolu, countdown baslatma ve renk/metin polish'i T47'de eklenecek - burada sadece
    /// SetCharacterSelectionLocked baglantisini kurup buton metnini degistiriyoruz.</summary>
    private void OnReadyClicked()
    {
        isReady = !isReady;
        SetCharacterSelectionLocked(isReady);

        if (readyButtonText != null) readyButtonText.text = isReady ? "Hazir Degil" : "Hazir";

        Debug.Log("[LobbyUI] Ready durumu: " + (isReady ? "HAZIR (secim kilitli)" : "HAZIR DEGIL (secim acik)") + " (T47'de countdown eklenecek).");
    }

    private void OnStartGameClicked()
    {
        Debug.Log("[LobbyUI] Start Game tiklandi (T48'de force-start modal mantigi eklenecek).");
    }

    private void OnLeaveClicked()
    {
        Debug.Log("[LobbyUI] Leave tiklandi (Oturum 1.5'te lobi listesine donus mantigi eklenecek).");
    }
}
