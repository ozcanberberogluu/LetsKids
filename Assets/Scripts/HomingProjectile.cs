using Photon.Pun;
using UnityEngine;

public class HomingProjectile : MonoBehaviourPun
{
    public float speed = 12f;
    public int damage = 10;
    public float lifeTime = 6f;
    public float turnRate = 540f;
    public LayerMask hitMask;

    int targetViewId = -1;
    Vector3 worldTarget;
    Transform targetT;
    bool armed = false;

    public void Setup(int viewId, Vector3 fallbackTarget, float spd, int dmg, LayerMask mask)
    {
        // Prefab disabled geldiyse aç
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        targetViewId = viewId;
        worldTarget = fallbackTarget;
        speed = spd;
        damage = dmg;
        hitMask = mask;

        if (targetViewId > 0)
        {
            var pv = PhotonView.Find(targetViewId);
            if (pv) targetT = pv.transform;   // hedef live takip
        }

        armed = true;
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        if (!armed) return;

        Vector3 aimPos = targetT ? targetT.position : worldTarget;
        Vector3 to = (aimPos - transform.position);
        if (to.sqrMagnitude < 0.01f) { Destroy(gameObject); return; }
        to.Normalize();

        Quaternion tr = Quaternion.LookRotation(to, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, tr, turnRate * Time.deltaTime);
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hitMask.value != 0 && ((1 << other.gameObject.layer) & hitMask.value) == 0) return;

        var hp = other.GetComponentInParent<EnemyHealth>();
        if (!hp) return;

        hp.TakeDamage(damage);
        Destroy(gameObject);
    }
}
