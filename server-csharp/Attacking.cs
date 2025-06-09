using SpacetimeDB;
using SpacetimeDB.Internal.TableHandles;
using System;
using static Module;

public static partial class Module
{
    [Table(Name = "registered_hits", Public = true)]
    public partial struct RegisteredHits
    {
        [PrimaryKey, Unique, AutoInc]
        public uint hit_id;

        [SpacetimeDB.Index.BTree]
        public uint hit_entity_id;

        [SpacetimeDB.Index.BTree]
        public uint source_entity_id;
    }

    [Type]
    public enum AttackState { Ready, Starting, Committed }

    [Type]
    public enum DamageType { Physical, Magical, True}

    [Table(Name = "attacking", Public = true)]
    public partial struct Attacking
    {
        [PrimaryKey, Unique]
        public uint entity_id;

        public uint ability_instance_id;

        public uint target_entity_id;

        public Timestamp attack_start_time;

        public AttackState attack_state;
    }

    [Type]
    public enum HitType {BasicAttack, Spell}

    [Reducer]
    public static void SetAttackTarget(ReducerContext ctx, uint entityId, uint targetEntityId, uint abilityId)
    {
        #region entity checking
        var nullableEntity = ctx.Db.entity.entity_id.Find(entityId);

        if (nullableEntity == null)
        {
            Log.Info($"passed entity {entityId} is null");
            return;
        }

        Entity entity = nullableEntity.Value;

        var nullableTargetEntity = ctx.Db.entity.entity_id.Find(targetEntityId);

        if (nullableTargetEntity == null)
        {
            Log.Info($"passed entity {targetEntityId} is null");
            return;
        }

        Entity targetEntity = nullableTargetEntity.Value;

        var nChamp = ctx.Db.champion_instance.entity_id.Find(entityId);
        if (nChamp == null)
        {
            return;
        }
        ChampionInstance champ = nChamp.Value;

        var nAbility = ctx.Db.ability.ability_instance_id.Find(abilityId);
        if (nAbility == null) return;
        Ability ability = nAbility.Value;

        #endregion

        ctx.Db.walking.entity_id.Delete(entityId);


        var newAttacking = new Attacking()
        {
            entity_id = entityId,
            target_entity_id = targetEntityId,
            attack_start_time = ctx.Timestamp,
            attack_state = AttackState.Ready,
            ability_instance_id = champ.basic_attack_ability_instance_id
        };

        ctx.Db.attacking.entity_id.Delete(entityId);
        ctx.Db.attacking.Insert(newAttacking);
    }
    public static void DoOnHit(ReducerContext ctx, Actor actor, Actor targetActor, Buff buff)
    {
        switch (buff.buff_id)
        {
            case BuffId.RedBuffOnHit:

                Buff newBurn = new Buff(targetActor.entity_id, BuffId.Burning, ctx.Timestamp, 4);
                Buff tempBurn = new Buff(actor.entity_id, BuffId.Burning, ctx.Timestamp, 4);
                AddBuff(ctx, newBurn);
                AddBuff(ctx, tempBurn);

                Buff newSlow = new Buff(targetActor.entity_id, BuffId.Slowed, ctx.Timestamp, 4);
                Buff tempSlow = new Buff(actor.entity_id, BuffId.Slowed, ctx.Timestamp, 4);
                AddBuff(ctx, newSlow);
                AddBuff(ctx, tempSlow);
                break;

            default:
                break;
        }

        
    }

    [Reducer]
    public static void HitActor(ReducerContext ctx, Actor actor, Actor targetActor, ActorBaseStats actorBaseStats, ActorBaseStats targetBaseStats, HitType hitType, float damage)
    {
        if (hitType == HitType.BasicAttack)
        {
            foreach (Buff buff in ctx.Db.buff.entity_id.Filter(actor.entity_id))
            {
                if (ctx.Db.buff_on_hit.buff_id.Find((uint)buff.buff_id) != null)
                {
                    Log.Info("we have an onhit buff");
                    DoOnHit(ctx, actor, targetActor, buff);
                }
            }
        }


        Actor newTargetActor = targetActor;

        
        ctx.Db.registered_hits.Insert(new()
        {
            hit_entity_id = targetActor.entity_id,
            source_entity_id = actor.entity_id
        });

        Log.Info($"fixin to do {damage} damage to {targetActor.entity_id}");
        float newCurrentHealth = targetActor.current_health - damage;
        SetActorHealth(ctx, targetActor, targetBaseStats, newCurrentHealth);
    }


    /// <returns>True if the entity was too far away from the position and auto adds a walk target. If false, it clears the current walk command from the walking table.</returns>
    public static bool WalkIfTooFarAway(ReducerContext ctx, Entity entity, DbVector2 targetPos, float range)
    {
        if (DbVector2.Distance(entity.position, targetPos) > range)
        {
            ctx.Db.walking.entity_id.Delete(entity.entity_id);
            ctx.Db.walking.Insert(new()
            {
                entity_id = entity.entity_id,
                target_walk_pos = targetPos
            });

            return true;
        }
        ctx.Db.walking.entity_id.Delete(entity.entity_id);
        return false;
    }

