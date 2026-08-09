using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterClassData", menuName = "Farmer Squad/Character Class Data")]
public class CharacterClassData : ScriptableObject
{
    [Header("Movement")]
    public float walkSpeed;
    public float carrySpeedMultiplier;
    [Tooltip("Sadece Çocuk için: İnek/At gibi ağır hayvan taşırken hız çarpanı.")]
    public float heavyCarrySpeedMultiplier;

    [Header("Feeding")]
    public float feedDuration;

    [Header("Carrying")]
    public int carryCapacityLight;

    [Header("Negotiation (sadece Yaşlı için anlamlı, diğerlerinde 0)")]
    public float negotiationRejectReduction;
    public float negotiationRewardBonus;
}
