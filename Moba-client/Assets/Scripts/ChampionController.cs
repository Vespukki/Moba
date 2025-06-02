using SpacetimeDB.Types;
using System;
using UnityEngine;

public class ChampionController : ActorController
{
    public uint owner;

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
        owner = champ.PlayerId;
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
