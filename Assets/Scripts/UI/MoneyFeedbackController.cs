using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// T75: WalletManager.OnTransactionNotified'a abone olup HUDController'daki Kasa metnini
/// aninda ziplatmadan, ~0.4 saniyede sayarak gunceller (ARCHITECTURE.md "## HUD").
///
/// BILINCLI TASARIM - NetworkBehaviour DEGIL: Tamamen client-side, saf goruntu katmani.
/// WalletManager.Balance zaten NGO tarafindan senkronize NetworkVariable - bu script sadece
/// o dogru degerin EKRANA nasil YAZILDIGINI (aninda mi, sayarak mi) yonetiyor, network state
/// tasimiyor. Bu yuzden host/client fark etmeksizin ayni script her yerde ayni sekilde calisir.
///
/// HUDController.Instance UZERINDEN erisim: Inspector referansi yerine, projenin var olan
/// static-Instance deseni kullanildi (bkz. HUDController.cs ust yorumu - VehicleSpawner ve
/// NegotiationManager de ayni deseni kullaniyor).
///
/// LATE-JOIN KORUMASI: Ilk frame'lerde HUDController.Instance/walletManager hazir olana kadar
/// bekler (TryInitialize), hazir olunca WalletManager.Balance.Value'yu DOGRUDAN (animasyonsuz)
/// okuyup gosterir. Bu, gec katilan bir client'in kacirdigi gecmis transaction ClientRpc'leri
/// icin sahte bir animasyon oynatmasini engeller - NGO zaten gec katilan client'a gecmis
/// ClientRpc'leri tekrar oynatmaz, sadece Balance'in GUNCEL degerini senkronize eder.
/// initialized=false iken gelen bir OnTransactionNotified olayi yok sayilir (guvenlik icin -
/// pratikte olmaz cunku Balance NetworkVariable senkronu ClientRpc'den once tamamlanir), cunku
/// TryInitialize zaten o transaction'i icermis GUNCEL degeri okuyacaktir.
///
/// KUYRUKLAMA YOK / IPTAL EDILMEZ: Yeni bir transaction bildirimi gelirse mevcut animasyon
/// sifirlanmaz - hedef deger (targetValue) guncellenir ve o anki gosterilen degerden
/// (displayedValue) yeni hedefe dogru hiz (currentRate) yeniden hesaplanarak devam edilir.
///
/// T76: Ucan +/-$ yazi + obje havuzu. Her transaction bildiriminde, Kasa panelinin (TopRightPanel)
/// hemen ustunde bir TMP metin belirir, FloatingDuration saniyede yukari kayip fade-out ile
/// kaybolur. Pozitif delta yesil+"+", negatif delta kirmizi+"-" (renk TEK BASINA yeterli degil,
/// isaret de zorunlu - erisilebilirlik). Havuz FloatingPoolSize kadar TMP objesi sahne
/// calisirken (ilk transaction/init aninda) BIR KEZ olusturulur, sonrasinda surekli
/// Instantiate/Destroy YAPILMAZ - SetActive ile devreye alinip round-robin geri donulur. Metin,
/// WalletQuotaText'in bulundugu AYNI Canvas (HUDCanvas, Screen Space - Overlay) icinde, onun
/// ebeveyni TopRightPanel altinda spawn olur (world space DEGIL). Font, WalletQuotaText'ten
/// kopyalanir (LilitaOne-RegularSDF, proje standardi).
/// </summary>
public class MoneyFeedbackController : MonoBehaviour
{
    private const float AnimDurationSeconds = 0.4f;

    private const int FloatingPoolSize = 5;
    private const float FloatingDuration = 0.8f;
    private const float FloatingDistance = 24f; // BUGFIX (3. canli test, kullanici bulgusu 02.09.2026): eskiden 40f idi, yazi Kasa metninden gereginden fazla uzaklasiyordu ("sag altta kalmis" hissi) - kisaltildi, artik Kasa metnine daha yakin kalip soluyor.
    private const float FloatingFontSizeMultiplier = 1.4f; // BUGFIX (3. canli test, kullanici bulgusu 02.09.2026): kullanici yaziyi kucuk buldu, Kasa metninden %40 buyuk yapildi.

