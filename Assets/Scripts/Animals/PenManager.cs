using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// `citler` objesine eklenir. Tek ortak agilin hayvan stogunu (Dictionary&lt;AnimalSpecies,int&gt;) tutar,
/// AnimalIdleWander'in (Faz 11 - T44) okuyacagi sinir BoxCollider'ini (bounds) saglar ve
/// baslangic stogunu (3 Tavuk + 2 Koyun, ARCHITECTURE.md "Ağıl & Teslimat Alanı") sahneye instantiate eder.
/// NOT: citler uzerinde onceden (fence mesh'inden gelen) bir BoxCollider zaten var - bounds alani
/// GetComponent ile OTOMATIK bulunmuyor, Inspector'dan DOGRU BoxCollider (bounds icin eklenen, ikinci) elle atanmali.
/// </summary>
public class PenManager : NetworkBehaviour
{
    [Header("Sinir (Bounds)")]
    [Tooltip("Aginin ic sinirini temsil eden, trigger OLMAYAN BoxCollider. AnimalIdleWander (T44) bunu okuyacak. Inspector'dan elle atanmali.")]
    public BoxCollider bounds;

    [Header("Baslangic Stogu Icin Hayvan Verileri")]
    [Tooltip("Baslangicta 3 adet spawn edilecek.")]
    public AnimalData chickenData;
    [Tooltip("Baslangicta 2 adet spawn edilecek.")]
    public AnimalData sheepData;

    [Header("Spawn Noktalari (opsiyonel)")]
    [Tooltip("Bos birakilirsa hayvanlar bounds icinde rastgele bir noktada spawn olur.")]
    public Transform[] spawnPoints;

    private readonly Dictionary<AnimalSpecies, int> stock = new Dictionary<AnimalSpecies, int>();

    private void Awake()
    {
        foreach (AnimalSpecies s in System.Enum.GetValues(typeof(AnimalSpecies)))
        {
            stock[s] = 0;
        }
    }

    private void Start()
    {
        // Host authoritative: sadece server (veya networking hic baslamamissa solo/editor test) stok spawn eder.
        // AnimalHunger.cs (T14) ile ayni desen - bkz. o dosyadaki not.
        if (!IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return;
        }

        SpawnInitialStock();
    }

    private void SpawnInitialStock()
    {
        SpawnAnimal(chickenData, 3);
        SpawnAnimal(sheepData, 2);
    }

    private void SpawnAnimal(AnimalData data, int count)
    {
        if (data == null || data.prefabRef == null)
        {
            Debug.LogWarning("PenManager: AnimalData veya prefabRef atanmamis, spawn atlandi.");
            return;
        }

        bool networked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetSpawnPoint(i);
            GameObject instance = Instantiate(data.prefabRef, pos, Quaternion.identity);

            AnimalBase animalBase = instance.GetComponent<AnimalBase>();
            if (animalBase != null)
            {
                animalBase.ownerPen = transform;
                animalBase.countedInStock = true; // baslangic stogu - PenStockEntryDetector tekrar saymasin
            }

            if (networked)
            {
                NetworkObject netObj = instance.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
            }

            AddStock(data.species, 1);
        }
    }

    private Vector3 GetSpawnPoint(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return spawnPoints[index % spawnPoints.Length].position;
        }
        return GetRandomPointInBounds();
    }

    /// <summary>
    /// Bounds icinde X/Z rastgele, Y sabit (agilin taban yuksekligi) bir nokta doner.
    /// AnimalIdleWander (T44) de hedef nokta secerken ayni bounds referansini kullanacak.
    /// </summary>
    public Vector3 GetRandomPointInBounds()
    {
        if (bounds == null)
        {
            return transform.position;
        }

        Bounds b = bounds.bounds; // world-space
        float x = Random.Range(b.min.x, b.max.x);
        float z = Random.Range(b.min.z, b.max.z);
        return new Vector3(x, transform.position.y, z);
    }

    public void AddStock(AnimalSpecies species, int amount)
    {
        if (!stock.ContainsKey(species))
        {
            stock[species] = 0;
        }
        stock[species] += amount;
    }

    public bool RemoveStock(AnimalSpecies species, int amount)
    {
        if (!stock.ContainsKey(species) || stock[species] < amount)
        {
            return false;
        }
        stock[species] -= amount;
        return true;
    }

    public int GetStock(AnimalSpecies species)
    {
        return stock.ContainsKey(species) ? stock[species] : 0;
    }
}
