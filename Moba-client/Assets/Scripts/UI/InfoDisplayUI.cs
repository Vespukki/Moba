using TMPro;
using UnityEngine;

public class InfoDisplayUI : MonoBehaviour
{
    public TextMeshProUGUI text;

    public void Initialize(string Name)
    {
        text.text = Name;
    }
}
