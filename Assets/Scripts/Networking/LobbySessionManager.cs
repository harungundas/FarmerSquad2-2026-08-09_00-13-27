using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TASKS.md T50 — Basit lokal lobi kayıt defteri (statik, sahne objesine bağlı değil).
///
/// NOT: Gerçek Steamworks/NGO lobi keşfi henüz YOK (bkz. LobbyManager.cs T45 notu ve
/// HANDOFF.md "2-client network testi henüz yapılmadı"). Bu sınıf, TASKS.md T50 Context'inin
/// "gerçek veri (hardcoded değil, boş lobiler silinir)" şartını tek Unity Editor instance'ı
/// içinde karşılar: ActiveLobbies listesi SADECE bu sınıfın metotlarıyla değişir, LobbyListUI
/// hiçbir zaman sabit bir liste göstermez. Gerçek network lobi keşfi eklendiğinde bu sınıfın
/// iç implementasyonu değişecek ama CreateLobby/JoinLobby/LeaveLobby imzaları aynı kalabilir.
/// </summary>
public static class LobbySessionManager
{
    public struct LobbyInfo
    {
        public int lobbyCode;
        public int playerCount;
        public string hostUsername;
    }

    public const int MaxPlayersPerLobby = 5;

    public static readonly List<LobbyInfo> ActiveLobbies = new List<LobbyInfo>();

    /// <summary>Local oyuncunun şu an içinde olduğu lobi (yoksa null). Leave/playerCount
    /// güncellemeleri bunun üzerinden yapılır.</summary>
    public static LobbyInfo? CurrentLobby { get; private set; }

    /// <summary>[Lobi Oluştur]. Mevcut kodlarla çakışmayan 4 haneli rakam-only kod üretir
    /// (GDD/HANDOFF: Random.Range(1000,9999)), listeye playerCount=1 ile ekler, CurrentLobby yapar.</summary>
    public static LobbyInfo CreateLobby(string hostUsername)
    {
        int code = GenerateUniqueCode();

        LobbyInfo info = new LobbyInfo { lobbyCode = code, playerCount = 1, hostUsername = hostUsername };
        ActiveLobbies.Add(info);
        CurrentLobby = info;
        return info;
    }

    /// <summary>[Lobiye Katıl] listesinden bir satıra tıklanınca çağrılır. Lobi doluysa
    /// (playerCount >= MaxPlayersPerLobby) veya bulunamıyorsa false döner, katılmaz.</summary>
    public static bool JoinLobby(int lobbyCode, out LobbyInfo joined)
    {
        int idx = FindIndexByCode(lobbyCode);
        if (idx < 0)
        {
            joined = default;
            return false;
        }

        LobbyInfo info = ActiveLobbies[idx];
        if (info.playerCount >= MaxPlayersPerLobby)
        {
            joined = info;
            return false;
        }

        info.playerCount++;
        ActiveLobbies[idx] = info;
        CurrentLobby = info;
        joined = info;
        return true;
    }

    /// <summary>[Leave]. CurrentLobby'nin playerCount'unu 1 azaltır; 0'a düşerse listeden
    /// tamamen siler ("boş lobiler silinir" — TASKS.md T50 Context). CurrentLobby'yi temizler.</summary>
    public static void LeaveLobby()
    {
        if (CurrentLobby == null) return;

        int idx = FindIndexByCode(CurrentLobby.Value.lobbyCode);
        if (idx >= 0)
        {
            LobbyInfo info = ActiveLobbies[idx];
            info.playerCount = Mathf.Max(0, info.playerCount - 1);
            if (info.playerCount <= 0)
            {
                ActiveLobbies.RemoveAt(idx);
            }
            else
            {
                ActiveLobbies[idx] = info;
            }
        }

        CurrentLobby = null;
    }

    /// <summary>Oturum 1'de gerçek 2. bir client olmadığı için (bkz. HANDOFF), 5/5-dolu ve
    /// boş-lobiye-katılma senaryolarını test edebilmek için LobbyListUI tarafından çağrılan
    /// yardımcı (T48'in simulatedNotReadyCount deseniyle aynı mantık — gerçek network gelince
    /// bu çağrı kaldırılacak, CreateLobby/JoinLobby imzaları değişmeyecek).</summary>
    public static LobbyInfo CreateFakeLobby(string hostUsername, int playerCount)
    {
        int code = GenerateUniqueCode();
        LobbyInfo info = new LobbyInfo
        {
            lobbyCode = code,
            playerCount = Mathf.Clamp(playerCount, 1, MaxPlayersPerLobby),
            hostUsername = hostUsername
        };
        ActiveLobbies.Add(info);
        return info;
    }

    private static int GenerateUniqueCode()
    {
        int code;
        int guard = 0;
        do
        {
            code = Random.Range(1000, 9999);
            guard++;
        } while (FindIndexByCode(code) >= 0 && guard < 100);
        return code;
    }

    private static int FindIndexByCode(int code)
    {
        for (int i = 0; i < ActiveLobbies.Count; i++)
        {
            if (ActiveLobbies[i].lobbyCode == code) return i;
        }
        return -1;
    }
}
