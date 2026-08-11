using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// hayvan_birakma_alani objesindeki mevcut BoxCollider (trigger) uzerinden,
/// teslimat alaninin icinde hangi hayvanlarin (AnimalBase) bulundugunu takip eder.
/// Beklenen siparis turu henuz yok (Faz 5'te OrderData ile baglanacak) - simdilik
/// sadece icerideki hayvanlari tespit eder (Debug.Log) ve basit bir renk feedback'i
/// gosterir (yesil = alanda en az 1 hayvan var, orijinal renk = bos).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DeliveryZoneDetector : MonoBehaviour
{
    [Header("Feedback")]
    public Color occupiedColor = Color.green;

    private readonly List<AnimalBase> animalsInside = new List<AnimalBase>();
    private Renderer zoneRenderer;
    private Color emptyColor;

    /// <summary>Su anda alanin icindeki hayvanlarin salt-okunur listesi (T27 DeliveryResolver bunu okuyacak).</summary>
    public IReadOnlyList<AnimalBase> AnimalsInside => animalsInside;

    private void Awake()
    {
        zoneRenderer = GetComponent<Renderer>();
        if (zoneRenderer != null)
        {
            emptyColor = zoneRenderer.material.color;
        }
    }

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

        UpdateFeedback();
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

        UpdateFeedback();
    }

    private void UpdateFeedback()
    {
        if (zoneRenderer == null) return;
        zoneRenderer.material.color = animalsInside.Count > 0 ? occupiedColor : emptyColor;
    }
}
