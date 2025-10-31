using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using System;
using ExitGames.Client.Photon;
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
        PhotonNetwork.AutomaticallySyncScene = true;

        connectBtn.onClick.AddListener(() =>
        {
            if (PhotonNetwork.IsConnected) return;

            PhotonNetwork.NickName = string.IsNullOrWhiteSpace(nicknameInput.text)
                ? $"Player_{UnityEngine.Random.Range(1000, 9999)}"
                : nicknameInput.text.Trim();

            PhotonNetwork.ConnectUsingSettings();
        });

        createRoomBtn.onClick.AddListener(CreateRoomFlow);
        joinRoomBtn.onClick.AddListener(JoinRoomFlow);

        ToggleLobbyControls(false);
    }

    void ToggleLobbyControls(bool on)
    {
        createRoomBtn.interactable = on;
        joinRoomBtn.interactable = on;
    }

    public override void OnConnectedToMaster()
    {
        ToggleLobbyControls(true);
        PhotonNetwork.JoinLobby(); // opsiyonel
    }

    void CreateRoomFlow()
    {
        if (!PhotonNetwork.IsConnected) return;

        string roomCode = GenerateRoomCode(6); // örn: A3F9KZ
        var roomOptions = new RoomOptions
        {
            MaxPlayers = 8,
            PublishUserId = true,
            CleanupCacheOnLeave = true,
            PlayerTtl = 0
        };

        var props = new Hashtable
        {
            { NetKeys.ROOM_CREATED_AT, DateTime.UtcNow.Ticks.ToString() },
            { NetKeys.ROOM_OWNER_USERID, PhotonNetwork.LocalPlayer.UserId }
        };
        roomOptions.CustomRoomProperties = props;
        roomOptions.CustomRoomPropertiesForLobby = new string[] { NetKeys.ROOM_CREATED_AT };

        PhotonNetwork.CreateRoom(roomCode, roomOptions, TypedLobby.Default);
    }

    void JoinRoomFlow()
    {
        if (!PhotonNetwork.IsConnected) return;
        string code = joinCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code)) return;
        PhotonNetwork.JoinRoom(code);
    }

    string GenerateRoomCode(int len)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(len);
        for (int i = 0; i < len; i++) sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
        return sb.ToString();
    }

    public override void OnCreatedRoom()
    {
        var ht = new Hashtable
        {
            { NetKeys.PLAYER_READY, false },
            { NetKeys.PLAYER_GENDER, "M" },
            { NetKeys.PLAYER_NAME, PhotonNetwork.NickName }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
    }

    public override void OnJoinedRoom()
    {
        var ht = new Hashtable();
        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(NetKeys.PLAYER_READY))
            ht[NetKeys.PLAYER_READY] = false;
        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(NetKeys.PLAYER_GENDER))
            ht[NetKeys.PLAYER_GENDER] = "M";
        if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(NetKeys.PLAYER_NAME))
            ht[NetKeys.PLAYER_NAME] = PhotonNetwork.NickName;

        if (ht.Count > 0) PhotonNetwork.LocalPlayer.SetCustomProperties(ht);

        PhotonNetwork.LoadLevel("Lobby");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"CreateRoomFailed: {returnCode} {message}");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"JoinRoomFailed: {returnCode} {message}");
    }
}
