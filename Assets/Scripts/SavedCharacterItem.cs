using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SavedCharacterItem : MonoBehaviour
{
    public Button button;
    public TMP_Text nameText;
    public TMP_Text stateText; // "Boþ", "Siz", "X tarafýndan seçildi"
    public Image bg;

    int _index;
    System.Action<int> _onClick;

    public void Bind(SavedPlayer sp, int index, System.Action<int> onClick)
    {
        _index = index;
        _onClick = onClick;
        if (nameText) nameText.text = $"{sp.name} ({(sp.gender == "F" ? "Kadýn" : "Erkek")})";
        if (stateText) stateText.text = "Boþ";
        if (button) button.onClick.AddListener(() => _onClick?.Invoke(_index));
        SetStateFree();
    }

    public void SetStateMine()
    {
        if (stateText) stateText.text = "Siz";
        if (bg) bg.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        if (button) button.interactable = true;
    }

    public void SetStateTaken(string byName)
    {
        if (stateText) stateText.text = $"{byName} tarafýndan seçildi";
        if (bg) bg.color = new Color(0.8f, 0.3f, 0.3f, 1f);
        if (button) button.interactable = false;
    }

    public void SetStateFree()
    {
        if (stateText) stateText.text = "Boþ";
        if (bg) bg.color = Color.white;
        if (button) button.interactable = true;
    }
}
