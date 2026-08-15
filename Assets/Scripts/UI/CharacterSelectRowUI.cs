using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CharacterSelectUI.cs icin tek bir karakter satiri (PlayerRowUI.cs deseniyle tutarli).
/// Sahnede 5 sabit satir olarak durur, CharacterSelectUI her frame/degisiklikte SetRow ile gunceller.
/// </summary>
public class CharacterSelectRowUI : MonoBehaviour
{
    [Header("Baglantilar")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statText;
    public TextMeshProUGUI occupantText;
    public Button selectButton;

    private int characterIndex;
    private Action<int> onSelect;

    private void Awake()
    {
        if (selectButton != null) selectButton.onClick.AddListener(HandleSelectClicked);
    }

    public void Setup(int index, string characterName, string statSummary, Action<int> selectCallback)
    {
        characterIndex = index;
        onSelect = selectCallback;
        if (nameText != null) nameText.text = characterName;
        if (statText != null) statText.text = statSummary;
    }

    /// <summary>occupantLabel bos ise "Boş" gosterilir. isMine=true ise buton "Seçildi" olur ve
    /// tiklanamaz hale gelmez (birakma imkani icin acik kalir), baskasi seçtiyse buton kapanir.</summary>
    public void SetOccupant(string occupantLabel, bool isMine, bool isTakenByOther)
    {
        if (occupantText != null) occupantText.text = string.IsNullOrEmpty(occupantLabel) ? "Boş" : occupantLabel;
        if (selectButton != null) selectButton.interactable = !isTakenByOther;
    }

    private void HandleSelectClicked()
    {
        onSelect?.Invoke(characterIndex);
    }
}
