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

    public TMP_InputField nameInput; // TMP
    public Button femaleBtn, maleBtn;
    public TMP_Text remainingPointsText;

    [Header("Stat UI")]
    public TMP_Text spdText, powText, defText, atkspdText, hpText;
    public Button spdPlus, powPlus, defPlus, atkspdPlus, hpPlus;

    public Button readyBtn;

    [Header("Room Controls")]
    public Button closeRoomBtn; // sadece odanýn kurucusunda (ve MasterClient) görünür

    [Header("Game Start")]
    public Button startGameBtn; // sadece owner görür, AllReady ise aktif

    [Header("Others")]
    public Transform othersContent;
    public GameObject otherItemPrefab;

    private int poolPoints = 10;
    private Stats localStats = new Stats();
    private string gender = "M";

    private Dictionary<int, OtherItem> otherItems = new Dictionary<int, OtherItem>();

    // Shutdown/sahne geçiþi korumalarý
    bool isLeaving = false;
    bool pendingLoad = false;

    void OnEnable() { PhotonNetwork.AddCallbackTarget(this); }
    void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

    void Start()
    {
        roomCodeText.text = $"Oda: {PhotonNetwork.CurrentRoom.Name}";

        var p = PhotonNetwork.LocalPlayer;

        // REJOIN/ÝLK GÝRÝÞ: her zaman resetle
        gender = "M";
        nameInput.text = p.NickName;     // varsayýlan olarak NickName
        localStats = new Stats();        // base statlar
        poolPoints = 10;

        // herkes hazýr deðil baþlangýçta
        var htInit = new ExitGames.Client.Photon.Hashtable
        {
            { NetKeys.PLAYER_READY, false }
        };
        p.SetCustomProperties(htInit);

        HookUI();
        PushProperties();     // resetlenmiþ deðerleri yayýnla
        RefreshOthersList();
        RefreshOwnerControls();
        UpdateStartButton();  // start butonunu güncelle
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

        UpdateLocalUI();

        readyBtn.onClick.AddListener(() =>
        {
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

                // 1) Host: Saved Rooms'a kaydet
                LocalRoomStorage.SaveCurrentRoomSnapshot();

                // 2) Herkesi GameScene'e taþý
                PhotonNetwork.LoadLevel("GameScene");
            });
        }

        if (closeRoomBtn != null)
        {
            closeRoomBtn.onClick.AddListener(() =>
            {
                if (!IsOwner()) return;

                // Event yayýnla (garanti)
                PhotonNetwork.RaiseEvent(
                    NetKeys.EVT_ROOM_SHUTDOWN,
                    null,
                    new RaiseEventOptions { Receivers = ReceiverGroup.All },
                    new SendOptions { Reliability = true }
                );

                // Property de set et (edge-case güvence)
                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new ExitGames.Client.Photon.Hashtable { { NetKeys.ROOM_SHUTDOWN, true } }
                );

                // Küçük gecikmeyle güvenli leave
                if (!isLeaving) StartCoroutine(LeaveAfterDelay());
            });
        }
    }

    System.Collections.IEnumerator LeaveAfterDelay()
    {
        isLeaving = true;
        yield return null; // 1 frame
        yield return new WaitForSeconds(0.05f);
        GoToMainMenu();
    }

    // Odayý kuran (owner) ve þu an MasterClient olan kiþi mi?
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
        // Ready butonu sadece puanlar bitince aktif
        readyBtn.interactable = poolPoints == 0;
    }

    void PushProperties()
    {
        var dict = localStats.ToDict();
        string json = MiniJson.Serialize(dict);

        var ht = new ExitGames.Client.Photon.Hashtable{
            { NetKeys.PLAYER_NAME, string.IsNullOrWhiteSpace(nameInput.text)? PhotonNetwork.NickName : nameInput.text.Trim() },
            { NetKeys.PLAYER_GENDER, gender },
            { NetKeys.PLAYER_STATS, json }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
    }

    // === START butonu mantýðý ===
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
        {
            oi.Refresh(targetPlayer);
        }
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
        // Odayý kuran ayrýldýysa -> daðýt
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.ROOM_OWNER_USERID, out var owner) &&
            newMasterClient.UserId != (string)owner)
        {
            if (!isLeaving) GoToMainMenu();
        }
        RefreshOwnerControls(); // Master deðiþtiyse owner görünürlüðünü güncelle
    }

    // Event dinleyici (garanti daðýtým)
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == NetKeys.EVT_ROOM_SHUTDOWN)
            GoToMainMenu();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(NetKeys.ROOM_SHUTDOWN))
            GoToMainMenu();
    }

    void GoToMainMenu()
    {
        if (pendingLoad) return;
        pendingLoad = true;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom(); // sahneyi OnLeftRoom'da yükle
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
