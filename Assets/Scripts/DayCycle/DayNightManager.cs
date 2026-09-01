using UnityEngine;

/// <summary>
/// KULLANICI BUG RAPORU DUZELTMESI (bu oturumda tekrar duzeltildi): Onceki surum, gokyuzu
/// gecisini SADECE dizi eleman SAYISINA gore boluyordu (6 gunduz + 3 gece = 9 parca) - bu da
/// gece gokyuzune sadece %66.7 ilerlemede (6/9) gecilmesine sebep oluyordu. Ama HUDController.cs
/// oyun-ici saati AYNI ilerlemeyi 06:00->18:00 araligina yayarak gosteriyor - %66.7 ilerleme TAM
/// OLARAK saat 14:00'e denk geliyordu ("daha saat 14:00 olunca hava kararmaya basliyor" bug
/// raporu buydu). Kok neden: gokyuzu gecisi SAATE degil, dizi UZUNLUGUNA gore hesaplaniyordu.
///
/// YENI TASARIM: gokyuzu ve isik artik DOGRUDAN oyun-ici SAATE (HUDController.cs'teki AYNI
/// 06:00->18:00 formulu) gore karar veriliyor:
///   - duskStartHour'a KADAR (varsayilan 17:00) tamamen GUNDUZ - isik sabit parlak, sadece
///     daySkyboxes dizisinde (sabah->aksamustu) ilerlenir.
///   - duskStartHour'dan SONRA (son ~1 saat) aksam/gece baslar - isik bu dar pencerede kademeli
///     kararir, nightSkyboxes dizisinde (alacakaranlik->gece) ilerlenir.
/// Boylece "ne zaman kararmaya baslar" dogrudan ayarlanabilir bir SAAT (duskStartHour) ile
/// kontrol edilir, dizi uzunluklarindan tamamen BAGIMSIZ hale gelir.
///
/// Serbest Mod (musteri penceresi bitince Timer=0'da durur, saat=18:00'de kalir) basladiginda
/// gokyuzu/isik en karanlik karede SABIT kalir - sonraki gun basladiginda (Timer sifirdan
/// yeniden customerWindowSeconds'a donunce) saat otomatik 06:00'ya duser, dizi bastan baslar.
/// Sadece GORSEL bir sistemdir, NetworkBehaviour DEGIL - CurrentDay/Timer zaten NetworkVariable
/// (host-authoritative, Everyone read izniyle) oldugu icin her client kendi tarafinda Update()
/// ile bunlari OKUYARAK ayni sonuca ulasir, ekstra senkron gerekmez.
/// </summary>
public class DayNightManager : MonoBehaviour
{
    [Header("Baglantilar")]
    public DayCycleManager dayCycleManager;
    public Light sunLight;

    [Header("Gokyuzu Sirasi - Gunduz (Assets/SkyBox/Fantasy Skybox FREE/Cubemaps/Classic)")]
    [Tooltip("Sabah -> ogle -> aksamustu sirasiyla dizilmeli - saat 06:00'dan duskStartHour'a kadar bu sirayla ilerlenir.")]
    public Material[] daySkyboxes;

    [Header("Gokyuzu Sirasi - Gece")]
    [Tooltip("Alacakaranlik -> gece sirasiyla dizilmeli - SADECE duskStartHour'dan sonra (saat 18:00'e kadar) oynatilir.")]
    public Material[] nightSkyboxes;

    [Header("Saat Esigi (HUDController.cs'teki 06:00-18:00 saatiyle DOGRUDAN eslesir)")]
    [Tooltip("Bu saatten ONCESI TAMAMEN gunduz. Bu saatten 18:00'e kadar olan dar pencerede aksam/gece gecisi yasanir. Kullanici raporuna gore varsayilan 17:00 (sadece son 1 saat kararir) - cok erken kararma sikayeti icin buyutulebilir (orn. 17.5).")]
    public float duskStartHour = 17f;

    [Header("Isik Ayarlari - Gunduz")]
    public float dayLightIntensity = 2f;
    public Color dayLightColor = Color.white;

    [Header("Isik Ayarlari - Gece")]
    public float nightLightIntensity = 0.15f;
    public Color nightLightColor = new Color(0.55f, 0.65f, 0.9f);

    private const float DayStartHour = 6f;
    private const float DayEndHour = 18f;

    private Material lastAppliedMaterial = null;

    private void OnEnable()
    {
        if (dayCycleManager == null)
        {
            Debug.LogWarning("[DayNightManager] dayCycleManager atanmamis.");
        }

        lastAppliedMaterial = null; // ilk Update()'te kesin bir uygulama yapilsin diye
    }

    private void Update()
    {
        if (dayCycleManager == null) return;
        ApplyForHour(dayCycleManager.CurrentDay.Value, GetGameHour());
    }

    /// <summary>HUDController.cs'teki saat hesabiyla BIREBIR AYNI formul (06:00 -> 18:00).</summary>
    private float GetGameHour()
    {
        if (dayCycleManager.customerWindowSeconds <= 0f) return DayStartHour;
        float elapsed = dayCycleManager.customerWindowSeconds - dayCycleManager.Timer.Value;
        float progress = Mathf.Clamp01(elapsed / dayCycleManager.customerWindowSeconds);
        return DayStartHour + progress * (DayEndHour - DayStartHour);
    }

    private void ApplyForHour(int day, float hour)
    {
        if (sunLight == null) sunLight = RenderSettings.sun;

        bool isDuskOrNight = hour >= duskStartHour;
        float lightT; // 0 = tam gunduz aydinligi, 1 = tam gece karanligi
        Material chosen = null;

        if (!isDuskOrNight)
        {
            lightT = 0f; // duskStartHour'a kadar isik sabit gunduz parlakliginda kalir
            if (daySkyboxes != null && daySkyboxes.Length > 0)
            {
                float dayFraction = Mathf.Clamp01((hour - DayStartHour) / Mathf.Max(0.01f, duskStartHour - DayStartHour));
                int index = Mathf.Clamp(Mathf.FloorToInt(dayFraction * daySkyboxes.Length), 0, daySkyboxes.Length - 1);
                chosen = daySkyboxes[index];
            }
        }
        else
        {
            float duskFraction = Mathf.Clamp01((hour - duskStartHour) / Mathf.Max(0.01f, DayEndHour - duskStartHour));
            lightT = duskFraction; // sadece bu dar pencerede kademeli kararir
            if (nightSkyboxes != null && nightSkyboxes.Length > 0)
            {
                int index = Mathf.Clamp(Mathf.FloorToInt(duskFraction * nightSkyboxes.Length), 0, nightSkyboxes.Length - 1);
                chosen = nightSkyboxes[index];
            }
        }

        if (sunLight != null)
        {
            sunLight.intensity = Mathf.Lerp(dayLightIntensity, nightLightIntensity, lightT);
            sunLight.color = Color.Lerp(dayLightColor, nightLightColor, lightT);
        }

        if (chosen == null || chosen == lastAppliedMaterial) return; // materyal degismediyse tekrar atama yok

        lastAppliedMaterial = chosen;
        RenderSettings.skybox = chosen;
        DynamicGI.UpdateEnvironment();

        Debug.Log("[DayNightManager] Gun " + day + " saat=" + hour.ToString("00.0") + " gokyuzu: " + chosen.name);
    }
}
