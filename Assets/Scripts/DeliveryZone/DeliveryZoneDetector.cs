using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// hayvan_birakma_alani objesindeki mevcut BoxCollider (trigger) uzerinden,
/// teslimat alaninin icinde hangi hayvanlarin (AnimalBase) bulundugunu takip eder.
/// Beklenen siparis turu henuz yok (Faz 5'te OrderData ile baglanacak) - simdilik
/// sadece icerideki hayvanlari tespit eder (Debug.Log).
///
/// DEGISIKLIK (kullanici karari): dolu/bos renk feedback'i (yesil/orijinal) KALDIRILDI -
/// dogru/yanlis teslimat gorseli ileride ayri bir sistemde (DeliveryResolver, T27 - henuz
/// yazilmadi) ele alinacak. Zone materyali artik sabit kalir, doluluk durumuna gore
/// degismez. Materyal: Assets/Materials/DeliveryZone_Overlay.mat (PP_Water'dan kopyalanip
/// ayristirildi - orijinal Nature Pack su materyaline dokunulmadi, alpha=0.18 ile icerideki
/// hayvan daha net gorunsun diye ekstra seffaf yapildi).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DeliveryZoneDetector : MonoBehaviour
{
    private readonly List<AnimalBase> animalsInside = new List<AnimalBase>();

    /// <summary>Su anda alanin icindeki hayvanlarin salt-okunur listesi (DeliveryResolver bunu okuyacak).</summary>
    public IReadOnlyList<AnimalBase> AnimalsInside => animalsInside;

    private void OnTriggerEnter(Collider other)
    {
        AnimalBase animal = other.GetComponent<AnimalBase>();
        if (animal == null) return;

        if (!animalsInside.Contains(animal))
        {
            animalsInside.Add(animal);
            AnimalSpecies species = animal.animalData != null ? animal.animalData.species : default;
            Debug.Log("[DeliveryZoneDetector] Teslimat alanina girdi: " + species);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        AnimalBase animal = other.GetComponent<AnimalBase>();
        if (animal == null) return;

        if (animalsInside.Remove(animal))
        {
            AnimalSpecies species = animal.animalData != null ? animal.animalData.species : default;
            Debug.Log("[DeliveryZoneDetector] Teslimat alanindan cikti: " + species);
        }
    }


/// <summary>
    /// NegotiationManager (T27 duzeltmesi) basarili teslimat sonrasi hayvani Destroy/Despawn
    /// etmeden ONCE bunu cagirir - OnTriggerExit, obje yok edildiginde HER ZAMAN guvenilir
    /// tetiklenmeyebildigi icin listeden elle cikartma garantisi saglar.
    /// </summary>
    public void ClearAnimal(AnimalBase animal)
    {
        animalsInside.Remove(animal);
    }
}
