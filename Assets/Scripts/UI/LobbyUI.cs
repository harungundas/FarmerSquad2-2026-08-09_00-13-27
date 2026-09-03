using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lobi ekrani ana kontrolcusu (ARCHITECTURE.md "## Ana Menu & Lobi UI").
///
/// KULLANICI BUG RAPORU DUZELTMESI (bu oturumda BUYUK REVIZYON): Eskiden ready/countdown/
/// oyuncu-listesi TAMAMEN LOKALDI (her client kendi ekraninda kendi hesabini yapiyordu,
/// birbirinden habersizdi) - iki oyuncu lobide birbirini GORMUYORDU, client "Hazir" deyince
/// kendini SOLO saniyor host'u beklemeden erken oyuna giriyordu. Artik TUM oyuncu listesi,
/// ready durumlari, countdown ve "oyun gercekten basladi" bilgisi LobbyNetworkState
/// (host-yetkili NetworkBehaviour, bkz. o dosya) uzerinden okunuyor - bu script sadece
/// GORUNTULUYOR ve ISTEK GONDERIYOR, kendi kafasina gore karar VERMIYOR.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("Panel Kok (MainMenuController.Show()/Hide() icin)")]
    public GameObject panelRoot;

    [Header("Ust Bilgi")]
    public TextMeshProUGUI lobbyCodeText;
    public TextMeshProUGUI playerCountText;

    [Header("Ortada - 5 Oyuncu Slotu (placeholder isim)")]
    public TextMeshProUGUI[] slotNameTexts = new TextMeshProUGUI[5];

    [Header("Ortada - 5 Oyuncu Slotunun Ready Durum Metni")]
    public TextMeshProUGUI[] slotReadyStatusTexts = new TextMeshProUGUI[5];

    [Header("Ortada - 5 Oyuncu Slotunun Image bileseni")]
    public Image[] slotPortraitImages = new Image[5];

    [Header("Alt - 5 Karakter Portre Butonu (ZORUNLU, her zaman gorunur)")]
    public Button[] characterPortraitButtons = new Button[5];
    public Image[] characterPortraitBackgrounds = new Image[5];
    public string[] characterNames = new string[] { "Yetiskin", "Sisman", "Cocuk", "Kadin", "Yasli" };

    [Header("Statik Karakter Fotograflari (Assets/character_slots, characterNames ile AYNI SIRA)")]
    public Sprite[] characterPortraitSprites = new Sprite[5];

    [Header("Sag Ust - Ready (GreenBtn)")]
    public Button readyButton;
    public TextMeshProUGUI readyButtonText;

    [Header("Sag Alt - Start Game (host-only). Countdown calisirken Baslat->Iptal'e donusur.")]
    public Button startGameButton;
    public Image startGameButtonImage;
    public TextMeshProUGUI startGameButtonText;
    public Sprite startGameDefaultSprite;
    public Sprite startGameCancelSprite;

    [Header("T60: Zorluk Ayarlari (sadece host degistirebilir, 3 preset)")]
    public Button[] difficultyButtons = new Button[3];
    public TextMeshProUGUI[] difficultyButtonTexts = new TextMeshProUGUI[3];
    public float[] difficultyMultipliers = new float[] { 0.8f, 1.0f, 1.3f };
    public GameObject difficultyPanelRoot;

    [Header("Sol Alt - Leave (RedBtn)")]
    public Button leaveButton;

    [Header("Ortada Sag - Countdown")]
    public TextMeshProUGUI countdownText;

    [Header("Force Start Modal (Start Game basinca hazir olmayan varsa acilir)")]
    public GameObject forceStartModalPanel;
    public TextMeshProUGUI forceStartModalText;
    public Button forceStartConfirmButton;
    public Button forceStartCancelButton;

    private int lobbyCode;
    private int selectedCharacterIndex = 0;
    private bool isLocalPlayerHost = false;
    private bool isSelectionLocked = false;
    private bool isReady = false;
    private bool wasGameStartedLastFrame = false;

    private static readonly Color SelectedColor = new Color(1f, 0.92f, 0.55f, 1f);
    private static readonly Color UnselectedColor = Color.white;
    private static readonly Color LockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private static readonly Color NotReadyStatusColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    private static readonly Color ReadyStatusColor = new Color(0.15f, 0.75f, 0.2f, 1f);
    private const string NotReadyStatusLabel = "\u2715 Haz\u0131r De\u011fil";
    private const string ReadyStatusLabel = "\u2713 Haz\u0131r";

    [Header("Local Karakter Degisimi (localSlotIndex'teki Image'e statik foto uygulanir - artik SADECE fallback icin, gercek slot RefreshFromNetworkState'ten geliyor)")]
    public int localSlotIndex = 0;

    [Header("Lobi Listesi Baglantisi (Leave sonrasi donus icin)")]
    public LobbyListUI lobbyListUI;

    [Header("Karakter secimini GERCEK govdeye (CharacterSelectionManager, network) baglar")]
    public CharacterSelectionManager selectionManager;

    [Header("Leave dogrudan Ana Menu'ye donuyor")]
    public GameObject mainMenuPanelRoot;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        WireButtons();
        WireCharacterPortraits();
        WireDifficultyButtons();
    }

    /// <summary>MainMenuController.OnCreateLobbyClicked/LobbyListUI.OnLobbyRowClicked sonrasi cagirir.</summary>
    public void Show(int code)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        lobbyCode = code;

        isLocalPlayerHost = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost;

        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = isLocalPlayerHost
                ? "IP: " + GameNetworkManager.GetLocalIPAddress() + " (arkada\u015f\u0131na s\u00f6yle)"
                : "Ba\u011fland\u0131n (client)";
        }

        if (startGameButton != null) startGameButton.gameObject.SetActive(isLocalPlayerHost);

        // T60: Zorluk preset paneli sadece host'a GORUNUR (client'ta hic gorunmez, sadece
        // gizleme degil - RPC de zaten host-olmayan bir istek gonderemez cunku buton yok).
        if (difficultyPanelRoot != null) difficultyPanelRoot.SetActive(isLocalPlayerHost);
        RefreshDifficultyButtonHighlight();

        isReady = false;
        isSelectionLocked = false;
        wasGameStartedLastFrame = false;
        if (readyButtonText != null) readyButtonText.text = "Hazir";
        if (countdownText != null) countdownText.text = "-";
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(false);

        SelectCharacter(0);

        // KULLANICI BUG RAPORU DUZELTMESI: sunucuya GERCEK kullanici adimizi/secimimizi
        // bildiriyoruz ki diger client'lar bizi "Oyuncu {id}" yerine gercek adimizla gorsun.
        if (LobbyNetworkState.Instance != null)
        {
            LobbyNetworkState.Instance.RequestSetUsernameServerRpc(MainMenuController.CurrentUsername);
            LobbyNetworkState.Instance.RequestSetCharacterServerRpc(0);
            LobbyNetworkState.Instance.RequestSetReadyServerRpc(false);
        }
        else
        {
            Debug.LogWarning("[LobbyUI] LobbyNetworkState.Instance null - lobi gercek zamanli senkron calismayacak (sahnede LobbyNetworkState objesi var mi kontrol et).");
        }
    }

    public void Show()
    {
        Show(Random.Range(1000, 9999));
    }

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;
        RefreshFromNetworkState();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>KULLANICI BUG RAPORU DUZELTMESI: her frame LobbyNetworkState'ten GERCEK
    /// veriyi okuyup ekrani gunceller - oyuncu listesi, ready durumlari, countdown ve oyunun
    /// GERCEKTEN baslayip baslamadigi artik burada, TEK bir yerden, tum client'lar icin AYNI
    /// sekilde belirleniyor.</summary>
    private void RefreshFromNetworkState()
    {
        var state = LobbyNetworkState.Instance;
        if (state == null)
        {
            UpdatePlaceholderSlotsFallback();
            return;
        }

        int count = state.PlayerCount();
        if (playerCountText != null) playerCountText.text = count + "/" + LobbySessionManager.MaxPlayersPerLobby;

        for (int i = 0; i < slotNameTexts.Length; i++)
        {
            if (i < count && i < state.Players.Count)
            {
                var p = state.Players[i];
                if (slotNameTexts[i] != null) slotNameTexts[i].text = p.username.ToString();
                UpdateReadyStatusText(i, p.isReady);
                ApplySlotPortrait(i, p.characterIndex);
            }
            else
            {
                if (slotNameTexts[i] != null) slotNameTexts[i].text = "";
                UpdateReadyStatusText(i, false);
            }
        }

        int remaining = state.CountdownSecondsRemaining.Value;
        if (countdownText != null) countdownText.text = remaining >= 0 ? remaining.ToString() : "-";
        if (isLocalPlayerHost) SetStartButtonVisual(remaining >= 0);

        // T60: host'un secmis oldugu zorluk carpani her frame yansitilir - QuotaManager
        // NetworkVariable oldugu icin bu deger client'larda da dogru senkron gelir (butonlar
        // gorunmese de HUD/QuotaManager degeri tum client'larda aynidir).
        RefreshDifficultyButtonHighlight();

        // KULLANICI BUG RAPORU DUZELTMESI: oyunun GERCEKTEN basladigi an TUM client'lara
        // senkron bir NetworkVariable ile bildirilir - hicbir client kendi kafasina gore
        // "basladi" sanip erken girmez, hepsi AYNI anda bu deger true olunca gecis yapar.
        if (state.GameStarted.Value && !wasGameStartedLastFrame)
        {
            wasGameStartedLastFrame = true;
            if (HUDController.Instance != null) HUDController.Instance.Show();
            Hide();
        }
    }

    /// <summary>LobbyNetworkState hic bulunamazsa (beklenmedik durum) sadece local oyuncuyu gosteren guvenlik agi.</summary>
    private void UpdatePlaceholderSlotsFallback()
    {
        for (int i = 0; i < slotNameTexts.Length; i++)
        {
            if (slotNameTexts[i] != null) slotNameTexts[i].text = i == 0 ? MainMenuController.CurrentUsername : "";
            UpdateReadyStatusText(i, i == 0 && isReady);
        }
    }

    private void UpdateReadyStatusText(int slotIndex, bool ready)
    {
        if (slotReadyStatusTexts == null || slotIndex < 0 || slotIndex >= slotReadyStatusTexts.Length || slotReadyStatusTexts[slotIndex] == null)
        {
            return;
        }

        bool slotHasPlayer = slotNameTexts != null && slotIndex < slotNameTexts.Length && slotNameTexts[slotIndex] != null && !string.IsNullOrEmpty(slotNameTexts[slotIndex].text);

        if (!slotHasPlayer)
        {
            slotReadyStatusTexts[slotIndex].text = "";
            slotReadyStatusTexts[slotIndex].gameObject.SetActive(false);
            return;
        }

        slotReadyStatusTexts[slotIndex].gameObject.SetActive(true);
        slotReadyStatusTexts[slotIndex].text = ready ? ReadyStatusLabel : NotReadyStatusLabel;
        slotReadyStatusTexts[slotIndex].color = ready ? ReadyStatusColor : NotReadyStatusColor;
    }

    private void WireButtons()
    {
        if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
        if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGameClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
        if (forceStartConfirmButton != null) forceStartConfirmButton.onClick.AddListener(OnForceStartConfirmed);
        if (forceStartCancelButton != null) forceStartCancelButton.onClick.AddListener(OnForceStartCancelled);
    }

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

    public void OnCharacterSelected(int charIndex)
    {
        if (isSelectionLocked) return;

        SelectCharacter(charIndex);

        string charName = (charIndex >= 0 && charIndex < characterNames.Length) ? characterNames[charIndex] : charIndex.ToString();
        Debug.Log("[LobbyUI] Karakter secildi: " + charName + " (index " + charIndex + ")");

        ApplySlotPortrait(localSlotIndex, charIndex);

        if (selectionManager != null)
        {
            selectionManager.RequestSelectCharacterServerRpc(charIndex);
        }
        else
        {
            Debug.LogWarning("[LobbyUI] selectionManager atanmamis - karakter secimi sunucuya iletilemedi.");
        }

        // KULLANICI BUG RAPORU DUZELTMESI: lobi ekranindaki portre de (diger oyunculara
        // gorunen) senkronize edilsin diye LobbyNetworkState'e de bildiriliyor.
        if (LobbyNetworkState.Instance != null)
        {
            LobbyNetworkState.Instance.RequestSetCharacterServerRpc(charIndex);
        }
    }

    private void ApplySlotPortrait(int slotIndex, int charIndex)
    {
        if (slotPortraitImages == null || slotIndex < 0 || slotIndex >= slotPortraitImages.Length || slotPortraitImages[slotIndex] == null)
        {
            return;
        }
        if (characterPortraitSprites == null || charIndex < 0 || charIndex >= characterPortraitSprites.Length || characterPortraitSprites[charIndex] == null)
        {
            return;
        }

        slotPortraitImages[slotIndex].sprite = characterPortraitSprites[charIndex];
        slotPortraitImages[slotIndex].color = Color.white;
        slotPortraitImages[slotIndex].preserveAspect = true;
    }

    private void SelectCharacter(int index)
    {
        selectedCharacterIndex = index;
        for (int i = 0; i < characterPortraitBackgrounds.Length; i++)
        {
            if (characterPortraitBackgrounds[i] == null) continue;
            characterPortraitBackgrounds[i].color = (i == index) ? SelectedColor : UnselectedColor;
        }
    }

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

    /// <summary>KULLANICI BUG RAPORU DUZELTMESI: artik ready durumu SADECE sunucuya
    /// bildirilir (RequestSetReadyServerRpc) - kendi kendine countdown BASLATMAZ. Countdown
    /// SADECE host'un Start Game butonuyla baslar (kullanicinin bekledigi davranis: client
    /// hazir dese bile host baslatmadan oyuna giremez).</summary>
    private void OnReadyClicked()
    {
        isReady = !isReady;
        SetCharacterSelectionLocked(isReady);
        if (readyButtonText != null) readyButtonText.text = isReady ? "Hazir Degil" : "Hazir";

        if (LobbyNetworkState.Instance != null)
        {
            LobbyNetworkState.Instance.RequestSetReadyServerRpc(isReady);
        }

        Debug.Log("[LobbyUI] Ready durumu (sunucuya bildirildi): " + isReady);
    }

    private void SetStartButtonVisual(bool cancelMode)
    {
        if (startGameButtonText != null) startGameButtonText.text = cancelMode ? "Iptal" : "Baslat";

        if (startGameButtonImage != null)
        {
            if (cancelMode && startGameCancelSprite != null) startGameButtonImage.sprite = startGameCancelSprite;
            else if (!cancelMode && startGameDefaultSprite != null) startGameButtonImage.sprite = startGameDefaultSprite;
        }
    }

    /// <summary>Host-only. LobbyNetworkState (gercek sunucu-yetkili durum) uzerinden
    /// countdown baslatir/iptal eder - kendi lokal sayacini TUTMAZ.</summary>
    private void OnStartGameClicked()
    {
        if (!isLocalPlayerHost)
        {
            Debug.LogWarning("[LobbyUI] Start Game sadece lobiyi olusturan host tarafindan kullanilabilir.");
            return;
        }

        var state = LobbyNetworkState.Instance;
        if (state == null)
        {
            Debug.LogWarning("[LobbyUI] LobbyNetworkState.Instance null, oyun baslatilamiyor.");
            return;
        }

        bool countdownRunning = state.CountdownSecondsRemaining.Value >= 0;
        if (countdownRunning)
        {
            state.CancelCountdownServer();
            Debug.Log("[LobbyUI] Start Game iptal edildi (host), countdown durduruldu.");
            return;
        }

        // Host kendini otomatik ready yapsin (kullanici raporu: "başlata basınca kendisi de otomatik hazır görünsün").
        if (!isReady)
        {
            isReady = true;
            SetCharacterSelectionLocked(true);
            if (readyButtonText != null) readyButtonText.text = "Hazir Degil";
            state.RequestSetReadyServerRpc(true);
        }

        int playerCount = state.PlayerCount();
        if (playerCount <= 1)
        {
            Debug.Log("[LobbyUI] Start Game (SOLO): tek oyuncu, countdown direkt basliyor.");
            state.StartCountdownServer();
            return;
        }

        int notReadyCount = playerCount - state.ReadyCount();
        if (notReadyCount > 0)
        {
            if (forceStartModalText != null)
                forceStartModalText.text = notReadyCount + " oyuncu hazir degil. Yine de baslat?";
            OpenForceStartModal();
        }
        else
        {
            Debug.Log("[LobbyUI] Start Game: herkes hazir, countdown basliyor.");
            state.StartCountdownServer();
        }
    }

    private void OpenForceStartModal()
    {
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(true);
        Debug.Log("[LobbyUI] Force start modal acildi.");
    }

    private void OnForceStartConfirmed()
    {
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(false);
        if (LobbyNetworkState.Instance != null) LobbyNetworkState.Instance.StartCountdownServer();
        Debug.Log("[LobbyUI] Force start onaylandi (Devam), countdown basliyor.");
    }

    private void OnForceStartCancelled()
    {
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(false);
        Debug.Log("[LobbyUI] Force start iptal edildi (Geri), lobi beklemeye devam.");
    }

    /// <summary>T60: 3 preset butonu ({Kolay,Normal,Zor} - difficultyMultipliers dizisiyle
    /// AYNI SIRA) sunucuya SetDifficultyServerRpc gonderecek sekilde bagla. Butonlar sadece
    /// host icin gorunur (bkz. Show()) - ama yine de gercek guvenlik server-side'da
    /// (QuotaManager.SetDifficultyServerRpc icindeki IsServer + GameStarted kontrolu).</summary>
    private void WireDifficultyButtons()
    {
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            int capturedIndex = i;
            if (difficultyButtons[i] != null)
            {
                difficultyButtons[i].onClick.AddListener(() => OnDifficultySelected(capturedIndex));
            }
        }
    }

    private void OnDifficultySelected(int presetIndex)
    {
        if (!isLocalPlayerHost)
        {
            Debug.LogWarning("[LobbyUI] Zorluk sadece host tarafindan degistirilebilir.");
            return;
        }

        if (presetIndex < 0 || presetIndex >= difficultyMultipliers.Length) return;

        if (QuotaManager.Instance == null)
        {
            Debug.LogWarning("[LobbyUI] QuotaManager.Instance null - zorluk ayari gonderilemedi.");
            return;
        }

        float multiplier = difficultyMultipliers[presetIndex];
        QuotaManager.Instance.SetDifficultyServerRpc(multiplier);
        Debug.Log("[LobbyUI] Zorluk secildi (host): x" + multiplier);
    }

    /// <summary>Aktif zorluk carpanina gore hangi preset butonunun "secili" gorundugunu
    /// gunceller (renk vurgusu). Hem host hem client icin cagrilabilir - QuotaManager.Instance
    /// NetworkVariable oldugu icin her iki tarafta da senkron okunur, ama butonlarin kendisi
    /// sadece host'ta interactable/gorunur (Show() icinde ayarlandi).</summary>
    private void RefreshDifficultyButtonHighlight()
    {
        if (QuotaManager.Instance == null || difficultyButtonTexts == null) return;

        float current = QuotaManager.Instance.DifficultyMultiplier.Value;
        for (int i = 0; i < difficultyButtonTexts.Length; i++)
        {
            if (difficultyButtonTexts[i] == null) continue;
            bool isActive = i < difficultyMultipliers.Length && Mathf.Approximately(difficultyMultipliers[i], current);
            difficultyButtonTexts[i].color = isActive ? SelectedColor : UnselectedColor;
        }
    }

    /// <summary>KULLANICI BUG RAPORU DUZELTMESI (dolayli): eskiden burada SADECE eski
    /// LobbySessionManager (gercek agla hicbir ilgisi olmayan sahte kayit) temizleniyordu -
    /// GERCEK NetworkManager baglantisi hic kapatilmiyordu, oyuncu "ayrildim" sansa da aslinda
    /// hala bagliydi. Artik gercekten Shutdown() cagriliyor.</summary>
    private void OnLeaveClicked()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            (Unity.Netcode.NetworkManager.Singleton.IsHost || Unity.Netcode.NetworkManager.Singleton.IsClient))
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }

        LobbySessionManager.LeaveLobby();
        isReady = false;
        Hide();

        if (mainMenuPanelRoot != null)
        {
            mainMenuPanelRoot.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[LobbyUI] mainMenuPanelRoot atanmamis - Leave sonrasi Ana Menuye donulemedi.");
        }

        Debug.Log("[LobbyUI] Leave: NetworkManager kapatildi, Ana Menuye donuluyor.");
    }
}
