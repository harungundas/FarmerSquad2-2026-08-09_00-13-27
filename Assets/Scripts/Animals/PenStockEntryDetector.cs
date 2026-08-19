using UnityEngine;
using Unity.Netcode;

/// <summary>
/// `citler` objesine eklenir (PenManager ile ayni GameObject). citler uzerinde zaten var olan
/// isTrigger=true BoxCollider'i (PenManager.bounds'un kullandigi trigger-OLMAYAN BoxCollider'dan
/// FARKLI, ikinci bir BoxCollider) kullanarak, agila FIZIKSEL OLARAK giren hayvanlari yakalar.
///
/// Amac (NegotiationManager.SpawnAlimAnimals sinif yorumundaki eksik parca): Alim siparisi
/// kabul edilince hayvanlar teslimat alaninda (hayvan_birakma_alani) spawn olur ama PenManager
/// stoguna HENUZ eklenmez - "Agila tasima/stok girisi oyuncunun elle tasimasiyla olacak" diye
/// not edilmisti. Bu script tam o adimi tamamlar: oyuncu CarryController ile hayvani sirtlayip
/// agilin icine tasiyip BIRAKTIGINDA (E tusu, DropAll -> IsBeingCarried=false), hayvanin
/// collider'i bu trigger'a girer, PenManager.AddStock cagrilir, hayvan artik "agilin bir
/// parcasi" sayilir (ownerPen atanir, AnimalIdleWander devreye girer).
///
/// SADECE tasinmiyorken (IsBeingCarried=false) sayim yapilir - yoksa oyuncu hayvani sirtinda
/// tasiyip agilin icinden sadece GECERSE (birakmadan), erken/yanlis sayim olurdu.
///
/// countedInStock bayragi (AnimalBase) ile TEK SEFERLIK sayim garantisi saglanir - hayvan
/// agila girip cikip tekrar girerse (ör. AnimalIdleWander veya oyuncu tekrar tasirsa) ikinci
/// kez sayilmaz.
///
/// Baslangic stogu (PenManager.SpawnInitialStock) zaten kendi spawn'inda countedInStock=true
/// isaretliyor - bu yuzden bu trigger'a onlar icin GEREK YOK ama zararsizdir (yine de
/// countedInStock kontrolu ikinci sayimi engeller).
///
/// Ag modeli: CarryController'in kendi yorumunda belirtildigi gibi tasima su an SADECE LOCAL
/// bir islem (network-authoritative degil) - bu yuzden burada da ayni basitlik korunuyor,
/// stok ekleme yalnizca (varsa) server'da/host'ta calisir, digger client'larda sadece local
/// gorsel/log etkisi olur.
/// </summary>
[RequireComponent(typeof(PenManager))]
public class PenStockEntryDetector : MonoBehaviour
{
    private PenManager penManager;

    private void Awake()
    {
        penManager = GetComponent<PenManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        AnimalBase animal = other.GetComponentInParent<AnimalBase>();
        if (animal == null) return;
        if (animal.IsBeingCarried) return; // oyuncu hala sirtinda tasiyor, henuz birakilmadi
        if (animal.countedInStock) return; // zaten sayilmis
        if (animal.animalData == null)
        {
            Debug.LogWarning("[PenStockEntryDetector] " + animal.gameObject.name + " icin animalData atanmamis, stoga eklenemedi.");
            return;
        }

        bool isServerAuthoritative = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        if (!isServerAuthoritative) return;

        animal.countedInStock = true;
        animal.ownerPen = transform;
        penManager.AddStock(animal.animalData.species, 1);

        Debug.Log("[PenStockEntryDetector] Agila eklendi: " + animal.animalData.species + " (yeni stok=" + penManager.GetStock(animal.animalData.species) + ")");
    }
}
