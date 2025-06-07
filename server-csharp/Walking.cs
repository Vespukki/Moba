using SpacetimeDB;
using System;
public static partial class Module
{
    [Table(Name = "walking", Public = true)]
    public partial struct Walking
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        public DbVector2 target_walk_pos;
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
    public static void SetFutureTargetWalkPos(ReducerContext ctx, SetWalkTargetTimer caller)
    {
        SetTargetWalkPos(ctx, caller.entity_id, caller.position, caller.remove_other_actions);
    }

    [Reducer]
    public static void SetTargetWalkPos(ReducerContext ctx, uint entityId, DbVector2 position, bool removeOtherActions = true)
    {
        ctx.Db.set_walk_target_timer.entity_id.Delete(entityId);

        var nullableAttacking = ctx.Db.attacking.entity_id.Find(entityId);

        if (nullableAttacking != null)
        {
            float timeSinceAttackStarted = GetTimestampDifferenceInSeconds(nullableAttacking.Value.attack_start_time, ctx.Timestamp);

            var nullableActor = ctx.Db.actor.entity_id.Find(entityId);
            if (nullableActor == null) return;
            Actor actor = nullableActor.Value;

            var nStats = ctx.Db.actor_base_stats.actor_id.Find(actor.actor_id);
            if (nStats == null) return;
            ActorBaseStats stats = nStats.Value;

            float windupTime = (1f / stats.attack_speed) * stats.windup_percent;
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
}
