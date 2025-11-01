using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Linq;

public class GameSceneManager : MonoBehaviourPunCallbacks
{
    [Header("Spawn Points (0 = Master)")]
    public Transform[] spawnPoints;

    void Start()
    {
        // Her client sadece kendi avatarını spawnlar
        int index = GetDeterministicSpawnIndex(PhotonNetwork.LocalPlayer);
        var pt = GetSpawnPoint(index);
        PhotonNetwork.Instantiate("PhotonPrefabs/Player", pt.position, pt.rotation, 0);
    }

    Transform GetSpawnPoint(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return new GameObject("TempSpawn").transform;
        index = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
        return spawnPoints[index];
    }

    int GetDeterministicSpawnIndex(Player p)
    {
        if (PhotonNetwork.MasterClient == p) return 0;
        var others = PhotonNetwork.PlayerList.Where(x => x != PhotonNetwork.MasterClient)
                         .OrderBy(x => x.ActorNumber).ToList();
        int idx = others.FindIndex(x => x == p);
        return 1 + Mathf.Max(0, idx);
    }
}
