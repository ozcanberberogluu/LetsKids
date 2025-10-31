using Photon.Realtime;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class OtherItem : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text genderText;
    public TMP_Text statsText;

    public void Bind(Player p) => Refresh(p);

    public void Refresh(Player p)
    {
        string nm = p.NickName;
        if (p.CustomProperties.TryGetValue(NetKeys.PLAYER_NAME, out var n)) nm = n.ToString();
        nameText.text = nm + (p.IsLocal ? " (Siz)" : "");

        string g = p.CustomProperties.TryGetValue(NetKeys.PLAYER_GENDER, out var gg) ? gg.ToString() : "?";
        genderText.text = g == "F" ? "Kadýn" : "Erkek";

        if (p.CustomProperties.TryGetValue(NetKeys.PLAYER_STATS, out var st) && st is string json && !string.IsNullOrEmpty(json))
        {
            var dict = MiniJson.Deserialize(json) as Dictionary<string, object>;
            statsText.text = $"Hýz:{dict["spd"]} Güç:{dict["pow"]} Def:{dict["def"]} AtkHýz:{dict["atkspd"]} HP:{dict["hp"]}";
        }
        else
        {
            statsText.text = "Statlar yükleniyor...";
        }
    }
}
