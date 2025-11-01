using System;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[Serializable]
public class SavedRoom
{
    public string roomCode;
    public string createdAtLocal; // "Gün/Ay/Yýl saat"
    public long createdAtTicks;
    public List<SavedPlayer> players = new List<SavedPlayer>();
}

[Serializable]
public class SavedPlayer
{
    public string userId;
    public string nick;
    public string name;     // PLAYER_NAME
    public string gender;   // "M"/"F"
    public string statsJson; // <-- Dictionary yerine JSON string
}

[Serializable]
public class SavedRoomList
{
    public List<SavedRoom> items = new List<SavedRoom>();
}

public static class LocalRoomStorage
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "saved_rooms.json");

    public static void SaveCurrentRoomSnapshot()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

        var now = DateTime.Now;
        var sr = new SavedRoom
        {
            roomCode = PhotonNetwork.CurrentRoom.Name,
            createdAtLocal = now.ToString("dd/MM/yyyy HH:mm"),
            createdAtTicks = now.Ticks
        };

        foreach (var pl in PhotonNetwork.PlayerList)
        {
            var sp = new SavedPlayer
            {
                userId = pl.UserId,
                nick = pl.NickName,
                name = GetProp(pl, NetKeys.PLAYER_NAME, pl.NickName),
                gender = GetProp(pl, NetKeys.PLAYER_GENDER, "M"),
                statsJson = GetStatsJson(pl)
            };
            sr.players.Add(sp);
        }

        var list = LoadAll();
        // son kayýt en baþa
        list.items.Insert(0, sr);
        // istersen limit koyabilirsin (örn 50)
        SaveAll(list);
#if UNITY_EDITOR
        Debug.Log($"[LocalRoomStorage] Saved room {sr.roomCode} with {sr.players.Count} players -> {FilePath}");
#endif
    }

    static string GetProp(Player p, string key, string def)
    {
        if (p.CustomProperties != null && p.CustomProperties.TryGetValue(key, out var v) && v != null)
            return v.ToString();
        return def;
    }

    static string GetStatsJson(Player p)
    {
        if (p.CustomProperties != null &&
            p.CustomProperties.TryGetValue(NetKeys.PLAYER_STATS, out var st) &&
            st is string json && !string.IsNullOrEmpty(json))
        {
            return json;
        }
        // fallback base statlar
        return MiniJson.Serialize(new Stats().ToDict());
    }

    public static SavedRoomList LoadAll()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var text = File.ReadAllText(FilePath);
                var list = JsonUtility.FromJson<SavedRoomList>(text);
                return list ?? new SavedRoomList();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalRoomStorage] Load failed: {e.Message}");
        }
        return new SavedRoomList();
    }

    static void SaveAll(SavedRoomList list)
    {
        try
        {
            var json = JsonUtility.ToJson(list, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalRoomStorage] Save failed: {e.Message}");
        }
    }
}
