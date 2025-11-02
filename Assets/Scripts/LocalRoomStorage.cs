using System;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[Serializable]
public class SavedRoom
{
    public string saveId;             // YENİ: bu kaydın benzersiz kimliği (GUID)
    public string ownerUserId;
    public string roomCode;
    public string createdAtLocal;
    public long createdAtTicks;
    public List<SavedPlayer> players = new List<SavedPlayer>();
}

[Serializable]
public class SavedPlayer
{
    public string userId;
    public string nick;
    public string name;
    public string gender;
    public string statsJson;
}

[Serializable]
public class SavedRoomList
{
    public List<SavedRoom> items = new List<SavedRoom>();
}

public static class LocalRoomStorage
{
    public static string OwnerKey
    {
        get
        {
            if (!PlayerPrefs.HasKey("localOwnerId"))
            {
                var guid = System.Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString("localOwnerId", guid);
                PlayerPrefs.Save();
            }
            return Sanitize(PlayerPrefs.GetString("localOwnerId", "guest"));
        }
    }

    static string BaseDir => Path.Combine(Application.persistentDataPath, "saved_rooms");
    static string OwnerDir => Path.Combine(BaseDir, OwnerKey);
    static string FilePath => Path.Combine(OwnerDir, "saved_rooms.json");

    const string MIGRATION_FLAG_KEY = "savedrooms.migrated.v2";

    static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "guest";
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
    static void EnsureOwnerDir()
    {
        if (!Directory.Exists(OwnerDir)) Directory.CreateDirectory(OwnerDir);
    }

    // ---------- PUBLIC API ----------
    /// <summary>Odadaki mevcut durumu DISKE kaydeder. Aynı saveId varsa ÜZERİNE YAZAR (upsert).</summary>
    public static void SaveCurrentRoomSnapshot()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

        EnsureOwnerDir();

