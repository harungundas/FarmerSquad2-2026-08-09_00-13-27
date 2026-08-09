using UnityEngine;

public enum AnimalSpecies
{
    Chicken,
    Sheep,
    Goat,
    Cow,
    Horse
}

public enum AnimalWeightClass
{
    Light,
    Heavy
}

[CreateAssetMenu(fileName = "NewAnimalData", menuName = "Farmer Squad/Animal Data")]
public class AnimalData : ScriptableObject
{
    public AnimalSpecies species;
    public AnimalWeightClass weightClass;

    [Header("Economy")]
    public float buyPrice;
    public float sellPrice;

    [Header("Prefab")]
    public GameObject prefabRef;
}
