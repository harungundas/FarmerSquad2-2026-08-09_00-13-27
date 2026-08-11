/// <summary>
/// Musteri siparisi. Bir CustomerVehicle'in (T23) tasidigi, hangi hayvan turunden kac
/// tane, hangi yonde (Satis/Alim) istendigini ve bu siparisin taban (pazarliksiz) toplam
/// fiyatini tutan struct.
///
/// basePrice HESABI (ARCHITECTURE.md Bolum 0 deltasi + T04 deviation'i - GDD'nin
/// "AnimalData.basePrice" tek alanindan FARKLI): AnimalData'da tek bir "basePrice" alani
/// YOK, T04'te buyPrice/sellPrice olarak ikiye ayrildi (Assets/Scripts/Animals/AnimalData.cs).
/// Yon kurali (ARCHITECTURE.md "## Pazarlik Sistemi"): Satis = ciftlik hayvani MUSTERIYE
/// satiyor -> yuksek fiyat (AnimalData.sellPrice). Alim = musteri hayvani ciftlige
/// getiriyor, ciftlik SATIN ALIYOR -> dusuk fiyat (AnimalData.buyPrice). basePrice = count * o birim fiyat.
/// </summary>
public enum OrderDirection
{
    Satis,
    Alim
}

[System.Serializable]
public struct OrderData
{
    public AnimalSpecies species;
    public int count;
    public OrderDirection direction;
    public float basePrice;

    /// <summary>
    /// AnimalData referansindan dogru birim fiyati (direction'a gore buyPrice/sellPrice)
    /// okuyup basePrice'i hesaplayarak dolu bir OrderData olusturur.
    /// </summary>
    public OrderData(AnimalSpecies species, int count, OrderDirection direction, AnimalData animalData)
    {
        this.species = species;
        this.count = count;
        this.direction = direction;
        this.basePrice = CalculateBasePrice(count, direction, animalData);
    }

    /// <summary>
    /// Satis -> AnimalData.sellPrice, Alim -> AnimalData.buyPrice kullanir, count ile carpar.
    /// animalData null ise (veya species uyusmuyorsa caller'in sorumlulugundadir) 0 doner.
    /// </summary>
    public static float CalculateBasePrice(int count, OrderDirection direction, AnimalData animalData)
    {
        if (animalData == null) return 0f;
        float unitPrice = direction == OrderDirection.Satis ? animalData.sellPrice : animalData.buyPrice;
        return count * unitPrice;
    }
}
