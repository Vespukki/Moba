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
    public ChampionInstance championInstance;

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
        championInstance = champ;
    }

    internal void UpdateChampion(ChampionInstance newChamp)
    {
        Debug.Log("Champ update");
        championInstance = newChamp;
    }

    internal void UpdateWalker(Walking newWalker)
    {
        targetPos = DbPositionToWorldPosition(newWalker.TargetWalkPos, transform.position.y);
        currentWalk = newWalker;
    }

    public void DeleteWalker()
    {
        currentWalk = null;
    }

    public void AttackingCreated(Attacking attack)
    {
        UpdateAttacker(null, attack);
    }

    internal void UpdateAttacker(Attacking lastAttack, Attacking newAttack)
    {
        if (newAttack.AbilityInstanceId == championInstance.BasicAttackAbilityInstanceId)
        {
            if (newAttack.AttackState == AttackState.Ready)
            {
                animator.SetBool("AttackStarted", false);
            }
            else
            {
                animator.SetFloat("RNG", UnityEngine.Random.Range(0f, 1f));
                animator.SetBool("AttackStarted", true);
            }
        }
        else if (newAttack.AbilityInstanceId == championInstance.QAbilityInstanceId)
        {
            if (lastAttack != null && newAttack.AttackState == lastAttack.AttackState) return;
            //assume everyone is fiora for now
            if(GameManager.Instance.championControllers.TryGetValue(newAttack.TargetEntityId, out ChampionController targetChamp))
            {
                if (newAttack.AttackState == AttackState.Ready)
                {
                    animator.SetTriggerOneFrame(this, "StopDash");
                }
                else if (newAttack.AttackState == AttackState.Starting)
                {
                    animator.SetTriggerOneFrame(this, "StartDash");
                }
            }
        }
       
    }
    public void AttackingDeleted(Attacking attack)
    {
        Debug.Log("ATTACK DELETED: " +  attack.AbilityInstanceId);
        animator.SetBool("AttackStarted", false);
        animator.SetTriggerOneFrame(this, "CancelAttackAnimation");
        if (attack.AbilityInstanceId == championInstance.QAbilityInstanceId)
        {
            animator.SetTriggerOneFrame(this, "StopDash");
        }
    }
}
