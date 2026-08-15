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

    [Header("T48 - Force Start Modal (Start Game basinca hazir olmayan varsa acilir)")]
    public GameObject forceStartModalPanel;
    public TextMeshProUGUI forceStartModalText;
    public Button forceStartConfirmButton; // Devam
    public Button forceStartCancelButton;  // Geri

    [Header("T48 - Simulated Not-Ready Count (Oturum 1, gercek network yok - Inspector'dan degistirilebilir test icin. 0 = herkes ready senaryosu)")]
    public int simulatedNotReadyCount = 2;


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

    [Header("T47 - Lobi Countdown (DayCycleManager'in 240sn gun-ici sayacindan TAMAMEN AYRI)")]
    private const int CountdownStartSeconds = 15;
    private int countdownSecondsRemaining = CountdownStartSeconds;
    private bool isCountdownRunning = false;
    private Coroutine countdownCoroutine;



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
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(false);
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
        if (forceStartConfirmButton != null) forceStartConfirmButton.onClick.AddListener(OnForceStartConfirmed);
        if (forceStartCancelButton != null) forceStartCancelButton.onClick.AddListener(OnForceStartCancelled);
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


        /// <summary>T46'da kurulan local toggle + kilit baglantisinin uzerine T47'de countdown
    /// eklendi. Ready -> "Hazir" (isReady=false) durumundan "Hazir Degil" durumuna gecince
    /// (isReady=true) countdown baslar; "Hazir Degil" basinca (isReady=false donerken)
    /// countdown iptal/sifirlanir.</summary>
    private void OnReadyClicked()
    {
        isReady = !isReady;
        SetCharacterSelectionLocked(isReady);

        if (readyButtonText != null) readyButtonText.text = isReady ? "Hazir Degil" : "Hazir";

        if (isReady)
        {
            StartCountdownIfAllReady();
        }
        else
        {
            ResetCountdown();
        }

        Debug.Log("[LobbyUI] Ready durumu: " + (isReady ? "HAZIR (secim kilitli)" : "HAZIR DEGIL (secim acik)") + ".");
    }

    /// <summary>T47: Session 1'de gercek network yok, bu yuzden "herkes ready mi" kontrolu
    /// hardcoded/basitlestirilmis: local oyuncu (Oyuncu 1) Ready basinca, diger 4 sahte
    /// oyuncunun da (Oyuncu 2-5) zaten hazir oldugu varsayilir ve countdown direkt baslar.
    /// Gercek "X oyuncu hazir degil" sayimi ve Force Start Modal T48'de eklenecek - o gorev
    /// bu varsayimi degistirebilir, simdilik TASKS.md T47 Test kriterine ("Ready basinca
    /// timer baslar") gore tek kosul isReady==true.</summary>
    private void StartCountdownIfAllReady()
    {
        if (isCountdownRunning) return;

        countdownSecondsRemaining = CountdownStartSeconds;
        isCountdownRunning = true;
        UpdateCountdownDisplay();

        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    private System.Collections.IEnumerator CountdownRoutine()
    {
        while (countdownSecondsRemaining > 0)
        {
            yield return new WaitForSeconds(1f);
            if (!isCountdownRunning) yield break; // ResetCountdown tarafindan iptal edildi
            countdownSecondsRemaining--;
            UpdateCountdownDisplay();
        }

        if (!isCountdownRunning) yield break;

        if (countdownText != null) countdownText.text = "Oyun Basliyor";
        Debug.Log("[LobbyUI] Countdown tamamlandi - Oyun Basliyor.");

        yield return new WaitForSeconds(2f);
        if (!isCountdownRunning) yield break;

        isCountdownRunning = false;
        countdownCoroutine = null;
        // NOT: Gercek scene gecisi (SceneManager.LoadScene) T48/T50'de eklenecek.
        // T47 kapsami sadece UI/timer oldugu icin burada placeholder log birakiliyor.
        Debug.Log("[LobbyUI] (Placeholder) Game scene'e gecis burada tetiklenecek (T48/T50).");
    }

    /// <summary>T47: Countdown metnini gunceller ("15","14",...,"0").</summary>
    private void UpdateCountdownDisplay()
    {
        if (countdownText != null) countdownText.text = countdownSecondsRemaining.ToString();
    }

    /// <summary>T47: Countdown iptal/sifirlama. "Hazir Degil" basilinca cagirilir - coroutine'i
    /// durdurur, sayaci sifirlar, countdownText'i "-" yapar. NOT: DayCycleManager'in 240sn
    /// gun-ici sayaciyla KARISTIRILMAMALI, bu tamamen ayri bir lobi-ici sistemdir.</summary>
    private void ResetCountdown()
    {
        isCountdownRunning = false;
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        countdownSecondsRemaining = CountdownStartSeconds;
        if (countdownText != null) countdownText.text = "-";
        Debug.Log("[LobbyUI] Countdown sifirlandi.");
    }

private void OnStartGameClicked()
    {
        if (simulatedNotReadyCount > 0)
        {
            OpenForceStartModal();
        }
        else
        {
            Debug.Log("[LobbyUI] Start Game: herkes hazir, countdown modalsiz direkt basliyor.");
            StartCountdownIfAllReady();
        }
    }

    /// <summary>T48: Ready olmayan oyuncu varsa Start Game basilinca acilir. Metin, kac
    /// oyuncunun hazir olmadigini dinamik gosterir (Oturum 1'de simulatedNotReadyCount'tan
    /// okunur, gercek network T49/T50'de bu degeri gercek sayimla degistirecek).</summary>
    private void OpenForceStartModal()
    {
        if (forceStartModalText != null)
            forceStartModalText.text = simulatedNotReadyCount + " oyuncu hazir degil. Yine de baslat?";
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(true);
        Debug.Log("[LobbyUI] Force start modal acildi (" + simulatedNotReadyCount + " oyuncu hazir degil).");
    }

    /// <summary>T48: [Devam] basilinca cagirilir. Modali kapatir, countdown'u 15sn'ye
    /// sifirlayip T47'nin StartCountdownIfAllReady() ile otomatik baslatir.</summary>
    private void OnForceStartConfirmed()
    {
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(false);
        ResetCountdown();
        StartCountdownIfAllReady();
        Debug.Log("[LobbyUI] Force start onaylandi (Devam), countdown 15sn'den basliyor.");
    }

    /// <summary>T48: [Geri] basilinca cagirilir. Modali kapatir, lobi beklemeye devam eder
    /// (countdown'a dokunmaz).</summary>
    private void OnForceStartCancelled()
    {
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(false);
        Debug.Log("[LobbyUI] Force start iptal edildi (Geri), lobi beklemeye devam.");
    }

    private void OnLeaveClicked()
    {
        Debug.Log("[LobbyUI] Leave tiklandi (Oturum 1.5'te lobi listesine donus mantigi eklenecek).");
    }
}
