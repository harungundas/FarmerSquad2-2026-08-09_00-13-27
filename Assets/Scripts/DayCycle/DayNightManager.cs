using UnityEngine;

/// <summary>
/// KULLANICI BUG RAPORU DUZELTMESI (bu oturumda YENIDEN YAZILDI): Eskiden gun basina TEK bir
/// statik gokyuzu (rastgele TAMAMEN gunduz VEYA TAMAMEN gece) seciliyordu - kullanici raporu:
/// "ilk gun hep gunesliydi, ikinci gun komple geceydi" bu ISTENEN davranis DEGILDI (GDD/PLAN
/// karari: gercek bir gunduz->gece GECISI olmaliydi).
///
/// YENI TASARIM: gokyuzu artik GUN ICINDE ZAMANLA ilerler. daySkyboxes (sabah->ogle->aksamustu
/// sirasiyla Inspector'da dizilmis olmali) + nightSkyboxes (alacakaranlik->gece sirasiyla) TEK
/// bir sirali diziye (fullSequence) birlestirilir. DayCycleManager.Timer'in customerWindowSeconds
/// penceresi boyunca (240/180sn'den 0'a) azalmasiyla orantili olarak bu dizide ilerlenir - gun
/// basinda (Timer=customerWindowSeconds, ilerleme=0) ilk (sabah) gokyuzunden baslanir, musteri
/// penceresi kapanmaya yaklastikca (ilerleme->1) son (gece) gokyuzune dogru gidilir. Directional
/// Light de ayni ilerlemeye gore SUREKLI (Lerp ile) gunduz<->gece degerleri arasinda gecis yapar -
/// ani bir sicrama degil, kademeli bir aydinlanma degisimi.
///
/// Serbest Mod (musteri penceresi bitince Timer=0'da durur) basladiginda gokyuzu son karede
/// (gece) SABIT kalir - sonraki gun basladiginda (DayCycleManager.StartDayServer, Timer sifirdan
/// yeniden customerWindowSeconds'a donunce) ilerleme otomatik 0'a duser ve dizi bastan (sabah)
/// baslar. Sadece GORSEL bir sistemdir, NetworkBehaviour DEGIL - CurrentDay/Timer zaten
/// NetworkVariable (host-authoritative, Everyone read izniyle) oldugu icin her client kendi
/// tarafinda Update() ile bunlari OKUYARAK ayni sonuca ulasir, ekstra senkron gerekmez.
/// </summary>
public class DayNightManager : MonoBehaviour
{
    [Header("Baglantilar")]
    public DayCycleManager dayCycleManager;
    public Light sunLight;

    [Header("Gokyuzu Sirasi - Gunduz (Assets/SkyBox/Fantasy Skybox FREE/Cubemaps/Classic)")]
    [Tooltip("Sabah -> ogle -> aksamustu sirasiyla dizilmeli - gun bu sirayla ilerler.")]
    public Material[] daySkyboxes;

    [Header("Gokyuzu Sirasi - Gece")]
    [Tooltip("Alacakaranlik -> gece sirasiyla dizilmeli - daySkyboxes'tan SONRA oynatilir.")]
    public Material[] nightSkyboxes;

    [Header("Isik Ayarlari - Gunduz")]
    public float dayLightIntensity = 2f;
    public Color dayLightColor = Color.white;

    [Header("Isik Ayarlari - Gece")]
    public float nightLightIntensity = 0.15f;
    public Color nightLightColor = new Color(0.55f, 0.65f, 0.9f);

    private Material[] fullSequence;
    private int lastAppliedIndex = -1;

    private void OnEnable()
    {
        BuildSequence();

        if (dayCycleManager == null)
        {
            Debug.LogWarning("[DayNightManager] dayCycleManager atanmamis.");
        }

        lastAppliedIndex = -1; // ilk Update()'te kesin bir uygulama yapilsin diye
    }

    private void BuildSequence()
    {
        int dayCount = daySkyboxes != null ? daySkyboxes.Length : 0;
        int nightCount = nightSkyboxes != null ? nightSkyboxes.Length : 0;
        fullSequence = new Material[dayCount + nightCount];
        for (int i = 0; i < dayCount; i++) fullSequence[i] = daySkyboxes[i];
        for (int i = 0; i < nightCount; i++) fullSequence[dayCount + i] = nightSkyboxes[i];
    }

    private void Update()
    {
        if (dayCycleManager == null) return;
        ApplyForProgress(dayCycleManager.CurrentDay.Value, GetDayProgress());
    }

    /// <summary>0 (gun basi/sabah) - 1 (musteri penceresi bitti/gece) arasi ilerleme oranı.</summary>
    private float GetDayProgress()
    {
        if (dayCycleManager.customerWindowSeconds <= 0f) return 0f;
        float elapsed = dayCycleManager.customerWindowSeconds - dayCycleManager.Timer.Value;
        return Mathf.Clamp01(elapsed / dayCycleManager.customerWindowSeconds);
    }

    private void ApplyForProgress(int day, float progress)
    {
        if (fullSequence == null || fullSequence.Length == 0)
        {
            Debug.LogWarning("[DayNightManager] daySkyboxes/nightSkyboxes atanmamis, gokyuzu degistirilemedi.");
            return;
        }

        if (sunLight == null) sunLight = RenderSettings.sun;

        // Isigi HER FRAME kademeli olarak gunduz<->gece degerleri arasinda interpole et (ani
        // sicrama yerine surekli/yumusak gecis).
        if (sunLight != null)
        {
            sunLight.intensity = Mathf.Lerp(dayLightIntensity, nightLightIntensity, progress);
            sunLight.color = Color.Lerp(dayLightColor, nightLightColor, progress);
        }

        int index = Mathf.Clamp(Mathf.FloorToInt(progress * fullSequence.Length), 0, fullSequence.Length - 1);
        if (index == lastAppliedIndex) return; // gokyuzu materyali degismediyse tekrar atama yok

        lastAppliedIndex = index;

        var chosen = fullSequence[index];
        if (chosen == null) return;

        RenderSettings.skybox = chosen;
        DynamicGI.UpdateEnvironment();

        Debug.Log("[DayNightManager] Gun " + day + " ilerleme=%" + (progress * 100f).ToString("F0") + " gokyuzu: " + chosen.name);
    }
}
