using SpacetimeDB.Types;
using System;
using UnityEngine;

public class ChampionController : ActorController
{
    public uint ownerPlayerId;

    private uint attackRange = 1;

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
        ownerPlayerId = champ.PlayerId;
    }

    internal void UpdateChampion(ChampionInstance newChamp)
    {
        Debug.Log("Champ update");
    }

    internal void UpdateWalker(Walking newWalker)
    {
        targetPos = DbPositionToWorldPosition(newWalker.TargetWalkPos, transform.position.y);
    }

    internal void UpdateAttacker(Attacking attack, EntityController target)
    {
        /*
        DbVector2 myPos = WorldPositionToDbPosition(transform.position);
        DbVector2 targetPos = WorldPositionToDbPosition(target.transform.position);

        float dx = myPos.X - targetPos.X;
        float dy = myPos.Y - targetPos.Y;

        float dist =Mathf.Sqrt(dx * dx + dy * dy);
        if(dist < )*/
    }
}
