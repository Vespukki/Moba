using SpacetimeDB.Types;
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

    public Image spriteImage;
    public void Initialize(Buff buff, BuffDisplayInfo info)
    {
        titleText.SetText(info.buffName);
        bodyText.SetText(info.buffDescription);

        if (buff.Source == "")
        {
            secondBar.enabled = false;
        }
        sourceText.SetText(buff.Source);

        BuffDisplay.LoadBuffSpriteAsync(buff, (Sprite sprite) => spriteImage.sprite = sprite);
    }
}
