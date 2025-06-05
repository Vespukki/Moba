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
