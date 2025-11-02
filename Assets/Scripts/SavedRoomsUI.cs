using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;

public class SavedRoomsUI : MonoBehaviourPunCallbacks
{
    [Header("Panel")]
    public GameObject panelRoot;
    public Transform listContent;
    public GameObject itemPrefab;

    [Header("Controls")]
    public Button openBtn;
    public Button startBtn;
    public Button closeBtn;
    public Button removeBtn;
    public Button removeAllBtn;   // YENÝ: tüm kayýtlarý sil
    public TMP_Text infoText;

    SavedRoomList _list;
    int _selectedIndex = -1;
    readonly List<SavedRoomItemUI> _spawned = new();

    bool _pendingCreateFromSaved = false;
    SavedRoom _pendingData = null;

    void Start()
    {
        if (openBtn) openBtn.onClick.AddListener(TogglePanel);
        if (startBtn) startBtn.onClick.AddListener(StartSelected);
        if (closeBtn) closeBtn.onClick.AddListener(() => { if (panelRoot) panelRoot.SetActive(false); });
        if (removeBtn) removeBtn.onClick.AddListener(RemoveSelected);
        if (removeAllBtn) removeAllBtn.onClick.AddListener(RemoveAll); // YENÝ

        if (panelRoot) panelRoot.SetActive(false);
        Refresh();
        UpdateOpenBtnState();
    }

    void TogglePanel()
    {
        if (!panelRoot) return;
        panelRoot.SetActive(!panelRoot.activeSelf);
        if (panelRoot.activeSelf) Refresh();
    }

    void Refresh()
    {
        _list = LocalRoomStorage.LoadAll();
        foreach (Transform t in listContent) Destroy(t.gameObject);
        _spawned.Clear();
        _selectedIndex = -1;
        UpdateButtons();

        for (int i = 0; i < _list.items.Count; i++)
        {
            var data = _list.items[i];
            var go = Instantiate(itemPrefab, listContent);
            var ui = go.GetComponent<SavedRoomItemUI>();
            ui.Bind(data, i, OnSelectItem);
            _spawned.Add(ui);
        }
    }

    void OnSelectItem(int index)
    {
        _selectedIndex = index;
        for (int i = 0; i < _spawned.Count; i++)
            _spawned[i].SetSelected(i == _selectedIndex);
        UpdateButtons();
    }

    void UpdateButtons()
    {
        bool hasSelection = _selectedIndex >= 0;
        bool hasAny = _list != null && _list.items.Count > 0;

        if (startBtn) startBtn.interactable = hasSelection;
        if (removeBtn) removeBtn.interactable = hasSelection;
        if (removeAllBtn) removeAllBtn.interactable = hasAny;

        if (infoText)
        {
            if (!hasSelection) infoText.text = "Bir kayýt seçin.";
            else
            {
                var r = _list.items[_selectedIndex];
                infoText.text = $"{r.roomCode} - {r.createdAtLocal} - {r.players.Count} oyuncu";
            }
        }
    }

    void UpdateOpenBtnState()
    {
        if (!openBtn) return;
        openBtn.interactable = PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby;
    }

    void RemoveSelected()
    {
        if (_selectedIndex < 0) return;
        _list.items.RemoveAt(_selectedIndex);
        LocalRoomStorage.SaveAll(_list);
        Refresh();
    }

    // YENÝ: tüm kayýtlarý sil
    void RemoveAll()
    {
        LocalRoomStorage.ClearAll(true); // tüm kalýntýlarý temizle
        Refresh();
    }

    void StartSelected()
    {
        if (_selectedIndex < 0) return;
        var data = _list.items[_selectedIndex];

        _pendingCreateFromSaved = true;
        _pendingData = data;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        if (!PhotonNetwork.InLobby)
        {
            if (PhotonNetwork.NetworkClientState != ClientState.JoiningLobby)
                PhotonNetwork.JoinLobby();
            return;
        }

        ContinuePending();
    }

    void ContinuePending()
    {
        if (!_pendingCreateFromSaved || _pendingData == null) return;
        if (!(PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby && !PhotonNetwork.InRoom)) return;

        var data = _pendingData;

        // KAYDIN saveId'si yoksa bir kere üret ve yerel dosyada güncelle
        if (string.IsNullOrEmpty(data.saveId))
        {
            data.saveId = System.Guid.NewGuid().ToString("N");
            // liste içinde bul ve güncelle
            var all = LocalRoomStorage.LoadAll();
            int idx = all.items.FindIndex(x => x != null && x.roomCode == data.roomCode && (x.saveId == null || x.saveId == ""));
            if (idx >= 0) { all.items[idx].saveId = data.saveId; LocalRoomStorage.SaveAll(all); }
        }

        var options = new RoomOptions
        {
            MaxPlayers = 8,
            PublishUserId = true,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable {
            { NetKeys.ROOM_OWNER_USERID, PhotonNetwork.LocalPlayer.UserId },
            { NetKeys.ROOM_CREATED_AT, System.DateTime.Now.Ticks },
            { NetKeys.ROOM_SAVED_JSON, LocalRoomStorage.ToJson(data) },
            { NetKeys.ROOM_CLAIMS_JSON, "" },
            { NetKeys.ROOM_SAVE_ID, data.saveId } // YENÝ: kaydýn kimliðini odaya yaz
        },
            CustomRoomPropertiesForLobby = new string[] { NetKeys.ROOM_OWNER_USERID, NetKeys.ROOM_CREATED_AT }
        };

        bool ok = PhotonNetwork.CreateRoom(data.roomCode, options, TypedLobby.Default);
        if (!ok)
        {
            string altName = $"{data.roomCode}_{Random.Range(100, 999)}";
            PhotonNetwork.CreateRoom(altName, options, TypedLobby.Default);
        }

        _pendingCreateFromSaved = false;
        _pendingData = null;
    }

    // ===== PUN =====
    public override void OnConnectedToMaster()
    {
        UpdateOpenBtnState();
    }

    public override void OnJoinedLobby()
    {
        UpdateOpenBtnState();
        if (_pendingCreateFromSaved) ContinuePending();
    }

    public override void OnLeftLobby()
    {
        UpdateOpenBtnState();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        UpdateOpenBtnState();
    }



    public override void OnLeftRoom()
    {
        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.InLobby && PhotonNetwork.NetworkClientState != ClientState.JoiningLobby)
                PhotonNetwork.JoinLobby();
        }
    }
}
