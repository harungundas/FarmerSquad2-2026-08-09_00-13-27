using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;

/// <summary>
/// Sahnedeki tum PlayerController'lari tarar, IsOwner=true olan local player'i bulur
/// ve CinemachineCamera'nin Follow/LookAt hedefi olarak atar.
/// Bulunana kadar her frame tekrar dener (network spawn zamanlamasi degisebilir).
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class ThirdPersonCamera : MonoBehaviour
{
    private CinemachineCamera vcam;
    private bool targetAssigned = false;

    private void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
    }

    private void Update()
    {
        if (targetAssigned) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc.IsOwner)
            {
                vcam.Follow = pc.transform;
                vcam.LookAt = pc.transform;
                targetAssigned = true;
                break;
            }
        }
    }
}
