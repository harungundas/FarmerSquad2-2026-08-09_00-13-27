using UnityEngine;

/// <summary>
/// KULLANICI RAPORU (iki kez tekrarlandi): kasa (StandInteraction) ve bilgisayar
/// (ComputerInteraction) trigger kutulari birbirine yakin oldugu icin oyuncu ikisinin de
/// menzilindeyken AYNI F basisi HER IKI scripti de tetikliyordu - ekranda SADECE birinin
/// prompt'u gorunse bile, hangi scriptin F'i gercekten ISLEDIGI bundan bagimsizdi.
///
/// Cozum: iki fazli bir hakem. Her interaction script'i:
/// 1) Update() icinde (oyuncu menzildeyken VE gercekten aksiyon alinabilir durumdayken)
///    Register(this, oyuncuya-olan-mesafe-karesi) cagirir.
/// 2) LateUpdate() icinde (Unity'nin TUM Update() cagrilarini bitirdigi, garantili sonraki
///    faz) IsWinner(this) ile kontrol eder - SADECE o frame'in en yakin adayi true doner,
///    prompt'u gosterir VE F tusunu isler. Digerleri o frame hicbir sey yapmaz.
///
/// Boylece "hangisi icin bastigim belli" (en yakin olan kazanir, ekranda o gorunur) VE
/// "sadece o islenir" ayni anda saglanir.
/// </summary>
public static class InteractionArbiter
{
    private static int _registeredFrame = -1;
    private static object _winnerKey;
    private static float _winnerDistSqr;

    /// <summary>Update() fazinda cagrilir - aday oyuncuya olan mesafe karesini bildirir.</summary>
    public static void Register(object key, float distSqr)
    {
        if (Time.frameCount != _registeredFrame)
        {
            _registeredFrame = Time.frameCount;
            _winnerKey = key;
            _winnerDistSqr = distSqr;
            return;
        }

        if (distSqr < _winnerDistSqr)
        {
            _winnerKey = key;
            _winnerDistSqr = distSqr;
        }
    }

    /// <summary>LateUpdate() fazinda cagrilir - bu frame icin TUM Register() cagrilari
    /// tamamlanmis olur (Unity Update -> LateUpdate sirasini garanti eder).</summary>
    public static bool IsWinner(object key)
    {
        return _registeredFrame == Time.frameCount && ReferenceEquals(_winnerKey, key);
    }
}
