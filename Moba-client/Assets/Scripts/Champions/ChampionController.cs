using NUnit.Framework;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.VFX;

public class ChampionController : ActorController
{
    public uint ownerPlayerId;

    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
    }

    public async void PlayVFX(HitVFX hitVfx)
    {
        Debug.Log("play vfx");
        var vfx = Instantiate(hitVfx.hitVfxPrefab, centerTransform);

        float timer = hitVfx.timer;

        while (timer > 0)
        {
            await Task.Yield();
            timer -= Time.deltaTime;
        }

        Destroy(vfx);
    }

    public void Initialize(Entity entity, Actor actor, ChampionInstance champ, ActorBaseStats baseStats)
    {
        Initialize(entity, actor, baseStats);
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

    public void AttackingCreated(Attacking attack)
    {
        UpdateAttacker(attack);
    }

    internal void UpdateAttacker(Attacking attack)
    {
        if (attack.AttackState == AttackState.Ready)
        {
            animator.SetBool("AttackStarted", false);
        }
        else
        {
            animator.SetFloat("RNG", UnityEngine.Random.Range(0f, 1f));
            animator.SetBool("AttackStarted", true);
        }
    }
    public void AttackingDeleted(Attacking attack)
    {
        animator.SetBool("AttackStarted", false);
        animator.SetTriggerOneFrame(this, "CancelAttackAnimation");
    }
}
