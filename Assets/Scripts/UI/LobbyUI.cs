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
///
/// BUG DUZELTMESI (kullanici raporu): Start Game butonu (a) host-only degildi, herkes
/// basabiliyordu, (b) host basinca kendisi otomatik Hazir olmuyordu, (c) countdown
/// basladiktan sonra geri donmek icin Iptal secenegi yoktu. Ucu de bu surumde eklendi:
/// isLocalPlayerHost kontrolu (LobbySessionManager.CurrentLobby.hostUsername ile
/// MainMenuController.CurrentUsername karsilastirmasi), BeginHostCountdown() host'u
/// otomatik ready yapar, StartCountdownIfAllReady/ResetCountdown artik Start Game
/// butonunun gorselini (Baslat mavi <-> Iptal kirmizi) merkezi olarak yonetir.
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

    [Header("Ortada - 5 Oyuncu Slotunun Ready Durum Metni (NameText altinda). Kirmizi X = Hazir Degil, Yesil tik = Hazir.")]
    public TextMeshProUGUI[] slotReadyStatusTexts = new TextMeshProUGUI[5];

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

    [Header("Sag Alt - Start Game (host-only). Countdown calisirken Baslat->Iptal'e donusur (renk+metin).")]
    public Button startGameButton;
    public Image startGameButtonImage;
    public TextMeshProUGUI startGameButtonText;
    public Sprite startGameDefaultSprite; // mavi "Baslat"
    public Sprite startGameCancelSprite;  // kirmizi "Iptal"

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

    /// <summary>BUG DUZELTMESI: local oyuncu bu lobiyi olusturan host mu? Show(code) icinde
    /// LobbySessionManager.CurrentLobby.hostUsername ile MainMenuController.CurrentUsername
    /// karsilastirilarak hesaplanir. Start Game butonu SADECE bu true iken gorunur/etkin olur.</summary>
    private bool isLocalPlayerHost = false;

    private static readonly Color SelectedColor = new Color(1f, 0.92f, 0.55f, 1f);
    private static readonly Color UnselectedColor = Color.white;
    private static readonly Color LockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    // Oyuncu adi altindaki Ready durum metni renkleri (kirmizi/yesil).
    private static readonly Color NotReadyStatusColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    private static readonly Color ReadyStatusColor = new Color(0.15f, 0.75f, 0.2f, 1f);
    private const string NotReadyStatusLabel = "\u2715 Haz\u0131r De\u011fil";
    private const string ReadyStatusLabel = "\u2713 Haz\u0131r";

    [Header("T46 - Local Karakter Degisimi (localSlotIndex'teki Image'e statik foto uygulanir)")]
    public int localSlotIndex = 0; // Oyuncu 1 = sen (hardcoded, T49'da client ID'ye baglanacak)

    [Header("T50 - Lobi Listesi Baglantisi (Leave sonrasi donus icin)")]
    public LobbyListUI lobbyListUI;

    [Header("BUG DUZELTMESI - Karakter secimini GERCEK govdeye (CharacterSelectionManager, network) baglar. \nEskiden bu script sadece kozmetik fotograf degistiriyordu, sunucuya hicbir istek gitmiyordu - \nbu yuzden secim oyun icinde hicbir zaman uygulanmiyordu (herkes varsayilan Yetiskin'de kaliyordu).")]
    public CharacterSelectionManager selectionManager;

    [Header("BUG DUZELTMESI (kullanici karari) - Leave artik Lobi Listesi DEGIL, DOGRUDAN Ana Menu'ye donuyor")]
    public GameObject mainMenuPanelRoot;

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
        // BUG DUZELTMESI (kullanici raporu): sahnede Panel yanlislikla aktif kaydedilmisti,
        // bu da LobbyUI'nin oyun daha Ana Menu'deyken gorunur olmasina ve Ayarlar/diger
        // panellerin ustunu kapatmasina neden oluyordu. Artik Awake KENDI baslangic
        // gorunurlugunu sahne dosyasinin kaydedilmis haline guvenmeden garanti eder.
        if (panelRoot != null) panelRoot.SetActive(false);
        WireButtons();
        WireCharacterPortraits();
    }

    /// <summary>MainMenuController.OnCreateLobbyClicked/OnJoinLobbyClicked sonrasi cagirir.
    /// Lobi kodunu (rakam-only, T50'de gercek host tarafinda ayni yontemle uretilecek) ve
    /// baslangic degerlerini ayarlar.</summary>
    /// <summary>MainMenuController.OnCreateLobbyClicked/LobbyListUI.OnLobbyRowClicked sonrasi
    /// cagirir. T50: lobi kodu artik burada rastgele URETILMEZ, LobbySessionManager'in GERCEK
    /// kodu parametre olarak alinir (host icin CreateLobby, katilan icin JoinLobby donusu).
    ///
    /// BUG DUZELTMESI: isLocalPlayerHost burada hesaplanir (CurrentLobby.hostUsername ==
    /// MainMenuController.CurrentUsername) ve Start Game butonu SADECE host icin aktif/gorunur
    /// yapilir. Ayrica local ready/countdown state'i temiz sayfa acar (Show her cagirildiginda).</summary>
    public void Show(int code)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        lobbyCode = code;
        if (lobbyCodeText != null) lobbyCodeText.text = lobbyCode.ToString();

        isLocalPlayerHost = LobbySessionManager.CurrentLobby.HasValue
            && LobbySessionManager.CurrentLobby.Value.lobbyCode == lobbyCode
            && LobbySessionManager.CurrentLobby.Value.hostUsername == MainMenuController.CurrentUsername;

        if (startGameButton != null) startGameButton.gameObject.SetActive(isLocalPlayerHost);

        // Temiz baslangic: onceki bir lobiden kalma ready/countdown state'i tasinmasin.
        isReady = false;
        System.Array.Clear(playerReady, 0, playerReady.Length);
        readyCount = 0;
        if (readyButtonText != null) readyButtonText.text = "Hazir";
        ResetCountdown();

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

        for (int i = 0; i < slotReadyStatusTexts.Length; i++)
        {
            UpdateReadyStatusText(i, playerReady[i]);
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
        Debug.Log("[LobbyUI] Karakter secildi (local/kozmetik): " + charName + " (index " + charIndex + ")");

        ApplySlotPortrait(localSlotIndex, charIndex);

        // BUG DUZELTMESI: eskiden burada selectionManager hicbir zaman cagrilmiyordu - secim
        // sadece kozmetik fotografi degistiriyordu, sunucuya hicbir istek gitmiyordu, bu yuzden
        // gercek govde (PlayerController.IsControllable) hep varsayilan Yetiskin'de kaliyordu.
        // Simdi gercek atama isteği burada sunucuya gonderiliyor.
        if (selectionManager != null)
        {
            Debug.Log("[LobbyUI] Sunucuya karakter atama istegi gonderiliyor: " + charName + " (index " + charIndex + ")");
            selectionManager.RequestSelectCharacterServerRpc(charIndex);
        }
        else
        {
            Debug.LogWarning("[LobbyUI] selectionManager atanmamis - karakter secimi sunucuya iletilemedi, oyun icinde varsayilan (Yetiskin) govde kullanilmaya devam edecek.");
        }
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

/// <summary>Slot altindaki Ready durum metnini gunceller. Slot bos ise (o slotun oyuncu
    /// adi bos ise) metin GIZLENIR (SetActive false). Slot doluysa Hazir (yesil) veya
    /// Hazir Degil (kirmizi) yazisi uygulanir. Font/boyut/pozisyon burada degistirilmez -
    /// sadece aktiflik, metin icerigi ve renk.</summary>
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

    /// <summary>BUG DUZELTMESI - eskiden OnReadyClicked icine gomulmustu, artik ortak metod:
    /// local oyuncunun ready durumunu VERILEN degere set eder (toggle degil, dogrudan atama).
    /// Hem manuel Ready butonundan hem de host Start Game basinca otomatik "Hazir" olmasindan
    /// (BeginHostCountdown) cagirilir. Zaten istenen durumdaysa hicbir sey yapmaz (cift sayim
    /// onlenir).</summary>
    private void SetLocalReady(bool ready)
    {
        if (isReady == ready) return;

        isReady = ready;
        SetCharacterSelectionLocked(isReady);

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
        UpdateReadyStatusText(localSlotIndex, isReady);
    }

    /// <summary>T46'da kurulan local toggle + kilit baglantisinin uzerine T47'de countdown
    /// eklendi. Ready -> "Hazir" (isReady=false) durumundan "Hazir Degil" durumuna gecince
    /// (isReady=true) countdown baslar; "Hazir Degil" basinca (isReady=false donerken)
    /// countdown iptal/sifirlanir. BUG DUZELTMESI: artik SetLocalReady() ortak metodunu
    /// kullanir (host'un otomatik ready olmasiyla ayni kod yolu).</summary>
    private void OnReadyClicked()
    {
        SetLocalReady(!isReady);

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
    /// countdown baslar, degilse return. BUG DUZELTMESI: countdown baslarken host icin
    /// Start Game butonunu (varsa) kirmizi "Iptal" gorunumune merkezi olarak cevirir -
    /// countdown herkes-ready ile mi yoksa host'un Start Game/Force Start ile mi
    /// baslatildigina bakmaksizin ayni gorsel garanti edilir.</summary>
    private void StartCountdownIfAllReady()
    {
        if (isCountdownRunning) return;

        countdownSecondsRemaining = CountdownStartSeconds;
        isCountdownRunning = true;
        UpdateCountdownDisplay();

        if (isLocalPlayerHost) SetStartButtonVisual(true);

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
        if (isLocalPlayerHost) SetStartButtonVisual(false);

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

        // BUG DUZELTMESI (kullanici raporu): Oyun basladiktan sonra Lobi Canvas'i (Panel)
        // gizli kalmiyordu - hem gorsel olarak ekranda kaliyordu hem de ustundeki UI
        // raycast'leri (Canvas Graphic Raycaster) oyuncunun karakteri kontrol etmesini
        // engelliyordu/karisiyordu. Oyun basinca lobi ekrani tamamen kapatilir.
        // BUG DUZELTMESI (kullanici raporu: "HUD'lar gorunmuyor"): HUDController.Show()
        // hicbir yerde cagirilmiyordu, HUD Awake()'te Hide() ile kapali kaliyor ve hep oyle
        // kaliyordu. Gercek oyun-baslama noktasi TAM BURASI - HUD de burada acilir.
        if (HUDController.Instance != null)
        {
            HUDController.Instance.Show();
            Debug.Log("[LobbyUI] HUDController.Instance.Show() cagirildi - HUD paneller acildi.");
        }
        else
        {
            Debug.LogWarning("[LobbyUI] HUDController.Instance null - HUD acilamadi (sahnede HUDController var mi kontrol et).");
        }

        Hide();
    }

    /// <summary>T47: Countdown metnini gunceller ("15","14",...,"0").</summary>
    private void UpdateCountdownDisplay()
    {
        if (countdownText != null) countdownText.text = countdownSecondsRemaining.ToString();
    }

    /// <summary>T47: Countdown iptal/sifirlama. "Hazir Degil" basilinca veya host Start Game'i
    /// (artik "Iptal" durumundayken) tekrar basinca cagirilir - coroutine'i durdurur, sayaci
    /// sifirlar, countdownText'i "-" yapar. BUG DUZELTMESI: host icin Start Game butonunu
    /// merkezi olarak mavi "Baslat" gorunumune geri dondurur. NOT: DayCycleManager'in 240sn
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
        if (isLocalPlayerHost) SetStartButtonVisual(false);
        Debug.Log("[LobbyUI] Countdown sifirlandi.");
    }

    /// <summary>BUG DUZELTMESI: Start Game butonunun gorselini (sprite + metin) countdown
    /// durumuna gore degistirir. cancelMode=true -> kirmizi "Iptal", false -> mavi "Baslat".
    /// Sprite referanslari atanmamissa sadece metin degisir (sessiz devam eder).</summary>
    private void SetStartButtonVisual(bool cancelMode)
    {
        if (startGameButtonText != null) startGameButtonText.text = cancelMode ? "Iptal" : "Baslat";

        if (startGameButtonImage != null)
        {
            if (cancelMode && startGameCancelSprite != null) startGameButtonImage.sprite = startGameCancelSprite;
            else if (!cancelMode && startGameDefaultSprite != null) startGameButtonImage.sprite = startGameDefaultSprite;
        }
    }

    /// <summary>BUG DUZELTMESI: host'un Start Game (veya Force Start Devam) ile countdown'u
    /// fiilen baslattigi TEK ortak yol. Host henuz Ready basmamissa burada otomatik "Hazir"
    /// yapilir (kullanici raporu: "başlaya basınca kendisi de otomatik olarak hazır olarak
    /// görünsün"), sonra StartCountdownIfAllReady() cagrilir (bu da butonu Iptal'e cevirir).</summary>
    private void BeginHostCountdown()
    {
        if (!isReady) SetLocalReady(true);
        StartCountdownIfAllReady();
    }

    /// <summary>BUG DUZELTMESI: artik ucu de burada ele aliniyor:
    /// (1) Host-only: isLocalPlayerHost degilse hicbir sey yapmaz (buton zaten Show() icinde
    ///     non-host icin gizleniyor, bu ekstra bir guvenlik katmani).
    /// (2) Countdown zaten calisiyorsa buton "Iptal" durumundadir - bu tiklama countdown'u
    ///     durdurur, host'un otomatik-ready durumunu geri alir ve butonu "Baslat"a dondurur.
    /// (3) Countdown calismiyorsa eski akis: solo->direkt baslat, herkes ready degilse
    ///     Force Start modal, herkes ready ise direkt baslat - hepsi BeginHostCountdown()
    ///     uzerinden gecer (otomatik ready + gorsel guncelleme tek yerden).</summary>
    private void OnStartGameClicked()
    {
        if (!isLocalPlayerHost)
        {
            Debug.LogWarning("[LobbyUI] Start Game sadece lobiyi olusturan host tarafindan kullanilabilir.");
            return;
        }

        if (isCountdownRunning)
        {
            ResetCountdown();
            SetLocalReady(false);
            Debug.Log("[LobbyUI] Start Game iptal edildi (host), countdown durduruldu.");
            return;
        }

        int playerCount = GetCurrentPlayerCount();

        // DUZELTME: Solo modda (playerCount==1) modal gosterme - direkt countdown
        if (playerCount == 1)
        {
            Debug.Log("[LobbyUI] Start Game (SOLO): tek oyuncu, modal yok, countdown direkt basliyor.");
            BeginHostCountdown();
            return;
        }

        // Cok oyunculu modda: baslatici hariç geri kalan oyuncularin hazir/hazir olmama durumunu kontrol et
        // notReadyCount = (playerCount - 1) - readyCount
        // Cunku baslatici kendisi BeginHostCountdown() icinde otomatik hazir yapilacak
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
            BeginHostCountdown();
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

    /// <summary>T48: [Devam] basilinca cagirilir. Modali kapatir, BeginHostCountdown() ile
    /// host'u otomatik ready yapip countdown'u 15sn'den baslatir (buton Iptal'e doner).</summary>
    private void OnForceStartConfirmed()
    {
        if (forceStartModalPanel != null) forceStartModalPanel.SetActive(false);
        ResetCountdown();
        BeginHostCountdown();
        Debug.Log("[LobbyUI] Force start onaylandi (Devam), countdown 15sn'den basliyor.");
    }

    /// <summary>T48: [Geri] basilinca cagirilir. Modali kapatir, lobi beklemeye devam eder
    /// (countdown'a dokunmaz, host henuz otomatik ready yapilmadigi icin geri alinacak bir
    /// sey de yok).</summary>
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

        if (mainMenuPanelRoot != null)
        {
            mainMenuPanelRoot.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[LobbyUI] mainMenuPanelRoot atanmamis - Leave sonrasi Ana Menuye donulemedi.");
        }

        Debug.Log("[LobbyUI] Leave: LobbySessionManager.LeaveLobby() cagirildi, Ana Menuye donuluyor.");
    }
}
