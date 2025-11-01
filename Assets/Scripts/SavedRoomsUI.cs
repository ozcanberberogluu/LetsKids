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
    public GameObject panelRoot;        // Kayýtlý Odalar paneli (SetActive false/true)
    public Transform listContent;       // Scroll/Content (Vertical Layout)
    public GameObject itemPrefab;       // SavedRoomItemUI prefab

    [Header("Controls")]
    public Button openBtn;              // "Kayýtlý Odalar" butonu (panel toggle)
    public Button startBtn;             // "Baþlat" (seçili kayýttan oda kur)
    public Button closeBtn;             // paneli kapat
    public Button removeBtn;            // seçili kaydý sil
    public TMP_Text infoText;           // seçili bilgi yazýsý

    SavedRoomList _list;
    int _selectedIndex = -1;
    readonly List<SavedRoomItemUI> _spawned = new();

    // pending create state
    bool _pendingCreateFromSaved = false;
    SavedRoom _pendingData = null;

    void Start()
    {
        if (openBtn) openBtn.onClick.AddListener(TogglePanel);
        if (startBtn) startBtn.onClick.AddListener(StartSelected);
        if (closeBtn) closeBtn.onClick.AddListener(() => { if (panelRoot) panelRoot.SetActive(false); });
        if (removeBtn) removeBtn.onClick.AddListener(RemoveSelected);

        if (panelRoot) panelRoot.SetActive(false);
        Refresh();

        // Baþlangýçta Saved Rooms butonunu da lobby durumuna göre kilitle
        UpdateOpenBtnState();
    }

    // === UI ===
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
        if (startBtn) startBtn.interactable = (_selectedIndex >= 0);
        if (removeBtn) removeBtn.interactable = (_selectedIndex >= 0);

        if (infoText)
        {
            if (_selectedIndex < 0) infoText.text = "Bir kayýt seçin.";
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
        // Sadece Lobby’deyken açýlabilir olsun (Connect'e basýlmadan týklanamasýn)
        openBtn.interactable = PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby;
    }

    void RemoveSelected()
    {
        if (_selectedIndex < 0) return;
        _list.items.RemoveAt(_selectedIndex);
        LocalRoomStorage.SaveAll(_list);
        Refresh();
    }

    // === Saved'den oda kurma ===
    void StartSelected()
    {
        if (_selectedIndex < 0) return;
        var data = _list.items[_selectedIndex];

        _pendingCreateFromSaved = true;
        _pendingData = data;

        // Doðru duruma gel (Master + Lobby + odada deðil)
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return; // OnLeftRoom -> ContinuePending()
        }

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            return; // OnConnectedToMaster -> (NetworkManager zaten JoinLobby yapýyor)
        }

        if (!PhotonNetwork.InLobby)
        {
            // Yalnýzca gerçekten lobby'de deðilsek dene (JoiningLobby spam'ini engeller)
            if (PhotonNetwork.NetworkClientState != ClientState.JoiningLobby)
                PhotonNetwork.JoinLobby();
            return; // OnJoinedLobby -> ContinuePending()
        }

        // Zaten hazýr durumdaysak direkt oluþtur
        ContinuePending();
    }

    void ContinuePending()
    {
        if (!_pendingCreateFromSaved || _pendingData == null) return;
        if (!(PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby && !PhotonNetwork.InRoom)) return;

        var data = _pendingData;

        var options = new RoomOptions
        {
            MaxPlayers = 8,
            PublishUserId = true,
            CustomRoomProperties = new Hashtable {
                { NetKeys.ROOM_OWNER_USERID, PhotonNetwork.LocalPlayer.UserId },
                { NetKeys.ROOM_CREATED_AT, System.DateTime.Now.Ticks },
                { NetKeys.ROOM_SAVED_JSON, LocalRoomStorage.ToJson(data) },
                { NetKeys.ROOM_CLAIMS_JSON, "" }
            },
            CustomRoomPropertiesForLobby = new string[] { NetKeys.ROOM_OWNER_USERID, NetKeys.ROOM_CREATED_AT }
        };

        bool ok = PhotonNetwork.CreateRoom(data.roomCode, options, TypedLobby.Default);
        if (!ok)
        {
            // Oda adý dolu olabilir: farklý adla tekrar dene
            string altName = $"{data.roomCode}_{Random.Range(100, 999)}";
            PhotonNetwork.CreateRoom(altName, options, TypedLobby.Default);
        }

        _pendingCreateFromSaved = false;
        _pendingData = null;
    }

    // === PUN CALLBACKS ===
    public override void OnConnectedToMaster()
    {
        // Burada JoinLobby çaðýrmayalým; NetworkManager zaten çaðýrýyor.
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
        // Odadan çýkýnca lobiye dön; pending varsa devam et
        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.InLobby && PhotonNetwork.NetworkClientState != ClientState.JoiningLobby)
                PhotonNetwork.JoinLobby();
        }
    }
}
