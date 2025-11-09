using Photon.Pun;
using UnityEngine;

public class SimpleProjectile : MonoBehaviourPun
{
    public float speed = 12f;
    public int damage = 10;
    public float lifeTime = 6f;
    public LayerMask hitMask;

    Vector3 targetPos;
    bool launched;

    public void Launch(Vector3 worldTarget, float spd, int dmg, LayerMask mask)
    {
        targetPos = worldTarget;
        speed = spd;
        damage = dmg;
        hitMask = mask;
        launched = true;
        Destroy(gameObject, lifeTime); // auto cleanup
    }

    void Update()
    {
        if (!launched) return;
        Vector3 dir = (targetPos - transform.position);
        float dist = dir.magnitude;
        if (dist < 0.1f) { Destroy(gameObject); return; }
        dir /= Mathf.Max(0.0001f, dist);

        transform.position += dir * speed * Time.deltaTime;
        if (dir.sqrMagnitude > 0.00001f)
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & hitMask.value) == 0) return;

        var hp = other.GetComponentInParent<EnemyHealth>();
        if (hp) hp.TakeDamage(damage);

        Destroy(gameObject);
    }
}
