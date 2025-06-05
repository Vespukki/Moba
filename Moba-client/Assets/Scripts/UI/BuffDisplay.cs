using SpacetimeDB.Types;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class BuffDisplay : MonoBehaviour, IHoverable
{
    public Image buffImage;
    public Buff buff;
    public GameObject infoDisplayPrefab;

    private InfoDisplayUI infoDisplayInstance;

    public void BeginHover()
    {
        if (infoDisplayInstance == null)
        {
            infoDisplayInstance = Instantiate(infoDisplayPrefab, PlayerController.Local.mouseTransform).GetComponent<InfoDisplayUI>();
            infoDisplayInstance.Initialize(buff.BuffName);
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
        if (SpriteManager.spriteLookup.TryGetValue(newBuff.BuffId, out Sprite sprite))
        {
            buffImage.sprite = sprite;
        }
        else
        {
            LoadSpriteAsync(newBuff);
        }

    }

    private async void LoadSpriteAsync(Buff buff)
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
