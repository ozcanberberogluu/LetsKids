using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// Player Canvas içindeki karakter item slotlarını yönetir.
/// - 1(None), 2(Sword), 3(Bow), 4(Magic)
/// - Sahip olunan silahlar: pickup ile açılır
/// - Tuşla silah seçimi → PlayerCombat.SelectWeapon()
/// </summary>
public class PlayerEquipmentUI : MonoBehaviourPun
{
    [Header("UI Refs (Canvas/CharacterItems)")]
    public GameObject slotNone;   // noltem root
    public GameObject slotSword;  // Sword root
    public GameObject slotBow;    // Bow root
    public GameObject slotMagic;  // Magic root

    [Header("Highlight Images (opsiyonel)")]
    public Image bgNone;
    public Image bgSword;
    public Image bgBow;
    public Image bgMagic;

    [Header("Colors")]
    public Color selectedColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    public Color normalColor = Color.white;

    // sahiplik
    bool hasSword = false;
    bool hasBow = false;
    bool hasMagic = false;

    PlayerCombat combat;

    void Start()
    {
        combat = GetComponent<PlayerCombat>();
        // Başlangıç: None aktif
        SetSelected(WeaponType.None);
        // None slotu her zaman görünür kalsın
        SetSlotVisible(slotNone, true);
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            TrySelect(WeaponType.None);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            TrySelect(WeaponType.Sword);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            TrySelect(WeaponType.Bow);
        else if (Input.GetKeyDown(KeyCode.Alpha4))
            TrySelect(WeaponType.Staff);
    }

    public void TrySelect(WeaponType type)
    {
        // mevcut TrySelect içeriğin; ya da sadece highlight güncelleyin
        // combat yerine artık PlayerWeaponsController kullanıyoruz,
        // ama UI highlight’ı devam etsin diye bu metodu exposed tuttuk.
    }

    void SetSelected(WeaponType type)
    {
        if (bgNone) bgNone.color = (type == WeaponType.None) ? selectedColor : normalColor;
        if (bgSword) bgSword.color = (type == WeaponType.Sword) ? selectedColor : normalColor;
        if (bgBow) bgBow.color = (type == WeaponType.Bow) ? selectedColor : normalColor;
        if (bgMagic) bgMagic.color = (type == WeaponType.Staff) ? selectedColor : normalColor;
    }

    void SetSlotVisible(GameObject slot, bool v)
    {
        if (slot && slot.activeSelf != v) slot.SetActive(v);
    }

    // === Pickup entegrasyonu (loot aldığında çağır) ===
    public void AcquireWeapon(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Sword:
                hasSword = true; SetSlotVisible(slotSword, true); break;
            case WeaponType.Bow:
                hasBow = true; SetSlotVisible(slotBow, true); break;
            case WeaponType.Staff:
                hasMagic = true; SetSlotVisible(slotMagic, true); break;
        }
    }

    // Test için (isteğe bağlı): Inspector'da butonla çağırabilirsin
    [ContextMenu("TEST: Give All")]
    void TestGiveAll()
    {
        AcquireWeapon(WeaponType.Sword);
        AcquireWeapon(WeaponType.Bow);
        AcquireWeapon(WeaponType.Staff);
    }


}
