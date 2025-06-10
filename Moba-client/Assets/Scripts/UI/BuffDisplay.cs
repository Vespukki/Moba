using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class BuffDisplay : MonoBehaviour, IHoverable
{
    public Image buffImage;
    public Buff buff;
    public BuffDisplayInfo buffInfo;
    public GameObject infoDisplayPrefab;

    public TextMeshProUGUI stackText;

    private InfoDisplayUI infoDisplayInstance;

    public Transform durationIndicator;


    public void BeginHover()
    {
        if (infoDisplayInstance == null)
        {
            infoDisplayInstance = Instantiate(infoDisplayPrefab, PlayerController.Local.mouseTransform).GetComponent<InfoDisplayUI>();
            infoDisplayInstance.Initialize(buff, buffInfo);
        }
    }

    public void EndHover()
    {
        if (infoDisplayInstance != null)
        {
            Destroy(infoDisplayInstance.gameObject);
        }
    }


    internal void Initialize(Buff newBuff, BuffDisplayInfo info)
    {
        GameManager.Instance.buffIdToBuffDisplay.Add(newBuff.BuffInstanceId, this);

        buffInfo = info;
        buff = newBuff;

        if (newBuff.Stacks == 0 && !info.showZeroStacks)
        {
            stackText.text = "";
        }
        else
        {
            stackText.text = newBuff.Stacks.ToString();
        }

        SpriteManager.LoadBuffSpriteAsync(newBuff, (Sprite sprite) => buffImage.sprite = sprite);

    }

    private void Update()
    {
        if (buff == null) return;

        DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        long cur_time = (long)((System.DateTime.UtcNow - epochStart).TotalMilliseconds * 1000);

        long elapsedTime = cur_time - buff.StartTimestamp.MicrosecondsSinceUnixEpoch;

        float elapsedTimeSeconds = (float)((double)elapsedTime / (double)1_000_000);

        float durationPercent = elapsedTimeSeconds / buff.Duration;

        durationIndicator.rotation = Quaternion.Euler(0, 0, durationPercent * -360f);
    }

    private void OnDestroy()
    {
        if (buff != null)
        {
            GameManager.Instance.buffIdToBuffDisplay.Remove(buff.BuffInstanceId);
        }
    }

    
}