    /// <returns>returns new rotation that actor should have in order to be looking at the position.</returns>
    public static float GetLookAtRotation(DbVector2 entityPos, DbVector2 targetPos)
    {
        var difference = targetPos - entityPos;

        var direction = difference.Normalized();

        return(DbVector2.RotationFromDirection(direction));
    }

    public static void HandleBasicAttack(ReducerContext ctx, Attacking attack, Ability ability, Entity entity, Entity targetEntity, Actor actor, ActorBaseStats actorBaseStats)
    {

        Actor newActor = actor;
        Attacking newAttack = attack;
        Ability newAbility = ability;

        newActor.rotation = GetLookAtRotation(entity.position, targetEntity.position);

        switch (attack.attack_state)
        {
            case AttackState.Ready:
                
                if(ability.ready_time.MicrosecondsSinceUnixEpoch <= ctx.Timestamp.MicrosecondsSinceUnixEpoch)
                {
                    newAttack.attack_state = AttackState.Starting;
                    newAttack.attack_start_time = ctx.Timestamp;
                }
                break;
            case AttackState.Starting:
                if (GetTimestampDifferenceInSeconds(newAttack.attack_start_time, ctx.Timestamp) >= 1f / actorBaseStats.attack_speed * actorBaseStats.windup_percent)
                {
                    //attack is locked in at this point

                    var nullableTargetActor = ctx.Db.actor.entity_id.Find(attack.target_entity_id);
                    if (nullableTargetActor != null)
                    {
                        Actor targetActor = nullableTargetActor.Value;

                        var nTargetBaseStats = ctx.Db.actor_base_stats.actor_id.Find(targetActor.actor_id);
                        if (nTargetBaseStats == null) return;
                        ActorBaseStats targetBaseStats = nTargetBaseStats.Value;

                        HitActor(ctx, actor, targetActor, actorBaseStats, targetBaseStats, HitType.BasicAttack,
                            CalculateBasicAttackDamage(ctx, actor, targetActor, actorBaseStats, targetBaseStats));

                        Log.Info($"just did damage after {GetTimestampDifferenceInSeconds(actor.last_attack_time, ctx.Timestamp)} secs");
                        newActor.last_attack_time = ctx.Timestamp;

                        newAttack.attack_state = AttackState.Ready;

                        newAbility.ready_time = new Timestamp(attack.attack_start_time.MicrosecondsSinceUnixEpoch + (int)((1f / actorBaseStats.attack_speed) * 1_000_000));
                    }
                }
                break;
            default:
                break;
        }
        ctx.Db.ability.ability_instance_id.Delete(ability.ability_instance_id);
        ctx.Db.ability.Insert(newAbility);

        ctx.Db.attacking.entity_id.Delete(attack.entity_id);
        ctx.Db.attacking.Insert(newAttack);

        ctx.Db.actor.entity_id.Delete(actor.entity_id);
        ctx.Db.actor.Insert(newActor);
    }

    [Reducer]
    public static void AttackWithActor(ReducerContext ctx, Attacking attack, Ability ability)
    {

        #region find entity and actor
        var nullableEntity = ctx.Db.entity.entity_id.Find(attack.entity_id);
        if (nullableEntity == null)
        {
            Log.Info($"deleting attacker because entity is null");
            ctx.Db.attacking.entity_id.Delete(attack.entity_id);
            return;
        }
        Entity entity = nullableEntity.Value;

        var nullableUnit = ctx.Db.actor.entity_id.Find(attack.entity_id);
        if (nullableUnit == null)
        {
            ctx.Db.attacking.entity_id.Delete(attack.entity_id);
            return;
        }
        Actor actor = nullableUnit.Value;

        var nullableTargetEntity = ctx.Db.entity.entity_id.Find(attack.target_entity_id);
        if (nullableTargetEntity == null)
        {
            ctx.Db.attacking.entity_id.Delete(attack.entity_id);
            return;
        }
        Entity targetEntity = nullableTargetEntity.Value;

        var nBaseStats = ctx.Db.actor_base_stats.actor_id.Find(actor.actor_id);
        if (nBaseStats == null) return;
        ActorBaseStats actorBaseStats = nBaseStats.Value;


        #endregion


        if (WalkIfTooFarAway(ctx, entity, targetEntity.position, actorBaseStats.attack_range)) return;

        //beyond here, we are in range

        UseTargetedAbility(ctx, ability, attack, entity, actor, targetEntity, actorBaseStats);

        

    }

    public static float CalculateBasicAttackDamage(ReducerContext ctx, Actor actor, Actor targetActor, ActorBaseStats actorBaseStats, ActorBaseStats targetBaseStats)
    {
        return 100f;
    }

    [Reducer]
    public static void MakeAllAttacks(ReducerContext ctx)
    {
        var list = ctx.Db.attacking.Iter();
        foreach (var attacker in list)
        {
            var nAbility = ctx.Db.ability.ability_instance_id.Find(attacker.ability_instance_id);
            if (nAbility == null)
            {
                Log.Info("null ability");
                continue;
            }

            AttackWithActor(ctx, attacker, nAbility.Value);
        }

    }

}