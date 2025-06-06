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
    public GameObject infoDisplayPrefab;

    public TextMeshProUGUI stackText;

    private InfoDisplayUI infoDisplayInstance;

    public Transform durationIndicator;

    public float timeSinceStart = 0;

    public void BeginHover()
    {
        if (infoDisplayInstance == null)
        {
            infoDisplayInstance = Instantiate(infoDisplayPrefab, PlayerController.Local.mouseTransform).GetComponent<InfoDisplayUI>();
            infoDisplayInstance.Initialize(buff.BuffName, buff.BuffDescription, buff.Source);
        }
    }

    public void EndHover()
    {
        if (infoDisplayInstance != null)
        {
            Destroy(infoDisplayInstance.gameObject);
        }
    }


    internal void Initialize(Buff newBuff)
    {
        GameManager.Instance.buffIdToBuffDisplay.Add(newBuff.BuffInstanceId, this);

        


        buff = newBuff;
        stackText.text = newBuff.Stacks.ToString();
        if (SpriteManager.spriteLookup.TryGetValue(newBuff.BuffId, out Sprite sprite))
        {
            buffImage.sprite = sprite;
        }
        else
        {
            LoadBuffSpriteAsync(newBuff);
        }

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

    private async void LoadBuffSpriteAsync(Buff buff)
    {
        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(buff.BuffId);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            SpriteManager.spriteLookup[buff.BuffId] = handle.Result;
            buffImage.sprite = handle.Result;
        }
        else
        {
            Debug.LogWarning($"Failed to load sprite with id: {buff.BuffId}");
        }
    }
}