    private bool initialized = false;
    private float displayedValue = 0f;
    private float targetValue = 0f;
    private float currentRate = 0f; // birim/saniye, isaretli (artis/azalis)
    private bool isAnimating = false;

    // T77: HUD panel scale-punch efekti. Coroutine yerine, dosyanin geri kalaninda zaten
    // kullanilan Update()-icinde-elapsed-time deseniyle (AnimateStep/UpdateFloatingTexts ile
    // AYNI stil) uygulandi - bilincli kucuk sapma, HANDOFF'ta not dusuldu.
    private const float PunchDurationSeconds = 0.15f; // toplam sure (yukari + asagi)
    private const float PunchScale = 1.10f;

    private RectTransform kasaPanelRectTransform;
    private bool isPunching = false;
    private float punchElapsed = 0f;

    // T78: Para hareketi sesleri. AudioManager.Instance.PlaySFX() uzerinden calinir - bu
    // sayede merkezi SFX hacim ayari (SettingsUI, PlayerPrefs) otomatik gecerli olur, ayrica
    // AynI anda birden fazla SFX PlayOneShot ile ust uste binebilir (AudioManager.cs zaten
    // bu sekilde tasarlandi). Klipler Assets/sound/ altinda (proje fiili konvansiyonu -
    // TASKS.md'nin 'Assets/Audio/SFX/' onerisi degil, AudioManager.cs zaten bu klasoru
    // kullaniyordu, tutarlilik icin ayni klasor secildi).
    [Header("T78 - Para Hareketi Sesleri (AudioManager uzerinden calinir)")]
    public AudioClip coinGainClip;
    public AudioClip coinLossClip;
    public AudioClip rentDeductionClip;

    private const float SfxCooldownSeconds = 0.1f;
    private readonly Dictionary<TransactionReason, float> lastSfxPlayTime = new Dictionary<TransactionReason, float>();

    private class FloatingTextSlot
    {
        public RectTransform rt;
        public TextMeshProUGUI text;
        public CanvasGroup canvasGroup;
        public bool active;
        public float elapsed;
        public Vector2 startAnchoredPos;
    }

    private FloatingTextSlot[] floatingPool;
    private int floatingCursor = 0;

    private void OnEnable()
    {
        WalletManager.OnTransactionNotified += HandleTransaction;
    }

    private void OnDisable()
    {
        WalletManager.OnTransactionNotified -= HandleTransaction;
    }

    private void Update()
    {
        if (!initialized)
        {
            TryInitialize();
            return;
        }

        if (isAnimating)
        {
            AnimateStep();
        }

        UpdateFloatingTexts();
        UpdatePunch();
    }

    private void TryInitialize()
    {
        HUDController hud = HUDController.Instance;
        if (hud == null || hud.walletManager == null) return;

        displayedValue = hud.walletManager.Balance.Value;
        targetValue = displayedValue;
        isAnimating = false;
        hud.SetWalletDisplayValue(displayedValue);
        initialized = true;

        InitFloatingPool(hud);
    }

    private void HandleTransaction(float delta, TransactionReason reason)
    {
        if (!initialized) return; // TryInitialize zaten guncel Balance'i okuyacak, kayip yok.

        targetValue += delta;

        float remaining = targetValue - displayedValue;
        currentRate = remaining / AnimDurationSeconds;
        isAnimating = true;

        SpawnFloatingText(delta);
        TriggerPunch();
        PlayTransactionSfx(reason);
    }

    private void AnimateStep()
    {
        HUDController hud = HUDController.Instance;
        if (hud == null) return;

        float dt = Time.unscaledDeltaTime; // ESC duraklatmasinda (Time.timeScale=0) donmasin diye
        displayedValue += currentRate * dt;

        bool reachedOrPassed = (currentRate > 0f && displayedValue >= targetValue)
                             || (currentRate < 0f && displayedValue <= targetValue)
                             || currentRate == 0f;

        if (reachedOrPassed)
        {
            displayedValue = targetValue;
            isAnimating = false;
        }

        hud.SetWalletDisplayValue(displayedValue);
    }

