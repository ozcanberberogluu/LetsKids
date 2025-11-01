using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SavedRoomItemUI : MonoBehaviour
{
    public Button button;
    public TMP_Text titleText;   // oda kodu
    public TMP_Text subtitleText;// tarih + oyuncu sayýsý
    public Image bg;

    int _index;
    System.Action<int> _onSelect;

    public void Bind(SavedRoom data, int index, System.Action<int> onSelect)
    {
        _index = index;
        _onSelect = onSelect;
        if (titleText) titleText.text = data.roomCode;
        if (subtitleText) subtitleText.text = $"{data.createdAtLocal} · {data.players.Count} oyuncu";
        if (button) button.onClick.AddListener(() => _onSelect?.Invoke(_index));
        SetSelected(false);
    }

    public void SetSelected(bool sel)
    {
        if (bg) bg.color = sel ? new Color(0.2f, 0.8f, 0.2f, 1f) : Color.white;
    }
}
