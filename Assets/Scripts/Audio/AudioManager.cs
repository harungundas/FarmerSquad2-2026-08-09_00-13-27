using UnityEngine;

/// <summary>
/// Merkezi ses yoneticisi (kullanici talebi): oyun muzigi + SFX'i AYRI hacim kanallariyla
/// yonetir. Tek sahne mimarisi (SampleScene - ana menu/lobi/oyun hepsi ayni sahnede panel
/// olarak) oldugu icin DontDestroyOnLoad'a gerek yok, sahne hic yeniden yuklenmiyor.
///
/// Muzik: Start()'ta otomatik baslar ve sonsuz loop eder (kullanici: "sürekli çalmaya
/// devam edecek"). SFX: PlaySFX(clip) ile PlayOneShot - ayni anda birden fazla SFX ust
/// uste calabilir, muzigi kesmez.
///
/// Hacimler PlayerPrefs'te kalici ("MusicVolume","SFXVolume", 0-1 araligi, varsayilan 1).
/// SettingsUI.cs (Ses sekmesi) bu sinifin SetMusicVolume/SetSFXVolume metodlarini cagirir.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string MusicVolumeKey = "MusicVolume";
    private const string SFXVolumeKey = "SFXVolume";

    [Header("Kaynaklar")]
    [Tooltip("Surekli calan oyun muzigi (loop).")]
    public AudioClip musicClip;
    [Tooltip("Standa giris yapan aracin bir kez calacagi korna sesi (CustomerVehicle.cs bunu okur).")]
    public AudioClip carHornClip;

    private AudioSource musicSource;
    private AudioSource sfxSource;

    public float MusicVolume { get; private set; } = 1f;
    public float SFXVolume { get; private set; } = 1f;

    private void Awake()
    {
        // Kullanici birden fazla AudioManager instance'i olusturursa (orn. sahneyi yanlislikla
        // tekrar acarsa) fazlalari yok et - tek ses kaynagi garanti edilsin.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.clip = musicClip;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        SFXVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);
        musicSource.volume = MusicVolume;
    }

    private void Start()
    {
        if (musicClip != null)
        {
            musicSource.Play();
        }
    }

    /// <summary>Herhangi bir SFX'i bir kez calar (PlayOneShot - musigi kesmez, ust uste binebilir).</summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, SFXVolume);
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        if (musicSource != null) musicSource.volume = MusicVolume;
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
    }

    public void SetSFXVolume(float value)
    {
        SFXVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SFXVolumeKey, SFXVolume);
    }
}
