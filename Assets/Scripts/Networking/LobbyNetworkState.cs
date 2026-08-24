using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// KULLANICI BUG RAPORU DUZELTMESI: LobbyUI'daki ready/countdown/oyuncu-listesi sistemi
/// TAMAMEN LOKALDI - her client kendi ekraninda kendi ready durumunu, kendi countdown'unu
/// hesapliyordu, birbirinden HABERSIZDI. Sonuclar:
///   1) Lobide iki oyuncu birbirini GORMUYORDU (her client sadece kendini slot 0'da
///      goruyordu, LobbySessionManager gercek NetworkManager baglantisina hic baglanmiyordu).
///   2) Client "Hazir" deyince GetCurrentPlayerCount() hep 1 dondugu icin SOLO saniyor,
///      host'un GERCEKTEN baslatmasini BEKLEMEDEN kendi basina countdown'unu isletip oyuna
///      giriyordu.
///
/// Bu script GERCEK, sunucu-yetkili (host-authoritative) lobi durumunu tutar: bagli her
/// client icin (clientId, username, characterIndex, isReady) NetworkList'te saklanir,
/// countdown NetworkVariable ile TUM client'lara senkron akar, oyunun GERCEKTEN basladigi
/// an (GameStarted) TUM client'lara AYNI ANDA bildirilir. LobbyUI bu veriyi SADECE OKUR,
/// kendi kafasina gore karar VERMEZ.
///
/// Sahneye NetworkManager objesiyle birlikte YERLESTIRILMIS bir NetworkObject olarak
/// eklenir (WalletManager/NegotiationManager ile ayni "sahne-ici NetworkObject" deseni) -
/// host baslayinca otomatik spawn olur, sonradan baglanan client'lara da senkronize edilir.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class LobbyNetworkState : NetworkBehaviour
{
    public struct LobbyPlayerData : INetworkSerializable, System.IEquatable<LobbyPlayerData>
    {
        public ulong clientId;
        public FixedString64Bytes username;
        public int characterIndex;
        public bool isReady;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref username);
            serializer.SerializeValue(ref characterIndex);
            serializer.SerializeValue(ref isReady);
        }

        public bool Equals(LobbyPlayerData other)
        {
            return clientId == other.clientId
                && username.Equals(other.username)
                && characterIndex == other.characterIndex
                && isReady == other.isReady;
        }
    }

    /// <summary>Bagli TUM oyuncularin GERCEK listesi - her client bunu okuyarak lobi
    /// slotlarini doldurur, boylece herkes birbirini gorur.</summary>
    public NetworkList<LobbyPlayerData> Players = new NetworkList<LobbyPlayerData>();

    [Header("Lobi Countdown (host-yetkili, tum client'lara senkron)")]
    [Tooltip("-1 = countdown calismiyor. 0-15 arasi = kalan saniye.")]
    public NetworkVariable<int> CountdownSecondsRemaining = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Tooltip("true olunca TUM client'lar (host dahil) AYNI ANDA lobi ekranini kapatip HUD'u acar.")]
    public NetworkVariable<bool> GameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public const int CountdownStartSeconds = 15;

    [Header("Baglanti")]
    [Tooltip("Countdown 0'a inince BeginGameServer() burada cagrilir (sadece sunucuda calisir).")]
    public DayCycleManager dayCycleManager;

    public static LobbyNetworkState Instance { get; private set; }

    private float countdownTimerAccum = 0f;
    private bool countdownActive = false;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnectedServer;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnectedServer;

            OnClientConnectedServer(NetworkManager.LocalClientId);
            foreach (var kv in NetworkManager.ConnectedClients)
            {
                OnClientConnectedServer(kv.Key);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnectedServer;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectedServer;
        }
    }

    private void OnClientConnectedServer(ulong clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId == clientId) return;
        }

        Players.Add(new LobbyPlayerData
        {
            clientId = clientId,
            username = new FixedString64Bytes("Oyuncu " + clientId),
            characterIndex = 0,
            isReady = false
        });

        Debug.Log("[LobbyNetworkState] Oyuncu eklendi: clientId=" + clientId + " (toplam " + Players.Count + ")");
    }

    private void OnClientDisconnectedServer(ulong clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId == clientId)
            {
                Players.RemoveAt(i);
                Debug.Log("[LobbyNetworkState] Oyuncu ayrildi: clientId=" + clientId + " (kalan " + Players.Count + ")");
                break;
            }
        }

        if (countdownActive) CancelCountdownServer();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSetUsernameServerRpc(string username, ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;
        SetPlayerField(id, username, null, null);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSetCharacterServerRpc(int characterIndex, ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;
        SetPlayerField(id, null, characterIndex, null);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSetReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;
        SetPlayerField(id, null, null, ready);

        if (!ready)
        {
            if (countdownActive) CancelCountdownServer();
            return;
        }

        // BUG DUZELTMESI (2-client testinde bulundu: iki oyuncu da Hazir dedi ama countdown
        // hic baslamadi, sadece host'un manuel [Baslat] butonuna basmasiyla basladi):
        // StartCountdownServer() eskiden SADECE LobbyUI.OnStartGameClicked() icinden
        // cagriliyordu - "herkes hazir olunca OTOMATIK basla" kontrolu hicbir yerde yoktu
        // (TASKS.md FAZ 12 checklist'i bunun otomatik olmasi gerektigini dogruluyor).
        // Burada, sunucu-yetkili tarafta (ServerRpc icinde), bir oyuncu "hazir" dedigi an
        // TUM bagli oyuncularin hazir olup olmadigini kontrol edip oyle baslatiyoruz.
        if (!countdownActive && PlayerCount() > 0 && ReadyCount() >= PlayerCount())
        {
            Debug.Log("[LobbyNetworkState] Tum oyuncular hazir - countdown OTOMATIK basliyor.");
            StartCountdownServer();
        }
    }

    private void SetPlayerField(ulong clientId, string username, int? characterIndex, bool? ready)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId != clientId) continue;

            var d = Players[i];
            if (username != null) d.username = new FixedString64Bytes(username);
            if (characterIndex.HasValue) d.characterIndex = characterIndex.Value;
            if (ready.HasValue) d.isReady = ready.Value;
            Players[i] = d;
            return;
        }
    }

    public void StartCountdownServer()
    {
        if (!IsServer) return;
        if (countdownActive) return;

        countdownActive = true;
        countdownTimerAccum = 0f;
        CountdownSecondsRemaining.Value = CountdownStartSeconds;
        Debug.Log("[LobbyNetworkState] Countdown baslatildi (" + CountdownStartSeconds + "sn).");
    }

    public void CancelCountdownServer()
    {
        if (!IsServer) return;
        countdownActive = false;
        CountdownSecondsRemaining.Value = -1;
        Debug.Log("[LobbyNetworkState] Countdown iptal edildi.");
    }

    private void Update()
    {
        if (!IsServer || !countdownActive) return;

        countdownTimerAccum += Time.deltaTime;
        if (countdownTimerAccum < 1f) return;
        countdownTimerAccum -= 1f;

        CountdownSecondsRemaining.Value = Mathf.Max(0, CountdownSecondsRemaining.Value - 1);

        if (CountdownSecondsRemaining.Value <= 0)
        {
            countdownActive = false;
            GameStarted.Value = true;

            if (dayCycleManager != null)
            {
                dayCycleManager.BeginGameServer();
                Debug.Log("[LobbyNetworkState] Countdown bitti - DayCycleManager.BeginGameServer() cagrildi.");
            }
            else
            {
                Debug.LogWarning("[LobbyNetworkState] dayCycleManager atanmamis - gercek oyun baslatilamadi.");
            }
        }
    }

    public int PlayerCount() => Players.Count;

    public int ReadyCount()
    {
        int c = 0;
        for (int i = 0; i < Players.Count; i++) if (Players[i].isReady) c++;
        return c;
    }
}
