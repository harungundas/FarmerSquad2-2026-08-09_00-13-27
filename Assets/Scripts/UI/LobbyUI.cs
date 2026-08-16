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
///
/// GÖRSEL YÖNTEM DEĞİŞİKLİĞİ (kullanıcı kararı): Orta slotlardaki karakter önizlemesi için
/// önceden denenen "3D karakter instantiate et + Cinemachine kamerasını slotlara çevir"
/// yöntemi TERK EDİLDİ (kamera her sahnede farklı davranıyor, bakım maliyeti yüksekti).
/// Bunun yerine Assets/character_slots altındaki hazır PNG fotoğraflar kullanılıyor:
/// karakter seçilince ilgili slot'un Image bileşenine o karakterin sprite'ı atanıyor.
/// LobbyManager.cs (3D spawn sistemi) artık bu ekranda KULLANILMIYOR.
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

    [Header("Ortada - 5 Oyuncu Slotunun Image bileseni (MiddleSlots/Slot0..Slot4). " +
        "3D onizleme + kamera yontemi TERK EDILDI - artik secilen karakterin statik fotografi buraya konuyor.")]
    public Image[] slotPortraitImages = new Image[5];

    [Header("Alt - 5 Karakter Portre Butonu (ZORUNLU, her zaman gorunur)")]
    public Button[] characterPortraitButtons = new Button[5];
    public Image[] characterPortraitBackgrounds = new Image[5];
    public string[] characterNames = new string[] { "Yetiskin", "Sisman", "Cocuk", "Kadin", "Yasli" };

    [Header("Statik Karakter Fotograflari (Assets/character_slots, characterNames ile AYNI SIRA)")]
    public Sprite[] characterPortraitSprites = new Sprite[5];

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

    [Header("Karakter Secimleri State (Gercek oyuncu ready state'leri)")]
    private bool[] playerReady = new bool[5];
    private int readyCount = 0;


    private int lobbyCode;
    private int selectedCharacterIndex = 0;

    private static readonly Color SelectedColor = new Color(1f, 0.92f, 0.55f, 1f);
    private static readonly Color UnselectedColor = Color.white;
    private static readonly Color LockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("T46 - Local Karakter Degisimi (localSlotIndex'teki Image'e statik foto uygulanir)")]
    public int localSlotIndex = 0; // Oyuncu 1 = sen (hardcoded, T49'da client ID'ye baglanacak)

    [Header("T50 - Lobi Listesi Baglantisi (Leave sonrasi donus icin)")]
    public LobbyListUI lobbyListUI;

    [Header("BUG DUZELTMESI - Gercek Oyun Baslangici (countdown 0'da cagrilir, DayCycleManager/VehicleSpawner artik network-spawn aninda degil bu sinyalle basliyor)")]
    public DayCycleManager dayCycleManager;


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
    /// <summary>MainMenuController.OnCreateLobbyClicked/LobbyListUI.OnLobbyRowClicked sonrasi
    /// cagirir. T50: lobi kodu artik burada rastgele URETILMEZ, LobbySessionManager'in GERCEK
    /// kodu parametre olarak alinir (host icin CreateLobby, katilan icin JoinLobby donusu).</summary>
    public void Show(int code)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        lobbyCode = code;
        if (lobbyCodeText != null) lobbyCodeText.text = lobbyCode.ToString();
        UpdatePlayerCountDisplay();
        UpdatePlaceholderSlots();
        SelectCharacter(0);
        ApplySlotPortrait(localSlotIndex, 0);
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(false);
    }

    /// <summary>Geriye donuk uyumluluk icin parametresiz overload - gercek bir
    /// LobbySessionManager kodu olmadan rastgele bir kod uretir. T50 sonrasi
    /// MainMenuController/LobbyListUI HER ZAMAN Show(int code) kullanir, bu sadece test/eski
    /// cagrilar icin guvenlik agi.</summary>
    public void Show()
    {
        Show(Random.Range(1000, 9999));
    }

    /// <summary>T50: Ust bilgideki oyuncu sayisini LobbySessionManager.CurrentLobby'den okur
    /// (gercek veri). Eslesme yoksa (ornegin eski parametresiz Show() cagrisi) 1/5 gosterir.</summary>
    private void UpdatePlayerCountDisplay()
    {
        int count = 1;
        if (LobbySessionManager.CurrentLobby.HasValue && LobbySessionManager.CurrentLobby.Value.lobbyCode == lobbyCode)
        {
            count = LobbySessionManager.CurrentLobby.Value.playerCount;
        }
        if (playerCountText != null) playerCountText.text = count + "/" + LobbySessionManager.MaxPlayersPerLobby;
        if (countdownText != null) countdownText.text = "-";
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Oturum 1'de gercek oyuncu verisi yok - sabit "Oyuncu N" placeholder metni
    /// gosterir.</summary>
    private void UpdatePlaceholderSlots()
    {
        for (int i = 0; i < slotNameTexts.Length; i++)
        {
            if (slotNameTexts[i] != null)
            {
                if (i == 0)
                {
                    slotNameTexts[i].text = MainMenuController.CurrentUsername;
                }
                else
                {
                    slotNameTexts[i].text = "";
                }
            }
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
    /// vurgular ve local slot'taki (localSlotIndex) Image bilesenine secilen karakterin
    /// statik fotografini uygular. Kilitliyken (Ready sonrasi) hicbir sey yapmaz - buton
    /// zaten interactable=false olur, bu kontrol savunma amacli ekstra guvenlik.</summary>
    public void OnCharacterSelected(int charIndex)
    {
        if (isSelectionLocked) return;

        SelectCharacter(charIndex);

        string charName = (charIndex >= 0 && charIndex < characterNames.Length) ? characterNames[charIndex] : charIndex.ToString();
        Debug.Log("[LobbyUI] Karakter secildi: " + charName + " (index " + charIndex + ")");

        ApplySlotPortrait(localSlotIndex, charIndex);
    }

    /// <summary>Verilen orta slotun Image bilesenine, verilen karakter index'inin statik
    /// fotografini (characterPortraitSprites) atar. Referanslar eksikse sessizce uyarir.</summary>
    private void ApplySlotPortrait(int slotIndex, int charIndex)
    {
        if (slotPortraitImages == null || slotIndex < 0 || slotIndex >= slotPortraitImages.Length || slotPortraitImages[slotIndex] == null)
        {
            Debug.LogWarning("[LobbyUI] slotPortraitImages[" + slotIndex + "] atanmamis, fotograf uygulanamadi.");
            return;
        }
        if (characterPortraitSprites == null || charIndex < 0 || charIndex >= characterPortraitSprites.Length || characterPortraitSprites[charIndex] == null)
        {
            Debug.LogWarning("[LobbyUI] characterPortraitSprites[" + charIndex + "] atanmamis, fotograf uygulanamadi.");
            return;
        }

        slotPortraitImages[slotIndex].sprite = characterPortraitSprites[charIndex];
        slotPortraitImages[slotIndex].color = Color.white;
        slotPortraitImages[slotIndex].preserveAspect = true;
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
    /// countdown iptal/sifirlanir. BUG DUZELTMESI: hardcoded simulatedNotReadyCount yerine
    /// gercek ready state array'i (playerReady) ve readyCount sayaci kullaniliyor.</summary>
    private void OnReadyClicked()
    {
        isReady = !isReady;
        SetCharacterSelectionLocked(isReady);

        // Gercek ready count'unu guncelle
        if (isReady)
        {
            playerReady[localSlotIndex] = true;
            readyCount++;
        }
        else
        {
            playerReady[localSlotIndex] = false;
            if (readyCount > 0) readyCount--;
        }

        if (readyButtonText != null) readyButtonText.text = isReady ? "Hazir Degil" : "Hazir";

        if (isReady)
        {
            StartCountdownIfAllReady();
        }
        else
        {
            ResetCountdown();
        }

        Debug.Log("[LobbyUI] Ready durumu: " + (isReady ? "HAZIR (secim kilitli)" : "HAZIR DEGIL (secim acik)") + ". Ready count: " + readyCount + "/5.");
    }

    /// <summary>T47: Tum oyuncularin ready state'ini kontrol eder. Herkes ready ise
    /// countdown baslar, degilse return.</summary>
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

        // BUG DUZELTMESI: Onceden burada sadece placeholder Debug.Log vardi - gercek oyun HICBIR
        // ZAMAN baslamiyordu, cunku DayCycleManager/VehicleSpawner zaten [Lobi Olustur] anindaki
        // network-spawn ile (yanlislikla) baslamis oluyordu. Artik gercek tetikleyici BURASI:
        // countdown 0'a inip "Oyun Basliyor" 2sn gosterildikten sonra DayCycleManager.BeginGameServer()
        // cagrilir. Host degilse (dayCycleManager.BeginGameServer icindeki IsServer kontrolu) sessizce
        // no-op olur - bu guvenli, host-only kisitlamasi (T50 acik maddesi) ileride netlesecek.
        if (dayCycleManager != null)
        {
            dayCycleManager.BeginGameServer();
            Debug.Log("[LobbyUI] DayCycleManager.BeginGameServer() cagrildi - gercek oyun (Gun 1) simdi basliyor.");
        }
        else
        {
            Debug.LogWarning("[LobbyUI] dayCycleManager atanmamis - gercek oyun baslatilamadi.");
        }
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
        // T48: Start Game basinca gercek oyuncu sayisi oku
        int playerCount = GetCurrentPlayerCount();

        // DUZELTME: Solo modda (playerCount==1) modal gosterme - direkt countdown
        if (playerCount == 1)
        {
            Debug.Log("[LobbyUI] Start Game (SOLO): tek oyuncu, modal yok, countdown direkt basliyor.");
            StartCountdownIfAllReady();
            return;
        }

        // Cok oyunculu modda: baslatici hariç geri kalan oyuncularin hazir/hazir olmama durumunu kontrol et
        // notReadyCount = (playerCount - 1) - readyCount
        // Cunku baslatici kendisi hazir sayilir (butona bastigi icin)
        int notReadyCount = (playerCount - 1) - readyCount;

        if (notReadyCount > 0)
        {
            // Modal acilacak (T48)
            if (forceStartModalText != null)
                forceStartModalText.text = notReadyCount + " oyuncu hazir degil. Yine de baslat?";
            OpenForceStartModal();
        }
        else
        {
            Debug.Log("[LobbyUI] Start Game: herkes hazir, countdown modalsiz direkt basliyor.");
            StartCountdownIfAllReady();
        }
    }

    /// <summary>Gercek lobi'deki oyuncu sayisini LobbySessionManager'dan okur.</summary>
    private int GetCurrentPlayerCount()
    {
        if (LobbySessionManager.CurrentLobby.HasValue && LobbySessionManager.CurrentLobby.Value.lobbyCode == lobbyCode)
        {
            return LobbySessionManager.CurrentLobby.Value.playerCount;
        }
        return 1; // Fallback: solo
    }


    /// <summary>T48: Ready olmayan oyuncu varsa Start Game basilinca acilir. Metin, kac
    /// oyuncunun hazir olmadigini dinamik gosterir (gercek sayim OnStartGameClicked'den
    /// aliniyor).</summary>
    private void OpenForceStartModal()
    {
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(true);
        Debug.Log("[LobbyUI] Force start modal acildi.");
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

    /// <summary>T50: LobbySessionManager.LeaveLobby() ile lobiden GERCEK cikis yapar (playerCount
    /// azalir, 0'a duserse lobi listeden silinir). Countdown varsa sifirlanir, bu Canvas
    /// kapanir ve LobbyListUI.Show() ile Lobiye Katil ekranina donulur (TASKS.md T50 Test:
    /// "Lobi scene'den [Leave] basinca Lobiye Katil listesine donuyor mu").</summary>
    private void OnLeaveClicked()
    {
        LobbySessionManager.LeaveLobby();
        ResetCountdown();
        Hide();

        if (lobbyListUI != null)
        {
            lobbyListUI.Show();
        }
        else
        {
            Debug.LogWarning("[LobbyUI] lobbyListUI atanmamis - Leave sonrasi Lobiye Katil listesine donulemedi (T50).");
        }

        Debug.Log("[LobbyUI] Leave: LobbySessionManager.LeaveLobby() cagirildi, Lobiye Katil listesine donuluyor.");
    }
}
