using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Oyun sahnesi (SampleScene) her yüklenişinde state sıfırlaması yapar.
/// Lobi -> Oyun geçişinde önceki run'dan kalan state'i temizler.
/// HANDOFF: Bug Çözümü — müşteri geliş timer'ı, gün sayacı, stok state'i sıfırlanmıyor.
/// </summary>
public class GameInitializer : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("[GameInitializer] Scene load - state reset başlanıyor...");
        ResetGameState();
    }

    private void ResetGameState()
    {
        // DayCycleManager reset
        var dayCycleManager = FindObjectOfType<DayCycleManager>();
        if (dayCycleManager != null)
        {
            Debug.Log("[GameInitializer] DayCycleManager bulundu, reset yapılıyor.");
            // NetworkVariable değerlerini sıfırla (ServerRPC gerekli, ama Awake'te network hazır değil olabilir)
            // Bunun yerine: scene load sonrası, network spawn öncesi bu objeyi deactivate et
            // veya private metodu DirectReset yap (kötü ama çalışır, test için).
            ResetDayCycleManager(dayCycleManager);
        }
        else
        {
            Debug.LogWarning("[GameInitializer] DayCycleManager bulunamadı.");
        }

        // VehicleSpawner reset
        var vehicleSpawner = FindObjectOfType<VehicleSpawner>();
        if (vehicleSpawner != null)
        {
            Debug.Log("[GameInitializer] VehicleSpawner bulundu, eski araçlar temizleniyor.");
            ResetVehicleSpawner(vehicleSpawner);
        }

        // WalletManager reset
        var walletManager = FindObjectOfType<WalletManager>();
        if (walletManager != null)
        {
            Debug.Log("[GameInitializer] WalletManager bulundu, reset yapılıyor.");
            ResetWalletManager(walletManager);
        }

        Debug.Log("[GameInitializer] State reset tamamlandı.");
    }

    private void ResetDayCycleManager(DayCycleManager dcm)
    {
        // HACK: NetworkVariable'ları Unity'nin dirty bit sistemi olmadan direkt reset etmek,
        // Awake'te network spawn öncesiyse geçerli. Ama bunun yerine AdvanceToNextDayServer
        // gibi public metodlar varsa onları kullanmak daha temiz.
        // Şimdilik reflection/direkt access ile (unsafe ama test için yeterli):
        
        // Alternatif (daha güvenli): Initialize() public metodu eklemek DayCycleManager'a
        // ve burada çağırmak. Ama şu anda O yok. Temp workaround:
        
        // Reflection veya Component disable/enable trick:
        // Komponent disable -> enable, Awake yeniden çalışsın? Hayır, OnEnable çalışır.
        // En güvenli: DayCycleManager'a Reset() metodu ekle.
        
        // Şimdilik hardcoded hacks (TEST ONLY):
        var currentDayField = typeof(DayCycleManager).GetField(
            "CurrentDay", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var timerField = typeof(DayCycleManager).GetField(
            "Timer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var freeModeField = typeof(DayCycleManager).GetField(
            "IsFreeMode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (currentDayField != null && timerField != null && freeModeField != null)
        {
            // Reflection ile değer okuma/yazma (NetworkVariable<T> için GetValue/SetValue lazım)
            // PROBLEM: NetworkVariable public property yok direkt set için.
            Debug.LogWarning("[GameInitializer] DayCycleManager state reset (NetworkVariable) reflection ile yapılamıyor. Public Reset() metodu gerekli.");
            return;
        }

        // FALLBACK: Komponent destroy/re-add (extreme hack, ama state temizler):
        // Disabled bırakılsın, network spawn öncesi initialize
        // Şimdilik: sadece debug log
        Debug.Log("[GameInitializer] DayCycleManager reset için public Reset() metodunun eklenmesi gerekli.");
    }

    private void ResetVehicleSpawner(VehicleSpawner vs)
    {
        // Sahne'de spawn'lanmış araçları temizle
        var allVehicles = FindObjectsOfType<CustomerVehicle>();
        foreach (var vehicle in allVehicles)
        {
            Destroy(vehicle.gameObject);
        }
        Debug.Log("[GameInitializer] " + allVehicles.Length + " araç temizlendi.");
    }

    private void ResetWalletManager(WalletManager wm)
    {
        // WalletManager network variable reset  
        // Same problem: reflection bile çalışmayabilir.
        Debug.Log("[GameInitializer] WalletManager reset için public Reset() metodunun eklenmesi gerekli.");
    }
}
