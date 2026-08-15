using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Lobi UI (ARCHITECTURE.md "## Ana Menü & Lobi UI" + "## Ortak UI Kiti").
/// TASKS.md T41: "Lobi kodu gösterimi, katılan oyuncu listesi, kick butonu (host için)".
///
/// SAPMA (bkz. HANDOFF.md T41 notu, kullanıcı onayladı): T07 (LobbyManager.cs / gerçek Steam
/// lobi sistemi) YOK - sadece ham NGO host/client (UTP) var. Bu yüzden gerçek bir "lobi kodu"
/// kavramı projede mevcut değil. Kapsam daraltıldı: <see cref="lobbyCodeText"/> sabit "—"
/// placeholder gösterir, gerçek oyuncu listesi + host-only kick NetworkManager.Singleton
/// üzerinden (ConnectedClientsList / DisconnectClient) çalışır. T07 gerçek Steam lobi sistemiyle
/// geldiğinde bu alan güncellenecek.
///
/// Sabit sayıda oyuncu satırı kullanılır (5 = GDD'deki oyuncu limiti), her satır
/// <see cref="PlayerRowUI"/> ile yönetilir - runtime Instantiate/prefab referansı gerekmez.
///
/// Diğer ekranlarla (MainMenuCanvas, WinScreenCanvas, LoseScreenController, MarketCanvas) AYNI
/// desen: kendi Canvas'ında, sahne geçişi değil panelRoot.SetActive(false/true) ile açılıp
/// kapanır. MainMenuController, StartHost()/StartClient() sonrası Show() çağırmalı (bkz.
/// MainMenuController.cs'teki küçük ekleme).
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("Baglantilar")]
    public GameObject panelRoot;
    public TextMeshProUGUI lobbyCodeText;
    public Button continueButton;

    [Header("Oyuncu Satirlari (sabit 5 slot, GDD oyuncu limiti)")]
    public PlayerRowUI[] playerRows;

    private bool subscribed;

    private void Awake()
    {
        if (lobbyCodeText != null) lobbyCodeText.text = "Lobi Kodu: \u2014";
        if (continueButton != null) continueButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>Lobi panelini acar. MainMenuController, StartHost()/StartClient() sonrasi
    /// cagirir.</summary>
    public void Show()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        Subscribe();
        RefreshPlayerList();
    }

    /// <summary>[Devam Et] (GreenBtn - Ortak UI Kiti rengi: Onay/Devam Et). Panel kapanir, oyun
    /// serbestce devam eder. GDD'de bu panel icin ayri bir "kapatma" tanimi yoktu, ekran kendi
    /// basina kalici kalamayacagi icin eklendi (bkz. HANDOFF Deviations).</summary>
    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientsChanged;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientsChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || NetworkManager.Singleton == null)
        {
            subscribed = false;
            return;
        }
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientsChanged;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientsChanged;
        subscribed = false;
    }

    private void OnClientsChanged(ulong clientId)
    {
        RefreshPlayerList();
    }

    /// <summary>Sahnedeki 5 sabit satiri NetworkManager.Singleton.ConnectedClientsList'e gore
    /// gunceller. Kick butonu sadece host icin ve kendi satirinda gizli gosterilir.</summary>
    public void RefreshPlayerList()
    {
        if (playerRows == null) return;

        if (NetworkManager.Singleton == null)
        {
            foreach (var row in playerRows)
            {
                if (row != null) row.Clear();
            }
            return;
        }

        bool isHost = NetworkManager.Singleton.IsServer;
        var clients = NetworkManager.Singleton.ConnectedClientsList;

        for (int i = 0; i < playerRows.Length; i++)
        {
            if (playerRows[i] == null) continue;

            if (i < clients.Count)
            {
                ulong clientId = clients[i].ClientId;
                bool isLocal = clientId == NetworkManager.Singleton.LocalClientId;
                bool isHostRow = clientId == NetworkManager.ServerClientId;

                string label = "Oyuncu " + clientId;
                if (isHostRow) label += " (Host)";
                if (isLocal) label += " (Sen)";

                bool showKick = isHost && !isLocal;
                playerRows[i].SetRow(label, showKick, clientId, OnKickClicked);
            }
            else
            {
                playerRows[i].Clear();
            }
        }
    }

    /// <summary>Host-only: NetworkManager.Singleton.DisconnectClient ile secili client'i lobiden
    /// atar.</summary>
    private void OnKickClicked(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        NetworkManager.Singleton.DisconnectClient(clientId);
        RefreshPlayerList();
    }
}