        // 1) saveId bul / üret ve odaya yaz
        string saveId = null;
        if (PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.ROOM_SAVE_ID, out var sid) &&
            sid is string s && !string.IsNullOrEmpty(s))
        {
            saveId = s;
        }
        else
        {
            saveId = System.Guid.NewGuid().ToString("N");
            var set = new ExitGames.Client.Photon.Hashtable { { NetKeys.ROOM_SAVE_ID, saveId } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(set);
        }

        // 2) Snapshot hazırlığı
        var now = DateTime.Now;
        var sr = new SavedRoom
        {
            saveId = saveId,
            ownerUserId = OwnerKey,
            roomCode = PhotonNetwork.CurrentRoom.Name,
            createdAtLocal = now.ToString("dd/MM/yyyy HH:mm"),
            createdAtTicks = now.Ticks,
            players = new List<SavedPlayer>()
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

        // 3) UPSERT
        var list = LoadAll();
        int idx = list.items.FindIndex(x => x != null && x.saveId == saveId);
        if (idx >= 0)
        {
            // aynı kayıt: baştaki tarihe göre yerini güncelle, CREATED'ı ilk kaydın tarihiyle koru (isteğe bağlı)
            var old = list.items[idx];
            sr.createdAtLocal = old.createdAtLocal;
            sr.createdAtTicks = old.createdAtTicks;
            list.items[idx] = sr;
        }
        else
        {
            list.items.Insert(0, sr);
        }
        SaveAll(list);

#if UNITY_EDITOR
        Debug.Log($"[LocalRoomStorage] UPSERT room saveId={saveId} code={sr.roomCode} -> {FilePath}");
#endif
    }

    public static SavedRoomList LoadAll()
    {
        try
        {
            EnsureOwnerDir();
            if (PlayerPrefs.GetInt(MIGRATION_FLAG_KEY, 0) == 0) MigrateLegacyOnce();

            if (File.Exists(FilePath))
            {
                var text = File.ReadAllText(FilePath);
                var list = JsonUtility.FromJson<SavedRoomList>(text) ?? new SavedRoomList();

                // Eksik saveId'leri toparla (bir kez GUID ata)
                bool changed = false;
                foreach (var it in list.items)
                {
                    if (it == null) continue;
                    if (string.IsNullOrEmpty(it.ownerUserId)) it.ownerUserId = OwnerKey;
                    if (string.IsNullOrEmpty(it.saveId))
                    {
                        it.saveId = System.Guid.NewGuid().ToString("N");
                        changed = true;
                    }
                }
                list.items.RemoveAll(x => x == null || x.ownerUserId != OwnerKey);
                if (changed) SaveAll(list);

                return list;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalRoomStorage] Load failed: {e.Message}");
        }
        return new SavedRoomList();
    }

    public static void SaveAll(SavedRoomList list)
    {
        try
        {
            EnsureOwnerDir();
            // sahip filtre + null temizliği
            list.items.RemoveAll(x => x == null || x.ownerUserId != OwnerKey);
            // duplicate saveId'leri teke indir
            var seen = new HashSet<string>();
            for (int i = list.items.Count - 1; i >= 0; i--)
            {
                var id = list.items[i].saveId ?? "";
                if (seen.Contains(id)) list.items.RemoveAt(i);
                else seen.Add(id);
            }

            var json = JsonUtility.ToJson(list, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalRoomStorage] SaveAll failed: {e.Message}");
        }
    }

    public static string ToJson(SavedRoom sr) => JsonUtility.ToJson(sr);
    public static SavedRoom FromJson(string json) => string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SavedRoom>(json);

    public static void ClearAll(bool alsoLegacy = true)
    {
        try
        {
            EnsureOwnerDir();
            if (File.Exists(FilePath)) File.Delete(FilePath);

            if (alsoLegacy)
            {
                string legacyRoot = Path.Combine(Application.persistentDataPath, "saved_rooms.json");
                if (File.Exists(legacyRoot)) File.Delete(legacyRoot);

                string baseDir = Path.Combine(Application.persistentDataPath, "saved_rooms");
                if (Directory.Exists(baseDir))
                {
                    foreach (var dir in Directory.GetDirectories(baseDir))
                    {
                        var fp = Path.Combine(dir, "saved_rooms.json");
                        if (File.Exists(fp)) File.Delete(fp);
                        try
                        {
                            if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                                Directory.Delete(dir);
                        }
                        catch { }
                    }
                }
            }
#if UNITY_EDITOR
            Debug.Log($"[LocalRoomStorage] Cleared all saved rooms. OwnerDir: {OwnerDir}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalRoomStorage] ClearAll failed: {e.Message}");
        }
    }

    // ---------- LEGACY MIGRATION (one-shot) ----------
    static void MigrateLegacyOnce()
    {
        // (Önceki cevaptaki gibi; burada sadece saveId eksikleri için GUID ataması da var.)
        bool changed = false;
        var merged = new SavedRoomList();
        var uniq = new HashSet<string>();

        string legacyRoot = Path.Combine(Application.persistentDataPath, "saved_rooms.json");
        if (File.Exists(legacyRoot))
        {
            try
            {
                var txt = File.ReadAllText(legacyRoot);
                var old = JsonUtility.FromJson<SavedRoomList>(txt);
                if (old?.items != null)
                {
                    foreach (var it in old.items)
                    {
                        if (it == null) continue;
                        it.ownerUserId = OwnerKey;
                        if (string.IsNullOrEmpty(it.saveId)) it.saveId = System.Guid.NewGuid().ToString("N");
                        string key = $"{it.saveId}";
                        if (uniq.Add(key)) merged.items.Add(it);
                    }
                    changed = true;
                }
                File.Delete(legacyRoot);
            }
            catch { }
        }

        string baseDir = Path.Combine(Application.persistentDataPath, "saved_rooms");
        if (Directory.Exists(baseDir))
        {
            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                var fp = Path.Combine(dir, "saved_rooms.json");
                if (!File.Exists(fp)) continue;
                if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(OwnerDir), StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    var txt = File.ReadAllText(fp);
                    var old = JsonUtility.FromJson<SavedRoomList>(txt);
                    if (old?.items != null)
                    {
                        foreach (var it in old.items)
                        {
                            if (it == null) continue;
                            it.ownerUserId = OwnerKey;
                            if (string.IsNullOrEmpty(it.saveId)) it.saveId = System.Guid.NewGuid().ToString("N");
                            string key = $"{it.saveId}";
                            if (uniq.Add(key)) merged.items.Add(it);
                        }
                        changed = true;
                    }
                    try { File.Delete(fp); } catch { }
                    try
                    {
                        if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                            Directory.Delete(dir);
                    }
                    catch { }
                }
                catch { }
            }
        }

        if (File.Exists(FilePath))
        {
            try
            {
                var txt = File.ReadAllText(FilePath);
                var exist = JsonUtility.FromJson<SavedRoomList>(txt);
                if (exist?.items != null)
                {
                    foreach (var it in exist.items)
                    {
                        if (it == null) continue;
                        if (string.IsNullOrEmpty(it.ownerUserId)) it.ownerUserId = OwnerKey;
                        if (string.IsNullOrEmpty(it.saveId)) it.saveId = System.Guid.NewGuid().ToString("N");
                        string key = $"{it.saveId}";
                        if (uniq.Add(key)) merged.items.Add(it);
                    }
                    changed = true;
                }
            }
            catch { }
        }

        if (changed)
        {
            merged.items.Sort((a, b) => b.createdAtTicks.CompareTo(a.createdAtTicks));
            SaveAll(merged);
        }

        PlayerPrefs.SetInt(MIGRATION_FLAG_KEY, 1);
        PlayerPrefs.Save();
    }

    // ---------- helpers ----------
    static string GetProp(Player p, string key, string def)
    {
        if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue(key, out var v) && v != null)
            return v.ToString();
        return def;
    }
    static string GetStatsJson(Player p)
    {
        if (p != null && p.CustomProperties != null &&
            p.CustomProperties.TryGetValue(NetKeys.PLAYER_STATS, out var st) &&
            st is string json && !string.IsNullOrEmpty(json))
        {
            return json;
        }
        return MiniJson.Serialize(new Stats().ToDict());
    }
}
