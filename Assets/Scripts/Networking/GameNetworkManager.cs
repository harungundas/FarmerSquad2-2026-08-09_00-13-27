using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// NetworkManager wrapper: gercek LAN host/client baglantisini yonetir.
///
/// KULLANICI KARARI (Steamworks ISTEMIYORUM, NetworkManager kendi basina yonetsin, LAN
/// yeterli): Steam/Facepunch KULLANILMIYOR. UnityTransport (zaten kurulu) uzerinden dogrudan
/// IP:port ile baglaniliyor. Host, tum ag arayuzlerinde dinlemek icin ServerListenAddress'i
/// "0.0.0.0" yapar; Client, StartClient(ip) ile HEDEF IP'yi acikca belirtir.
///
/// ONCEKI BUG (kullanici raporu: "lobi koduyla katilamadim"): ConnectionData hem host hem
/// client icin sabit "127.0.0.1" (localhost) idi - host SADECE kendi bilgisayarindan gelen
/// baglantilari kabul ediyordu, client de HER ZAMAN kendi bilgisayarina baglanmaya
/// calisiyordu. Iki farkli bilgisayarda bu asla calismazdi.
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    [Tooltip("Sabit port - hem host hem client ayni portu kullanmali. Firewall/router'da bu portun acik olmasi gerekebilir.")]
    public ushort port = 7777;

    /// <summary>[Lobi Oluştur]. Tum ag arayuzlerinde (0.0.0.0) dinlemeye baslar, boylece ayni
    /// LAN'daki baska bir bilgisayar bu makinenin gercek IP'sine baglanabilir.</summary>
    public bool StartHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("GameNetworkManager: NetworkManager.Singleton bulunamadi.");
            return false;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
        }

        bool started = NetworkManager.Singleton.StartHost();
        Debug.Log(started
            ? "[GameNetworkManager] Host baslatildi. Diger oyuncular su IP'ye baglanabilir: " + GetLocalIPAddress() + ":" + port
            : "[GameNetworkManager] Host baslatilamadi.");
        return started;
    }

    /// <summary>[Lobiye Katıl]. Verilen IP adresine (arkadasin sana soyledigi LAN IP'si) sabit
    /// portta baglanmaya calisir. Basari/basarisizlik NetworkManager'in kendi
    /// OnClientConnectedCallback / OnClientDisconnectCallback olaylariyla anlasilir (bu script
    /// sadece baglanti GIRISIMINI baslatir, sonucu LobbyListUI dinler).</summary>
    public bool StartClient(string hostIpAddress)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("GameNetworkManager: NetworkManager.Singleton bulunamadi.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(hostIpAddress))
        {
            Debug.LogError("[GameNetworkManager] Bos IP adresiyle baglanilamaz.");
            return false;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData(hostIpAddress.Trim(), port);
        }

        bool started = NetworkManager.Singleton.StartClient();
        Debug.Log(started
            ? "[GameNetworkManager] Client baglanti girisimi baslatildi: " + hostIpAddress + ":" + port
            : "[GameNetworkManager] Client baslatilamadi.");
        return started;
    }

    /// <summary>Eski parametresiz cagri - artik SADECE geriye uyumluluk icin, localhost'a
    /// baglanir (ayni makinede iki instance test ederken kullanislidir).</summary>
    [Obsolete("Cok makineli LAN baglantisi icin StartClient(string hostIpAddress) kullan.")]
    public bool StartClient()
    {
        return StartClient("127.0.0.1");
    }

    /// <summary>Bu bilgisayarin LAN'daki gercek IPv4 adresini bulur (127.0.0.1 DEGIL) -
    /// LobbyUI bunu host ekraninda gosterip arkadasa soylenmesini saglar. Bulunamazsa
    /// "127.0.0.1" doner (tek makine testi icin guvenli varsayilan).</summary>
    public static string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ipv4 = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(ip));
            if (ipv4 != null) return ipv4.ToString();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameNetworkManager] Yerel IP bulunamadi: " + e.Message);
        }
        return "127.0.0.1";
    }
}
