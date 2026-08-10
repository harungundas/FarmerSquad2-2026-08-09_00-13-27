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
}
