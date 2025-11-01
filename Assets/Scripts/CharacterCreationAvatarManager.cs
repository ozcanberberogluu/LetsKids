using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CharacterCreationAvatarManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [Header("Spawn Points (Point0 master için tercih)")]
    public Transform[] spawnPoints;

    // owner.ActorNumber -> PlayerAvatar
    Dictionary<int, PlayerAvatar> avatars = new Dictionary<int, PlayerAvatar>();

    void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

    void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("Not in room.");
            return;
        }

        // 1) KENDÝ avatarýný spawnla (her client sadece kendini instantiate eder)
        int index = GetDeterministicSpawnIndex(PhotonNetwork.LocalPlayer);
        var pt = GetSpawnPoint(index);
        var go = PhotonNetwork.Instantiate("PhotonPrefabs/Player", pt.position, pt.rotation, 0);
        var myAvatar = go.GetComponent<PlayerAvatar>();
        avatars[PhotonNetwork.LocalPlayer.ActorNumber] = myAvatar;

        // Ýlk cinsiyeti uygula
        myAvatar.ApplyGenderFromProperties();

        // 2) Geç gelenleri bir kez toparla
        Invoke(nameof(RefreshAllAvatarsOnce), 0.2f);
    }

    void RefreshAllAvatarsOnce()
    {
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

    // === DETERMINISTIK SPAWN ===
    // Master -> 0
    // Diðerleri: Master harici oyuncularý ActorNumber'a göre sýrala -> index = 1 + sýra
    int GetDeterministicSpawnIndex(Player p)
    {
        if (PhotonNetwork.MasterClient == p) return 0;

        var others = PhotonNetwork.PlayerList
            .Where(x => x != PhotonNetwork.MasterClient)
            .OrderBy(x => x.ActorNumber)
            .ToList();

        int idx = others.FindIndex(x => x == p);
        int spawn = 1 + Mathf.Max(0, idx);

        // Güvenlik: spawn point sayýsýný aþma
        if (spawnPoints != null && spawnPoints.Length > 0)
            spawn = Mathf.Clamp(spawn, 0, spawnPoints.Length - 1);

        return spawn;
    }

    // --- Property güncellemede model senkronu ---
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps != null && changedProps.ContainsKey(NetKeys.PLAYER_GENDER))
        {
            if (avatars.TryGetValue(targetPlayer.ActorNumber, out var av))
                av.ApplyGenderFromProperties();
            else
            {
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

        if (changedProps != null && changedProps.ContainsKey(NetKeys.ROOM_SHUTDOWN))
            GoToMainMenu();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(NetKeys.ROOM_SHUTDOWN))
            GoToMainMenu();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Invoke(nameof(RefreshAllAvatarsOnce), 0.2f);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        avatars.Remove(otherPlayer.ActorNumber);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // Odayý kuran ayrýldýysa -> daðýt
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.ROOM_OWNER_USERID, out var owner) &&
            newMasterClient.UserId != (string)owner)
        {
            GoToMainMenu();
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == NetKeys.EVT_ROOM_SHUTDOWN)
            GoToMainMenu();
    }

    void GoToMainMenu()
    {
        PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("MainMenu");
    }
}
