using UnityEngine;

/// <summary>
/// 6 kota tanimi (ARCHITECTURE.md "## Ekonomi (Kasa) Sistemi" + Bolum 0 delta tablosu).
/// DIKKAT: Bu degerler GDD'nin ORIJINAL sayilari DEGIL, ARCHITECTURE.md'deki SAPMA (nihai)
/// degerleridir: Gun3=45$, Gun6=115$, Gun9=250$, Gun12=400$, Gun15=520$, Gun18=700$.
/// Kira, kotanin kendisidir - QuotaManager kesilen miktar olarak dogrudan bu degeri kullanir
/// (cumulative bakiye DEGIL, periyodik/tek seferlik kesinti).
/// </summary>
[CreateAssetMenu(fileName = "QuotaData", menuName = "FarmerSquad/QuotaData")]
public class QuotaData : ScriptableObject
{
    [System.Serializable]
    public struct QuotaEntry
    {
        public int day;
        public float requiredAmount;
    }

    public QuotaEntry[] quotas = new QuotaEntry[]
    {
        new QuotaEntry { day = 3,  requiredAmount = 45f },
        new QuotaEntry { day = 6,  requiredAmount = 115f },
        new QuotaEntry { day = 9,  requiredAmount = 250f },
        new QuotaEntry { day = 12, requiredAmount = 400f },
        new QuotaEntry { day = 15, requiredAmount = 520f },
        new QuotaEntry { day = 18, requiredAmount = 700f },
    };

    /// <summary>
    /// Verilen gun icin kota tanimli mi, varsa gerekli tutari doldurur.
    /// </summary>
    public bool TryGetQuota(int day, out float requiredAmount)
    {
        foreach (var entry in quotas)
        {
            if (entry.day == day)
            {
                requiredAmount = entry.requiredAmount;
                return true;
            }
        }

        requiredAmount = 0f;
        return false;
    }

    /// <summary>
    /// T38: Verilen gun, tanimli kotalar arasindaki SON (en buyuk day degerine sahip) kota mi?
    /// DayCycleManager bunu Gun 18 (final kota) basarisinda WinScreenController.Show() cagirmak
    /// icin kullanir - gun sayisini burada SABITLEMEK yerine quotas dizisinin son elemanina
    /// bakar, boylece kota tablosu degisirse (Design Change) tek yerden guncellenir.
    /// </summary>
    public bool IsFinalDay(int day)
    {
        if (quotas == null || quotas.Length == 0) return false;
        return quotas[quotas.Length - 1].day == day;
    }
}
