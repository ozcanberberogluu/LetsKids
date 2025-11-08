using UnityEngine;
using Photon.Pun;

public class SmoothNetworkTransform : MonoBehaviourPun, IPunObservable
{
    public float posLerp = 12f;
    public float rotLerp = 12f;

    Vector3 netPos;
    Quaternion netRot;

    void Awake()
    {
        netPos = transform.position;
        netRot = transform.rotation;
    }

    void Update()
    {
        if (photonView.IsMine) return;
        transform.position = Vector3.Lerp(transform.position, netPos, posLerp * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, netRot, rotLerp * Time.deltaTime);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            netPos = (Vector3)stream.ReceiveNext();
            netRot = (Quaternion)stream.ReceiveNext();
        }
    }
}
