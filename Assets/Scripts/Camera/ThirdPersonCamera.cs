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
        // DUZELTME (kullanici raporu): eskiden "targetAssigned" bayragiyla hedef SADECE BIR KEZ
        // atanip kilitleniyordu. Lobiye girince CharacterSelectionManager otomatik Yetiskin'i
        // (IsControllable=true) atadigi icin kamera hemen ona kilitleniyordu; oyuncu lobide
        // BASKA bir karakter secince eski govde IsControllable=false olup gorunmez/kinematik
        // kaliyordu ama kamera hala O ESKI, artik donmus govdeye bakmaya devam ediyordu ("bosluga
        // atma" bug'i). Artik HER FRAME, gercekten IsOwner VE IsControllable=true olan govde
        // aranir ve hedef degistiyse guncellenir - karakter degisiminde otomatik takip eder.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc.IsOwner && pc.IsControllable.Value)
            {
                if (vcam.Follow != pc.transform)
                {
                    vcam.Follow = pc.transform;
                    vcam.LookAt = pc.transform;
                }
                return;
            }
        }
    }
}
