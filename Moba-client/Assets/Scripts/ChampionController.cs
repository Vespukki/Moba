using SpacetimeDB.Types;
using System;
using UnityEngine;

public class ChampionController : ActorController
{

    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
    }

    public void Initialize(Entity entity, Actor actor, ChampionInstance champ)
    {
        Initialize(entity, actor);
    }

    internal void UpdateChampion(ChampionInstance newChamp)
    {
        Debug.Log("Champ update");
    }

    internal void UpdateWalker(Walking newWalker)
    {
        targetPos = DbPositionToWorldPosition(newWalker.TargetWalkPos, transform.position.y);
    }
}
