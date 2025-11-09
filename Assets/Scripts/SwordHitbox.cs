using Photon.Pun;
using UnityEngine;

public class SwordHitbox : MonoBehaviour
{
    public int damage = 10;

    void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    void OnTriggerStay(Collider other)
    {
        // Ýstersen sürekli tick hasar için Stay kullan
        // TryHit(other);
    }

    void TryHit(Collider col)
    {
        var hp = col.GetComponentInParent<EnemyHealth>();
        if (!hp) return;
        hp.TakeDamage(damage);
    }
}
