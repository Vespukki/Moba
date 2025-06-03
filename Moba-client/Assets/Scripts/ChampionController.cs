using SpacetimeDB.Types;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class ChampionController : ActorController
{
    public uint ownerPlayerId;

    protected override void Start()
    {
        base.Start();
    }

    public async void DoAttack()
    {
        attackInitialized = true;

        animator.SetBool("Attacking", true);
        attackTimer = 0;

        while (attackTimer < .13793f * (1/.69))
        {
            await Task.Yield();
            attackTimer += Time.deltaTime;
        }
        //now attack is locked in

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

    bool attackInitialized = false;
    float attackTimer = 0;
    bool timerStarted = false;
    bool allowAttackReset = true;

    public void AttackingCreated(Attacking attack)
    {
        //do nothing for now I guess
    }

    internal void UpdateAttacker(Attacking attack)
    {
        if (attack.AttackState == AttackState.Ready)
        {
            animator.SetBool("AttackStarted", false);
        }
        else
        {
            animator.SetBool("AttackStarted", true);
        }

        /*if (!attack.IsAttacking)
        {
            return;
        }

        if (!attackInitialized) //do once when the attack first starts
        {
            InitializeAttack();
        }

        if (attack.HasDamaged && !timerStarted)
        {
            allowAttackReset = false;
            //animator.SetBool("AttackReady", )
        }*/

    }
    private void InitializeAttack()
    {
        float postHitRatio = 1f - .13793f;

        float attackTime = 1f / .69f;

        attackTimer = attackTime * postHitRatio;

        timerStarted = true;
        animator.SetBool("Attacking", true);
    }

    public void AttackingDeleted(Attacking attack)
    {
        animator.SetBool("AttackStarted", false);
        animator.SetTriggerOneFrame(this, "CancelAttackAnimation");
    }
}
