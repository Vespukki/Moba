using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class SpriteManager : MonoBehaviour
{
    public static SpriteManager instance;
    private static Dictionary<BuffId, Sprite> buffSpriteLookup = new(); 
    private static Dictionary<AbilityId, Sprite> abilitySpriteLookup = new(); 
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    public static async void LoadAbilitySpriteAsync(Ability ability, Action<Sprite> onLoaded)
    {
        if (abilitySpriteLookup.TryGetValue(ability.AbilityId, out Sprite sprite))
        {
            onLoaded?.Invoke(sprite);
            return;
        }


        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(ability.AbilityId.ToString());
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            abilitySpriteLookup[ability.AbilityId] = handle.Result;
            onLoaded?.Invoke(handle.Result);
        }
        else
        {
            Debug.LogWarning($"Failed to load sprite with id: {ability.AbilityId}");
            onLoaded?.Invoke(null); // optionally invoke with null to indicate failure
        }
    }

    public static async void LoadBuffSpriteAsync(Buff buff, Action<Sprite> onLoaded)
    {
        if (buffSpriteLookup.TryGetValue(buff.BuffId, out Sprite sprite))
        {
            onLoaded?.Invoke(sprite);
            return;
        }


        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(buff.BuffId.ToString());
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            buffSpriteLookup[buff.BuffId] = handle.Result;
            onLoaded?.Invoke(handle.Result);
        }
        else
        {
            Debug.LogWarning($"Failed to load sprite with id: {buff.BuffId}");
            onLoaded?.Invoke(null); // optionally invoke with null to indicate failure
        }
    }
}
