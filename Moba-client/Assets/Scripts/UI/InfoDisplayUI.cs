using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoDisplayUI : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public TextMeshProUGUI sourceText;
    public Image firstBar;
    public Image secondBar;

    public void Initialize(string title, string body, string source)
    {
        titleText.SetText(title);
        bodyText.SetText(body);
        sourceText.SetText(source);
    }
}
