using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using System.Linq;
using System;
using TMPro;
using UnityEngine.SceneManagement;

[Serializable]
public class Stats
{
    public int spd = 5;
    public int pow = 3;
    public int defn = 7;
    public int atkspd = 5;
    public int hp = 80;

    public Dictionary<string, int> ToDict() => new Dictionary<string, int> {
        {"spd",spd},{"pow",pow},{"def",defn},{"atkspd",atkspd},{"hp",hp}
    };
    public static Stats FromDict(Dictionary<string, object> d)
    {
        return new Stats
        {
            spd = Convert.ToInt32(d["spd"]),
            pow = Convert.ToInt32(d["pow"]),
            defn = Convert.ToInt32(d["def"]),
            atkspd = Convert.ToInt32(d["atkspd"]),
            hp = Convert.ToInt32(d["hp"])
        };
    }
}

public class CharacterCreationManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public TMP_Text roomCodeText;

    public TMP_InputField nameInput;
    public Button femaleBtn, maleBtn;
    public TMP_Text remainingPointsText;

    [Header("Stat UI")]
    public TMP_Text spdText, powText, defText, atkspdText, hpText;
    public Button spdPlus, powPlus, defPlus, atkspdPlus, hpPlus;

    public Button readyBtn;

    [Header("Room Controls")]
    public Button closeRoomBtn;

    [Header("Game Start")]
    public Button startGameBtn;

    [Header("Others")]
    public Transform othersContent;
    public GameObject otherItemPrefab;

    [Header("Saved Characters UI")]
    public Transform savedListContent;
    public GameObject savedCharacterItemPrefab;

    private int poolPoints = 10;
    private Stats localStats = new Stats();
    private string gender = "M";

    private Dictionary<int, OtherItem> otherItems = new Dictionary<int, OtherItem>();

    bool isLeaving = false;
    bool pendingLoad = false;

    // saved/claim
    SavedRoom _saved;
    readonly List<SavedCharacterItem> _savedItems = new();

    // Init bekçisi
    bool _initialized = false;
    Coroutine _waitJoinCo;

    void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

    void Start()
    {
        // GECÝKMELÝ BAÞLATMA: InRoom deðilsek bekle
        TryInit();
    }

    void TryInit()
    {
        if (_initialized) return;

        if (PhotonNetwork.InRoom)
        {
            Init();
        }
        else
        {
            if (_waitJoinCo != null) StopCoroutine(_waitJoinCo);
            _waitJoinCo = StartCoroutine(WaitUntilInRoom());
        }
    }

    System.Collections.IEnumerator WaitUntilInRoom()
    {
        // bekle: baðlanýyor/joining olabilir
        while (!PhotonNetwork.InRoom) yield return null;
        Init();
    }

    void Init()
    {
        if (_initialized) return;
        _initialized = true;

        roomCodeText.text = PhotonNetwork.CurrentRoom != null
            ? $"Oda: {PhotonNetwork.CurrentRoom.Name}"
            : "Oda: ?";

        var p = PhotonNetwork.LocalPlayer;

        // Reset state (rejoin dahil)
        gender = "M";
        nameInput.text = p != null ? p.NickName : "";
        localStats = new Stats();
        poolPoints = 10;

        var htInit = new ExitGames.Client.Photon.Hashtable
        {
            { NetKeys.PLAYER_READY, false },
            { NetKeys.PLAYER_SELECTED_CHAR, -1 }
        };
        if (PhotonNetwork.InRoom) p.SetCustomProperties(htInit);

        HookUI();
        UpdateLocalUI();
        PushProperties();

        RefreshOthersList();
        RefreshOwnerControls();
        UpdateStartButton();

        // Saved snapshot varsa listele
        LoadSavedSnapshotFromRoom();
        RenderSavedList();
        AutoClaimIfOwned();
    }

    void HookUI()
    {
        femaleBtn.onClick.AddListener(() => { gender = "F"; PushProperties(); });
        maleBtn.onClick.AddListener(() => { gender = "M"; PushProperties(); });

        nameInput.onEndEdit.AddListener(_ => PushProperties());

        spdPlus.onClick.AddListener(() => TryAdd(ref localStats.spd));
        powPlus.onClick.AddListener(() => TryAdd(ref localStats.pow));
        defPlus.onClick.AddListener(() => TryAdd(ref localStats.defn));
        atkspdPlus.onClick.AddListener(() => TryAdd(ref localStats.atkspd));
        hpPlus.onClick.AddListener(() => TryAdd(ref localStats.hp));

        readyBtn.onClick.AddListener(() =>
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable { { NetKeys.PLAYER_READY, true } }
            );
            readyBtn.interactable = false;
            UpdateStartButton();
        });

        if (startGameBtn != null)
        {
            startGameBtn.onClick.AddListener(() =>
            {
                if (!IsOwner()) return;
                if (!AllReady()) return;

                LocalRoomStorage.SaveCurrentRoomSnapshot();
                PhotonNetwork.LoadLevel("GameScene");
            });
        }

        if (closeRoomBtn != null)
        {
            closeRoomBtn.onClick.AddListener(() =>
            {
                if (!IsOwner()) return;

                PhotonNetwork.RaiseEvent(
                    NetKeys.EVT_ROOM_SHUTDOWN, null,
                    new RaiseEventOptions { Receivers = ReceiverGroup.All },
                    new SendOptions { Reliability = true });

                if (PhotonNetwork.CurrentRoom != null)
                    PhotonNetwork.CurrentRoom.SetCustomProperties(
                        new ExitGames.Client.Photon.Hashtable { { NetKeys.ROOM_SHUTDOWN, true } });

                if (!isLeaving) StartCoroutine(LeaveAfterDelay());
            });
        }
    }

    System.Collections.IEnumerator LeaveAfterDelay()
    {
        isLeaving = true;
        yield return null;
        yield return new WaitForSeconds(0.05f);
        GoToMainMenu();
    }

    bool IsOwner()
    {
        if (!PhotonNetwork.IsMasterClient) return false;
        if (PhotonNetwork.CurrentRoom == null) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.ROOM_OWNER_USERID, out var owner)) return false;
        return PhotonNetwork.LocalPlayer.UserId == (string)owner;
    }

    void RefreshOwnerControls()
    {
        if (closeRoomBtn != null)
            closeRoomBtn.gameObject.SetActive(IsOwner());
        UpdateStartButton();
    }

    void TryAdd(ref int statField)
    {
        if (poolPoints <= 0) return;
        statField += 1;
        poolPoints -= 1;
        UpdateLocalUI();
        PushProperties();
    }

    void UpdateLocalUI()
    {
        spdText.text = localStats.spd.ToString();
        powText.text = localStats.pow.ToString();
        defText.text = localStats.defn.ToString();
        atkspdText.text = localStats.atkspd.ToString();
        hpText.text = localStats.hp.ToString();
        remainingPointsText.text = $"Kalan Puan: {poolPoints}";
        readyBtn.interactable = poolPoints == 0;
    }

    void PushProperties()
    {
        if (!PhotonNetwork.InRoom) return; // <-- KRÝTÝK: InRoom deðilken çaðýrma
        var dict = localStats.ToDict();
        string json = MiniJson.Serialize(dict);

        var ht = new ExitGames.Client.Photon.Hashtable{
            { NetKeys.PLAYER_NAME, string.IsNullOrWhiteSpace(nameInput.text)? PhotonNetwork.NickName : nameInput.text.Trim() },
            { NetKeys.PLAYER_GENDER, gender },
            { NetKeys.PLAYER_STATS, json }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
    }

    bool AllReady()
    {
        return PhotonNetwork.PlayerList.All(pl =>
            pl.CustomProperties.TryGetValue(NetKeys.PLAYER_READY, out var v) && v is bool b && b);
    }

    void UpdateStartButton()
    {
        if (startGameBtn == null) return;
        bool owner = IsOwner();
        startGameBtn.gameObject.SetActive(owner);
        startGameBtn.interactable = owner && AllReady();
    }

    void RefreshOthersList()
    {
        foreach (Transform t in othersContent) Destroy(t.gameObject);
        otherItems.Clear();

        foreach (var pl in PhotonNetwork.PlayerList)
        {
            var go = Instantiate(otherItemPrefab, othersContent);
            var oi = go.GetComponent<OtherItem>();
            oi.Bind(pl);
            otherItems[pl.ActorNumber] = oi;
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (otherItems.TryGetValue(targetPlayer.ActorNumber, out var oi))
            oi.Refresh(targetPlayer);

        if (changedProps != null && changedProps.ContainsKey(NetKeys.PLAYER_READY))
            UpdateStartButton();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshOthersList();
        UpdateStartButton();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshOthersList();
        UpdateStartButton();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.ROOM_OWNER_USERID, out var owner) &&
            newMasterClient.UserId != (string)owner)
        {
            if (!isLeaving) GoToMainMenu();
        }
        RefreshOwnerControls();
    }

    // ------- Saved Snapshot & Claim -------

    void LoadSavedSnapshotFromRoom()
    {
        _saved = null;
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.ROOM_SAVED_JSON, out var js) &&
            js is string json && !string.IsNullOrEmpty(json))
        {
            _saved = LocalRoomStorage.FromJson(json);
        }
    }

    void RenderSavedList()
    {
        if (savedListContent == null || savedCharacterItemPrefab == null) return;
        foreach (Transform t in savedListContent) Destroy(t.gameObject);
        _savedItems.Clear();

        if (_saved == null || _saved.players == null) return;

        for (int i = 0; i < _saved.players.Count; i++)
        {
            var sp = _saved.players[i];
            var go = Instantiate(savedCharacterItemPrefab, savedListContent);
            var ui = go.GetComponent<SavedCharacterItem>();
            ui.Bind(sp, i, TryClaimCharacter);
            _savedItems.Add(ui);
        }

        RefreshClaimsUI();
    }

    Dictionary<int, string> ReadClaims()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.ROOM_CLAIMS_JSON, out var js) &&
            js is string json && !string.IsNullOrEmpty(json))
        {
            var d = MiniJson.Deserialize(json) as Dictionary<string, object>;
            var map = new Dictionary<int, string>();
            if (d != null)
                foreach (var kv in d)
                    if (int.TryParse(kv.Key, out var idx))
                        map[idx] = kv.Value?.ToString();
            return map;
        }
        return new Dictionary<int, string>();
    }

    string SerializeClaims(Dictionary<int, string> map)
    {
        var d = new Dictionary<string, object>();
        foreach (var kv in map) d[kv.Key.ToString()] = kv.Value;
        return MiniJson.Serialize(d);
    }

    void RefreshClaimsUI()
    {
        if (_saved == null) return;
        var claims = ReadClaims();
        for (int i = 0; i < _savedItems.Count; i++)
        {
            var ui = _savedItems[i];
            if (!claims.TryGetValue(i, out var by) || string.IsNullOrEmpty(by))
            {
                ui.SetStateFree();
            }
            else
            {
                if (by == PhotonNetwork.LocalPlayer.UserId) ui.SetStateMine();
                else
                {
                    string byName = "?";
                    var sp = _saved.players.Find(p => p.userId == by);
                    if (sp != null) byName = sp.name;
                    ui.SetStateTaken(byName);
                }
            }
        }
    }

    void AutoClaimIfOwned()
    {
        if (_saved == null) return;
        int myIndex = _saved.players.FindIndex(p => p.userId == PhotonNetwork.LocalPlayer.UserId);
        if (myIndex >= 0) TryClaimCharacter(myIndex);
    }

    void TryClaimCharacter(int index)
    {
        if (_saved == null || index < 0 || index >= _saved.players.Count) return;
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            object currentObj = null;
            room.CustomProperties?.TryGetValue(NetKeys.ROOM_CLAIMS_JSON, out currentObj);

            var claims = ReadClaims();

            if (claims.TryGetValue(index, out var by) && !string.IsNullOrEmpty(by) && by != PhotonNetwork.LocalPlayer.UserId)
            {
                RefreshClaimsUI();
                return;
            }

            int previous = -1;
            foreach (var kv in claims)
                if (kv.Value == PhotonNetwork.LocalPlayer.UserId) { previous = kv.Key; break; }
            if (previous >= 0) claims.Remove(previous);

            claims[index] = PhotonNetwork.LocalPlayer.UserId;
            string newJson = SerializeClaims(claims);

            var expected = new ExitGames.Client.Photon.Hashtable { { NetKeys.ROOM_CLAIMS_JSON, currentObj } };
            var set = new ExitGames.Client.Photon.Hashtable { { NetKeys.ROOM_CLAIMS_JSON, newJson } };

            bool success = room.SetCustomProperties(set, expected);
            if (success)
            {
                PhotonNetwork.LocalPlayer.SetCustomProperties(
                    new ExitGames.Client.Photon.Hashtable { { NetKeys.PLAYER_SELECTED_CHAR, index } }
                );

                var sp = _saved.players[index];
                nameInput.text = sp.name;
                gender = sp.gender == "F" ? "F" : "M";

                var dict = MiniJson.Deserialize(sp.statsJson) as Dictionary<string, object>;
                if (dict != null)
                {
                    localStats = Stats.FromDict(dict);
                    poolPoints = 0;
                    UpdateLocalUI();
                }
                PushProperties();
                RefreshClaimsUI();
                return;
            }
        }
        RefreshClaimsUI();
    }

    // ----- Events / Shutdown -----
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == NetKeys.EVT_ROOM_SHUTDOWN)
            GoToMainMenu();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(NetKeys.ROOM_SHUTDOWN))
            GoToMainMenu();

        if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(NetKeys.ROOM_CLAIMS_JSON))
            RefreshClaimsUI();

        if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(NetKeys.ROOM_SAVED_JSON))
        {
            LoadSavedSnapshotFromRoom();
            RenderSavedList();
        }
    }

    // Odaya sonradan girenler için: InRoom olunca tekrar Init denemesi
    public override void OnJoinedRoom()
    {
        TryInit();
    }

    void GoToMainMenu()
    {
        if (pendingLoad) return;
        pendingLoad = true;

        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        else SceneManager.LoadScene("MainMenu");
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
