using UnityEngine;

/// <summary>
/// 3 ekipman + 3 yetenek tanımı (ARCHITECTURE.md "## Market & Yükseltme" + Bölüm 0 delta tablosu).
/// DİKKAT: id alanları kod içinde (MarketManager, T34 PenUpgradeSecondPen vb.) referans olarak
/// kullanılacak, sabit tutulmalı. Sıra (index 0..5) MarketUI.cs'teki 6 buton dizisiyle BİREBİR
/// eşleşmelidir - sırayı değiştirmek UI'daki eşleşmeyi bozar.
/// Bu görev (T33) SADECE satın alma + para düşüşünü kapsar; upgrade'lerin gerçek oyun-içi etkisi
/// (El Arabası aktifleştirme, Yem Dağıtıcı, İkinci Ağıl, hız/yemleme/pazarlık bonusları) BAŞKA
/// task'ların kapsamındadır (bkz. TASKS.md T34 ve devamı).
/// </summary>
[CreateAssetMenu(fileName = "UpgradeData", menuName = "FarmerSquad/UpgradeData")]
public class UpgradeData : ScriptableObject
{
    public enum UpgradeCategory { Equipment, Skill }

    [System.Serializable]
    public struct UpgradeEntry
    {
        public string id;
        public string displayNameTr;
        public UpgradeCategory category;
        public float price;
    }

    // Sıra sabit: 0=El Arabası,1=Yem Dağıtıcı,2=Geniş Çitler,3=Lojistik Kondisyonu,
    // 4=Çiftlik Becerisi,5=Pazarlık Ustalığı (ARCHITECTURE.md "## Market & Yükseltme")
    public UpgradeEntry[] upgrades = new UpgradeEntry[]
    {
        new UpgradeEntry { id = "Wheelbarrow",       displayNameTr = "El Arabası",         category = UpgradeCategory.Equipment, price = 20f },
        new UpgradeEntry { id = "AutoFeeder",        displayNameTr = "Yem Dağıtıcı",        category = UpgradeCategory.Equipment, price = 30f },
        new UpgradeEntry { id = "WidePens",          displayNameTr = "Geniş Çitler",        category = UpgradeCategory.Equipment, price = 25f },
        new UpgradeEntry { id = "LogisticsFitness",  displayNameTr = "Lojistik Kondisyonu", category = UpgradeCategory.Skill,     price = 30f },
        new UpgradeEntry { id = "FarmSkill",         displayNameTr = "Çiftlik Becerisi",    category = UpgradeCategory.Skill,     price = 20f },
        new UpgradeEntry { id = "NegotiationMastery",displayNameTr = "Pazarlık Ustalığı",   category = UpgradeCategory.Skill,     price = 35f },
    };

    public bool TryGetUpgrade(int index, out UpgradeEntry entry)
    {
        if (index >= 0 && index < upgrades.Length)
        {
            entry = upgrades[index];
            return true;
        }
        entry = default;
        return false;
    }
}
