using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerAvatar : MonoBehaviour
{
    public GameObject maleModel;
    public GameObject femaleModel;

    PhotonView pv;
    Player owner;

    void Awake()
    {
        pv = GetComponent<PhotonView>();
        owner = pv.Owner;
    }

    // Sahneye ilk geldiðinde veya property güncellendiðinde çaðrýlýr
    public void ApplyGenderFromProperties()
    {
        string g = "M";
        if (owner != null && owner.CustomProperties != null &&
            owner.CustomProperties.TryGetValue(NetKeys.PLAYER_GENDER, out var v))
        {
            g = v.ToString();
        }
        SetGender(g);
    }

    public void SetGender(string g)
    {
        bool isFemale = g == "F";
        if (maleModel != null) maleModel.SetActive(!isFemale);
        if (femaleModel != null) femaleModel.SetActive(isFemale);
    }
}
