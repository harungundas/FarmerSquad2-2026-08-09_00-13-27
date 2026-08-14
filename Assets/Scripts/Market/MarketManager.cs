using UnityEngine;

/// <summary>
/// T32 STUB — sadece DayCycleManager'ın kota başarılı gününde çağırabileceği bir bağlantı noktası
/// sağlamak için var. Gerçek market UI'ı, 6 upgrade seçeneği, satın alma ServerRpc'leri ve
/// "[Sonraki Güne Geç]" butonu T33'te doldurulacak (ARCHITECTURE.md "## Market & Yükseltme").
///
/// T33 NOTU (T32'den devralınan bilinçli sınırlama): Şu an DayCycleManager, market'i "açtıktan"
/// hemen sonra kendi kendine sonraki güne geçiyor (bkz. DayCycleManager.CompleteDayServer).
/// Gerçek akışta bu YANLIŞ olacak — market ekranı oyuncunun ürün seçip [Sonraki Güne Geç] demesini
/// BEKLEMELİ. T33'te bu metod (veya yeni bir callback/event) DayCycleManager'ın sonraki güne
/// geçişini TETİKLEYEN taraf haline gelmeli, DayCycleManager'ın kendiliğinden geçmesi KALDIRILMALI.
/// </summary>
public class MarketManager : MonoBehaviour
{
    /// <summary>
    /// Gün sonu marketini açar. Şimdilik sadece log basar.
    /// </summary>
    public void OpenMarket(int dayJustCompleted)
    {
        Debug.Log("[MarketManager] STUB: Gun " + dayJustCompleted + " sonu market acilacakti " +
                   "(T33'te gercek UI + satin alma gelecek).");
    }
}
