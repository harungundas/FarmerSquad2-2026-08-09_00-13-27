using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Her hayvan instance'ina eklenir. Hangi ağıla ait oldugunu (ownerPen) ve
/// mevcut acllik degerini (hunger) tutar. Tur/fiyat/agirlik bilgisi AnimalData'dan okunur.
/// </summary>
public class AnimalBase : NetworkBehaviour
{
    [Header("Data")]
    public AnimalData animalData;

    [Header("Runtime State")]
    public Transform ownerPen;
    public float hunger = 100f;

    [Header("Agil Stok Durumu (PenStockEntryDetector tarafindan yazilir)")]
    [Tooltip("true ise bu hayvan zaten PenManager stok sayacina eklenmis demektir - tekrar agila girip cikinca ikinci kez sayilmasin diye.")]
    public bool countedInStock = false;

    [Header("Tasima Durumu (CarryController tarafindan yazilir)")]
    [Tooltip("true iken bu hayvan bir oyuncu tarafindan sirtlanmis demektir - AnimalIdleWander bu\n    sure boyunca hareketini DURDURUR (aksi halde tasinirken bile kendi dolasma hedefine\n    gitmeye calisir, bu da CarryController'in parent-offset'ini her frame ezerdi).")]
    public bool IsBeingCarried = false;

    // BUG DUZELTMESI: Koyun ve Keci prefablarinda (UrsaAnimation paketi) Animator.applyRootMotion
    // ACIK geliyordu. Hayvanlarin TUM hareketi (AnimalIdleWander + CarryController) elle/scripted
    // yapiliyor (ARCHITECTURE.md: "NavMesh YOK") - Animator'in kendi root-motion'i buna paralel
    // olarak her frame transform.position'i degistirip scripted konumun ustune biniyordu. Sonuc:
    // hayvan tasinirken (CarryController.PickUp() pozisyonu SADECE BIR KERE ayarliyor) birkac
    // saniye icinde animasyon klibindeki gomulu hareket birikip hayvani haritada uzak bir noktaya
    // suruklu-yordu ("elde gorunmuyor, haritada isinlanmis gibi" bug'i). DropAll() bunu maskeliyordu
    // cunku pozisyonu her zaman oyuncunun O ANKI konumundan sifirdan yeniden hesapliyor.
    // Kalici cozum: her hayvan icin Animator varsa applyRootMotion'i acilista zorla kapat.
    private void Awake()
    {
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }
}
