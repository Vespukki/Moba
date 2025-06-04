using SpacetimeDB;
using System;
using System.Diagnostics;

public static partial class Module
{
    [Type]
    public enum Team { Red, Blue, Neutral }

    [Table(Name = "actor", Public = true)]
    public partial struct Actor
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        //public uint statblock_id;
        public float rotation;
        public float current_health;
        public float max_health;
        public Team team;
        public uint attack_range;
        public float attack_speed;
        public float windup_percent; //percentage
        public Timestamp last_attack_time;
    }

    [Table(Name = "walking", Public = true)]
    public partial struct Walking
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        public DbVector2 target_walk_pos;
    }

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
    public enum AttackState {Ready, Starting, Committed }


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

    [Table(Name = "set_walk_target_timer", Scheduled = nameof(SetFutureTargetWalkPos), ScheduledAt = nameof(scheduled_at))]
    public partial struct SetWalkTargetTimer
    {
        [PrimaryKey, AutoInc]
        public ulong scheduled_id;

        [Unique]
        public uint entity_id;
        public ScheduleAt scheduled_at;
        public DbVector2 position;
        public bool remove_other_actions;
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
    public static void SetFutureTargetWalkPos(ReducerContext ctx, SetWalkTargetTimer caller)
    {
        SetTargetWalkPos(ctx, caller.entity_id, caller.position, caller.remove_other_actions);
    }

    [Reducer]
    public static void SetTargetWalkPos(ReducerContext ctx, uint entityId, DbVector2 position, bool removeOtherActions = true)
    {
        ctx.Db.set_walk_target_timer.entity_id.Delete(entityId);

        var nullableAttacking = ctx.Db.attacking.entity_id.Find(entityId);

        if(nullableAttacking != null)
        {
            float timeSinceAttackStarted = GetTimestampDifferenceInSeconds(nullableAttacking.Value.attack_start_time, ctx.Timestamp);

            var nullableActor = ctx.Db.actor.entity_id.Find(entityId);
            if (nullableActor == null) return;
            Actor actor = nullableActor.Value;

            float windupTime = (1f / actor.attack_speed) * actor.windup_percent;
            float timeUntilHit = windupTime - timeSinceAttackStarted;

            if (Math.Abs(timeUntilHit) < .4f * windupTime)
            {
                ctx.Db.set_walk_target_timer.Insert(new()
                {
                    scheduled_at = new Timestamp(nullableAttacking.Value.attack_start_time.MicrosecondsSinceUnixEpoch + (int)(windupTime * 1.4f * 1_000_000f)),
                    entity_id = entityId,
                    position = position,
                    remove_other_actions = removeOtherActions
                });
                return;
            }
        } 

        if (removeOtherActions) ctx.Db.attacking.entity_id.Delete(entityId);


            var newWalking = new Walking()
            {
                entity_id = entityId,
                target_walk_pos = position
            };

            ctx.Db.walking.entity_id.Delete(entityId);
            ctx.Db.walking.Insert(newWalking);

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

    [Reducer]
    public static void MoveActor(ReducerContext ctx, Walking walker)
    {
        float moveSpeed = 250; //Units per Second

        #region find entity and actor
        var nullableEntity = ctx.Db.entity.entity_id.Find(walker.entity_id);
        if (nullableEntity == null)
        {
            ctx.Db.walking.entity_id.Delete(walker.entity_id);
            return;
        }
        Entity entity = nullableEntity.Value;

        var nullableUnit = ctx.Db.actor.entity_id.Find(walker.entity_id);
        if (nullableUnit == null)
        {
            ctx.Db.walking.entity_id.Delete(walker.entity_id);
            return;
        }
        Actor actor = nullableUnit.Value;
        #endregion

        #region movement math
        var difference = walker.target_walk_pos - entity.position;

        float distance = difference.Magnitude();

        float distanceToMove = moveSpeed * deltaTime.Microseconds / 1_000_000; //in seconds

        var direction = difference.Normalized();

        float velocity = (entity.last_position - entity.position).Magnitude();

        DbVector2 newPos;
        if (distance <= distanceToMove)
        {
            newPos = walker.target_walk_pos;
            ctx.Db.walking.entity_id.Delete(walker.entity_id);
        }
        else
        {
            newPos = new(entity.position.x + (direction.x * distanceToMove), entity.position.y + (direction.y * distanceToMove));
        }

        float finalRotation = DbVector2.RotationFromDirection(direction);
        #endregion

        #region update entity and actor

        Actor newActor = actor;

        newActor.rotation = finalRotation;

        ctx.Db.actor.entity_id.Delete(walker.entity_id);
        ctx.Db.actor.Insert(newActor);


        DbVector2 newLastPos = entity.position;

        ctx.Db.entity.entity_id.Delete(walker.entity_id);
        ctx.Db.entity.Insert(new Entity()
        {
            entity_id = walker.entity_id,
            position = newPos,
            last_position = newLastPos,
        });
        #endregion
    }

    [Reducer]
    public static void MoveAllPlayers(ReducerContext ctx)
    {

        var list = ctx.Db.walking.Iter();
        foreach (var walker in list)
        {
            MoveActor(ctx, walker);
        }
    }

    [Reducer]
    public static void SetEntityHealth(ReducerContext ctx, uint entityId, float newMaxHealth, float newCurrentHealth)
    {
        Log.Info("Changing health");

        var actor = ctx.Db.actor.entity_id.Find(entityId);

        if (actor == null) return;

        if (newMaxHealth < 0) newMaxHealth = 0;

        if (newCurrentHealth < 0) newCurrentHealth = 0;

        else if (newCurrentHealth > newMaxHealth) newCurrentHealth = newMaxHealth;

        ctx.Db.actor.entity_id.Delete(entityId);
        ctx.Db.actor.Insert(new()
        {
            entity_id = actor.Value.entity_id,
            rotation = actor.Value.rotation,
            current_health = newCurrentHealth,
            max_health = newMaxHealth
        });
    }
}