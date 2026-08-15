using UnityEngine;

/// <summary>
/// T45: Lobi ekranındaki 3D karakter önizlemesinin üstünde float eden isim etiketini
/// hedef karakterin world pozisyon+rotasyonuna her LateUpdate'te senkronlar.
///
/// Neden child-parent DEĞİL: karakter modellerinin scale'i 0.08 (bkz. ARCHITECTURE.md
/// "## Karakter Sistemi") - isim etiketini doğrudan child yaparsak ya devasa küçülür ya da
/// telafi (localScale = 1/0.08) hesaplaması gerekir. TASKS.md T45 Context'in verdiği ikinci
/// seçenek (LateUpdate ile world-space takip) kullanıldı - daha basit ve scale'den bağımsız.
///
/// "adlar karakterle hareket ediyor mu (sahnede dönerken adlar da dönsün)" test kriteri için
/// rotasyon da senkronlanır.
/// </summary>
public class FloatingNameFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 2.0f, 0f);

    private void LateUpdate()
    {
        if (target == null)
        {
            // Hedef karakter yok edildiyse (orn. slot yeniden spawn edildi), etiket de gitsin.
            Destroy(gameObject);
            return;
        }

        transform.position = target.position + worldOffset;
        transform.rotation = target.rotation;
    }
}
