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

        // 1) KEND� avatar�n� spawnla (her client sadece kendini instantiate eder)
        var index = GetSpawnIndexFor(PhotonNetwork.LocalPlayer);
        var pt = GetSpawnPoint(index);
        var go = PhotonNetwork.Instantiate("PhotonPrefabs/Player", pt.position, pt.rotation, 0);
        var myAvatar = go.GetComponent<PlayerAvatar>();
        avatars[PhotonNetwork.LocalPlayer.ActorNumber] = myAvatar;

        // �lk cinsiyeti uygula
        myAvatar.ApplyGenderFromProperties();

        // 2) MEVCUT oyuncular i�in (remote) avatar referanslar�n� topla (OnPlayerEnteredRoom tetiklenene kadar bekleyebilir)
        //    PUN, instantiate edilen objeleri otomatik e�ler. Start an�nda uzaktakiler hen�z Dictionary'e ekli olmayabilir;
        //    OnPhotonInstantiate veya k���k gecikmeyle ��z�l�r. Bu y�zden a�a��daki "Ge� gelenleri yakalama" y�ntemi:
        Invoke(nameof(RefreshAllAvatarsOnce), 0.2f);
    }

    void RefreshAllAvatarsOnce()
    {
        // Sahnedeki t�m PlayerAvatar�lar� bul ve s�zl��e koy
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

    // MasterClient -> 0, di�erleri ActorNumber s�ras�na g�re 1..n
    int GetSpawnIndexFor(Player p)
    {
        if (PhotonNetwork.MasterClient == p) return 0;

        var others = PhotonNetwork.PlayerList
            .Where(x => x != PhotonNetwork.MasterClient)
            .OrderBy(x => x.ActorNumber)
            .ToList();
        int idx = others.FindIndex(x => x == p);
        return 1 + Mathf.Max(0, idx); // 1'den ba�lat
    }

    // Herhangi bir oyuncunun property�si de�i�ti�inde modelini g�ncelle
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
            // Avatar hen�z s�zl�kte yoksa sahneden bulmay� dene
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

    // Yeni biri geldi�inde: k�sa s�re sonra avatarlar� tazele (onun instantiate�i tamamlan�nca)
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Invoke(nameof(RefreshAllAvatarsOnce), 0.2f);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        avatars.Remove(otherPlayer.ActorNumber);
        // (�ste�e ba�l�) Sahnede onun Player objesi otomatik temizlenir (CleanupCacheOnLeave true ise)
    }
}
