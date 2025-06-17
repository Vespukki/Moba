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

    [Table(Name = "nav_mesh_vertex", Public = true)]
    public partial struct NavMeshVertex
    {
        [PrimaryKey, AutoInc]
        public uint vertex_id;

        public DbVector2 position;
    }

    [Table(Name = "nav_mesh_edge", Public = true)]
    public partial struct NavMeshEdge
    {
        [PrimaryKey, AutoInc]
        public uint edge_id;

        public uint from_vertex_id;
        public uint to_vertex_id;
    }


    [Table(Name = "path", Public = true)]
    public partial struct Path
    {
        [PrimaryKey, AutoInc]
        public uint path_id;

        [SpacetimeDB.Index.BTree]
        public uint entity_id;

        public DbVector2 position;

        public uint order;
    }

    /// <summary>
    /// Creates path from entities position to the given position
    /// </summary>
    [Reducer]
    public static void CreatePath(ReducerContext ctx, Entity entity, DbVector2 position)
    {
        Path newPath = new()
        {
            entity_id = entity.entity_id,
            order = 0,
            path_id = 0,
            position = new(position.x, entity.position.y)

        };

        Path newPath2 = new()
        {
            entity_id = entity.entity_id,
            order = 1,
            path_id = 0,
            position = position,

        };

        ctx.Db.path.Insert(newPath);
        ctx.Db.path.Insert(newPath2);
    }

    [Reducer]
    public static void SetFutureTargetWalkPos(ReducerContext ctx, SetWalkTargetTimer caller)
    {
        SetTargetWalkPos(ctx, caller.entity_id, caller.position, caller.remove_other_actions);
    }

    [Reducer]
    public static void SetTargetWalkPos(ReducerContext ctx, uint entityId, DbVector2 position, bool removeOtherActions = true)
    {
        var nEntity = ctx.Db.entity.entity_id.Find(entityId);
        if (nEntity == null) return;
        Entity entity = nEntity.Value;

        if (entity.busy) return;

        ctx.Db.path.entity_id.Delete(entity.entity_id); // THIS PART IS TEMP
        CreatePath(ctx, entity, position);

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
    public static void MoveActor(ReducerContext ctx, Walking walker, Entity entity)
    {
        Log.Info("bruh");
        float moveSpeed = 250; //Units per Second

        #region find entity and actor

        

        List<Path> paths = ctx.Db.path.entity_id.Filter(entity.entity_id).ToList();
        if (paths.Count == 0) return;

        paths.Sort((a, b) => a.order.CompareTo(b.order));

        Log.Info(paths.Count.ToString());

        var nullableUnit = ctx.Db.actor.entity_id.Find(entity.entity_id);
        if (nullableUnit == null)
        {
            ctx.Db.walking.entity_id.Delete(entity.entity_id);
            return;
        }
        Actor actor = nullableUnit.Value;
        #endregion

        #region movement math
        var difference = paths[0].position - entity.position;

        float distance = difference.Magnitude();

        float distanceToMove = moveSpeed * deltaTime.Microseconds / 1_000_000; //in seconds

        var direction = difference.Normalized();

        float velocity = (entity.last_position - entity.position).Magnitude();

        DbVector2 newPos;
        if (distance <= distanceToMove)
        {
            newPos = paths[0].position;
            if (paths.Count - 1 <= 0)
            {
                ctx.Db.walking.entity_id.Delete(entity.entity_id);
            }
            else
            {
                //target next point
                ctx.Db.path.path_id.Delete(paths[0].path_id);
            }
        }
        else
        {
            newPos = new(entity.position.x + (direction.x * distanceToMove), entity.position.y + (direction.y * distanceToMove));
        }

        Log.Info(direction.ToString());
        float finalRotation = DbVector2.RotationFromDirection(direction);
        Log.Info(direction.ToString());
        #endregion

        #region update entity and actor

        Actor newActor = actor;

        newActor.rotation = finalRotation;

        ctx.Db.actor.entity_id.Delete(entity.entity_id);
        ctx.Db.actor.Insert(newActor);


        DbVector2 newLastPos = entity.position;

        ctx.Db.entity.entity_id.Delete(entity.entity_id);
        ctx.Db.entity.Insert(new Entity()
        {
            entity_id = entity.entity_id,
            position = newPos,
            last_position = newLastPos,
            busy = false //walking isnt a busy action
        });
        #endregion
    }

    [Reducer]
    public static void MoveAllPlayers(ReducerContext ctx)
    {

        var list = ctx.Db.walking.Iter();
        foreach (var walker in list)
        {
            var nullableEntity = ctx.Db.entity.entity_id.Find(walker.entity_id);
            if (nullableEntity == null)
            {
                Log.Info("null entity, skipping walk and deleting it");
                ctx.Db.walking.entity_id.Delete(walker.entity_id);
                continue;
            }
            Entity entity = nullableEntity.Value;

            MoveActor(ctx, walker, entity);

        }
    }
}
