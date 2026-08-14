using UnityEngine;

/// <summary>
/// T32 STUB — sadece DayCycleManager'ın kota başarısız gününde çağırabileceği bir bağlantı noktası
/// sağlamak için var. Gerçek İflas ekranı (GDD Bölüm 10.8 mockup: Gün X Sonu Kaybedildi,
/// Gerekli/Bakiye/Açık, [Ana Menüye Dön]/[Tekrar Dene]) T39'da doldurulacak
/// (ARCHITECTURE.md "## Kazanma/Kaybetme Ekranları").
/// </summary>
public class LoseScreenController : MonoBehaviour
{
    /// <summary>
    /// İflas ekranını açar. Şimdilik sadece log basar.
    /// </summary>
    public void Show(int failedDay)
    {
        Debug.Log("[LoseScreenController] STUB: Gun " + failedDay + " sonu IFLAS ekrani acilacakti " +
                   "(T39'da gercek UI + Gerekli/Bakiye/Acik gosterimi gelecek).");
    }
}
