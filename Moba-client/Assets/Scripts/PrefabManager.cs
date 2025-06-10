using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PrefabManager : MonoBehaviour
{
    public static PrefabManager Instance;
    public PlayerController PlayerPrefab;

    public Dictionary<ChampId, GameObject> idsToChampPrefabs = new();
    [SerializeField] GameObject fioraPrefab;
    [SerializeField] GameObject dummyPrefab;
    [SerializeField] GameObject buffDisplayPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this) 
        {

            Debug.LogError("Extra Prefab Manager Spawned");
        }

        idsToChampPrefabs.Clear();
        idsToChampPrefabs.Add(ChampId.Fiora, fioraPrefab);
        idsToChampPrefabs.Add(ChampId.Dummy, dummyPrefab);
    }

    public static PlayerController SpawnPlayer(Player player)
    {
        var playerController = Instantiate(Instance.PlayerPrefab);
        playerController.name = $"PlayerController - {player.Name}";
        playerController.Initialize(player);
        return playerController;
    }

    public static ChampionController SpawnChampion(Entity entity, Actor actor, ChampionInstance champ, ActorBaseStats baseStats)
    {
        if (Instance.idsToChampPrefabs.TryGetValue(champ.ChampId, out GameObject champPrefab))
        {
            var champController = Instantiate(champPrefab).GetComponentInChildren<ChampionController>();
            champController.name = $"ChampionController - {champ.ChampId}";
            champController.Initialize(entity, actor, champ, baseStats);

            if (GameManager.LocalIdentity == champ.PlayerIdentity)
            {
                if (GameManager.Instance.abilities.TryGetValue(champ.QAbilityInstanceId, out Ability qAbility))
                {
                    GameManager.Instance.qAbilityDisplay.Initialize(qAbility);
                }
            }
            
            return champController;

        }

        else
        {
            Debug.LogError($"no prefab found for champ ID {champ.ChampId}");
            return null;
        }
    }

    internal static GameObject SpawnBuffDisplay(Buff buff, BuffDisplayInfo info, Transform buffHolder)
    {
        BuffDisplay buffDisplay = Instantiate(Instance.buffDisplayPrefab, buffHolder).GetComponent<BuffDisplay>();
        buffDisplay.Initialize(buff, info);
        return buffDisplay.gameObject;
    }
}
