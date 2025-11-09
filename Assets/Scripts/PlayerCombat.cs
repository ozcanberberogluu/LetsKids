using UnityEngine;
using Photon.Pun;

public class PlayerCombat : MonoBehaviourPun
{
    [Header("TEST MODE")]
    [Tooltip("True ise tüm slotlar aktif olur, 1-2-3-4 ile silah seçilir, sol tıkla seçili silahın attack animi oynar.")]
    public bool testEnableAllSlots = false;

    [Header("Weapon")]
    public WeaponType currentWeapon = WeaponType.None;

    [Header("Attack Timing")]
    public float baseCooldown = 0.8f;   // temel bekleme
    public float atkspdScale = 0.06f;  // cooldown = base / (1 + atkspd*scale)
    public float minCooldown = 0.15f;  // alt sınır

    [Header("Animator Params")]
    public string animParam_Attack = "Attack";
    public string animParam_AttackSpeed = "AttackSpeed"; // varsa multiplier
    // Weapon int param adını otomatik bulacağız:
    string animParam_WeaponInt = null;

    Animator anim;
    Transform cachedModel;

    PlayerStats stats;
    PlayerMovementController mover;
    PlayerEquipmentUI equipmentUI;

    float nextAttackTime;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        mover = GetComponent<PlayerMovementController>();
        equipmentUI = GetComponent<PlayerEquipmentUI>();
        RebindAnimator();
    }

    void Start()
    {
        // Test modunda tüm slotları aç
        if (photonView.IsMine && testEnableAllSlots && equipmentUI)
        {
            equipmentUI.AcquireWeapon(WeaponType.Sword);
            equipmentUI.AcquireWeapon(WeaponType.Bow);
            equipmentUI.AcquireWeapon(WeaponType.Staff);
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (anim == null || (cachedModel && !cachedModel.gameObject.activeInHierarchy))
            RebindAnimator();

        // TEST: 1-2-3-4 ile slot seçimi
        if (testEnableAllSlots)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectWeapon(WeaponType.None);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectWeapon(WeaponType.Sword);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectWeapon(WeaponType.Bow);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectWeapon(WeaponType.Staff);
        }

        if (Input.GetMouseButtonDown(0))
            TryAttack();
    }

    // === Attack ===
    void TryAttack()
    {
        if (mover && !mover.MoveEnabled) return;

        float cd = ComputeCooldown();
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + cd;

        PlayAttackAnimLocal(currentWeapon, cd);
        photonView.RPC(nameof(RpcAttack), RpcTarget.Others, (int)currentWeapon, cd);
    }

    float ComputeCooldown()
    {
        int atk = stats ? stats.atkspd : 0;
        float scaled = baseCooldown / Mathf.Max(1f, (1f + atk * atkspdScale));
        return Mathf.Max(minCooldown, scaled);
    }

    void PlayAttackAnimLocal(WeaponType weapon, float cd)
    {
        if (!anim) return;

        if (!string.IsNullOrEmpty(animParam_AttackSpeed) && HasParam(animParam_AttackSpeed))
            anim.SetFloat(animParam_AttackSpeed, (baseCooldown / Mathf.Max(cd, 0.0001f)));

        SetWeaponIntOnAnimator((int)weapon);
        anim.ResetTrigger(animParam_Attack);
        anim.SetTrigger(animParam_Attack);
    }

    [PunRPC]
    void RpcAttack(int weapon, float cd)
    {
        currentWeapon = (WeaponType)weapon;
        PlayAttackAnimLocal(currentWeapon, cd);
    }

    // === Weapon switching ===
    public void SelectWeapon(WeaponType newType)
    {
        if (!photonView.IsMine) return;
        SetWeaponLocal(newType);
        photonView.RPC(nameof(RpcSetWeapon), RpcTarget.Others, (int)newType);
    }

    void SetWeaponLocal(WeaponType newType)
    {
        currentWeapon = newType;
        SetWeaponIntOnAnimator((int)currentWeapon);
        // Debug
        Debug.Log($"[Combat] SetWeapon → {currentWeapon} (animParam='{animParam_WeaponInt}')", this);
    }

    [PunRPC]
    void RpcSetWeapon(int newType)
    {
        currentWeapon = (WeaponType)newType;
        SetWeaponIntOnAnimator((int)currentWeapon);
    }

    // === Animator binding ===
    public void RebindAnimator()
    {
        cachedModel = GetActiveModelTransform();
        anim = GetComponentInChildren<Animator>(true);

        // Param adını otomatik keşfet
        animParam_WeaponInt = DetectWeaponIntParam();
        if (anim != null && animParam_WeaponInt != null)
            anim.SetInteger(animParam_WeaponInt, (int)currentWeapon);
        else
            Debug.LogWarning("[Combat] Weapon int param bulunamadı! ('WeaponT' / 'WeaponType' / 'WeaponTyp')", this);
    }

    string DetectWeaponIntParam()
    {
        if (!anim) return null;
        // Adaylar: kod + Animator koşullarında görünen isimler
        string[] cands = { "WeaponT", "WeaponType", "WeaponTyp" };
        foreach (var p in anim.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Int) continue;
            foreach (var c in cands)
                if (p.name == c) return p.name;
        }
        return null;
    }

    void SetWeaponIntOnAnimator(int value)
    {
        if (anim == null) return;
        if (animParam_WeaponInt == null) animParam_WeaponInt = DetectWeaponIntParam();
        if (animParam_WeaponInt != null) anim.SetInteger(animParam_WeaponInt, value);
    }

    Transform GetActiveModelTransform()
    {
        var chars = transform.Find("Characters");
        if (!chars) return null;
        foreach (Transform g in chars)
            foreach (Transform model in g)
                if (model.gameObject.activeInHierarchy) return model;
        return null;
    }

    bool HasParam(string p)
    {
        if (!anim || string.IsNullOrEmpty(p)) return false;
        foreach (var prm in anim.parameters)
            if (prm.name == p) return true;
        return false;
    }

    public void Equip(WeaponType newType) => SelectWeapon(newType);
}
