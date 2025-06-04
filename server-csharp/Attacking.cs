using System;
using SpacetimeDB;

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


    [Table(Name = "attacking", Public = true)]
    public partial struct Attacking
    {
        [PrimaryKey, Unique]
        public uint entity_id;

        public uint target_entity_id;

        public Timestamp attack_start_time;

        public AttackState attack_state;
    }

    [Table(Name = "attack_cooldown", Public = true)]
    public partial struct AttackCooldown
    {
        [PrimaryKey, Unique]
        public uint entity_id;

        public Timestamp finishedTime;
    }


    [Reducer]
    public static void SetAttackTarget(ReducerContext ctx, uint entityId, uint targetEntityId)
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
        #endregion

        ctx.Db.walking.entity_id.Delete(entityId);

        var oldAttacking = ctx.Db.attacking.entity_id.Find(entityId);

        if (oldAttacking != null)
        {
            var newAttacking = new Attacking()
            {
                entity_id = oldAttacking.Value.entity_id,
                target_entity_id = targetEntityId,
                attack_start_time = ctx.Timestamp,
                attack_state = AttackState.Ready
            };

            ctx.Db.attacking.entity_id.Delete(entityId);
            ctx.Db.attacking.Insert(newAttacking);
        }

        else
        {
            var newAttacking = new Attacking()
            {
                entity_id = entityId,
                target_entity_id = targetEntityId,
                attack_start_time = ctx.Timestamp,
                attack_state = AttackState.Ready
            };

            ctx.Db.attacking.Insert(newAttacking);
        }
    }



    [Reducer]
    public static void AttackWithActor(ReducerContext ctx, Attacking attack)
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

        var nullableAttackCooldown = ctx.Db.attack_cooldown.entity_id.Find(actor.entity_id);
        #endregion

        if (DbVector2.Distance(entity.position, targetEntity.position) > actor.attack_range)
        {
            ctx.Db.walking.entity_id.Delete(attack.entity_id);
            ctx.Db.walking.Insert(new()
            {
                entity_id = attack.entity_id,
                target_walk_pos = targetEntity.position
            });
            //SetTargetWalkPos(ctx, entity.entity_id, targetEntity.position, false);

            Log.Info("too far, walking over there");
        }
        else
        {

            ctx.Db.walking.entity_id.Delete(attack.entity_id);

            var difference = targetEntity.position - entity.position;

            var direction = difference.Normalized();

            float finalRotation = DbVector2.RotationFromDirection(direction);

            Actor newActor = actor;
            newActor.rotation = finalRotation;

            Attacking newAttack = attack;



            switch (attack.attack_state)
            {
                case AttackState.Ready:
                    Log.Info("ready");
                    if ((!nullableAttackCooldown.HasValue) || (nullableAttackCooldown.HasValue
                        && nullableAttackCooldown.Value.finishedTime.MicrosecondsSinceUnixEpoch <= ctx.Timestamp.MicrosecondsSinceUnixEpoch))
                    {
                        newAttack.attack_state = AttackState.Starting;
                        newAttack.attack_start_time = ctx.Timestamp;
                    }
                    break;
                case AttackState.Starting:

                    Log.Info("Starting");

                    if (GetTimestampDifferenceInSeconds(newAttack.attack_start_time, ctx.Timestamp) >= 1f / actor.attack_speed * actor.windup_percent)
                    {
                        var nullableTargetActor = ctx.Db.actor.entity_id.Find(attack.target_entity_id);
                        if (nullableTargetActor != null)
                        {
                            Actor targetActor = nullableTargetActor.Value;
                            Actor newTargetActor = targetActor;

                            Log.Info($"just did damage after {GetTimestampDifferenceInSeconds(actor.last_attack_time, ctx.Timestamp)} secs");
                            newActor.last_attack_time = ctx.Timestamp;
                            ctx.Db.registered_hits.Insert(new()
                            {
                                hit_entity_id = targetActor.entity_id,
                                source_entity_id = entity.entity_id
                            });

                            ctx.Db.attack_cooldown.entity_id.Delete(actor.entity_id);
                            ctx.Db.attack_cooldown.Insert(new()
                            {
                                entity_id = actor.entity_id,
                                finishedTime = new Timestamp(attack.attack_start_time.MicrosecondsSinceUnixEpoch + (int)((1f / actor.attack_speed) * 1_000_000))
                            });
                            newAttack.attack_state = AttackState.Ready;

                            newTargetActor.current_health -= 100;

                            ctx.Db.actor.entity_id.Delete(targetActor.entity_id);
                            ctx.Db.actor.Insert(newTargetActor);
                        }
                    }
                    break;
                case AttackState.Committed:

                    break;
                default:
                    break;
            }

            ctx.Db.attacking.entity_id.Delete(attack.entity_id);
            ctx.Db.attacking.Insert(newAttack);

            ctx.Db.actor.entity_id.Delete(actor.entity_id);
            ctx.Db.actor.Insert(newActor);

            Log.Info($"close enough, attacking because {entity.entity_id} is {DbVector2.Distance(entity.position, targetEntity.position)} away from {targetEntity.entity_id}");
        }
    }

    [Reducer]
    public static void MakeAllAttacks(ReducerContext ctx)
    {
        var list = ctx.Db.attacking.Iter();
        foreach (var attacker in list)
        {
            AttackWithActor(ctx, attacker);
        }

    }

}