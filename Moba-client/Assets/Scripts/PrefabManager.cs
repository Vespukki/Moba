using SpacetimeDB.Types;
using UnityEngine;

public class PrefabManager : MonoBehaviour
{
    public static PrefabManager Instance;
    public PlayerController PlayerPrefab;
    public GameObject ChampionPrefab;

    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this) 
        {
            Debug.LogError("Extra Prefab Manager Spawned");
        }
    }

    public static PlayerController SpawnPlayer(Player player)
    {
        var playerController = Instantiate(Instance.PlayerPrefab);
        playerController.name = $"PlayerController - {player.Name}";
        playerController.Initialize(player);
        return playerController;
    }

    public static ChampionController SpawnChampion(Entity entity, Actor actor, ChampionInstance champ)
    {
        var champController = Instantiate(Instance.ChampionPrefab).GetComponentInChildren<ChampionController>();
        champController.name = $"ChampionController - {champ.ChampId}";
        champController.Initialize(entity, actor, champ);
        return champController;
    }
}
