using UnityEngine;
using Unity.Netcode;

/// <summary>
/// NetworkManager wrapper: host baslatma ve client olarak baglanma.
/// Simdilik UI'siz; test icin [ContextMenu] ile Inspector'dan cagrilir.
/// Transport: gecici olarak Unity Transport (UTP) kullanilir.
/// Facepunch.Steamworks transport'a gecildiginde bu script degismeyecek,
/// sadece NetworkManager uzerindeki transport component'i degisecek.
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    [ContextMenu("Start Host")]
    public void StartHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("GameNetworkManager: NetworkManager.Singleton bulunamadi.");
            return;
        }

        bool started = NetworkManager.Singleton.StartHost();
        Debug.Log(started ? "Host baslatildi." : "Host baslatilamadi.");
    }

    [ContextMenu("Start Client")]
    public void StartClient()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("GameNetworkManager: NetworkManager.Singleton bulunamadi.");
            return;
        }

        bool started = NetworkManager.Singleton.StartClient();
        Debug.Log(started ? "Client baslatildi." : "Client baslatilamadi.");
    }
}
