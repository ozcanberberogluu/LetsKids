using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;
using System.Linq;
using System.Collections.Generic;

public class CharacterCreationAvatarManager : MonoBehaviourPunCallbacks
{
    [Header("Spawn Points (Point0 = Master)")]
    public Transform[] spawnPoints;

    // owner.ActorNumber -> PlayerAvatar
    Dictionary<int, PlayerAvatar> avatars = new Dictionary<int, PlayerAvatar>();

    void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("Not in room.");
            return;
        }

        // 1) KENDÝ avatarýný spawnla (her client sadece kendini instantiate eder)
        var index = GetSpawnIndexFor(PhotonNetwork.LocalPlayer);
        var pt = GetSpawnPoint(index);
        var go = PhotonNetwork.Instantiate("PhotonPrefabs/Player", pt.position, pt.rotation, 0);
        var myAvatar = go.GetComponent<PlayerAvatar>();
        avatars[PhotonNetwork.LocalPlayer.ActorNumber] = myAvatar;

        // Ýlk cinsiyeti uygula
        myAvatar.ApplyGenderFromProperties();

        // 2) MEVCUT oyuncular için (remote) avatar referanslarýný topla (OnPlayerEnteredRoom tetiklenene kadar bekleyebilir)
        //    PUN, instantiate edilen objeleri otomatik eþler. Start anýnda uzaktakiler henüz Dictionary'e ekli olmayabilir;
        //    OnPhotonInstantiate veya küçük gecikmeyle çözülür. Bu yüzden aþaðýdaki "Geç gelenleri yakalama" yöntemi:
        Invoke(nameof(RefreshAllAvatarsOnce), 0.2f);
    }

    void RefreshAllAvatarsOnce()
    {
        // Sahnedeki tüm PlayerAvatar’larý bul ve sözlüðe koy
        var all = FindObjectsOfType<PlayerAvatar>();
        foreach (var av in all)
        {
            var pv = av.GetComponent<PhotonView>();
            if (pv != null && pv.Owner != null)
            {
                avatars[pv.Owner.ActorNumber] = av;
                av.ApplyGenderFromProperties();
            }
        }
    }

    Transform GetSpawnPoint(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("SpawnPoints array empty. Using origin.");
            var go = new GameObject("TempSpawn");
            go.transform.position = Vector3.zero;
            return go.transform;
        }
        index = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
        return spawnPoints[index];
    }

    // MasterClient -> 0, diðerleri ActorNumber sýrasýna göre 1..n
    int GetSpawnIndexFor(Player p)
    {
        if (PhotonNetwork.MasterClient == p) return 0;

        var others = PhotonNetwork.PlayerList
            .Where(x => x != PhotonNetwork.MasterClient)
            .OrderBy(x => x.ActorNumber)
            .ToList();
        int idx = others.FindIndex(x => x == p);
        return 1 + Mathf.Max(0, idx); // 1'den baþlat
    }

    // Herhangi bir oyuncunun property’si deðiþtiðinde modelini güncelle
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps == null || changedProps.Count == 0) return;
        if (!changedProps.ContainsKey(NetKeys.PLAYER_GENDER)) return;

        if (avatars.TryGetValue(targetPlayer.ActorNumber, out var av))
        {
            av.ApplyGenderFromProperties();
        }
        else
        {
            // Avatar henüz sözlükte yoksa sahneden bulmayý dene
            var all = FindObjectsOfType<PlayerAvatar>();
            foreach (var a in all)
            {
                var pv = a.GetComponent<PhotonView>();
                if (pv != null && pv.Owner == targetPlayer)
                {
                    avatars[targetPlayer.ActorNumber] = a;
                    a.ApplyGenderFromProperties();
                    break;
                }
            }
        }
    }

    // Yeni biri geldiðinde: kýsa süre sonra avatarlarý tazele (onun instantiate’i tamamlanýnca)
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Invoke(nameof(RefreshAllAvatarsOnce), 0.2f);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        avatars.Remove(otherPlayer.ActorNumber);
        // (Ýsteðe baðlý) Sahnede onun Player objesi otomatik temizlenir (CleanupCacheOnLeave true ise)
    }
}
