using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    public TMP_InputField nicknameInput;
    public Button connectBtn;
    public Button createRoomBtn;
    public TMP_InputField joinCodeInput;
    public Button joinRoomBtn;

    void Start()
    {
        connectBtn.onClick.AddListener(OnConnectClicked);
        createRoomBtn.onClick.AddListener(OnCreateClicked);
        joinRoomBtn.onClick.AddListener(OnJoinClicked);

        // Baþta butonlarý kilitle (yalnýzca Lobby'ye girince açýlacak)
        SetRoomButtonsInteractable(false);
    }

    void OnConnectClicked()
    {
        var nick = string.IsNullOrWhiteSpace(nicknameInput.text) ? $"Player{Random.Range(1000, 9999)}" : nicknameInput.text.Trim();
        PhotonNetwork.NickName = nick;

        if (!PhotonNetwork.IsConnected)
            PhotonNetwork.ConnectUsingSettings();
        else
            PhotonNetwork.JoinLobby(); // zaten baðlýysa direkt lobiye
    }

    void OnCreateClicked()
    {
        // Güvenlik: sadece Lobby’deyken oda kur
        if (!(PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby)) return;

        string code = GenerateRoomCode(6);
        RoomOptions opt = new RoomOptions { MaxPlayers = 8, PublishUserId = true };
        PhotonNetwork.CreateRoom(code, opt, TypedLobby.Default);
    }

    void OnJoinClicked()
    {
        if (!(PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby)) return;

        var code = joinCodeInput.text?.Trim();
        if (string.IsNullOrEmpty(code)) return;

        PhotonNetwork.JoinRoom(code);
    }

    string GenerateRoomCode(int len)
    {
        const string A = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(len);
        for (int i = 0; i < len; i++) sb.Append(A[Random.Range(0, A.Length)]);
        return sb.ToString();
    }

    void SetRoomButtonsInteractable(bool v)
    {
        if (createRoomBtn) createRoomBtn.interactable = v;
        if (joinRoomBtn) joinRoomBtn.interactable = v;
    }

    // ===== PUN Callbacks =====
    public override void OnConnectedToMaster()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.JoinLobby(); // Oda kur/katýl için þart
    }

    public override void OnJoinedLobby()
    {
        SetRoomButtonsInteractable(true);
    }

    public override void OnLeftLobby()
    {
        SetRoomButtonsInteractable(false);
    }

    public override void OnCreatedRoom()
    {
        // Owner bilgisini yaz (saved rooms için faydalý)
        if (PhotonNetwork.CurrentRoom != null)
        {
            var ht = new ExitGames.Client.Photon.Hashtable {
                { NetKeys.ROOM_OWNER_USERID, PhotonNetwork.LocalPlayer.UserId },
                { NetKeys.ROOM_CREATED_AT, System.DateTime.Now.Ticks }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(ht);
        }
    }

    public override void OnJoinedRoom()
    {
        // CharacterCreation’a geç
        PhotonNetwork.LoadLevel("CharacterCreation");
    }

    public override void OnLeftRoom()
    {
        // Oda kapandýktan sonra main menüde tekrar lobiye gir
        if (PhotonNetwork.IsConnected) PhotonNetwork.JoinLobby();
    }
}
