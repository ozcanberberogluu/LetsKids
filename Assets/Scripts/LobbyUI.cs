using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using ExitGames.Client.Photon;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

public class LobbyUI : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public Transform playerListContent;
    public GameObject playerListItemPrefab;
    public Button readyBtn;
    public Button startBtn;
    public TMP_Text roomCodeText;
    public TMP_Text statusText;

    public Button closeRoomBtn; // sadece owner görür

    Dictionary<int, PlayerListItem> items = new Dictionary<int, PlayerListItem>();
    bool isLeaving = false; // tekrar leave'i engelle
    bool pendingLoad = false;

    void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

    void Start()
    {
        roomCodeText.text = $"Oda Numarası: {PhotonNetwork.CurrentRoom.Name}";
        RefreshPlayerList();
        readyBtn.onClick.AddListener(ToggleReady);
        startBtn.onClick.AddListener(StartGame);

        if (closeRoomBtn != null)
            closeRoomBtn.onClick.AddListener(CloseRoom);

        UpdateButtons();
    }

    void RefreshPlayerList()
    {
        foreach (Transform t in playerListContent) Destroy(t.gameObject);
        items.Clear();

        foreach (var p in PhotonNetwork.PlayerList)
        {
            var go = Instantiate(playerListItemPrefab, playerListContent);
            var item = go.GetComponent<PlayerListItem>();
            item.Bind(p);
            items[p.ActorNumber] = item;
        }
    }

    void ToggleReady()
    {
        var p = PhotonNetwork.LocalPlayer;
        bool current = p.CustomProperties.TryGetValue(NetKeys.PLAYER_READY, out var v) && (bool)v;
        var ht = new ExitGames.Client.Photon.Hashtable { { NetKeys.PLAYER_READY, !current } };
        p.SetCustomProperties(ht);
    }

    void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!AllReady()) return;
        PhotonNetwork.LoadLevel("CharacterCreation");
    }

    // === Odayı kapat ===
    void CloseRoom()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 1) Olayı yayınla (herkes kendi kendine LeaveRoom + MainMenu yapacak)
        PhotonNetwork.RaiseEvent(
            NetKeys.EVT_ROOM_SHUTDOWN,
            null,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            new SendOptions { Reliability = true }
        );

        // 2) Property de set et (edge-case güvence)
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable { { NetKeys.ROOM_SHUTDOWN, true } }
        );

        // 3) Minik gecikme sonra güvenli çıkış
        if (!isLeaving) StartCoroutine(LeaveAfterDelay());
    }

    System.Collections.IEnumerator LeaveAfterDelay()
    {
        isLeaving = true;
        yield return null; // 1 frame
        yield return new WaitForSeconds(0.05f);
        GoToMainMenu();
    }

    bool AllReady()
    {
        return PhotonNetwork.PlayerList.All(pl =>
            pl.CustomProperties.TryGetValue(NetKeys.PLAYER_READY, out var v) && v is bool b && b);
    }

    void UpdateButtons()
    {
        startBtn.gameObject.SetActive(PhotonNetwork.IsMasterClient);
        startBtn.interactable = PhotonNetwork.IsMasterClient && AllReady();

        bool isOwner = false;
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.ROOM_OWNER_USERID, out var owner))
        {
            isOwner = PhotonNetwork.IsMasterClient && PhotonNetwork.LocalPlayer.UserId == (string)owner;
        }
        if (closeRoomBtn != null) closeRoomBtn.gameObject.SetActive(isOwner);

        statusText.text = AllReady() ? "Tüm oyuncular hazır." : "Hazır olmayanlar var.";
    }

    // --------- Events ----------
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(NetKeys.ROOM_SHUTDOWN))
            GoToMainMenu();
    }


    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.ROOM_OWNER_USERID, out var owner) &&
            newMasterClient.UserId != (string)owner)
        {
            GoToMainMenu();
            return;
        }
        UpdateButtons();
    }


    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == NetKeys.EVT_ROOM_SHUTDOWN)
        {
            GoToMainMenu(); // hepsi burada toplanıyor
        }
    }

    void GoToMainMenu()
    {
        if (pendingLoad) return;
        pendingLoad = true;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom(); // sahne değişimini OnLeftRoom’da yap
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    public override void OnLeftRoom()
    {
        // Buraya mutlaka düşer → güvenli sahne yükleme
        SceneManager.LoadScene("MainMenu");
    }


    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerList();
        UpdateButtons();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerList();
        UpdateButtons();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (items.TryGetValue(targetPlayer.ActorNumber, out var item))
        {
            item.Refresh(targetPlayer);
        }
        UpdateButtons();
    }
}
