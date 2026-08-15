using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LobbyUI.cs icin tek bir oyuncu satiri. Sahnede 5 sabit satir olarak durur (Instantiate
/// yapilmaz), LobbyUI.RefreshPlayerList her satiri SetRow/Clear ile gunceller.
/// Path: Assets/Scripts/UI/PlayerRowUI.cs (LobbyUI.cs ile ayni klasor, ARCHITECTURE.md
/// "## Ana Menü & Lobi UI" kapsaminda).
/// </summary>
public class PlayerRowUI : MonoBehaviour
{
    [Header("Baglantilar")]
    public TextMeshProUGUI nameText;
    public Button kickButton;

    private ulong clientId;
    private Action<ulong> onKick;

    private void Awake()
    {
        if (kickButton != null) kickButton.onClick.AddListener(HandleKickClicked);
    }

    /// <summary>Satiri doldurur ve gorunur yapar. showKick=false ise kick butonu (host olmayan
    /// veya kendi satiri icin) gizlenir.</summary>
    public void SetRow(string label, bool showKick, ulong id, Action<ulong> kickCallback)
    {
        gameObject.SetActive(true);
        clientId = id;
        onKick = kickCallback;

        if (nameText != null) nameText.text = label;
        if (kickButton != null) kickButton.gameObject.SetActive(showKick);
    }

    /// <summary>Bos slot - satiri gizler.</summary>
    public void Clear()
    {
        gameObject.SetActive(false);
        onKick = null;
    }

    private void HandleKickClicked()
    {
        onKick?.Invoke(clientId);
    }
}
