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

// Awake KASITLI OLARAK BURADA DEGIL: buton dinleyicisi artik Setup() icinde baglaniyor
    // (bkz. Setup yorumu) - iki ayri Awake zincirine (bu sinif + CharacterSelectUI) bagimli
    // kirilgan init sirasi, canli testte (2-client, T43 dogrulamasi) onSelect callback'inin
    // NULL kalmasina yol acmisti (Awake tetiklenmis ama callback bos kalmisti - kok neden tam
    // netlesmedi, muhtemelen Play modu/domain-reload zamanlamasi). OnEnable her panel
    // acilisinda Setup()'i yeniden cagirdigi icin bu artik kendi kendini onaran bir desen.

/// <summary>Satiri doldurur VE tiklama olayini (yeniden) baglar. Guvenle tekrar tekrar
    /// cagrilabilir - RemoveAllListeners ile eski baglanti temizlenip yenisi eklenir.
    /// CharacterSelectUI.OnEnable her panel acilisinda bunu tum satirlar icin tekrar cagirir
    /// (bkz. o sinifin yorumu - kirilgan Awake-zamanli cift-init zincirinin yerini aldi).</summary>
    public void Setup(int index, string characterName, string statSummary, Action<int> selectCallback)
    {
        characterIndex = index;
        onSelect = selectCallback;
        if (nameText != null) nameText.text = characterName;
        if (statText != null) statText.text = statSummary;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(HandleSelectClicked);
        }
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