    /// <summary>
    /// T76: 5 adet TMP UI metni, WalletQuotaText'in ebeveyni (TopRightPanel) altinda BIR KEZ
    /// olusturulur, hepsi baslangicta pasif (SetActive(false)). Havuz zaten kuruldu ise
    /// (floatingPool != null) hicbir sey yapmaz - tekrar tekrar cagirilsa bile guvenli.
    /// </summary>
    private void InitFloatingPool(HUDController hud)
    {
        if (floatingPool != null) return;

        if (hud.walletQuotaText == null)
        {
            Debug.LogWarning("[MoneyFeedbackController] walletQuotaText atanmamis, ucan yazi havuzu kurulamadi.");
            return;
        }

        RectTransform parent = hud.walletQuotaText.transform.parent as RectTransform;
        if (parent == null)
        {
            parent = hud.walletQuotaText.rectTransform;
        }

        // T77: Kasa paneli (TopRightPanel) - punch efekti bu RectTransform uzerinde oynar.
        kasaPanelRectTransform = parent;
        kasaPanelRectTransform.localScale = Vector3.one;

        floatingPool = new FloatingTextSlot[FloatingPoolSize];
        for (int i = 0; i < FloatingPoolSize; i++)
        {
            GameObject go = new GameObject("FloatingMoneyText_" + i, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(180f, 40f); // BUGFIX (3. canli test 02.09.2026): buyutulen font (FloatingFontSizeMultiplier) kirpilmasin diye kutu buyutuldu.
            // BUGFIX (2-client canli test, T76 - 1. bulgu): eskiden panelin USTUNE (anchor top, +4)
            // spawn oluyordu - ekran disina tasiyip hicbir cozunurlukte gorunmuyordu. Panelin ALT
            // kenarindan spawn olup yukari suzulecek sekilde degistirildi.
            // BUGFIX (2-client canli test - 2. bulgu, kullanici tarafindan bulundu): -4f/50f ile bile
            // panelin/ekranin ust kenarina cok yakin gidip bazi cozunurluklerde yarim tasiyordu.
            // Baslangic pozisyonu daha asagi (-24f) alindi, FloatingDistance 40f'a dusuruldu -
            // hem panel sinirlari icinde hem ekran ust kenarindan guvenli mesafede kaliyor.
            rt.anchoredPosition = new Vector2(0f, -8f); // BUGFIX (3. canli test, kullanici bulgusu 02.09.2026): eskiden -24f idi, Kasa metninden gereginden uzak basliyordu - Kasa metninin hemen altina yaklastirildi.

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = hud.walletQuotaText.font;
            text.fontSize = hud.walletQuotaText.fontSize * FloatingFontSizeMultiplier; // BUGFIX (3. canli test, kullanici bulgusu 02.09.2026): Kasa metninden %40 buyuk.
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            go.SetActive(false);

            floatingPool[i] = new FloatingTextSlot
            {
                rt = rt,
                text = text,
                canvasGroup = cg,
                active = false,
                elapsed = 0f,
                startAnchoredPos = rt.anchoredPosition
            };
        }
    }

    /// <summary>
    /// Havuzdan round-robin bir slot alir (en fazla FloatingPoolSize eszamanli gosterim -
    /// yeni bir transaction geldiginde en eski slot henuz bitmemis olsa bile devralinir, bu
    /// kabul edilen bir davranistir). Pozitif delta yesil "+", negatif delta kirmizi "-" -
    /// renk tek basina degil, isaret de her zaman yazilir (erisilebilirlik).
    /// </summary>
    private void SpawnFloatingText(float delta)
    {
        if (floatingPool == null || floatingPool.Length == 0) return;

        FloatingTextSlot slot = floatingPool[floatingCursor];
        floatingCursor = (floatingCursor + 1) % floatingPool.Length;

        bool positive = delta >= 0f;
        string sign = positive ? "+" : "-";
        slot.text.text = sign + Mathf.Abs(delta).ToString("0.##") + "$";
        slot.text.color = positive ? new Color(0.15f, 0.55f, 0.15f) : new Color(0.75f, 0.15f, 0.15f);

        slot.rt.anchoredPosition = slot.startAnchoredPos;
        slot.canvasGroup.alpha = 1f;
        slot.elapsed = 0f;
        slot.active = true;
        slot.rt.gameObject.SetActive(true);
    }

    /// <summary>Aktif slotlari her frame yukari kaydirip fade-out yapar, sure dolunca havuza geri
    /// (SetActive(false)) doner. Time.unscaledDeltaTime kullanilir (ESC duraklatmasinda donmasin).</summary>
    private void UpdateFloatingTexts()
    {
        if (floatingPool == null) return;

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < floatingPool.Length; i++)
        {
            FloatingTextSlot slot = floatingPool[i];
            if (!slot.active) continue;

            slot.elapsed += dt;
            float t = Mathf.Clamp01(slot.elapsed / FloatingDuration);

            // BUGFIX (3. bulgu, kullanici tarafindan bulundu): eskiden YUKARI (+=) kayiyordu,
            // bu da HUD panelinin USTUNE/icine giriyordu - hem dikkat cekmiyordu hem ekran ust
            // kenarindan tasma riski vardi. Artik ASAGI (-=) kayiyor - panelin altinda kalip
            // HUD'dan uzaklasiyor, hem daha dikkat cekici hem tasma riski yok.
            Vector2 pos = slot.startAnchoredPos;
            pos.y -= FloatingDistance * t;
            slot.rt.anchoredPosition = pos;
            slot.canvasGroup.alpha = 1f - t;

            if (t >= 1f)
            {
                slot.active = false;
                slot.rt.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>T77: Yeni bir transaction geldiginde punch'i basa sarar - eger onceki punch
    /// henuz bitmemisse bile bozulmadan (olcek 1'in altina dusmeden) yeniden baslar, cunku
    /// formul t=0 aninda daima scale=1 uretir (Mathf.Sin(0)=0).</summary>
    private void TriggerPunch()
    {
        isPunching = true;
        punchElapsed = 0f;
    }

    /// <summary>T77: Kasa panelini kisaca %110'a cikarip normale dondurur (~0.15sn toplam,
    /// sin egrisi: t=0 -> 1.0, t=yarisi -> 1.10 (tepe), t=1 -> 1.0). Olcek asla 1'in altina
    /// dusmez (Sin(pi*t) >= 0, t in [0,1]). Time.unscaledDeltaTime kullanilir (ESC'te donmaz).</summary>
    private void UpdatePunch()
    {
        if (!isPunching || kasaPanelRectTransform == null) return;

        float dt = Time.unscaledDeltaTime;
        punchElapsed += dt;

        float t = Mathf.Clamp01(punchElapsed / PunchDurationSeconds);
        float scale = 1f + (PunchScale - 1f) * Mathf.Sin(Mathf.PI * t);
        kasaPanelRectTransform.localScale = new Vector3(scale, scale, 1f);

        if (t >= 1f)
        {
            isPunching = false;
            kasaPanelRectTransform.localScale = Vector3.one;
        }
    }

    /// <summary>T78: reason'a gore dogru klibi secip, 0.1sn cooldown ile AudioManager
    /// uzerinden PlayOneShot calar. Cooldown SADECE sesi atlar - gorsel feedback (T75-77)
    /// her transaction'da her zaman calisir, bu metoddan etkilenmez.</summary>
    private void PlayTransactionSfx(TransactionReason reason)
    {
        if (AudioManager.Instance == null) return;

        AudioClip clip = GetClipForReason(reason);
        if (clip == null) return;

        float now = Time.unscaledTime;
        if (lastSfxPlayTime.TryGetValue(reason, out float lastTime) && now - lastTime < SfxCooldownSeconds)
        {
            return; // cooldown - sadece ses atlaniyor
        }

        AudioManager.Instance.PlaySFX(clip);
        lastSfxPlayTime[reason] = now;
    }

    private AudioClip GetClipForReason(TransactionReason reason)
    {
        switch (reason)
        {
            case TransactionReason.Satis: return coinGainClip;
            case TransactionReason.Harcama: return coinLossClip;
            case TransactionReason.Kira: return rentDeductionClip;
            default: return null;
        }
    }
}
