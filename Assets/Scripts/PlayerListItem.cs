using Photon.Realtime;
using UnityEngine;
using TMPro;

public class PlayerListItem : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text readyText;

    public void Bind(Player p)
    {
        nameText.text = p.NickName + (p.IsMasterClient ? " (Host)" : "");
        Refresh(p);
    }

    public void Refresh(Player p)
    {
        bool ready = p.CustomProperties.TryGetValue(NetKeys.PLAYER_READY, out var v) && v is bool b && b;
        readyText.text = ready ? "Hazýr" : "Hazýr Deðil";
    }
}
