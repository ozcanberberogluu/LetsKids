using UnityEngine;
using Photon.Pun;

public class PlayerCombat : MonoBehaviourPun
{
    public WeaponType currentWeapon = WeaponType.None;

    Animator anim;
    Transform cachedModel; // aktif model (ManBabylv1 / WomanBabylv1 gibi)

    void Awake()
    {
        RebindAnimator();
    }

    // Aktif modeli/animatörü yeniden bul (gender deðiþtiðinde çaðýr)
    public void RebindAnimator()
    {
        // aktif model (Characters altýnda aktif olan çocuk)
        cachedModel = GetActiveModelTransform();
        anim = GetComponentInChildren<Animator>(true); // çocuklarda ara

        // Güvenlik: Hâlâ bulamazsa bir sonraki frameda tekrar dene
        if (anim == null) Invoke(nameof(RebindAnimator), 0.1f);
    }

    Transform GetActiveModelTransform()
    {
        // Senin hiyerarþine göre ayarladým
        var chars = transform.Find("Characters");
        if (!chars) return null;
        foreach (Transform child in chars)
        {
            // Man / Woman klasörü içindeki aktif olaný bul
            foreach (Transform model in child)
                if (model.gameObject.activeInHierarchy) return model;
        }
        return null;
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // Model/animator runtime’da deðiþmiþse otomatik yeniden baðla
        if (anim == null || (cachedModel && !cachedModel.gameObject.activeInHierarchy))
            RebindAnimator();

        if (Input.GetMouseButtonDown(0))
            photonView.RPC(nameof(RpcAttack), RpcTarget.All, (int)currentWeapon);
    }

    [PunRPC]
    void RpcAttack(int weapon)
    {
        if (!anim) return;
        anim.ResetTrigger("Attack");
        anim.SetInteger("WeaponType", weapon);
        anim.SetTrigger("Attack");
    }

    // Ýleride loot ile çaðýracaksýn
    public void Equip(WeaponType newType)
    {
        if (!photonView.IsMine) return;
        currentWeapon = newType;
        // burada silah modelini aç/kapa için ayrýca bir RPC düþüneriz
    }
}
