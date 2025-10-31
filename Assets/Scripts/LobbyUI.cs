using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using ExitGames.Client.Photon;
using System.Linq;
using System.Collections.Generic;
using TMPro;

public class LobbyUI : MonoBehaviourPunCallbacks
{
    public Transform playerListContent;
    public GameObject playerListItemPrefab;
    public Button readyBtn;
    public Button startBtn;
    public TMP_Text roomCodeText;
    public TMP_Text statusText;

    Dictionary<int, PlayerListItem> items = new Dictionary<int, PlayerListItem>();

    void Start()
    {
        roomCodeText.text = $"Oda Numarasý: {PhotonNetwork.CurrentRoom.Name}";
        RefreshPlayerList();
        readyBtn.onClick.AddListener(ToggleReady);
        startBtn.onClick.AddListener(StartGame);
        UpdateStartButton();
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

    bool AllReady()
    {
        return PhotonNetwork.PlayerList.All(pl =>
            pl.CustomProperties.TryGetValue(NetKeys.PLAYER_READY, out var v) && v is bool b && b);
    }

    void UpdateStartButton()
    {
        startBtn.gameObject.SetActive(PhotonNetwork.IsMasterClient);
        startBtn.interactable = PhotonNetwork.IsMasterClient && AllReady();
        statusText.text = AllReady() ? "Tüm oyuncular hazýr." : "Hazýr olmayanlar var.";
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerList();
        UpdateStartButton();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerList();
        UpdateStartButton();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (items.TryGetValue(targetPlayer.ActorNumber, out var item))
        {
            item.Refresh(targetPlayer);
        }
        UpdateStartButton();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        UpdateStartButton();
    }
}
