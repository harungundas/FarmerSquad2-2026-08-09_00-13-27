using UnityEngine;
using TMPro;

/// <summary>
/// Merkezi etkilesim gosterge sistemi. HUDController.Instance deseniyle AYNI yaklasim:
/// tek bir static Instance, farkli scriptler (CarryController, HayCarryState, HayPile,
/// StandInteraction) kendi OverlapSphere/trigger tespitlerini YAPMAYA DEVAM EDER, sadece
/// bir hedef bulduklarinda Show(...)/ShowTextOnly(...) cagirir, bulamadiklarinda Hide().
///
/// Iki parca:
/// 1) Ekran alt-orta prompt metni (orn. "E - Basili Tut - Yem Al", "F - Bas - Kasayi Kullan").
/// 2) Hedefin ALTINDA duran dunya-uzayinda bir halka/glow decal (ayri bir GameObject,
///    ringObject alanina Inspector'dan atanir - InteractionRingGlow.shader kullanir).
///    Ring, hedefe PARENT edilmez (karakter/hayvan scale'leri farkli/tutarsiz oldugu icin -
///    bkz. T51 notlari) - her frame world-space pozisyonu SnapToGround benzeri bir raycast
///    ile zemine hizalanip kopyalanir.
///
/// BUG DUZELTMESI (kullanici raporu: "ne glow ne interact yazisi cikiyor"): eskiden "son
/// cagiran kazanir" kurali vardi - ama Show()/Hide() FARKLI GameObject'lerdeki scriptlerden
/// (orn. kasa uzerindeki StandInteraction VE oyuncu uzerindeki CarryController) AYNI FRAME
/// icinde cagrilabiliyordu, ve Unity'de farkli GameObject'lerin Update() sirasi GARANTILI
/// DEGIL. Sonuc: StandInteraction o frame Show() cagirsa bile, CarryController'in AYNI
/// FRAME'DE (yakinda tasinabilir hayvan olmadigi icin) cagirdigi Hide() bunu SESSIZCE
/// EZIYORDU - prompt/glow hicbir zaman gorunmuyordu.
///
/// DUZELTME: frame-bazli "Show kazanir" kurali eklendi. Show()/ShowTextOnly() cagrildiginda
/// o anki Time.frameCount 'lastShowFrame' alanina kaydedilir. Hide() cagrildiginda, eger
/// AYNI FRAME icinde zaten bir Show()/ShowTextOnly() olduysa Hide() YOK SAYILIR (bir sonraki
/// frame'de, kimse Show() cagirmazsa, Hide() normal calismaya devam eder). Boylece ayni
/// frame icinde birden fazla script cagirsa bile GOSTERMEK her zaman GIZLEMEYE karsi kazanir.
/// </summary>
public class InteractionIndicator : MonoBehaviour
{
    public static InteractionIndicator Instance;

    [Header("Ekran Prompt (alt-orta)")]
    public GameObject promptPanel;
    public TextMeshProUGUI promptText;

    [Header("Halka/Glow Decal (dunya-uzayinda, InteractionRingGlow shader'i kullanir)")]
    public GameObject ringObject;
    [Tooltip("Ring'in zeminden yuksekligi (raycast ile bulunan zemin Y'sine eklenir) - halka zemine gomulmesin diye.")]
    public float ringGroundOffset = 0.03f;
    [Tooltip("Ring bulunamayan durumlarda (raycast bos) dogrudan hedefin position.y'sini kullan.")]
    public float ringFallbackYOffset = 0f;

    private Transform currentTarget;
    private bool ringActive = false;

    /// <summary>BUG DUZELTMESI: bu frame'de (Time.frameCount) en son Show()/ShowTextOnly()
    /// cagrilan frame numarasi. Hide() bu frame numarasiyla ESLESIRSE calismaz - yani ayni
    /// frame icinde "once Show, sonra Hide" sirasinda (script calisma sirasi ne olursa olsun)
    /// Show HER ZAMAN kazanir.</summary>
    private int lastShowFrame = -1;

    private void Awake()
    {
        Instance = this;
        HideInternal();
    }

    private void LateUpdate()
    {
        // Hedef tasiniyor/dolasiyor olabilir (AnimalIdleWander) - ring her frame takip eder.
        if (ringActive && currentTarget != null)
        {
            PositionRingUnder(currentTarget);
        }
    }

    /// <summary>Belirli bir hedefi (hayvan, kasa vb.) hem halka ile isaretler hem prompt metnini gosterir.</summary>
    public void Show(Transform target, string message)
    {
        lastShowFrame = Time.frameCount;
        currentTarget = target;

        if (promptPanel != null) promptPanel.SetActive(true);
        if (promptText != null) promptText.text = message;

        if (ringObject != null && target != null)
        {
            ringActive = true;
            ringObject.SetActive(true);
            PositionRingUnder(target);
        }
    }

    /// <summary>Belirli bir hedef yokken (orn. "E - Bas - Birak") sadece prompt metnini gosterir, halka gizlenir.</summary>
    public void ShowTextOnly(string message)
    {
        lastShowFrame = Time.frameCount;
        currentTarget = null;
        ringActive = false;

        if (promptPanel != null) promptPanel.SetActive(true);
        if (promptText != null) promptText.text = message;
        if (ringObject != null) ringObject.SetActive(false);
    }

    /// <summary>BUG DUZELTMESI: eger bu AYNI FRAME icinde baska bir script zaten Show()/
    /// ShowTextOnly() cagirdiysa (lastShowFrame == Time.frameCount), bu Hide() cagrisi
    /// SESSIZCE YOK SAYILIR - Show, Hide'a karsi kazanir. Aksi halde normal gizleme yapilir.</summary>
    public void Hide()
    {
        if (lastShowFrame == Time.frameCount) return;
        HideInternal();
    }

    private void HideInternal()
    {
        currentTarget = null;
        ringActive = false;
        if (promptPanel != null) promptPanel.SetActive(false);
        if (ringObject != null) ringObject.SetActive(false);
    }

    private void PositionRingUnder(Transform target)
    {
        // CarryController.SnapToGround ile AYNI mantik (kendi/hedefin child'larini disla,
        // en yakin zemin/nesne yuksekligini bul) - burada AYRI bir kopya cunku CarryController
        // player-ozel (kendi transform'unu disliyor), burasi jenerik olmali.
        Vector3 origin = target.position + Vector3.up * 10f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 40f);

        float bestY = target.position.y + ringFallbackYOffset;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var hit in hits)
        {
            if (hit.transform == target || hit.transform.IsChildOf(target)) continue;
            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestY = hit.point.y;
                found = true;
            }
        }

        Vector3 pos = target.position;
        pos.y = bestY + (found ? ringGroundOffset : ringFallbackYOffset);
        ringObject.transform.position = pos;
    }
}
