using SpacetimeDB.Types;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityDisplay : MonoBehaviour
{
    public Ability ability;
    public Image frame;
    public Image abilityImage;
    public TextMeshProUGUI cdText;

    public void Initialize(Ability ability)
    {
        this.ability = ability;
        Debug.Log("ability display init");

        SpriteManager.LoadAbilitySpriteAsync(ability, (Sprite sprite) => abilityImage.sprite = sprite);
    }

    internal void UpdateAbility(Ability newRow)
    {
        ability = newRow;

    }

    private void Update()
    {
        if (ability == null) return;
        DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        long cur_time = (long)((System.DateTime.UtcNow - epochStart).TotalMilliseconds * 1000);

        long elapsedTime = ability.ReadyTime.MicrosecondsSinceUnixEpoch - cur_time;

        float elapsedTimeSeconds = (float)((double)elapsedTime / (double)1_000_000);

        cdText.text = elapsedTimeSeconds.ToString();
    }
}
