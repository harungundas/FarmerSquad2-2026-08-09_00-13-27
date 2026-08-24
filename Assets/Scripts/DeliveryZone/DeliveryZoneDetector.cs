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
    private BoxCollider zoneCollider;
    private readonly List<AnimalBase> liveAnimalsInside = new List<AnimalBase>();

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider>();
    }

    /// <summary>
    /// KULLANICI BUG RAPORU DUZELTMESI: eskiden OnTriggerEnter/Exit ile ADIM ADIM tutulan bir
    /// liste kullaniliyordu. CarryController bir hayvani sirtlarken TUM Collider'larini devre
    /// disi birakiyor (col.enabled=false, bkz. CarryController.PickUp) - hayvan bu sekilde
    /// tasinip alan DISINA cikarildiginda Collider disable/enable dongusu OnTriggerExit'i
    /// GUVENILIR sekilde tetiklemiyordu, bu yuzden hayvan haritada tamamen baska bir yerde
    /// olsa bile eski liste onu hala "icerde" saniyordu (kullanici raporu: "alanda hayvan
    /// yokken siparis onay ekraninda 1-2 hayvan gorunuyordu").
    /// DUZELTME: artik hicbir kalici liste TUTULMUYOR - her okumada (AnimalsInside) alanin
    /// GERCEK fiziksel icerigi Physics.OverlapBox ile SIFIRDAN hesaplaniyor. Tasinmakta olan
    /// (IsBeingCarried=true, collider'lari zaten kapali) hayvanlar da dogal olarak sonuca
    /// girmiyor (kapali collider + fiziksel sorgu onlari hic gormez), boylece "unutulmus
    /// kayit" artik yapisal olarak MUMKUN DEGIL.
    /// </summary>
    public IReadOnlyList<AnimalBase> AnimalsInside
    {
        get
        {
            liveAnimalsInside.Clear();

            if (zoneCollider == null) zoneCollider = GetComponent<BoxCollider>();
            if (zoneCollider == null) return liveAnimalsInside;

            Bounds b = zoneCollider.bounds;
            Collider[] hits = Physics.OverlapBox(b.center, b.extents, Quaternion.identity);

            foreach (var hit in hits)
            {
                var animal = hit.GetComponentInParent<AnimalBase>();
                if (animal == null) continue;
                if (animal.IsBeingCarried) continue; // guvenlik agi: tasiniyorsa sayilmamali
                if (liveAnimalsInside.Contains(animal)) continue; // ayni hayvanin birden fazla collider'i olabilir

                liveAnimalsInside.Add(animal);
            }

            return liveAnimalsInside;
        }
    }

    /// <summary>
    /// ARTIK NO-OP: liste kalici tutulmadigi, her okumada canli hesaplandigi icin elle
    /// cikartmaya gerek yok. NegotiationManager.RemoveDeliveredAnimals gibi eski cagiran
    /// kodlar bozulmasin diye metod GERIYE DONUK UYUMLULUK icin bos birakildi.
    /// </summary>
    public void ClearAnimal(AnimalBase animal)
    {
        // no-op (bkz. yukaridaki sinif yorumu)
    }
}
