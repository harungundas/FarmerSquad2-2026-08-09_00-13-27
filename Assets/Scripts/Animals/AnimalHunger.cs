using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Hayvanin aclik durumunu yonetir. AnimalBase.hunger (0-100) degerini zamanla azaltir.
/// Hunger 0'a inince satis degerine %40 ceza flag'i (IsUnderfed) aktif olur.
/// Karakter-bagimli besleme suresi/aclik azalma hizi (Kadin -%30) T20'de HayCarryState ile baglanacak,
/// bu script SADECE temel sayaci saglar (TASKS.md T14 kapsami).
/// </summary>
[RequireComponent(typeof(AnimalBase))]
public class AnimalHunger : NetworkBehaviour
{
    [Header("Aclik Ayarlari")]
    [Tooltip("Saniyede kac aclik puani azalir. Sayisal deger GDD'de verilmemis (varsayimsal) - playtest'te ayarlanacak.")]
    public float hungerDecayRate = 1f;

    [Header("Ceza Ayarlari")]
    [Tooltip("Ac hayvanin satis fiyatina uygulanacak carpan (GDD: %40 ceza -> 0.6x)")]
    public float underfedSellMultiplier = 0.6f;

    [Header("Runtime State (readonly)")]
    [SerializeField] private bool isUnderfed = false;
    public bool IsUnderfed => isUnderfed;

    private AnimalBase animalBase;

    private void Awake()
    {
        animalBase = GetComponent<AnimalBase>();
    }

    private void Update()
    {
        // Host authoritative: sadece server hunger'i degistirir, client'lar NetworkBehaviour senkronu bekler.
        // NOT: hunger su an plain float (NetworkVariable degil) - tek-oyunculu/editor test icin yeterli,
        // coklu-client senkron gerekiyorsa ileride NetworkVariable<float>'a tasinmasi gerekebilir (T14 kapsami disi).
        if (!IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return;
        }

        if (animalBase.hunger > 0f)
        {
            animalBase.hunger = Mathf.Max(0f, animalBase.hunger - hungerDecayRate * Time.deltaTime);
        }

        isUnderfed = animalBase.hunger <= 0f;
    }

    /// <summary>
    /// Hayvani doyurur, aclik 100'e resetlenir. HayCarryState (T18) tarafindan cagrilacak.
    /// </summary>
    public void Feed()
    {
        animalBase.hunger = 100f;
        isUnderfed = false;
    }

    /// <summary>
    /// Satis fiyatina uygulanacak carpan. Ac ise ceza carpani, degilse 1x.
    /// DeliveryResolver (T27) satis hesaplamasinda bunu kullanacak.
    /// </summary>
    public float GetSellPriceMultiplier()
    {
        return isUnderfed ? underfedSellMultiplier : 1f;
    }
}
