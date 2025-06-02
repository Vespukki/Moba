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
        public float attack_animation_time;
    }

    [Table(Name = "walking", Public = true)]
    public partial struct Walking
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        public DbVector2 target_walk_pos;
    }

    [Table(Name = "attacking", Public = true)]
    public partial struct Attacking
    {
        [PrimaryKey, Unique]
        public uint entity_id;

        public uint target_entity_id;
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
                target_entity_id = targetEntityId
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
            };

            ctx.Db.attacking.Insert(newAttacking);
        }
    }

    [Reducer]
    public static void SetTargetWalkPos(ReducerContext ctx, uint entityId, DbVector2 position, bool removeOtherActions = true)
    {
        var nullableEntity = ctx.Db.entity.entity_id.Find(entityId);

        if(nullableEntity == null)
        {
            Log.Info($"passed entity {entityId} is null");
            return;
        }

        Entity entity = nullableEntity.Value;

        if (removeOtherActions) ctx.Db.attacking.entity_id.Delete(entityId);


        var oldWalking = ctx.Db.walking.entity_id.Find(entityId);

        if (oldWalking != null)
        {
            var newWalking = new Walking()
            {
                entity_id = oldWalking.Value.entity_id,
                target_walk_pos = position
            };

            ctx.Db.walking.entity_id.Delete(entityId);
            ctx.Db.walking.Insert(newWalking);
        }

        else
        {
            var newWalking = new Walking()
            {
                entity_id = entityId,
                target_walk_pos = position,
            };
            ctx.Db.walking.Insert(newWalking);
        }

    }

    [Reducer]
    public static void AttackWithActor(ReducerContext ctx, Attacking attacker)
    {

        #region find entity and actor
        var nullableEntity = ctx.Db.entity.entity_id.Find(attacker.entity_id);
        if (nullableEntity == null)
        {
            Log.Info($"deleting attacker because entity is null");
            ctx.Db.attacking.entity_id.Delete(attacker.entity_id);
            return;
        }
        Entity entity = nullableEntity.Value;

        var nullableUnit = ctx.Db.actor.entity_id.Find(attacker.entity_id);
        if (nullableUnit == null)
        {
            ctx.Db.attacking.entity_id.Delete(attacker.entity_id);
            return;
        }
        Actor actor = nullableUnit.Value;

        var nullableTargetEntity = ctx.Db.entity.entity_id.Find(attacker.target_entity_id);
        if (nullableTargetEntity == null)
        {
            ctx.Db.attacking.entity_id.Delete(attacker.entity_id);
            return;
        }
        Entity targetEntity = nullableTargetEntity.Value;
        #endregion

        if (DbVector2.Distance(entity.position, targetEntity.position) > actor.attack_range)
        {
            SetTargetWalkPos(ctx,entity.entity_id, targetEntity.position, false);

            Log.Info("too far, walking over there");
        }
        else
        {
            ctx.Db.walking.entity_id.Delete(attacker.entity_id);
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
        float finalRotation;
        if (distance <= distanceToMove)
        {
            newPos = walker.target_walk_pos;
            finalRotation = actor.rotation;
            ctx.Db.walking.entity_id.Delete(walker.entity_id);
        }
        else
        {
            newPos = new(entity.position.x + (direction.x * distanceToMove), entity.position.y + (direction.y * distanceToMove));
            float rotationDegrees = MathF.Atan2(direction.x, direction.y) * 180 / MathF.PI;
            finalRotation = rotationDegrees;
        }
        #endregion

        #region update entity and actor

        ctx.Db.actor.entity_id.Delete(walker.entity_id);
        ctx.Db.actor.Insert(new Actor()
        {
            entity_id = walker.entity_id,
            rotation = finalRotation,
            current_health = actor.current_health,
            max_health = actor.max_health,
            team = actor.team,
            attack_range = actor.attack_range,
            attack_animation_time = actor.attack_animation_time
        });


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