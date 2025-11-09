using System.Collections;
using Photon.Pun;
using UnityEngine;

public class PlayerWeaponsController : MonoBehaviourPun
{
    [Header("TEST")]
    public bool testEnableAllSlots = true;

    [Header("Current Weapon (synced via RPC)")]
    public WeaponType currentWeapon = WeaponType.Sword;

    [Header("Item Pivots on Player.prefab/Items")]
    public Transform itemsRoot;
    public Transform swordPivot;
    public Transform arrowPivot;
    public Transform magePivot;

    [Header("Sword")]
    public float swordRotateSpeed = 240f;
    public int swordDamage = 10;

    [Header("Bow")]
    public string arrowPrefabName = "PhotonPrefabs/ArrowProjectile";
    public float arrowSpeed = 14f;
    public int bowDamage = 12;
    public float bowFireInterval = 1.0f;  // <- 1 sn

    [Header("Mage")]
    public string magePrefabName = "PhotonPrefabs/MageProjectile";
    public float mageSpeed = 11f;
    public int mageDamage = 14;
    public float mageFireInterval = 1.0f; // <- 1 sn

    [Header("Targeting/Range")]
    public LayerMask enemyMask;
    public float fireRange = 10f;
    public float scanEvery = 0.2f;

    PlayerMovementController mover;
    PlayerEquipmentUI ui;

    Transform cachedTarget;
    float nextBowTime, nextMageTime;

    void Awake()
    {
        mover = GetComponent<PlayerMovementController>();
        ui = GetComponent<PlayerEquipmentUI>();

        if (!itemsRoot) itemsRoot = transform.Find("Items");
        if (!swordPivot) swordPivot = itemsRoot ? itemsRoot.Find("SwordPivot") : null;
        if (!arrowPivot) arrowPivot = itemsRoot ? itemsRoot.Find("ArrowPivot") : null;
        if (!magePivot) magePivot = itemsRoot ? itemsRoot.Find("MagePivot") : null;

        if (photonView.IsMine) SelectWeapon(WeaponType.Sword, false);
        UpdatePivotActives();
        StartCoroutine(TargetScanLoop());
    }

    void Update()
    {
        UpdatePivotActives();

        if (currentWeapon == WeaponType.Sword && swordPivot)
            swordPivot.Rotate(0f, swordRotateSpeed * Time.deltaTime, 0f, Space.World);

        if (!photonView.IsMine) return;
        if (!mover || !mover.MoveEnabled) return;

        // TEST tuþlarý
        if (testEnableAllSlots)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectWeapon(WeaponType.Sword);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectWeapon(WeaponType.Bow);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectWeapon(WeaponType.Staff);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectWeapon(WeaponType.None);
        }

        // OTO FIRE
        if (currentWeapon == WeaponType.Bow)
        {
            if (Time.time >= nextBowTime)
            {
                if (TryGetTargetInRange(out Transform t))
                {
                    FireHoming(arrowPrefabName, arrowPivot, t, arrowSpeed, bowDamage);
                    nextBowTime = Time.time + bowFireInterval;
                }
                else
                {
                    // target yoksa bekleme yap, sýk dene
                    nextBowTime = Time.time + 0.2f;
                }
            }
        }
        else if (currentWeapon == WeaponType.Staff)
        {
            if (Time.time >= nextMageTime)
            {
                if (TryGetTargetInRange(out Transform t))
                {
                    FireHoming(magePrefabName, magePivot, t, mageSpeed, mageDamage);
                    nextMageTime = Time.time + mageFireInterval;
                }
                else
                {
                    nextMageTime = Time.time + 0.2f;
                }
            }
        }
    }

    // ---- Select (RPC) ----
    public void SelectWeapon(WeaponType type) => SelectWeapon(type, true);
    void SelectWeapon(WeaponType type, bool sync)
    {
        currentWeapon = type;
        if (sync && photonView.IsMine)
            photonView.RPC(nameof(RpcSetWeapon), RpcTarget.Others, (int)type);

        if (ui)
        {
            ui.TrySelect(type); // highlight (metodun zaten var)
        }
    }
    [PunRPC] void RpcSetWeapon(int t) { currentWeapon = (WeaponType)t; }

    void UpdatePivotActives()
    {
        if (swordPivot) swordPivot.gameObject.SetActive(currentWeapon == WeaponType.Sword);
        if (arrowPivot) arrowPivot.gameObject.SetActive(currentWeapon == WeaponType.Bow);
        if (magePivot) magePivot.gameObject.SetActive(currentWeapon == WeaponType.Staff);
    }

    // ---- Targeting ----
    IEnumerator TargetScanLoop()
    {
        var wait = new WaitForSeconds(scanEvery);
        while (true)
        {
            if (photonView.IsMine) cachedTarget = FindClosestInRange(fireRange);
            yield return wait;
        }
    }
    Transform FindClosestInRange(float range)
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, range, enemyMask, QueryTriggerInteraction.Collide);
        float best = float.MaxValue; Transform bestT = null;
        foreach (var c in cols)
        {
            float d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; bestT = c.transform; }
        }
        return bestT;
    }
    bool TryGetTargetInRange(out Transform t)
    {
        t = cachedTarget ? cachedTarget : FindClosestInRange(fireRange);
        return t != null;
    }

    // ---- Fire ----
    void FireHoming(string prefabPath, Transform firePivot, Transform target, float speed, int damage)
    {
        if (!firePivot || !target) return;

        int viewId = -1;
        var pv = target.GetComponentInParent<PhotonView>();
        if (pv) viewId = pv.ViewID;

        GameObject go = null;
        try
        {
            go = PhotonNetwork.Instantiate(prefabPath, firePivot.position, Quaternion.identity);
        }
        catch
        {
            Debug.LogError($"[Weapons] PhotonNetwork.Instantiate FAILED. Prefab must have PhotonView. Path='{prefabPath}'");
            return;
        }
        if (!go) return;

        // Prefab disabled ise aç
        if (!go.activeSelf) go.SetActive(true);

        var proj = go.GetComponent<HomingProjectile>();
        if (!proj)
        {
            Debug.LogError($"[Weapons] Prefab '{prefabPath}' has no HomingProjectile.");
            return;
        }
        proj.Setup(viewId, target.position, speed, damage, enemyMask);
    }
}
